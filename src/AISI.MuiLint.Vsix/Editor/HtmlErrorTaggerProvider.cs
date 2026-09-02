#nullable disable
using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace AISI.MuiLint.Vsix
{
    /// <summary>
    /// MEF tagger provider for the VS 2022 Web Tools HTML editor (<c>htmlx</c>) and the
    /// classic HTML editor (<c>html</c>). One <see cref="HtmlErrorTagger"/> per buffer even
    /// when both content types match (htmlx may derive from html). Views refcount Dispose
    /// so untitled buffers unhook <c>Changed</c> and the Error List factory.
    /// </summary>
    [Export(typeof(ITaggerProvider))]
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType("htmlx")]
    [ContentType("html")]
    [TagType(typeof(IErrorTag))]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    [Name("AISI.MuiLint.HtmlErrorTagger")]
    internal sealed class HtmlErrorTaggerProvider : ITaggerProvider, IWpfTextViewCreationListener
    {
        internal static readonly object TaggerKey = typeof(HtmlErrorTagger);

        private static readonly object ViewHookKey = typeof(HtmlErrorTaggerProvider);

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

            return GetOrCreateTagger(buffer) as ITagger<T>;
        }

        /// <inheritdoc />
        public void TextViewCreated(IWpfTextView textView)
        {
            if (textView?.TextBuffer is null)
            {
                return;
            }

            textView.Properties.GetOrCreateSingletonProperty(
                ViewHookKey,
                () =>
                {
                    HtmlErrorTagger tagger = GetOrCreateTagger(textView.TextBuffer);
                    tagger.AddView();
                    textView.Closed += (sender, args) => tagger.ReleaseView();
                    return true;
                });
        }

        private HtmlErrorTagger GetOrCreateTagger(ITextBuffer buffer)
        {
            return buffer.Properties.GetOrCreateSingletonProperty(
                TaggerKey,
                () => new HtmlErrorTagger(buffer, _textDocumentFactory, _tableDataSource));
        }
    }
}
