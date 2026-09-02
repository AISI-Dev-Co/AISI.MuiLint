#nullable disable
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using AISI.MuiLint;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;

namespace AISI.MuiLint.Vsix
{
    /// <summary>
    /// Squiggle tagger: runs <see cref="MuiLintPackage.AnalyzeHtml"/> off the UI thread
    /// and emits <see cref="ErrorTag"/> spans. Also publishes the same findings to the Error List.
    /// Teardown is buffer-close (<c>IVsTextBufferDataEvents.OnCloseEvent</c>), content-type
    /// drop, last view, or <see cref="ITextDocument"/> dispose — not view-refcount alone.
    /// </summary>
    internal sealed class HtmlErrorTagger : ITagger<IErrorTag>, IDisposable
    {
        private const int DebounceMilliseconds = 300;
        private const string FallbackPath = "buffer.html";

        private readonly ITextBuffer _buffer;
        private readonly ITextDocumentFactoryService _textDocumentFactory;
        private readonly HtmlErrorTableDataSource _tableDataSource;
        private readonly object _gate = new object();

        private ITextDocument _document;
        private CancellationTokenSource _debounce = new CancellationTokenSource();
        private ITextSnapshot _analyzedSnapshot;
        private IReadOnlyList<Diagnostic> _diagnostics = Array.Empty<Diagnostic>();
        private int _viewCount;
        private bool _disposed;
        private IConnectionPoint _closePoint;
        private uint _closeCookie;
        private BufferCloseSink _closeSink;
        private JoinableTask _debounceTask;

