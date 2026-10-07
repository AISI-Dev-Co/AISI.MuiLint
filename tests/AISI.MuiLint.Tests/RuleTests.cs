using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace AISI.MuiLint.Tests
{
    public sealed class RuleTests
    {
        private const string Extension = "/site/src/development/screens/SO/SO301000/extensions/SO301000_Custom.html";
        private const string TypeScript = "/site/src/development/screens/SO/SO301000/extensions/SO301000_Custom.ts";
        private const string Stock = "/site/src/screens/SO/SO301000/SO301000.html";

        private const string StockHtml =
            "<template>\n" +
            "  <qp-fieldset id=\"fsColumnA-Order\" view.bind=\"Document\">\n" +
            "    <field name=\"OrderNbr\"></field>\n" +
            "    <field name=\"Status\"></field>\n" +
            "  </qp-fieldset>\n" +
            "</template>\n";

        [Theory]
        [InlineData("/site/src/development/screens/SO/SO301000/extensions/SO301000_Custom.html", "/site/src/screens/SO/SO301000/SO301000.html")]
        [InlineData(@"C:\site\src\development\screens\SO\SO301000\extensions\SO301000_Custom.html", "C:/site/src/screens/SO/SO301000/SO301000.html")]
        [InlineData("/site/src/customizationScreens/AISI/SO/SO301000/extensions/SO301000_AISI.html", "/site/src/screens/SO/SO301000/SO301000.html")]
        [InlineData("/site/src/customizationScreens/AISI/screens/SO/SO301000/extensions/SO301000_AISI.html", "/site/src/screens/SO/SO301000/SO301000.html")]
        [InlineData("/site/src/screens/SO/SO301000/extensions/SO301000_Custom.html", "/site/src/screens/SO/SO301000/SO301000.html")]
        [InlineData("src/development/screens/SO/SO301000/extensions/SO301000_Custom.html", "src/screens/SO/SO301000/SO301000.html")]
        [InlineData("/site/src/development/screens/SO/SO301000/SO301000.html", null)]
        [InlineData("/somewhere/else/SO301000/extensions/SO301000_Custom.html", null)]
        public void StockHtmlPath_FindsTheStockScreen(string extension, string? expected)
        {
            Assert.Equal(expected, HtmlMergeScanner.StockHtmlPath(extension));
        }

        [Fact]
        public void SelectorNotInStock_FlagsATypoInAName()
        {
            const string html = "<template>\n  <field name=\"UsrPriority\" after=\"#fsColumnA-Order [name='OrderNbrr']\"></field>\n</template>\n";
            Diagnostic d = Assert.Single(Scan(html, WithStock()), x => x.Id == DiagnosticIds.SelectorNotInStock);
            Assert.Equal("[name='OrderNbrr']", html.Substring(d.Start, d.Length));
            Assert.Contains("SO301000.html", d.Message, StringComparison.Ordinal);
            Assert.Equal(Severity.Warning, d.Severity);
        }

        [Fact]
        public void SelectorNotInStock_FlagsAnUnknownId()
        {
            const string html = "<template><qp-fieldset id=\"fsExtra\" after=\"#fsColumnZ-Order\" view.bind=\"Document\"><field name=\"UsrA\"></field></qp-fieldset></template>";
            Diagnostic d = Assert.Single(Scan(html, WithStock()), x => x.Id == DiagnosticIds.SelectorNotInStock);
            Assert.Equal("#fsColumnZ-Order", html.Substring(d.Start, d.Length));
        }

        [Fact]
        public void SelectorNotInStock_QuietWhenTargetsExist()
        {
            const string html = "<template><field name=\"UsrA\" after=\"#fsColumnA-Order [name='Status']\"></field></template>";
            Assert.DoesNotContain(Scan(html, WithStock()), x => x.Id == DiagnosticIds.SelectorNotInStock);
        }

        [Fact]
        public void SelectorNotInStock_AcceptsFieldsThisFileAdds()
        {
            // The shape of Acumatica's own IN202500_PhoneRepairShop example: the second field chains off the first.
            const string html = "<template>"
                + "<field after=\"#fsColumnA-Order [name='Status']\" name=\"UsrRepairItem\"></field>"
                + "<field after=\"#fsColumnA-Order [name='UsrRepairItem']\" name=\"UsrRepairItemType\"></field>"
                + "</template>";
            IReadOnlyList<Diagnostic> results = Scan(html, WithStock());
            Assert.DoesNotContain(results, x => x.Severity == Severity.Error);
            Assert.DoesNotContain(results, x => x.Id == DiagnosticIds.SelectorNotInStock);
        }

        [Fact]
        public void SelectorNotInStock_FollowsIncludes()
        {
            Dictionary<string, string> files = WithStock();
            files[Stock] = "<template><qp-include url=\"../../common/address.html\"></qp-include></template>";
            files["/site/src/screens/common/address.html"] = "<template><field name=\"AddressLine1\"></field></template>";

            const string html = "<template><field name=\"UsrA\" after=\"[name='AddressLine1']\"></field></template>";
            Assert.DoesNotContain(Scan(html, files), x => x.Id == DiagnosticIds.SelectorNotInStock);
        }

        [Fact]
        public void SelectorNotInStock_StaysQuietWhenAnIncludeCannotBeRead()
        {
            Dictionary<string, string> files = WithStock();
            files[Stock] = "<template><qp-include url=\"../../common/missing.html\"></qp-include></template>";

            const string html = "<template><field name=\"UsrA\" after=\"[name='AnythingAtAll']\"></field></template>";
            Assert.DoesNotContain(Scan(html, files), x => x.Id == DiagnosticIds.SelectorNotInStock);
        }

        [Fact]
        public void SelectorNotInStock_StaysQuietWithoutAStockFile()
        {
            var files = new Dictionary<string, string> { [TypeScript] = string.Empty };
            const string html = "<template><field name=\"UsrA\" after=\"[name='Nope']\"></field></template>";
            Assert.DoesNotContain(Scan(html, files), x => x.Id == DiagnosticIds.SelectorNotInStock);
        }

        [Fact]
        public void SelectorNotInStock_CountsWhatOtherExtensionsAdd()
        {
            Dictionary<string, string> files = WithStock();
            files["/site/src/screens/SO/SO301000/extensions/SO301000_Payments.html"] = "<template><qp-fieldset id=\"fsPayments\"><field name=\"PaymentTotal\"></field></qp-fieldset></template>";
            files["/site/src/customizationScreens/Shipping/SO/SO301000/extensions/SO301000_Shipping.html"] = "<template><field name=\"UsrCarrier\" after=\"[name='Status']\"></field></template>";

            const string html = "<template><field name=\"UsrA\" after=\"#fsPayments [name='PaymentTotal']\"></field><field name=\"UsrB\" after=\"[name='UsrCarrier']\"></field></template>";
            IReadOnlyList<Diagnostic> results = HtmlMergeScanner.Analyze(Extension, html, path => files.TryGetValue(path, out string? text) ? text : null, BindingTests.Lister(files));
            Assert.DoesNotContain(results, x => x.Id == DiagnosticIds.SelectorNotInStock);
            Assert.Equal(3, Scan(html, files).Count(x => x.Id == DiagnosticIds.SelectorNotInStock));
        }

        [Fact]
        public void ExtensionOutsideExtensions_SpotsMergeAttributesInAnyFolder()
        {
            const string html = "<template><qp-fieldset modify=\"#fsColumnA-Order\" caption=\"Order\"></qp-fieldset></template>";
            Diagnostic d = Assert.Single(
                HtmlMergeScanner.Analyze("/site/src/development/screens/SO/SO301000/ext/Mine.html", html),
                x => x.Id == DiagnosticIds.ExtensionOutsideExtensions);
            Assert.Contains("uses modify=", d.Message, StringComparison.Ordinal);
            Assert.Equal((0, 0), (d.Start, d.Length));

            // Outside the custom trees it's not ours to judge.
            Assert.DoesNotContain(HtmlMergeScanner.Analyze("/elsewhere/Mine.html", html), x => x.Id == DiagnosticIds.ExtensionOutsideExtensions);
        }

        [Fact]
        public void ExtensionWithoutTypeScript_NeedsTheTsSibling()
        {
            const string html = "<template></template>";
            Assert.Contains(Scan(html, new Dictionary<string, string>()), x => x.Id == DiagnosticIds.ExtensionWithoutTypeScript);
            Assert.DoesNotContain(Scan(html, WithStock()), x => x.Id == DiagnosticIds.ExtensionWithoutTypeScript);

            // Without a way to read files there is nothing to check.
            Assert.DoesNotContain(HtmlMergeScanner.Analyze(Extension, html), x => x.Id == DiagnosticIds.ExtensionWithoutTypeScript);
        }

        [Fact]
        public void UsrHint_SkipsAStockFieldBeingMoved()
        {
            const string html = "<template><field name=\"Status\" after=\"[name='OrderNbr']\"></field></template>";
            Assert.DoesNotContain(Scan(html, WithStock()), x => x.Id == DiagnosticIds.FieldWithoutUsrPrefix);

            Diagnostic d = Assert.Single(HtmlMergeScanner.Analyze(Extension, html), x => x.Id == DiagnosticIds.FieldWithoutUsrPrefix);
            Assert.Equal(Severity.Suggestion, d.Severity);
        }

        [Fact]
        public void UsrHint_OnlyAppliesToExtensions()
        {
            const string html = "<template><field name=\"Priority\" after=\"[name='OrderNbr']\"></field></template>";
            Assert.DoesNotContain(
                HtmlMergeScanner.Analyze("/site/src/development/screens/XX/XX301000/XX301000.html", html),
                x => x.Id == DiagnosticIds.FieldWithoutUsrPrefix);
        }

        [Fact]
        public void DuplicateField_PointsAtTheFirstOne()
        {
            const string html = "<template>\n<field name=\"UsrA\"></field>\n<field name=\"UsrA\"></field>\n</template>";
            Diagnostic d = Assert.Single(HtmlMergeScanner.Analyze("x.html", html), x => x.Id == DiagnosticIds.DuplicateNameOrId);
            Assert.Equal(3, d.Line);
            Assert.Contains("line 2", d.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("[name='A']", null)]
        [InlineData("#fs [name=\"A\"]", null)]
        [InlineData("qp-tab:nth-child(2)", null)]
        [InlineData("[name='A'", "unclosed '['")]
        [InlineData("[name='A]", "unclosed ' quote")]
        [InlineData("#fs]", "unexpected ']'")]
        [InlineData("qp-tab:nth-child(2]", "unexpected ']'")]
        public void FindBracketProblem_ExplainsWhatIsWrong(string selector, string? expected)
        {
            Assert.Equal(expected, HtmlMergeScanner.FindBracketProblem(selector));
        }

        [Fact]
        public void Suppression_NextLine_OnlyCoversTheNextLine()
        {
            const string html =
                "<template>\n" +
                "  <!-- muilint-disable-next-line AISI0001 -->\n" +
                "  <field name=\"UsrA\"/>\n" +
                "  <field name=\"UsrB\"/>\n" +
                "</template>";
            Diagnostic d = Assert.Single(HtmlMergeScanner.Analyze("x.html", html), x => x.Id == DiagnosticIds.SelfClosing);
            Assert.Equal(4, d.Line);
        }

        [Fact]
        public void Suppression_OnlyCoversTheIdsItNames()
        {
            const string html =
                "<!-- muilint-disable AISI0005 -->\n" +
                "<template><field name=\"UsrA\"/><qp-fieldset></qp-fieldset></template>";
            IReadOnlyList<Diagnostic> results = HtmlMergeScanner.Analyze("x.html", html);
            Assert.DoesNotContain(results, x => x.Id == DiagnosticIds.EmptyFieldset);
            Assert.Contains(results, x => x.Id == DiagnosticIds.SelfClosing);
        }

        [Fact]
        public void Suppression_WithoutIds_CoversEverything()
        {
            const string html = "<!-- muilint-disable -->\n<template><field name=\"UsrA\"/><qp-fieldset></qp-fieldset></template>";
            Assert.Empty(HtmlMergeScanner.Analyze("/site/src/screens/SO/SO301000/SO301000.html", html));
        }

        [Fact]
        public void Suppression_IgnoresLookalikes()
        {
            const string html = "<!-- muilint-disabled -->\n<template><field name=\"UsrA\"/></template>";
            Assert.Contains(HtmlMergeScanner.Analyze("x.html", html), x => x.Id == DiagnosticIds.SelfClosing);
        }

        [Fact]
        public void EditorConfig_ChangesSeverity()
        {
            Dictionary<string, string> files = WithStock();
            files["/site/.editorconfig"] = "root = true\n\n[*.html]\ndotnet_diagnostic.AISI0001.severity = warning\n";
            Diagnostic d = Assert.Single(Scan("<template><field name=\"UsrA\"/></template>", files), x => x.Id == DiagnosticIds.SelfClosing);
            Assert.Equal(Severity.Warning, d.Severity);
        }

        [Fact]
        public void EditorConfig_NoneTurnsARuleOff()
        {
            Dictionary<string, string> files = WithStock();
            files["/site/.editorconfig"] = "[*]\ndotnet_diagnostic.AISI0001.severity = none # not our problem\n";
            Assert.DoesNotContain(Scan("<template><field name=\"UsrA\"/></template>", files), x => x.Id == DiagnosticIds.SelfClosing);
        }

        [Fact]
        public void EditorConfig_NearerFileWinsAndRootStopsTheWalk()
        {
            Dictionary<string, string> files = WithStock();
            files["/.editorconfig"] = "[*]\ndotnet_diagnostic.AISI0005.severity = none\n";
            files["/site/.editorconfig"] = "root = true\n[*]\ndotnet_diagnostic.AISI0001.severity = none\n";
            files["/site/src/.editorconfig"] = "[*.html]\ndotnet_diagnostic.AISI0001.severity = suggestion\n";

            IReadOnlyList<Diagnostic> results = Scan("<template><field name=\"UsrA\"/><qp-fieldset></qp-fieldset></template>", files);
            Assert.Equal(Severity.Suggestion, Assert.Single(results, x => x.Id == DiagnosticIds.SelfClosing).Severity);
            Assert.Contains(results, x => x.Id == DiagnosticIds.EmptyFieldset);
        }

        [Theory]
        [InlineData("*.html", "a/b/c.html", true)]
        [InlineData("*.html", "c.ts", false)]
        [InlineData("*.{html,ts}", "a/c.ts", true)]
        [InlineData("**/extensions/*.html", "src/SO/extensions/x.html", true)]
        [InlineData("**/extensions/*.html", "extensions/x.html", true)]
        [InlineData("**/extensions/*.html", "src/SO/x.html", false)]
        [InlineData("src/*.html", "src/x.html", true)]
        [InlineData("src/*.html", "src/a/x.html", false)]
        [InlineData("[!x]*.html", "a.html", true)]
        [InlineData("[!x]*.html", "x.html", false)]
        [InlineData("**/legacy/**.html", "src/legacy/SO/x.html", true)]
        [InlineData("**/legacy/**.html", "src/current/SO/x.html", false)]
        public void EditorConfig_GlobsFollowTheSpec(string glob, string path, bool expected)
        {
            Assert.Equal(expected, EditorConfig.Matches(glob, path));
        }

        private static Dictionary<string, string> WithStock()
        {
            return new Dictionary<string, string>
            {
                [Stock] = StockHtml,
                [TypeScript] = "export class SO301000_Custom {}",
            };
        }

        private static IReadOnlyList<Diagnostic> Scan(string html, Dictionary<string, string> files)
        {
            return HtmlMergeScanner.Analyze(Extension, html, path => files.TryGetValue(path, out string? text) ? text : null);
        }
    }
}
