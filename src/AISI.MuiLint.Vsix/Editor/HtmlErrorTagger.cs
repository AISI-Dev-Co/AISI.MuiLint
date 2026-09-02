#nullable disable
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using AISI.MuiLint;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Projection;
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
    /// drop, or <see cref="ITextDocument"/> dispose — not view-refcount.
    /// </summary>
    internal sealed class HtmlErrorTagger : ITagger<IErrorTag>, IDisposable
    {
        private const int DebounceMilliseconds = 300;
        private const string FallbackPath = "buffer.html";

        private readonly ITextBuffer _buffer;
        private readonly ITextDocumentFactoryService _textDocumentFactory;
        private readonly IVsEditorAdaptersFactoryService _adaptersFactory;
        private readonly HtmlErrorTableDataSource _tableDataSource;
        private readonly JoinableTaskContext _joinableTaskContext;
        private readonly object _gate = new object();

        private ITextDocument _document;
        private CancellationTokenSource _debounce = new CancellationTokenSource();
        private ITextSnapshot _analyzedSnapshot;
        private IReadOnlyList<Diagnostic> _diagnostics = Array.Empty<Diagnostic>();
        private int _viewCount;
        private int _disposed;
        private int _hookPosted;
        private IConnectionPoint _closePoint;
        private uint _closeCookie;
        private BufferCloseSink _closeSink;

        public HtmlErrorTagger(
            ITextBuffer buffer,
            ITextDocumentFactoryService textDocumentFactory,
            IVsEditorAdaptersFactoryService adaptersFactory,
            HtmlErrorTableDataSource tableDataSource,
            JoinableTaskContext joinableTaskContext)
        {
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            _textDocumentFactory = textDocumentFactory ?? throw new ArgumentNullException(nameof(textDocumentFactory));
            _adaptersFactory = adaptersFactory ?? throw new ArgumentNullException(nameof(adaptersFactory));
            _tableDataSource = tableDataSource ?? throw new ArgumentNullException(nameof(tableDataSource));
            _joinableTaskContext = joinableTaskContext ?? throw new ArgumentNullException(nameof(joinableTaskContext));

            ITextDocument document = TryGetHtmlDocument(_buffer);
            if (document != null)
            {
                _document = document;
                _document.FileActionOccurred += OnFileActionOccurred;
            }

            _textDocumentFactory.TextDocumentDisposed += OnTextDocumentDisposed;
            _buffer.Changed += OnBufferChanged;
            _buffer.ContentTypeChanged += OnContentTypeChanged;
            RequestHookBufferClose();
            ScheduleAnalyze();
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        private bool IsDisposed
        {
            get { return Volatile.Read(ref _disposed) != 0; }
        }

        public void AddView()
        {
            Interlocked.Increment(ref _viewCount);
        }

        public void ReleaseView()
        {
            Interlocked.Decrement(ref _viewCount);
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
            BeginDispose(unadvise: true);
        }

        internal void DisposeFromBufferClose()
        {
            BeginDispose(unadvise: false);
        }

        private void BeginDispose(bool unadvise)
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            {
                return;
            }

            _viewCount = 0;
            _buffer.Changed -= OnBufferChanged;
            _buffer.ContentTypeChanged -= OnContentTypeChanged;
            _textDocumentFactory.TextDocumentDisposed -= OnTextDocumentDisposed;
            if (_document != null)
            {
                _document.FileActionOccurred -= OnFileActionOccurred;
                _document = null;
            }

            CancellationTokenSource debounce = Interlocked.Exchange(ref _debounce, null);
            try
            {
                if (debounce != null)
                {
                    debounce.Cancel();
                }
            }
            catch (ObjectDisposedException)
            {
            }

            _tableDataSource.Remove(_buffer);
            if (_buffer.Properties.ContainsProperty(typeof(HtmlErrorTagger)))
            {
                _buffer.Properties.RemoveProperty(typeof(HtmlErrorTagger));
            }

            if (unadvise)
            {
                FileAndForget(UnadviseBufferCloseAsync, "AISI.MuiLint/UnadviseBufferClose");
            }
            else
            {
                _closePoint = null;
                _closeCookie = 0;
                _closeSink = null;
            }
        }

        private void OnBufferChanged(object sender, TextContentChangedEventArgs e)
        {
            if (IsDisposed)
            {
                return;
            }

            RequestHookBufferClose();
            ScheduleAnalyze();
        }

        private void OnContentTypeChanged(object sender, ContentTypeChangedEventArgs e)
        {
            if (IsDisposed)
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
            if (e == null || e.TextDocument == null)
            {
                return;
            }

            if (_document != null && object.ReferenceEquals(e.TextDocument, _document))
            {
                Dispose();
                return;
            }

            ITextBuffer documentBuffer = e.TextDocument.TextBuffer;
            if (documentBuffer != null && IsOurDocumentBuffer(documentBuffer))
            {
                Dispose();
            }
        }

        private bool IsOurDocumentBuffer(ITextBuffer candidate)
        {
            ITextDocument document = _document;
            if (document != null && document.TextBuffer != null &&
                object.ReferenceEquals(candidate, document.TextBuffer))
            {
                return true;
            }

            IReadOnlyList<ITextBuffer> sources = NestedSourceWalk.Flatten(_buffer, ProjectionSources);
            for (int i = 0; i < sources.Count; i++)
            {
                if (object.ReferenceEquals(sources[i], candidate))
                {
                    return true;
                }
            }

            return false;
        }

        private void ScheduleAnalyze()
        {
            if (IsDisposed)
            {
                return;
            }

            CancellationTokenSource next = new CancellationTokenSource();
            CancellationTokenSource previous = Interlocked.Exchange(ref _debounce, next);
            try
            {
                if (previous != null)
                {
                    previous.Cancel();
                }
            }
            catch (ObjectDisposedException)
            {
            }

            FileAndForget(() => DebounceAndLogAsync(next), "AISI.MuiLint/Debounce");
        }

        private async Task DebounceAndLogAsync(CancellationTokenSource owned)
        {
            try
            {
                await DebounceAsync(owned.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                LogFault(ex);
            }
            finally
            {
                try
                {
                    owned.Dispose();
                }
                catch (ObjectDisposedException)
                {
                }
            }
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
            catch (ObjectDisposedException)
            {
                return;
            }

            if (token.IsCancellationRequested || IsDisposed)
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
            catch (ObjectDisposedException)
            {
                return;
            }

            if (token.IsCancellationRequested || IsDisposed)
            {
                return;
            }

            try
            {
                await ResolveJoinableTaskFactory().SwitchToMainThreadAsync(token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            if (IsDisposed || token.IsCancellationRequested)
            {
                return;
            }

            lock (_gate)
            {
                if (IsDisposed)
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
                document = TryGetHtmlDocument(_buffer);
            }

            if (document != null && !string.IsNullOrEmpty(document.FilePath))
            {
                return document.FilePath;
            }

            return FallbackPath;
        }

        private void RequestHookBufferClose()
        {
            if (IsDisposed || _closePoint != null)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref _hookPosted, 1, 0) != 0)
            {
                return;
            }

            FileAndForget(HookBufferCloseAsync, "AISI.MuiLint/HookBufferClose");
        }

        private async Task HookBufferCloseAsync()
        {
            await ResolveJoinableTaskFactory().SwitchToMainThreadAsync();
            if (IsDisposed || _closePoint != null)
            {
                return;
            }

            AdviseBufferClose();
            if (_closePoint == null)
            {
                Interlocked.Exchange(ref _hookPosted, 0);
            }
        }

        private async Task UnadviseBufferCloseAsync()
        {
            await ResolveJoinableTaskFactory().SwitchToMainThreadAsync();

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

        private void AdviseBufferClose()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (IsDisposed || _closePoint != null)
            {
                return;
            }

            IVsTextBuffer vsBuffer = TryGetVsBuffer();
            if (vsBuffer == null)
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

        private ITextDocument TryGetHtmlDocument(ITextBuffer buffer)
        {
            ITextDocument document;
            if (_textDocumentFactory.TryGetTextDocument(buffer, out document))
            {
                return document;
            }

            IReadOnlyList<ITextBuffer> sources = NestedSourceWalk.Flatten(buffer, ProjectionSources);
            for (int i = 0; i < sources.Count; i++)
            {
                ITextBuffer source = sources[i];
                if (source != null && _textDocumentFactory.TryGetTextDocument(source, out document))
                {
                    return document;
                }
            }

            return null;
        }

        private static IEnumerable<ITextBuffer> ProjectionSources(ITextBuffer buffer)
        {
            IProjectionBufferBase projection = buffer as IProjectionBufferBase;
            if (projection == null)
            {
                return null;
            }

            return projection.SourceBuffers;
        }

        private IVsTextBuffer TryGetVsBuffer()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            IVsTextBuffer vsBuffer = _adaptersFactory.GetBufferAdapter(_buffer);
            if (vsBuffer != null)
            {
                return vsBuffer;
            }

            ITextDocument document = _document;
            if (document == null)
            {
                document = TryGetHtmlDocument(_buffer);
            }

            if (document != null && document.TextBuffer != null && !object.ReferenceEquals(document.TextBuffer, _buffer))
            {
                vsBuffer = _adaptersFactory.GetBufferAdapter(document.TextBuffer);
                if (vsBuffer != null)
                {
                    return vsBuffer;
                }
            }

            IReadOnlyList<ITextBuffer> sources = NestedSourceWalk.Flatten(_buffer, ProjectionSources);
            for (int i = 0; i < sources.Count; i++)
            {
                ITextBuffer source = sources[i];
                if (source == null)
                {
                    continue;
                }

                vsBuffer = _adaptersFactory.GetBufferAdapter(source);
                if (vsBuffer != null)
                {
                    return vsBuffer;
                }
            }

            return null;
        }

        private JoinableTaskFactory ResolveJoinableTaskFactory()
        {
            JoinableTaskFactory package = MuiLintVsPackage.PackageJoinableTaskFactory;
            if (package != null)
            {
                return package;
            }

            return _joinableTaskContext.Factory;
        }

        private void FileAndForget(Func<Task> work, string id)
        {
            ResolveJoinableTaskFactory().RunAsync(work).FileAndForget(id);
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
                _owner.DisposeFromBufferClose();
            }
        }
    }
}
