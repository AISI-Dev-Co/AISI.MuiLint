#nullable disable
using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace AISI.MuiLint.Vsix
{
    /// <summary>
    /// MEF tagger provider for the VS 2022 Web Tools HTML editor (<c>htmlx</c>) and the
    /// classic HTML editor (<c>html</c>).
    /// </summary>
    [Export(typeof(ITaggerProvider))]
    [ContentType("htmlx")]
    [ContentType("html")]
    [TagType(typeof(IErrorTag))]
    [Name("AISI.MuiLint.HtmlErrorTagger")]
    internal sealed class HtmlErrorTaggerProvider : ITaggerProvider
    {
        private readonly ITextDocumentFactoryService _textDocumentFactory;
        private readonly HtmlErrorTableDataSource _tableDataSource;

        [ImportingConstructor]
        public HtmlErrorTaggerProvider(
            ITextDocumentFactoryService textDocumentFactory,
            HtmlErrorTableDataSource tableDataSource)
        {
            _textDocumentFactory = textDocumentFactory ?? throw new ArgumentNullException(nameof(textDocumentFactory));
            _tableDataSource = tableDataSource ?? throw new ArgumentNullException(nameof(tableDataSource));
        }

        /// <inheritdoc />
        public ITagger<T> CreateTagger<T>(ITextBuffer buffer)
            where T : ITag
        {
            if (buffer is null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            HtmlErrorTagger tagger = buffer.Properties.GetOrCreateSingletonProperty(
                () => new HtmlErrorTagger(buffer, _textDocumentFactory, _tableDataSource));
            return tagger as ITagger<T>;
        }
    }
}
