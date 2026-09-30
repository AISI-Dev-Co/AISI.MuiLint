using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace AISI.MuiLint.Tests
{
    public sealed class MuiLintFixesTests
    {
        [Theory]
        [InlineData("<field name=\"UsrA\"/>", "<field name=\"UsrA\"></field>")]
        [InlineData("<field name=\"UsrA\" />", "<field name=\"UsrA\"></field>")]
        [InlineData("<qp-grid id=\"grid\"\n  view.bind=\"Lines\" />", "<qp-grid id=\"grid\"\n  view.bind=\"Lines\"></qp-grid>")]
        public void ExpandSelfClosing(string tag, string expected)
        {
            string html = "<template>\n  " + tag + "\n</template>";
            Diagnostic d = Single(html, DiagnosticIds.SelfClosing);

            TextEdit? edit = MuiLintFixes.ExpandSelfClosing(html, d);

            Assert.NotNull(edit);
            Assert.Equal("<template>\n  " + expected + "\n</template>", TestFiles.Apply(html, edit.Value));
        }

        [Fact]
        public void RemoveEmptyFieldset_TakesItsLinesWithIt()
        {
            const string html =
                "<template>\n" +
                "  <field name=\"UsrA\" after=\"[name='B']\"></field>\n" +
                "  <qp-fieldset id=\"fsEmpty\" view.bind=\"Document\">\n" +
                "    <!-- later -->\n" +
                "  </qp-fieldset>\n" +
                "</template>\n";

            string fixedHtml = TestFiles.Apply(html, MuiLintFixes.RemoveEmptyFieldset(html, Single(html, DiagnosticIds.EmptyFieldset))!.Value);

            Assert.Equal("<template>\n  <field name=\"UsrA\" after=\"[name='B']\"></field>\n</template>\n", fixedHtml);
        }

        [Fact]
        public void RemoveEmptyFieldset_OnOneLineWithOtherMarkup_RemovesJustTheFieldset()
        {
            const string html = "<template><qp-fieldset id=\"a\"></qp-fieldset><field name=\"UsrA\"></field></template>";
            string fixedHtml = TestFiles.Apply(html, MuiLintFixes.RemoveEmptyFieldset(html, Single(html, DiagnosticIds.EmptyFieldset))!.Value);
            Assert.Equal("<template><field name=\"UsrA\"></field></template>", fixedHtml);
        }

        [Fact]
        public void Suppress_KeepsIndentationAndLineEndings()
        {
            const string html = "<template>\r\n    <field name=\"UsrA\"/>\r\n</template>";
            string fixedHtml = TestFiles.Apply(html, MuiLintFixes.Suppress(html, Single(html, DiagnosticIds.SelfClosing)));
            Assert.Equal("<template>\r\n    <!-- muilint-disable-next-line AISI0001 -->\r\n    <field name=\"UsrA\"/>\r\n</template>", fixedHtml);
        }

        [Fact]
        public void Suppress_FileLevelFindingGoesAtTheTop()
        {
            const string path = "/site/src/screens/SO/SO301000/SO301000.html";
            const string html = "<template></template>";
            Diagnostic d = Assert.Single(HtmlMergeScanner.Analyze(path, html));
            Assert.True(MuiLintFixes.IsFileLevel(d));
            Assert.Equal("<!-- muilint-disable AISI0003 -->\n<template></template>", TestFiles.Apply(html, MuiLintFixes.Suppress(html, d)));
        }

        public static TheoryData<string> FailFixtures()
        {
            var data = new TheoryData<string>();
            foreach (string file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures", "fail"), "*.html", SearchOption.AllDirectories))
            {
                data.Add(file);
            }

            return data;
        }

        [Theory]
        [MemberData(nameof(FailFixtures))]
        public void Suppress_ActuallySilencesEveryFailFixture(string path)
        {
            string html = File.ReadAllText(path);
            IReadOnlyList<Diagnostic> before = HtmlMergeScanner.Analyze(path, html, TestFiles.ReadDisk);
            Assert.NotEmpty(before);

            foreach (Diagnostic d in before)
            {
                string suppressed = TestFiles.Apply(html, MuiLintFixes.Suppress(html, d));
                IReadOnlyList<Diagnostic> after = HtmlMergeScanner.Analyze(path, suppressed, TestFiles.ReadDisk);
                Assert.True(
                    after.Count(x => x.Id == d.Id) < before.Count(x => x.Id == d.Id),
                    "Suppressing " + d.Id + " on line " + d.Line + " did nothing");
            }
        }

        private static Diagnostic Single(string html, string id)
        {
            return Assert.Single(HtmlMergeScanner.Analyze("x.html", html), d => d.Id == id);
        }
    }
}
