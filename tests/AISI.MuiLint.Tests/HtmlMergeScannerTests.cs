using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Xunit;
using RoslynDiagnostic = Microsoft.CodeAnalysis.Diagnostic;

namespace AISI.MuiLint.Tests
{
    public sealed class HtmlMergeScannerTests
    {
        private static readonly string FixturesRoot = Path.Combine(AppContext.BaseDirectory, "Fixtures");

        private static readonly string[] TypeScriptRules =
        {
            DiagnosticIds.HalfAnExtension,
            DiagnosticIds.DecoratorViewNotDeclared,
            DiagnosticIds.ViewFromNonView,
        };

        public static TheoryData<string, string> FailFixtures()
        {
            return Enumerate("fail");
        }

        public static TheoryData<string, string> PassFixtures()
        {
            return Enumerate("pass");
        }

        [Theory]
        [MemberData(nameof(FailFixtures))]
        public void FailFixture_ReportsExpectedId(string id, string path)
        {
            string text = File.ReadAllText(path);
            IReadOnlyList<Diagnostic> results = MuiLinter.Analyze(path, text, TestFiles.ReadDisk);
            Assert.Contains(results, d => string.Equals(d.Id, id, StringComparison.Ordinal));
        }

        [Theory]
        [MemberData(nameof(PassFixtures))]
        public void PassFixture_DoesNotReportId(string id, string path)
        {
            string text = File.ReadAllText(path);
            IReadOnlyList<Diagnostic> results = MuiLinter.Analyze(path, text, TestFiles.ReadDisk);
            Assert.DoesNotContain(results, d => string.Equals(d.Id, id, StringComparison.Ordinal));
        }

        [Fact]
        public void Fixtures_CoverEveryRuleButTheStockOne()
        {
            // AISI0009 needs a stock screen next to the extension; RuleTests builds one in memory.
            string[] expected = Rules.All
                .Select(r => r.Id)
                .Where(id => id != DiagnosticIds.SelectorNotInStock)
                .ToArray();
            Assert.Equal(expected, FailFixtures().Select(row => (string)row[0]!).Distinct().OrderBy(x => x, StringComparer.Ordinal));
            Assert.Equal(expected, PassFixtures().Select(row => (string)row[0]!).Distinct().OrderBy(x => x, StringComparer.Ordinal));
        }

