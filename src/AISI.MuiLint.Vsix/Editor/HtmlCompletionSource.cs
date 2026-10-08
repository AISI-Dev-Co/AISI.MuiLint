#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using AISI.MuiLint;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;

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

            string text = snapshot.GetText();
            int caret = triggerPoint.Value.Position;
            MuiCompletionTarget target = MuiHtmlCompletion.Classify(text, caret);

            // Without a file on disk there is no stock screen or .ts to read.
            string path = EditorDocuments.Find(_buffer, _textDocumentFactory)?.FilePath;
            bool canRead = !string.IsNullOrEmpty(path) && Path.IsPathRooted(path);

            var items = new List<MuiCompletionItem>();
            switch (target)
            {
                case MuiCompletionTarget.TagName:
                    items.AddRange(MuiHtmlCompletion.GetTags());
                    items.Add(MuiHtmlCompletion.UsrFieldSnippet());
                    break;
                case MuiCompletionTarget.AttributeName:
                    string tagName = ReadTagName(text, caret);
                    items.AddRange(MuiHtmlCompletion.GetAttributes(tagName));
                    if (canRead)
                    {
                        items.AddRange(MuiHtmlCompletion.GetStockAttributes(tagName, path, MuiLintPackage.TryReadFile, MuiLintPackage.TryListFolder));
                    }

                    break;
                case MuiCompletionTarget.None:
                    // Always-available Usr field expansion when not inside a tag.
                    items.Add(MuiHtmlCompletion.UsrFieldSnippet());
                    break;
                default:
                    if (canRead)
                    {
                        items.AddRange(MuiHtmlCompletion.GetValues(path, text, caret, target, MuiLintPackage.TryReadFile, MuiLintPackage.TryListFolder));
                        if (target == MuiCompletionTarget.FieldValue)
                        {
                            AddDacFields(items, path);
                        }
                    }

                    break;
            }

            if (items.Count == 0)
            {
                return;
            }

            ITrackingSpan applicableTo = snapshot.CreateTrackingSpan(
                Span.FromBounds(MuiHtmlCompletion.ApplicableStart(text, caret, target), caret),
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

        /// <summary>Fields from the C# DAC extensions in the solution that the .ts doesn't declare yet.</summary>
        private static void AddDacFields(List<MuiCompletionItem> items, string htmlPath)
        {
            var offered = new HashSet<string>(StringComparer.Ordinal);
            foreach (MuiCompletionItem item in items)
            {
                offered.Add(item.DisplayText);
            }

            foreach (DacField field in DacFieldIndex.Get(DacFieldIndex.FindSourceFolder(htmlPath)))
            {
                if (offered.Add(field.Name))
                {
                    string description = "on " + field.Dac + " via " + field.Extension + " (C#). Not in the .ts yet; the lightbulb can declare it.";
                    items.Add(new MuiCompletionItem(field.Name, field.Name, MuiCompletionKind.BindingValue, description));
                }
            }
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
    }
}
