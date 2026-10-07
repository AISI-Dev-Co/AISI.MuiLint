#nullable disable
using System;
using System.ComponentModel.Composition;
using System.IO;
using AISI.MuiLint;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;

namespace AISI.MuiLint.Vsix
{
    /// <summary>Puts <see cref="HtmlGoToDefinitionFilter"/> in front of every HTML editor's command chain.</summary>
    [Export(typeof(IVsTextViewCreationListener))]
    [ContentType("htmlx")]
    [ContentType("html")]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    internal sealed class HtmlGoToDefinitionProvider : IVsTextViewCreationListener
    {
        private readonly IVsEditorAdaptersFactoryService _adapters;
        private readonly ITextDocumentFactoryService _documents;
        private readonly SVsServiceProvider _services;

        [ImportingConstructor]
        public HtmlGoToDefinitionProvider(
            IVsEditorAdaptersFactoryService adapters,
            ITextDocumentFactoryService documents,
            SVsServiceProvider services)
        {
            _adapters = adapters;
            _documents = documents;
            _services = services;
        }

        /// <inheritdoc />
        public void VsTextViewCreated(IVsTextView textViewAdapter)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            IWpfTextView view = _adapters.GetWpfTextView(textViewAdapter);
            if (view == null || view.Properties.ContainsProperty(typeof(HtmlGoToDefinitionFilter)))
            {
                return;
            }

            var filter = new HtmlGoToDefinitionFilter(view, _documents, _services);
            if (ErrorHandler.Succeeded(textViewAdapter.AddCommandFilter(filter, out IOleCommandTarget next)))
            {
                filter.Next = next;
                view.Properties.AddProperty(typeof(HtmlGoToDefinitionFilter), filter);
            }
        }
    }

    /// <summary>
    /// F12 on a merge selector, view.bind, state.bind or a field's name opens the stock HTML or
    /// the .ts at the declaration. Anything else goes on to the HTML editor as usual.
    /// </summary>
    internal sealed class HtmlGoToDefinitionFilter : IOleCommandTarget
    {
        private readonly IWpfTextView _view;
        private readonly ITextDocumentFactoryService _documents;
        private readonly SVsServiceProvider _services;

        public HtmlGoToDefinitionFilter(IWpfTextView view, ITextDocumentFactoryService documents, SVsServiceProvider services)
        {
            _view = view;
            _documents = documents;
            _services = services;
        }

        public IOleCommandTarget Next { get; set; }

        /// <inheritdoc />
        public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            int result = Next.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);

            // Keep F12 available even where the HTML editor itself has nothing to offer.
            if (pguidCmdGroup == VSConstants.GUID_VSStandardCommandSet97)
            {
                for (int i = 0; i < cCmds; i++)
                {
                    if (prgCmds[i].cmdID == (uint)VSConstants.VSStd97CmdID.GotoDefn)
                    {
                        prgCmds[i].cmdf = (uint)(OLECMDF.OLECMDF_SUPPORTED | OLECMDF.OLECMDF_ENABLED);
                        result = VSConstants.S_OK;
                    }
                }
            }

            return result;
        }

        /// <inheritdoc />
        public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (pguidCmdGroup == VSConstants.GUID_VSStandardCommandSet97
                && nCmdID == (uint)VSConstants.VSStd97CmdID.GotoDefn
                && TryGoToDefinition())
            {
                return VSConstants.S_OK;
            }

            return Next.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
        }

        private bool TryGoToDefinition()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            ITextDocument document = EditorDocuments.Find(_view.TextBuffer, _documents);
            if (document == null || !Path.IsPathRooted(document.FilePath))
            {
                return false;
            }

            // htmlx shows a projection; the offsets we need are in the file's own buffer.
            SnapshotPoint? caret = _view.BufferGraph.MapDownToBuffer(
                _view.Caret.Position.BufferPosition,
                PointTrackingMode.Positive,
                document.TextBuffer,
                PositionAffinity.Successor);
            if (caret == null)
            {
                return false;
            }

            SourceLocation? target = MuiNavigation.FindDefinition(
                document.FilePath,
                caret.Value.Snapshot.GetText(),
                caret.Value.Position,
                MuiLintPackage.TryReadFile);
            if (target == null)
            {
                return false;
            }

            IVsTextView view = VsDocuments.Open(_services, target.Value.Path);
            if (view == null)
            {
                return false;
            }

            view.SetCaretPos(target.Value.Line - 1, target.Value.Column - 1);
            view.CenterLines(target.Value.Line - 1, 1);
            return true;
        }
    }
}