        public HtmlErrorTagger(
            ITextBuffer buffer,
            ITextDocumentFactoryService textDocumentFactory,
            HtmlErrorTableDataSource tableDataSource)
        {
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            _textDocumentFactory = textDocumentFactory ?? throw new ArgumentNullException(nameof(textDocumentFactory));
            _tableDataSource = tableDataSource ?? throw new ArgumentNullException(nameof(tableDataSource));

            if (_textDocumentFactory.TryGetTextDocument(_buffer, out ITextDocument document))
            {
                _document = document;
                _document.FileActionOccurred += OnFileActionOccurred;
            }

            _textDocumentFactory.TextDocumentDisposed += OnTextDocumentDisposed;
            _buffer.Changed += OnBufferChanged;
            _buffer.ContentTypeChanged += OnContentTypeChanged;
            if (ThreadHelper.CheckAccess())
            {
                HookBufferClose();
            }

            ScheduleAnalyze();
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public void AddView()
        {
            Interlocked.Increment(ref _viewCount);
        }

        public void ReleaseView()
        {
            if (Interlocked.Decrement(ref _viewCount) <= 0)
            {
                Dispose();
            }
        }

        public IEnumerable<ITagSpan<IErrorTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (spans == null || spans.Count == 0)
            {
                yield break;
            }

            ITextSnapshot analyzedSnapshot;
            IReadOnlyList<Diagnostic> diagnostics;
            lock (_gate)
            {
                analyzedSnapshot = _analyzedSnapshot;
                diagnostics = _diagnostics;
            }

            if (analyzedSnapshot == null || diagnostics.Count == 0)
            {
                yield break;
            }

            ITextSnapshot target = spans[0].Snapshot;
            for (int i = 0; i < diagnostics.Count; i++)
            {
                Diagnostic diagnostic = diagnostics[i];
                SnapshotSpan? clamped = TryClamp(analyzedSnapshot, diagnostic);
                if (clamped == null)
                {
                    continue;
                }

                SnapshotSpan translated = clamped.Value.TranslateTo(target, SpanTrackingMode.EdgeInclusive);
                if (translated.Length <= 0)
                {
                    continue;
                }

                bool intersects = false;
                for (int s = 0; s < spans.Count; s++)
                {
                    if (translated.IntersectsWith(spans[s]))
                    {
                        intersects = true;
                        break;
                    }
                }

                if (!intersects)
                {
                    continue;
                }

                string toolTip = diagnostic.Id + ": " + diagnostic.Message;
                yield return new TagSpan<IErrorTag>(
                    translated,
                    new ErrorTag(PredefinedErrorTypeNames.SyntaxError, toolTip));
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _viewCount = 0;
            if (ThreadHelper.CheckAccess())
            {
                UnhookBufferClose();
            }

            _buffer.Changed -= OnBufferChanged;
            _buffer.ContentTypeChanged -= OnContentTypeChanged;
            _textDocumentFactory.TextDocumentDisposed -= OnTextDocumentDisposed;
            if (_document != null)
            {
                _document.FileActionOccurred -= OnFileActionOccurred;
                _document = null;
            }

            CancellationTokenSource debounce = Interlocked.Exchange(ref _debounce, null);
            if (debounce != null)
            {
                debounce.Cancel();
                debounce.Dispose();
            }

            JoinableTask pending = _debounceTask;
            _debounceTask = null;
            if (pending != null)
            {
                try
                {
                    pending.Join();
                }
                catch (OperationCanceledException)
                {
                }
            }

            _tableDataSource.Remove(_buffer);
            if (_buffer.Properties.ContainsProperty(typeof(HtmlErrorTagger)))
            {
                _buffer.Properties.RemoveProperty(typeof(HtmlErrorTagger));
            }
        }

        private void OnBufferChanged(object sender, TextContentChangedEventArgs e)
        {
            if (_disposed)
            {
                return;
            }

            if (ThreadHelper.CheckAccess())
            {
                HookBufferClose();
            }

            ScheduleAnalyze();
        }

        private void OnContentTypeChanged(object sender, ContentTypeChangedEventArgs e)
        {
            if (_disposed)
            {
                return;
            }

            IContentType type = _buffer.ContentType;
            if (type == null || (!type.IsOfType("htmlx") && !type.IsOfType("html")))
            {
                Dispose();
            }
        }

        private void OnFileActionOccurred(object sender, TextDocumentFileActionEventArgs e)
        {
            if ((e.FileActionType & FileActionTypes.DocumentRenamed) != 0)
            {
                ScheduleAnalyze();
            }
        }

        private void OnTextDocumentDisposed(object sender, TextDocumentEventArgs e)
        {
            if (e != null && e.TextDocument != null && e.TextDocument.TextBuffer == _buffer)
            {
                Dispose();
            }
        }

        private void ScheduleAnalyze()
        {
            if (_disposed)
            {
                return;
            }

            CancellationTokenSource next = new CancellationTokenSource();
            CancellationTokenSource previous = Interlocked.Exchange(ref _debounce, next);
            if (previous != null)
            {
                previous.Cancel();
                previous.Dispose();
            }

            CancellationToken token = next.Token;
            _debounceTask = ThreadHelper.JoinableTaskFactory.RunAsync(
                async () =>
                {
                    try
                    {
                        await DebounceAsync(token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    catch (Exception ex)
                    {
                        LogFault(ex);
                    }
                });
        }

        private async Task DebounceAsync(CancellationToken token)
        {
            try
            {
                await Task.Delay(DebounceMilliseconds, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (token.IsCancellationRequested || _disposed)
            {
                return;
            }

            ITextSnapshot snapshot = _buffer.CurrentSnapshot;
            string path = ResolvePath();
            string text = snapshot.GetText();

            IReadOnlyList<Diagnostic> diagnostics;
            try
            {
                diagnostics = await Task.Run(() => MuiLintPackage.AnalyzeHtml(path, text), token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (token.IsCancellationRequested || _disposed)
            {
                return;
            }

            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (_disposed || token.IsCancellationRequested)
            {
                return;
            }

            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _analyzedSnapshot = snapshot;
                _diagnostics = diagnostics;
                _tableDataSource.Update(_buffer, path, diagnostics);
            }

            EventHandler<SnapshotSpanEventArgs> handler = TagsChanged;
            if (handler != null)
            {
                handler(this, new SnapshotSpanEventArgs(new SnapshotSpan(snapshot, 0, snapshot.Length)));
            }
        }

        private string ResolvePath()
        {
            ITextDocument document = _document;
            if (document == null)
            {
                _textDocumentFactory.TryGetTextDocument(_buffer, out document);
            }

            if (document != null && !string.IsNullOrEmpty(document.FilePath))
            {
                return document.FilePath;
            }

            return FallbackPath;
        }

        private void HookBufferClose()
        {
            if (_disposed || _closePoint != null)
            {
                return;
            }

            ThreadHelper.ThrowIfNotOnUIThread();

            IVsTextBuffer vsBuffer;
            if (!_buffer.Properties.TryGetProperty(typeof(IVsTextBuffer), out vsBuffer) || vsBuffer == null)
            {
                return;
            }

            IConnectionPointContainer container = vsBuffer as IConnectionPointContainer;
            if (container == null)
            {
                return;
            }

            Guid iid = typeof(IVsTextBufferDataEvents).GUID;
            try
            {
                container.FindConnectionPoint(ref iid, out IConnectionPoint point);
                if (point == null)
                {
                    return;
                }

                BufferCloseSink sink = new BufferCloseSink(this);
                point.Advise(sink, out uint cookie);
                _closeSink = sink;
                _closePoint = point;
                _closeCookie = cookie;
            }
            catch (COMException)
            {
            }
        }

        private void UnhookBufferClose()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            IConnectionPoint point = _closePoint;
            uint cookie = _closeCookie;
            _closePoint = null;
            _closeCookie = 0;
            _closeSink = null;
            if (point == null || cookie == 0)
            {
                return;
            }

            try
            {
                point.Unadvise(cookie);
            }
            catch (COMException)
            {
            }
        }

        private static void LogFault(Exception ex)
        {
            try
            {
                ActivityLog.LogError("AISI.MuiLint", ex.ToString());
            }
            catch (Exception)
            {
            }
        }

        private static SnapshotSpan? TryClamp(ITextSnapshot snapshot, Diagnostic diagnostic)
        {
            int length = snapshot.Length;
            int start = diagnostic.Start;
            if (start < 0)
            {
                start = 0;
            }

            if (start > length)
            {
                start = length;
            }

            int spanLength = diagnostic.Length;
            if (spanLength < 0)
            {
                spanLength = 0;
            }

            if (start + spanLength > length)
            {
                spanLength = length - start;
            }

            if (spanLength <= 0)
            {
                return null;
            }

            return new SnapshotSpan(snapshot, start, spanLength);
        }

        private sealed class BufferCloseSink : IVsTextBufferDataEvents
        {
            private readonly HtmlErrorTagger _owner;

            public BufferCloseSink(HtmlErrorTagger owner)
            {
                _owner = owner;
            }

            public void OnFileChanged(uint grfChange, uint dwFileAttrs)
            {
            }

            public int OnLoadCompleted(int fReload)
            {
                return VSConstants.S_OK;
            }

            public void OnCloseEvent()
            {
                _owner.Dispose();
            }
        }
    }
}
