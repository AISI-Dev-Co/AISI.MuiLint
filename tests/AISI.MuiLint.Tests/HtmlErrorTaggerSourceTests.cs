using System;
using System.IO;
using Xunit;

namespace AISI.MuiLint.Tests
{
    /// <summary>
    /// Pins production HTML-editor glue. Toy Flatten graphs stay green if
    /// ProjectionSources reverts to <c>as IProjectionBuffer</c>.
    /// </summary>
    public sealed class HtmlErrorTaggerSourceTests
    {
        [Fact]
        public void ProjectionSources_UsesProjectionBufferBase_NotProjectionBuffer()
        {
            string src = ReadEditor("HtmlErrorTagger.cs");
            Assert.Contains("buffer as IProjectionBufferBase", src, StringComparison.Ordinal);
            string withoutBase = src.Replace("as IProjectionBufferBase", string.Empty, StringComparison.Ordinal);
            Assert.DoesNotContain("as IProjectionBuffer", withoutBase, StringComparison.Ordinal);
        }

        [Fact]
        public void FileAndForget_ResolvesPackageJtfEachCall_NotCtorCapture()
        {
            string tagger = ReadEditor("HtmlErrorTagger.cs");
            Assert.Contains("ResolveJoinableTaskFactory()", tagger, StringComparison.Ordinal);
            Assert.Contains("MuiLintVsPackage.PackageJoinableTaskFactory", tagger, StringComparison.Ordinal);
            Assert.DoesNotContain("JoinableTaskFactory joinableTaskFactory", tagger, StringComparison.Ordinal);
            Assert.Contains("JoinableTaskContext joinableTaskContext", tagger, StringComparison.Ordinal);

            string provider = ReadEditor("HtmlErrorTaggerProvider.cs");
            Assert.DoesNotContain("PackageJoinableTaskFactory ?? joinableTaskContext.Factory", provider, StringComparison.Ordinal);
            Assert.DoesNotContain("JoinableTaskFactory jtf", provider, StringComparison.Ordinal);
        }

        [Fact]
        public void CreateTagger_DoesNotCallAddView()
        {
            string src = ReadEditor("HtmlErrorTaggerProvider.cs");
            int start = src.IndexOf("public ITagger<T> CreateTagger", StringComparison.Ordinal);
            int end = src.IndexOf("public void TextViewCreated", StringComparison.Ordinal);
            Assert.True(start >= 0 && end > start, "CreateTagger / TextViewCreated missing");
            string createTagger = src.Substring(start, end - start);
            Assert.DoesNotContain("AddView()", createTagger, StringComparison.Ordinal);
            Assert.Contains("tagger.AddView()", src, StringComparison.Ordinal);
        }

        [Fact]
        public void Package_AutoLoadsOnShellInit_NoVssdk007Pragma()
        {
            string pkg = ReadEditor("MuiLintVsPackage.cs");
            Assert.Contains("ProvideAutoLoad(VSConstants.UICONTEXT.ShellInitialized_string, PackageAutoLoadFlags.BackgroundLoad)", pkg, StringComparison.Ordinal);
            Assert.DoesNotContain("warning disable VSSDK007", pkg, StringComparison.Ordinal);
            Assert.DoesNotContain("warning disable VSSDK007", ReadEditor("HtmlErrorTagger.cs"), StringComparison.Ordinal);
            Assert.DoesNotContain("warning disable VSSDK007", ReadEditor("HtmlErrorTaggerProvider.cs"), StringComparison.Ordinal);
        }

        private static string ReadEditor(string fileName)
        {
            string path = Path.Combine(AppContext.BaseDirectory, "EditorSnapshots", fileName);
            Assert.True(File.Exists(path), "Missing snapshot " + path);
            return File.ReadAllText(path);
        }
    }
}
