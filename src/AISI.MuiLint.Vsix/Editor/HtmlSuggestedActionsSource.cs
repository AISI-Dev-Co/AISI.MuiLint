#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AISI.MuiLint;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;

namespace AISI.MuiLint.Vsix
{
    /// <summary>
    /// Offers fixes for findings under the caret. Edits are computed by <see cref="MuiLintFixes"/>
    /// against the snapshot the tagger analysed, then tracked forward to whatever the buffer is now.
    /// </summary>
    internal sealed class HtmlSuggestedActionsSource : ISuggestedActionsSource
    {
        private readonly ITextBuffer _buffer;

        public HtmlSuggestedActionsSource(ITextBuffer buffer)
        {
            _buffer = buffer;
        }

        public event EventHandler<EventArgs> SuggestedActionsChanged
        {
            add { }
            remove { }
        }

        public Task<bool> HasSuggestedActionsAsync(ISuggestedActionCategorySet requestedActionCategories, SnapshotSpan range, CancellationToken cancellationToken)
        {
            return Task.FromResult(FindActions(range).Count > 0);
        }

        public IEnumerable<SuggestedActionSet> GetSuggestedActions(ISuggestedActionCategorySet requestedActionCategories, SnapshotSpan range, CancellationToken cancellationToken)
        {
            List<ISuggestedAction> actions = FindActions(range);
            if (actions.Count == 0)
            {
                return Enumerable.Empty<SuggestedActionSet>();
            }

            return new[] { new SuggestedActionSet(PredefinedSuggestedActionCategoryNames.CodeFix, actions) };
        }

        public bool TryGetTelemetryId(out Guid telemetryId)
        {
            telemetryId = Guid.Empty;
            return false;
        }

        public void Dispose()
        {
        }

        private List<ISuggestedAction> FindActions(SnapshotSpan range)
        {
            var fixes = new List<ISuggestedAction>();
            var suppressions = new List<ISuggestedAction>();
            HtmlErrorTagger tagger;
            if (!_buffer.Properties.TryGetProperty(typeof(HtmlErrorTagger), out tagger)
                || !tagger.TryGetAnalysis(out ITextSnapshot analyzed, out IReadOnlyList<Diagnostic> diagnostics)
                || range.Snapshot.TextBuffer != _buffer)
            {
                return fixes;
            }

            string text = null;
            var offered = new HashSet<string>(StringComparer.Ordinal);
            foreach (Diagnostic diagnostic in diagnostics)
            {
                if (!IsUnderCaret(diagnostic, analyzed, range))
                {
                    continue;
                }

                if (text == null)
                {
                    text = analyzed.GetText();
                }

                if (diagnostic.Id == DiagnosticIds.SelfClosing)
                {
                    Add(fixes, "Add the closing tag", analyzed, MuiLintFixes.ExpandSelfClosing(text, diagnostic));
                }
                else if (diagnostic.Id == DiagnosticIds.EmptyFieldset)
                {
                    Add(fixes, "Remove the empty qp-fieldset", analyzed, MuiLintFixes.RemoveEmptyFieldset(text, diagnostic));
                }

                bool fileLevel = MuiLintFixes.IsFileLevel(diagnostic);
                if (offered.Add(diagnostic.Id + ":" + diagnostic.Line))
                {
                    string title = "Suppress " + diagnostic.Id + (fileLevel ? " in this file" : " on this line");
                    Add(suppressions, title, analyzed, MuiLintFixes.Suppress(text, diagnostic));
                }
            }

            fixes.AddRange(suppressions);
            return fixes;
        }

        private void Add(List<ISuggestedAction> actions, string title, ITextSnapshot analyzed, TextEdit? edit)
        {
            if (edit.HasValue)
            {
                actions.Add(new MuiLintFixAction(title, _buffer, analyzed, edit.Value));
            }
        }

        private static bool IsUnderCaret(Diagnostic diagnostic, ITextSnapshot analyzed, SnapshotSpan range)
        {
            // File-level findings have no span; offer them anywhere on the first line.
            if (MuiLintFixes.IsFileLevel(diagnostic))
            {
                return range.Start.GetContainingLine().LineNumber == 0;
            }

            // IntersectsWith is inclusive at both ends, so a caret right after the span counts too.
            return new SnapshotSpan(analyzed, diagnostic.Start, diagnostic.Length)
                .TranslateTo(range.Snapshot, SpanTrackingMode.EdgeInclusive)
                .IntersectsWith(range);
        }

        private sealed class MuiLintFixAction : ISuggestedAction
        {
            private readonly ITextBuffer _buffer;
            private readonly ITrackingSpan _span;
            private readonly string _newText;

            public MuiLintFixAction(string displayText, ITextBuffer buffer, ITextSnapshot analyzed, TextEdit edit)
            {
                DisplayText = displayText;
                _buffer = buffer;
                _span = analyzed.CreateTrackingSpan(edit.Start, edit.Length, SpanTrackingMode.EdgeExclusive);
                _newText = edit.NewText;
            }

            public string DisplayText { get; }

            public bool HasActionSets
            {
                get { return false; }
            }

            public ImageMoniker IconMoniker
            {
                get { return default(ImageMoniker); }
            }

            public string IconAutomationText
            {
                get { return null; }
            }

            public string InputGestureText
            {
                get { return null; }
            }

            public bool HasPreview
            {
                get { return false; }
            }

            public Task<IEnumerable<SuggestedActionSet>> GetActionSetsAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult<IEnumerable<SuggestedActionSet>>(null);
            }

            public Task<object> GetPreviewAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult<object>(null);
            }

            public void Invoke(CancellationToken cancellationToken)
            {
                _buffer.Replace(_span.GetSpan(_buffer.CurrentSnapshot), _newText);
            }

            public bool TryGetTelemetryId(out Guid telemetryId)
            {
                telemetryId = Guid.Empty;
                return false;
            }

            public void Dispose()
            {
            }
        }
    }
}
