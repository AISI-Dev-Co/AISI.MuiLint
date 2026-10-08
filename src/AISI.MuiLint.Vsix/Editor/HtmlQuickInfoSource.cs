#nullable disable
using System.ComponentModel.Composition;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AISI.MuiLint;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;

namespace AISI.MuiLint.Vsix
{
    [Export(typeof(IAsyncQuickInfoSourceProvider))]
    [Name("AISI.MuiLint.QuickInfo")]
    [ContentType("htmlx")]
    [ContentType("html")]
    internal sealed class HtmlQuickInfoSourceProvider : IAsyncQuickInfoSourceProvider
    {
        private readonly ITextDocumentFactoryService _documents;

        [ImportingConstructor]
        public HtmlQuickInfoSourceProvider(ITextDocumentFactoryService documents)
        {
            _documents = documents;
        }

        /// <inheritdoc />
        public IAsyncQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer)
        {
            return textBuffer.Properties.GetOrCreateSingletonProperty(() => new HtmlQuickInfoSource(textBuffer, _documents));
        }
    }

    /// <summary>Hover on a selector or a binding: where it's declared, and what it is.</summary>
    internal sealed class HtmlQuickInfoSource : IAsyncQuickInfoSource
    {
        private readonly ITextBuffer _buffer;
        private readonly ITextDocumentFactoryService _documents;

        public HtmlQuickInfoSource(ITextBuffer buffer, ITextDocumentFactoryService documents)
        {
            _buffer = buffer;
            _documents = documents;
        }

        /// <inheritdoc />
        public Task<QuickInfoItem> GetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken)
        {
            SnapshotPoint? point = session.GetTriggerPoint(_buffer.CurrentSnapshot);
            string path = EditorDocuments.Find(_buffer, _documents)?.FilePath;
            if (point == null || string.IsNullOrEmpty(path) || !Path.IsPathRooted(path))
            {
                return Task.FromResult<QuickInfoItem>(null);
            }

            ITextSnapshot snapshot = point.Value.Snapshot;
            int caret = point.Value.Position;
            return Task.Run(
                () =>
                {
                    NavigationTarget? target = MuiNavigation.Find(path, snapshot.GetText(), caret, MuiLintPackage.TryReadFile, MuiLintPackage.TryListFolder);
                    if (target == null)
                    {
                        return null;
                    }

                    ITrackingSpan span = snapshot.CreateTrackingSpan(target.Value.Start, target.Value.Length, SpanTrackingMode.EdgeInclusive);
                    return new QuickInfoItem(span, target.Value.Description);
                },
                cancellationToken);
        }

        /// <inheritdoc />
        public void Dispose()
        {
        }
    }
}
