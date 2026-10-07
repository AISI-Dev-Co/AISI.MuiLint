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
    /// <summary>
    /// Offers fixes for findings under the caret. HTML edits are computed by <see cref="MuiLintFixes"/>
    /// against the snapshot the tagger analysed, then tracked forward to whatever the buffer is now;
    /// TypeScript ones open the .ts and edit it there, so you see what was written.
    /// </summary>
    internal sealed class HtmlSuggestedActionsSource : ISuggestedActionsSource
    {
        private readonly ITextBuffer _buffer;
        private readonly System.IServiceProvider _services;
        private readonly IVsEditorAdaptersFactoryService _adapters;

        public HtmlSuggestedActionsSource(ITextBuffer buffer, System.IServiceProvider services, IVsEditorAdaptersFactoryService adapters)
        {
            _buffer = buffer;
            _services = services;
            _adapters = adapters;
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
                else if (diagnostic.Id == DiagnosticIds.BindingNotInTypeScript && Path.IsPathRooted(diagnostic.Path))
                {
                    FieldDeclaration plan = TypeScriptFixes.PlanFieldDeclaration(diagnostic.Path, text, diagnostic, MuiLintPackage.TryReadFile, MuiLintPackage.TryListFolder);
                    if (plan != null)
                    {
                        string title = "Declare " + plan.Field + " in " + plan.TargetClass + " (" + Path.GetFileName(plan.TsPath) + ")";
                        fixes.Add(new DeclareFieldAction(title, plan, _services, _adapters));
                    }
                }
                else if (diagnostic.Id == DiagnosticIds.ExtensionWithoutTypeScript && Path.IsPathRooted(diagnostic.Path))
                {
                    string tsPath = Path.ChangeExtension(diagnostic.Path, ".ts");
                    string content = TypeScriptFixes.NewExtensionTypeScript(diagnostic.Path);
                    if (content != null)
                    {
                        fixes.Add(new CreateFileAction("Create " + Path.GetFileName(tsPath), tsPath, content, _services));
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

        private sealed class DeclareFieldAction : SuggestedAction
        {
            private readonly FieldDeclaration _plan;
            private readonly System.IServiceProvider _services;
            private readonly IVsEditorAdaptersFactoryService _adapters;

            public DeclareFieldAction(string displayText, FieldDeclaration plan, System.IServiceProvider services, IVsEditorAdaptersFactoryService adapters)
                : base(displayText, null)
            {
                _plan = plan;
                _services = services;
                _adapters = adapters;
            }

            public override void Invoke(CancellationToken cancellationToken)
            {
                ThreadHelper.ThrowIfNotOnUIThread();
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
                    foreach (TextEdit change in TypeScriptFixes.DeclareField(buffer.CurrentSnapshot.GetText(), _plan))
                    {
                        edit.Replace(new Span(change.Start, change.Length), change.NewText);
                    }

                    edit.Apply();
                }
            }
        }

        private sealed class CreateFileAction : SuggestedAction
        {
            private readonly string _path;
            private readonly string _content;
            private readonly System.IServiceProvider _services;

            public CreateFileAction(string displayText, string path, string content, System.IServiceProvider services)
                : base(displayText, null)
            {
                _path = path;
                _content = content;
                _services = services;
            }

            public override void Invoke(CancellationToken cancellationToken)
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                if (!File.Exists(_path))
                {
                    File.WriteAllText(_path, _content);
                }

                VsDocuments.Open(_services, _path);
            }
        }
    }
}
