#nullable disable
using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;

namespace AISI.MuiLint.Vsix
{
    /// <summary>One synchronous completion source per Modern UI HTML buffer, with no joinable task factory because completion is cheap.</summary>
    [Export(typeof(ICompletionSourceProvider))]
    [Name("AISI.MuiLint.HtmlCompletion")]
    [ContentType("htmlx")]
    [ContentType("html")]
    [Order(Before = "default")]
    internal sealed class HtmlCompletionSourceProvider : ICompletionSourceProvider
    {
        private readonly ITextDocumentFactoryService _textDocumentFactory;

        [ImportingConstructor]
        public HtmlCompletionSourceProvider(ITextDocumentFactoryService textDocumentFactory)
        {
            _textDocumentFactory = textDocumentFactory ?? throw new ArgumentNullException(nameof(textDocumentFactory));
        }

        /// <inheritdoc />
        public ICompletionSource TryCreateCompletionSource(ITextBuffer textBuffer)
        {
            if (textBuffer is null)
            {
                throw new ArgumentNullException(nameof(textBuffer));
            }

            return textBuffer.Properties.GetOrCreateSingletonProperty(
                () => new HtmlCompletionSource(textBuffer, _textDocumentFactory));
        }
    }
}
