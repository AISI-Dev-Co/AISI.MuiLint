#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using AISI.MuiLint;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Projection;

namespace AISI.MuiLint.Vsix
{
    /// <summary>
    /// Sync <see cref="ICompletionSource"/> over <see cref="MuiHtmlCompletion"/>.
    /// No fire-and-forget; AugmentCompletionSession stays on the calling thread.
    /// </summary>
    internal sealed class HtmlCompletionSource : ICompletionSource
    {
        private readonly ITextBuffer _buffer;
        private readonly ITextDocumentFactoryService _textDocumentFactory;
        private bool _disposed;

        public HtmlCompletionSource(ITextBuffer buffer, ITextDocumentFactoryService textDocumentFactory)
        {
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            _textDocumentFactory = textDocumentFactory ?? throw new ArgumentNullException(nameof(textDocumentFactory));
        }

        /// <inheritdoc />
        public void AugmentCompletionSession(ICompletionSession session, IList<CompletionSet> completionSets)
        {
            if (_disposed || session is null || completionSets is null)
            {
                return;
            }

            ITextSnapshot snapshot = _buffer.CurrentSnapshot;
            SnapshotPoint? triggerPoint = session.GetTriggerPoint(snapshot);
            if (triggerPoint == null)
            {
                return;
            }

            SnapshotPoint point = triggerPoint.Value;
            string text = snapshot.GetText();
            int caret = point.Position;
            MuiCompletionTarget target = MuiHtmlCompletion.Classify(text, caret);

            var items = new List<MuiCompletionItem>();
            switch (target)
            {
                case MuiCompletionTarget.TagName:
                    items.AddRange(MuiHtmlCompletion.GetTags());
                    items.Add(MuiHtmlCompletion.UsrFieldSnippet());
                    break;
                case MuiCompletionTarget.AttributeName:
                    items.AddRange(MuiHtmlCompletion.GetAttributes(ReadTagName(text, caret)));
                    break;
                case MuiCompletionTarget.SelectorValue:
                    {
                        string path = TryGetFilePath();
                        items.AddRange(MuiHtmlCompletion.GetSelectorValues(
                            text,
                            TryReadBaseHtml(path),
                            TryReadSiblingTypeScript(path)));
                    }

                    break;
                default:
                    // Always-available Usr field expansion when not inside a tag.
                    items.Add(MuiHtmlCompletion.UsrFieldSnippet());
                    break;
            }

            if (items.Count == 0)
            {
                return;
            }

            ITrackingSpan applicableTo = snapshot.CreateTrackingSpan(
                FindApplicableSpan(text, caret, target),
                SpanTrackingMode.EdgeInclusive);

            var completions = new List<Completion>(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                MuiCompletionItem item = items[i];
                // Prefer displayText ctor: the 5-arg overload takes ImageSource and
                // requires a PresentationCore reference the SDK compile assets omit.
                completions.Add(new Completion(item.DisplayText)
                {
                    InsertionText = item.InsertText,
                    Description = item.Description,
                });
            }

            completionSets.Add(new CompletionSet(
                "AISI.MuiLint",
                "MuiLint",
                applicableTo,
                completions,
                Array.Empty<Completion>()));
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _disposed = true;
        }

        private string TryGetFilePath()
        {
            ITextDocument document = TryGetHtmlDocument(_buffer);
            if (document != null && !string.IsNullOrEmpty(document.FilePath))
            {
                return document.FilePath;
            }

            return null;
        }

        /// <summary>
        /// Same walk as <c>HtmlErrorTagger.TryGetHtmlDocument</c>: htmlx often has a null
        /// path on the top buffer, so walk <see cref="IProjectionBufferBase"/> sources.
        /// </summary>
        private ITextDocument TryGetHtmlDocument(ITextBuffer buffer)
        {
            ITextDocument document;
            if (_textDocumentFactory.TryGetTextDocument(buffer, out document)
                && document != null
                && !string.IsNullOrEmpty(document.FilePath))
            {
                return document;
            }

            IReadOnlyList<ITextBuffer> sources = NestedSourceWalk.Flatten(buffer, ProjectionSources);
            for (int i = 0; i < sources.Count; i++)
            {
                ITextBuffer source = sources[i];
                if (source != null
                    && _textDocumentFactory.TryGetTextDocument(source, out document)
                    && document != null
                    && !string.IsNullOrEmpty(document.FilePath))
                {
                    return document;
                }
            }

            return null;
        }

        private static IEnumerable<ITextBuffer> ProjectionSources(ITextBuffer buffer)
        {
            // htmlx elision is IElisionBuffer : IProjectionBufferBase, not IProjectionBuffer.
            IProjectionBufferBase projection = buffer as IProjectionBufferBase;
            if (projection == null)
            {
                return null;
            }

            return projection.SourceBuffers;
        }

        private static string TryReadSiblingTypeScript(string htmlPath)
        {
            if (string.IsNullOrEmpty(htmlPath))
            {
                return null;
            }

            string tsPath = Path.ChangeExtension(htmlPath, ".ts");
            return TryReadAllText(tsPath);
        }

        private static string TryReadBaseHtml(string htmlPath)
        {
            if (string.IsNullOrEmpty(htmlPath))
            {
                return null;
            }

            // Extension HTML under .../<Screen>/extensions/<file>.html -> sibling base .../<Screen>/<Screen>.html
            string dir = Path.GetDirectoryName(htmlPath);
            if (string.IsNullOrEmpty(dir))
            {
                return null;
            }

            string parentName = Path.GetFileName(dir);
            if (!string.Equals(parentName, "extensions", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string screenDir = Path.GetDirectoryName(dir);
            if (string.IsNullOrEmpty(screenDir))
            {
                return null;
            }

            string screenName = Path.GetFileName(screenDir);
            if (string.IsNullOrEmpty(screenName))
            {
                return null;
            }

            string basePath = Path.Combine(screenDir, screenName + ".html");
            return TryReadAllText(basePath);
        }

        private static string TryReadAllText(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    return File.ReadAllText(path);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            return null;
        }

        private static string ReadTagName(string text, int caret)
        {
            int open = text.LastIndexOf('<', Math.Max(0, caret - 1));
            if (open < 0 || open + 1 >= text.Length)
            {
                return string.Empty;
            }

            int i = open + 1;
            while (i < caret && i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '-' || text[i] == '_'))
            {
                i++;
            }

            return text.Substring(open + 1, i - (open + 1));
        }

        private static Span FindApplicableSpan(string text, int caret, MuiCompletionTarget target)
        {
            if (caret < 0)
            {
                caret = 0;
            }

            if (caret > text.Length)
            {
                caret = text.Length;
            }

            if (target == MuiCompletionTarget.SelectorValue)
            {
                int start = caret;
                while (start > 0)
                {
                    char c = text[start - 1];
                    if (c == '"' || c == '\'')
                    {
                        break;
                    }

                    start--;
                }

                return Span.FromBounds(start, caret);
            }

            int tokenStart = caret;
            while (tokenStart > 0)
            {
                char c = text[tokenStart - 1];
                if (char.IsWhiteSpace(c) || c == '<' || c == '>' || c == '"' || c == '\'' || c == '=')
                {
                    break;
                }

                tokenStart--;
            }

            return Span.FromBounds(tokenStart, caret);
        }
    }
}
