#nullable disable
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace AISI.MuiLint.Vsix
{
    /// <summary>Lightbulb quick fixes for the findings the <see cref="HtmlErrorTagger"/> reports.</summary>
    [Export(typeof(ISuggestedActionsSourceProvider))]
    [Name("AISI.MuiLint.QuickFixes")]
    [ContentType("htmlx")]
    [ContentType("html")]
    internal sealed class HtmlSuggestedActionsSourceProvider : ISuggestedActionsSourceProvider
    {
        /// <inheritdoc />
        public ISuggestedActionsSource CreateSuggestedActionsSource(ITextView textView, ITextBuffer textBuffer)
        {
            return textBuffer == null ? null : new HtmlSuggestedActionsSource(textBuffer);
        }
    }
}
