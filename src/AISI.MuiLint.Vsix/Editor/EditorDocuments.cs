#nullable disable
using System.Collections.Generic;
using AISI.MuiLint;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Projection;

namespace AISI.MuiLint.Vsix
{
    internal static class EditorDocuments
    {
        /// <summary>Finds the file behind a buffer, looking through projection source buffers because htmlx rarely has a document on the top buffer.</summary>
        public static ITextDocument Find(ITextBuffer buffer, ITextDocumentFactoryService documents)
        {
            ITextDocument document;
            if (documents.TryGetTextDocument(buffer, out document))
            {
                return document;
            }

            IReadOnlyList<ITextBuffer> sources = NestedSourceWalk.Flatten(buffer, ProjectionSources);
            for (int i = 0; i < sources.Count; i++)
            {
                if (sources[i] != null && documents.TryGetTextDocument(sources[i], out document))
                {
                    return document;
                }
            }

            return null;
        }

        private static IEnumerable<ITextBuffer> ProjectionSources(ITextBuffer buffer)
        {
            IProjectionBufferBase projection = buffer as IProjectionBufferBase;
            if (projection == null)
            {
                return null;
            }

            return projection.SourceBuffers;
        }
    }
}
