#nullable disable
using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace AISI.MuiLint.Vsix
{
    /// <summary>
    /// MEF tagger provider for the VS 2022 Web Tools HTML editor (<c>htmlx</c>) and the
    /// classic HTML editor (<c>html</c>). One <see cref="HtmlErrorTagger"/> per buffer even
    /// when both content types match (htmlx may derive from html). Teardown is buffer close
    /// (<c>IVsTextBufferDataEvents.OnCloseEvent</c>), not a fake <see cref="CreateTagger"/>
    /// refcount (the aggregator can call CreateTagger N times with no ReleaseView).
    /// Peek/diff skip <see cref="TextViewCreated"/> (<c>Document</c> role); OnCloseEvent covers them.
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
        private readonly ITextDocumentFactoryService _textDocumentFactory;
        private readonly IVsEditorAdaptersFactoryService _adaptersFactory;
        private readonly HtmlErrorTableDataSource _tableDataSource;

        [ImportingConstructor]
        public HtmlErrorTaggerProvider(
            ITextDocumentFactoryService textDocumentFactory,
            IVsEditorAdaptersFactoryService adaptersFactory,
            HtmlErrorTableDataSource tableDataSource)
        {
            _textDocumentFactory = textDocumentFactory ?? throw new ArgumentNullException(nameof(textDocumentFactory));
            _adaptersFactory = adaptersFactory ?? throw new ArgumentNullException(nameof(adaptersFactory));
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

            // Keyed by typeof(ViewHook): htmlx may derive from html, so this listener can
            // fire twice for one view. PropertyCollection.GetOrCreateSingletonProperty<T>
            // requires T : class (returning bool does not compile on VS SDK 17.0).
            textView.Properties.GetOrCreateSingletonProperty(
                () =>
                {
                    HtmlErrorTagger tagger = GetOrCreateTagger(textView.TextBuffer);
                    tagger.AddView();
                    textView.Closed += (sender, args) => tagger.ReleaseView();
                    return new ViewHook();
                });
        }

        private HtmlErrorTagger GetOrCreateTagger(ITextBuffer buffer)
        {
            return buffer.Properties.GetOrCreateSingletonProperty(
                () => new HtmlErrorTagger(
                    buffer,
                    _textDocumentFactory,
                    _adaptersFactory,
                    _tableDataSource));
        }

        private sealed class ViewHook
        {
        }
    }
}
