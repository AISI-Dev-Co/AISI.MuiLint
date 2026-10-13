#nullable disable
using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;

namespace AISI.MuiLint.Vsix
{
    /// <summary>One tagger per buffer, never refcounted by CreateTagger because the aggregator can call it many times with no matching release.</summary>
    [Export(typeof(ITaggerProvider))]
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType("htmlx")]
    [ContentType("html")]
    [ContentType("TypeScript")]
    [TagType(typeof(IErrorTag))]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    [Name("AISI.MuiLint.HtmlErrorTagger")]
    internal sealed class HtmlErrorTaggerProvider : ITaggerProvider, IWpfTextViewCreationListener
    {
        private readonly ITextDocumentFactoryService _textDocumentFactory;
        private readonly IVsEditorAdaptersFactoryService _adaptersFactory;
        private readonly HtmlErrorTableDataSource _tableDataSource;
        private readonly JoinableTaskContext _joinableTaskContext;

        [ImportingConstructor]
        public HtmlErrorTaggerProvider(
            ITextDocumentFactoryService textDocumentFactory,
            IVsEditorAdaptersFactoryService adaptersFactory,
            HtmlErrorTableDataSource tableDataSource,
            JoinableTaskContext joinableTaskContext)
        {
            _textDocumentFactory = textDocumentFactory ?? throw new ArgumentNullException(nameof(textDocumentFactory));
            _adaptersFactory = adaptersFactory ?? throw new ArgumentNullException(nameof(adaptersFactory));
            _tableDataSource = tableDataSource ?? throw new ArgumentNullException(nameof(tableDataSource));
            _joinableTaskContext = joinableTaskContext ?? throw new ArgumentNullException(nameof(joinableTaskContext));
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
                    _tableDataSource,
                    _joinableTaskContext));
        }

        private sealed class ViewHook
        {
        }
    }
}
