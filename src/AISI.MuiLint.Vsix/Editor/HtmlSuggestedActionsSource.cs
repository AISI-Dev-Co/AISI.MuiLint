#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AISI.MuiLint;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;

namespace AISI.MuiLint.Vsix
{
    /// <summary>Lightbulb fixes for findings under the caret, and a ValueChanged handler for the field tag under it.</summary>
    internal sealed class HtmlSuggestedActionsSource : ISuggestedActionsSource
    {
        private readonly ITextBuffer _buffer;
        private readonly System.IServiceProvider _services;
        private readonly IVsEditorAdaptersFactoryService _adapters;
        private readonly ITextDocumentFactoryService _documents;

        public HtmlSuggestedActionsSource(ITextBuffer buffer, System.IServiceProvider services, IVsEditorAdaptersFactoryService adapters, ITextDocumentFactoryService documents)
        {
            _buffer = buffer;
            _services = services;
            _adapters = adapters;
            _documents = documents;
        }

        public event EventHandler<EventArgs> SuggestedActionsChanged
        {
            add { }
            remove { }
        }

        public Task<bool> HasSuggestedActionsAsync(ISuggestedActionCategorySet requestedActionCategories, SnapshotSpan range, CancellationToken cancellationToken)
        {
            return Task.FromResult(FindActions(range).Count > 0 || PlanHandler(range) != null);
        }

        public IEnumerable<SuggestedActionSet> GetSuggestedActions(ISuggestedActionCategorySet requestedActionCategories, SnapshotSpan range, CancellationToken cancellationToken)
        {
            List<SuggestedActionSet> sets = new List<SuggestedActionSet>();
            List<ISuggestedAction> actions = FindActions(range);
            if (actions.Count > 0)
            {
                sets.Add(new SuggestedActionSet(PredefinedSuggestedActionCategoryNames.CodeFix, actions));
            }

            TsDeclaration handler = PlanHandler(range);
            if (handler != null)
            {
                sets.Add(new SuggestedActionSet(PredefinedSuggestedActionCategoryNames.Refactoring, new[] { new DeclareAction(handler, _services, _adapters) }));
            }

            return sets;
        }

        public bool TryGetTelemetryId(out Guid telemetryId)
        {
            telemetryId = Guid.Empty;
            return false;
        }

        public void Dispose()
        {
        }

        /// <summary>The handler a field tag on the caret's line could get, asked for by both the lightbulb's check and its menu.</summary>
        private TsDeclaration PlanHandler(SnapshotSpan range)
        {
            string path = EditorDocuments.Find(_buffer, _documents)?.FilePath;
            if (range.Snapshot.TextBuffer != _buffer || string.IsNullOrEmpty(path) || !Path.IsPathRooted(path))
            {
                return null;
            }

            return TypeScriptFixes.PlanValueChangedHandler(path, range.Snapshot.GetText(), range.Start.Position, range.End.Position, MuiLintPackage.TryReadFile, MuiLintPackage.TryListFolder);
        }

        private List<ISuggestedAction> FindActions(SnapshotSpan range)
        {
            List<ISuggestedAction> fixes = new List<ISuggestedAction>();
            List<ISuggestedAction> suppressions = new List<ISuggestedAction>();
            HtmlErrorTagger tagger;
            if (!_buffer.Properties.TryGetProperty(typeof(HtmlErrorTagger), out tagger)
                || !tagger.TryGetAnalysis(out ITextSnapshot analyzed, out IReadOnlyList<Diagnostic> diagnostics)
                || range.Snapshot.TextBuffer != _buffer)
            {
                return fixes;
            }

            string text = null;
            HashSet<string> offered = new HashSet<string>(StringComparer.Ordinal);
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
                else if (diagnostic.Id == DiagnosticIds.BindingNotInTypeScript && Path.IsPathRooted(diagnostic.Path))
                {
                    TsDeclaration plan = TypeScriptFixes.PlanFieldDeclaration(diagnostic.Path, text, diagnostic, MuiLintPackage.TryReadFile, MuiLintPackage.TryListFolder);
                    if (plan != null)
                    {
                        fixes.Add(new DeclareAction(plan, _services, _adapters));
                    }
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
            if (!edit.HasValue)
            {
                return;
            }

            ITrackingSpan span = analyzed.CreateTrackingSpan(edit.Value.Start, edit.Value.Length, SpanTrackingMode.EdgeExclusive);
            string newText = edit.Value.NewText;
            actions.Add(new SuggestedAction(title, () => _buffer.Replace(span.GetSpan(_buffer.CurrentSnapshot), newText)));
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

        private class SuggestedAction : ISuggestedAction
        {
            private readonly Action _invoke;

            public SuggestedAction(string displayText, Action invoke)
            {
                DisplayText = displayText;
                _invoke = invoke;
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

            public virtual void Invoke(CancellationToken cancellationToken)
            {
                _invoke();
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

        private sealed class DeclareAction : SuggestedAction
        {
            private readonly TsDeclaration _plan;
            private readonly System.IServiceProvider _services;
            private readonly IVsEditorAdaptersFactoryService _adapters;

            public DeclareAction(TsDeclaration plan, System.IServiceProvider services, IVsEditorAdaptersFactoryService adapters)
                : base(plan.Title, null)
            {
                _plan = plan;
                _services = services;
                _adapters = adapters;
            }

            public override void Invoke(CancellationToken cancellationToken)
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                // An HTML-only extension has no .ts yet; Declare writes the imports and the extension into an empty one.
                if (!File.Exists(_plan.TsPath))
                {
                    File.WriteAllText(_plan.TsPath, string.Empty);
                }

                IVsTextView view = VsDocuments.Open(_services, _plan.TsPath);
                if (view == null || view.GetBuffer(out IVsTextLines lines) != 0)
                {
                    return;
                }

                // Edit the open document, unsaved changes and all, and leave it for the user to save.
                ITextBuffer buffer = _adapters.GetDocumentBuffer(lines);
                if (buffer == null)
                {
                    return;
                }

                using (ITextEdit edit = buffer.CreateEdit())
                {
                    foreach (TextEdit change in TypeScriptFixes.Declare(buffer.CurrentSnapshot.GetText(), _plan))
                    {
                        edit.Replace(new Span(change.Start, change.Length), change.NewText);
                    }

                    edit.Apply();
                }
            }
        }
    }
}
