using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace AISI.MuiLint.Tests
{
    /// <summary>VS ignores a snippet it can't read, without a word, so check them here.</summary>
    public sealed class SnippetTests
    {
        private static readonly XNamespace Ns = "http://schemas.microsoft.com/VisualStudio/2005/CodeSnippet";
        private static readonly string Folder = Path.Combine(TestFiles.RepoRoot, "src", "AISI.MuiLint.Vsix", "Snippets");

        public static TheoryData<string> Snippets()
        {
            var data = new TheoryData<string>();
            foreach (string file in Directory.GetFiles(Path.Combine(Folder, "TypeScript"), "*.snippet"))
            {
                data.Add(Path.GetFileName(file));
            }

            return data;
        }

        [Theory]
        [MemberData(nameof(Snippets))]
        public void EverySnippet_DeclaresWhatItUsesAndUsesWhatItDeclares(string file)
        {
            XDocument doc = XDocument.Load(Path.Combine(Folder, "TypeScript", file));
            XElement snippet = doc.Root!.Element(Ns + "CodeSnippet")!;
            XElement code = snippet.Element(Ns + "Snippet")!.Element(Ns + "Code")!;

            Assert.Equal("TypeScript", code.Attribute("Language")?.Value);
            Assert.Equal(Path.GetFileNameWithoutExtension(file), snippet.Element(Ns + "Header")!.Element(Ns + "Shortcut")!.Value);

            string[] declared = snippet.Descendants(Ns + "Literal").Select(l => l.Element(Ns + "ID")!.Value).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            string[] used = Regex.Matches(code.Value, @"\$(\w+)\$").Select(m => m.Groups[1].Value)
                .Where(id => id != "end" && id != "selected").Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray();
            Assert.Equal(declared, used);
            Assert.Contains("$end$", code.Value, StringComparison.Ordinal);
        }

        [Fact]
        public void Pkgdef_PointsAtTheSnippetFolder()
        {
            string pkgdef = File.ReadAllText(Path.Combine(Folder, "AISI.MuiLint.Snippets.pkgdef"));
            Assert.Contains(@"[$RootKey$\Languages\CodeExpansions\TypeScript\Paths]", pkgdef, StringComparison.Ordinal);
            Assert.Contains(@"""$PackageFolder$\Snippets\TypeScript""", pkgdef, StringComparison.Ordinal);
        }
    }
}