        [Fact]
        public void Scan_NullPath_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => Analyzer.Scan(null!, "<template></template>"));
        }

        [Fact]
        public void Scan_NullText_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => Analyzer.Scan("x.html", null!));
        }

        [Fact]
        public void Fieldset_DoesNotMatchAsField()
        {
            const string html = "<template><fieldset name=\"x\"/></template>";
            IReadOnlyList<Diagnostic> results = Analyzer.Scan("x.html", html);
            Assert.DoesNotContain(results, d => d.Id == DiagnosticIds.SelfClosing);
        }

        [Fact]
        public void StockPath_IsIgnoredUnderDevelopmentScreens()
        {
            Assert.False(HtmlMergeScanner.IsStockScreensPath(@"C:\\site\\FrontendSources\\screen\\src\\development\\screens\\SO\\SO301000\\SO301000.html"));
            Assert.False(HtmlMergeScanner.IsStockScreensPath("/site/FrontendSources/screen/src/customizationScreens/AISI/SO/SO301000/SO301000.html"));
            Assert.True(HtmlMergeScanner.IsStockScreensPath("/site/FrontendSources/screen/src/screens/SO/SO301000/SO301000.html"));
        }

        [Fact]
        public void SupportedDiagnostics_MapSeverities()
        {
            var analyzer = new Analyzer();
            Assert.Equal(DiagnosticSeverity.Error, analyzer.SupportedDiagnostics.Single(d => d.Id == DiagnosticIds.SelfClosing).DefaultSeverity);
            Assert.Equal(DiagnosticSeverity.Warning, analyzer.SupportedDiagnostics.Single(d => d.Id == DiagnosticIds.DuplicateNameOrId).DefaultSeverity);
            Assert.Equal(DiagnosticSeverity.Info, analyzer.SupportedDiagnostics.Single(d => d.Id == DiagnosticIds.FieldWithoutUsrPrefix).DefaultSeverity);
        }

        [Fact]
        public void EveryRule_HasADocPage()
        {
            string docs = Path.Combine(TestFiles.RepoRoot, "docs", "rules");
            Assert.All(Rules.All, r => Assert.True(File.Exists(Path.Combine(docs, r.Id + ".md")), "Missing docs/rules/" + r.Id + ".md"));
        }

        [Fact]
        public void RoslynAdditionalFile_ReportsSelfClosingField()
        {
            string path = Path.Combine("ext", "SO301000_Custom.html");
            const string html = "<template><field name=\"OrderNbr\"/></template>";
            ImmutableArray<RoslynDiagnostic> diags = RunAnalyzer(path, html);
            Assert.Contains(diags, d => d.Id == DiagnosticIds.SelfClosing);
        }

        [Fact]
        public void PackageStub_CallsAnalyzer()
        {
            const string html = "<template><field name=\"OrderNbr\"/></template>";
            IReadOnlyList<Diagnostic> viaAnalyzer = Analyzer.Scan("x.html", html);
            IReadOnlyList<Diagnostic> viaScanner = HtmlMergeScanner.Analyze("x.html", html);
            Assert.Equal(viaAnalyzer.Count, viaScanner.Count);
            Assert.Equal(viaAnalyzer[0].Id, viaScanner[0].Id);
        }

        [Fact]
        public void PackageAnalyze_EqualsHtmlMergeScanner()
        {
            const string html = "<template><field name=\"OrderNbr\"/></template>";
            IReadOnlyList<Diagnostic> viaPackage = AISI.MuiLint.Vsix.MuiLintPackage.Analyze("x.html", html);
            IReadOnlyList<Diagnostic> viaScanner = HtmlMergeScanner.Analyze("x.html", html);
            Assert.Equal(viaScanner.Count, viaPackage.Count);
            for (int i = 0; i < viaScanner.Count; i++)
            {
                Assert.Equal(viaScanner[i].Id, viaPackage[i].Id);
                Assert.Equal(viaScanner[i].Start, viaPackage[i].Start);
                Assert.Equal(viaScanner[i].Length, viaPackage[i].Length);
                Assert.Equal(viaScanner[i].Message, viaPackage[i].Message);
            }
        }

        private static ImmutableArray<RoslynDiagnostic> RunAnalyzer(string path, string html)
        {
            CSharpCompilation compilation = CSharpCompilation.Create(
                "MuiLintAnalyzerTest",
                new[] { CSharpSyntaxTree.ParseText("class Probe { }") },
                new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var additional = new TestAdditionalText(path, html);
            var analyzer = new Analyzer();
            var compilationWithAnalyzers = new CompilationWithAnalyzers(
                compilation,
                ImmutableArray.Create<DiagnosticAnalyzer>(analyzer),
                new AnalyzerOptions(ImmutableArray.Create<AdditionalText>(additional)),
                CancellationToken.None);

            return compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult();
        }


        private static TheoryData<string, string> Enumerate(string kind)
        {
            var data = new TheoryData<string, string>();
            string root = Path.Combine(FixturesRoot, kind);
            Assert.True(Directory.Exists(root), "Missing fixtures at " + root);
            foreach (string idDir in Directory.GetDirectories(root))
            {
                // TypeScript rules are checked on their .ts files; the stock screens beside them are context.
                string id = Path.GetFileName(idDir);
                bool typeScript = TypeScriptRules.Contains(id);
                foreach (string file in Directory.GetFiles(idDir, typeScript ? "*.ts" : "*.html", SearchOption.AllDirectories))
                {
                    if (!typeScript || !HtmlMergeScanner.IsStockScreensPath(file))
                    {
                        data.Add(id, file);
                    }
                }
            }

            return data;
        }

        private sealed class TestAdditionalText : AdditionalText
        {
            private readonly SourceText _text;

            public TestAdditionalText(string path, string text)
            {
                Path = path;
                _text = SourceText.From(text);
            }

            public override string Path { get; }

            public override SourceText GetText(CancellationToken cancellationToken = default)
            {
                return _text;
            }
        }
    }
}
