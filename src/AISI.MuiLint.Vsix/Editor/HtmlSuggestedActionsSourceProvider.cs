#nullable disable
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace AISI.MuiLint.Vsix
{
    /// <summary>Lightbulb quick fixes for the findings the <see cref="HtmlErrorTagger"/> reports, and the handler-writing action.</summary>
    [Export(typeof(ISuggestedActionsSourceProvider))]
    [Name("AISI.MuiLint.QuickFixes")]
    [ContentType("htmlx")]
    [ContentType("html")]
    internal sealed class HtmlSuggestedActionsSourceProvider : ISuggestedActionsSourceProvider
    {
        private readonly SVsServiceProvider _services;
        private readonly IVsEditorAdaptersFactoryService _adapters;
        private readonly ITextDocumentFactoryService _documents;

        [ImportingConstructor]
        public HtmlSuggestedActionsSourceProvider(SVsServiceProvider services, IVsEditorAdaptersFactoryService adapters, ITextDocumentFactoryService documents)
        {
            _services = services;
            _adapters = adapters;
            _documents = documents;
        }

        public ISuggestedActionsSource CreateSuggestedActionsSource(ITextView textView, ITextBuffer textBuffer)
        {
            return textBuffer == null ? null : new HtmlSuggestedActionsSource(textBuffer, _services, _adapters, _documents);
        }
    }
}
