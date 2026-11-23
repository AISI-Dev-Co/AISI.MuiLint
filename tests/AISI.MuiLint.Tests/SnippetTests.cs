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
            TheoryData<string> data = new TheoryData<string>();
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

        [Theory]
        [InlineData("muionvaluechanged")]
        [InlineData("muionrowselected")]
        [InlineData("muioncurrentrowchanged")]
        [InlineData("muirowcss")]
        [InlineData("muicellcss")]
        [InlineData("muioncallback")]
        public void HandlerSnippets_PassTheHandlerRules(string shortcut)
        {
            XElement snippet = XDocument.Load(Path.Combine(Folder, "TypeScript", shortcut + ".snippet")).Root!.Element(Ns + "CodeSnippet")!.Element(Ns + "Snippet")!;
            string code = snippet.Element(Ns + "Code")!.Value.Replace("$end$", string.Empty, StringComparison.Ordinal);
            foreach (XElement literal in snippet.Descendants(Ns + "Literal"))
            {
                code = code.Replace("$" + literal.Element(Ns + "ID")!.Value + "$", literal.Element(Ns + "Default")!.Value, StringComparison.Ordinal);
            }

            string ts =
                "export class XX301000 extends PXScreen {\n" +
                "    Document = createSingle(Header);\n" +
                "    Transactions = createCollection(Line);\n" +
                code + "\n" +
                "}\n" +
                "export class Header extends PXView { CustomerID: PXFieldState; }\n" +
                "export class Line extends PXView { OrderQty: PXFieldState; }\n";
            const string path = "/site/src/development/screens/XX/XX301000/XX301000.ts";
            Assert.Empty(TypeScriptScanner.Analyze(path, ts, p => p == path ? ts : null));
            Assert.Single(new TsModule(path, ts).Handlers);
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
