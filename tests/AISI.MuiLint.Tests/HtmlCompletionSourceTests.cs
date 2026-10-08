using System;
using System.IO;
using Xunit;

namespace AISI.MuiLint.Tests
{
    /// <summary>
    /// Pins completion-source HTML-editor glue. Toy Flatten graphs stay green if
    /// ProjectionSources reverts to <c>as IProjectionBuffer</c>.
    /// </summary>
    public sealed class HtmlCompletionSourceTests
    {
        [Fact]
        public void ProjectionSources_UsesProjectionBufferBase_NotProjectionBuffer()
        {
            string src = ReadEditor("EditorDocuments.cs");
            Assert.Contains("buffer as IProjectionBufferBase", src, StringComparison.Ordinal);
            string withoutBase = src.Replace("as IProjectionBufferBase", string.Empty, StringComparison.Ordinal);
            Assert.DoesNotContain("as IProjectionBuffer", withoutBase, StringComparison.Ordinal);
        }

        private static string ReadEditor(string fileName)
        {
            string path = Path.Combine(AppContext.BaseDirectory, "EditorSnapshots", fileName);
            Assert.True(File.Exists(path), "Missing snapshot " + path);
            return File.ReadAllText(path);
        }
    }
}
