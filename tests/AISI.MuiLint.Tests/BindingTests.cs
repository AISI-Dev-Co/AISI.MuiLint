using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace AISI.MuiLint.Tests
{
    public sealed class BindingTests
    {
        internal const string Extension = "/site/src/development/screens/SO/SO301000/extensions/SO301000_Custom.html";
        internal const string ExtensionTs = "/site/src/development/screens/SO/SO301000/extensions/SO301000_Custom.ts";
        internal const string StockTs = "/site/src/screens/SO/SO301000/SO301000.ts";
        private const string ViewsTs = "/site/src/screens/SO/SO301000/views.ts";

        private const string Stock =
            "import { PXScreen, PXView, PXFieldState, PXActionState, createSingle, createCollection, graphInfo, viewInfo } from \"client-controls\";\n" +
            "import { SOLine } from \"./views\";\n" +
            "\n" +
            "@graphInfo({ graphType: \"PX.Objects.SO.SOOrderEntry\", primaryView: \"Document\" })\n" +
            "export class SO301000 extends PXScreen {\n" +
            "    AddInvBySite: PXActionState;\n" +
            "    @viewInfo({ containerName: \"Order Summary\" })\n" +
            "    Document = createSingle(SOOrderHeader);\n" +
            "    Transactions = createCollection(SOLine);\n" +
            "}\n" +
            "\n" +
            "export class SOOrderHeader extends PXView {\n" +
            "    OrderType: PXFieldState;\n" +
            "    OrderNbr: PXFieldState<PXFieldOptions.CommitChanges>;\n" +
            "    OrderDate: PXFieldState;\n" +
            "}\n";

        private const string Views =
            "export class SOLine extends PXView {\n" +
            "    InventoryID: PXFieldState;\n" +
            "    OrderQty: PXFieldState;\n" +
            "}\n";

        private const string Custom =
            "import { SO301000, SOOrderHeader } from \"src/screens/SO/SO301000/SO301000\";\n" +
            "export interface SOOrderHeader_Custom extends SOOrderHeader {}\n" +
            "export class SOOrderHeader_Custom {\n" +
            "    UsrPriority: PXFieldState;\n" +
            "}\n";

        [Fact]
        public void UnknownView_IsReportedOnTheValue()
        {
            const string html = "<template><qp-fieldset id=\"fs\" after=\"#x\" view.bind=\"Documnet\"><field name=\"UsrPriority\"></field></qp-fieldset></template>";
            Diagnostic d = Assert.Single(Scan(html), x => x.Id == DiagnosticIds.BindingNotInTypeScript);
            Assert.Equal("Documnet", html.Substring(d.Start, d.Length));
            Assert.Contains("SO301000 has no view called 'Documnet'", d.Message, StringComparison.Ordinal);
            Assert.Equal(Severity.Warning, d.Severity);
        }

        [Fact]
        public void FieldInsideAView_IsCheckedAgainstThatViewOnly()
        {
            const string html =
                "<template><qp-fieldset id=\"fs\" after=\"#x\" view.bind=\"Transactions\">" +
                "<field name=\"OrderQty\"></field><field name=\"OrderDate\"></field>" +
                "</qp-fieldset></template>";
            Diagnostic d = Assert.Single(Scan(html), x => x.Id == DiagnosticIds.BindingNotInTypeScript);
            Assert.Equal("OrderDate", html.Substring(d.Start, d.Length));
            Assert.Contains("view 'Transactions' (SOLine)", d.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ExtensionFields_CountForTheViewTheyExtend()
        {
            const string html = "<template><qp-fieldset id=\"fs\" after=\"#x\" view.bind=\"Document\"><field name=\"UsrPriority\"></field></qp-fieldset></template>";
            Assert.DoesNotContain(Scan(html), x => x.Id == DiagnosticIds.BindingNotInTypeScript);
        }

        [Fact]
        public void MergedFieldWithoutAView_MustBeOnSomeView()
        {
            Assert.DoesNotContain(Scan("<template><field name=\"UsrPriority\" after=\"[name='OrderDate']\"></field></template>"), x => x.Id == DiagnosticIds.BindingNotInTypeScript);

            Diagnostic d = Assert.Single(Scan("<template><field name=\"UsrPriorty\" after=\"[name='OrderDate']\"></field></template>"), x => x.Id == DiagnosticIds.BindingNotInTypeScript);
            Assert.Contains("not declared on any view of SO301000", d.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void DottedFieldName_NamesItsOwnView()
        {
            Assert.DoesNotContain(Scan("<template><field name=\"Transactions.OrderQty\"></field></template>"), x => x.Id == DiagnosticIds.BindingNotInTypeScript);
            Assert.Contains(Scan("<template><field name=\"Document.OrderQty\"></field></template>"), x => x.Id == DiagnosticIds.BindingNotInTypeScript);
        }

        [Fact]
        public void ActionsAndPanels_AreScreenMembers()
        {
            IReadOnlyList<Diagnostic> results = Scan(
                "<template>" +
                "<qp-button id=\"b1\" state.bind=\"AddInvBySite\"></qp-button>" +
                "<qp-button id=\"b2\" state.bind=\"AddInvBySight\"></qp-button>" +
                "<qp-panel id=\"Transactions\"></qp-panel>" +
                "<qp-panel id=\"TransactionsDialog\"></qp-panel>" +
                "</template>");
            Assert.Collection(
                results,
                d => Assert.Contains("no action called 'AddInvBySight'", d.Message, StringComparison.Ordinal),
                d => Assert.Contains("qp-panel id 'TransactionsDialog'", d.Message, StringComparison.Ordinal));
        }

        [Fact]
        public void FieldsAndViewsFromOtherExtensions_Count()
        {
            Dictionary<string, string> files = Files();

            // A stock feature extension that SO301000.ts never imports: the build stitches it in.
            files["/site/src/screens/SO/SO301000/extensions/SO301000_Payments.ts"] =
                "import { SO301000, SOOrderHeader } from \"../SO301000\";\n" +
                "export interface SO301000_Payments extends SO301000 {}\n" +
                "export class SO301000_Payments {\n    Payments = createCollection(SOPayment);\n}\n" +
                "export class SOPayment extends PXView {\n    CuryAmt: PXFieldState;\n}\n" +
                "export interface SOOrderHeader_Payments extends SOOrderHeader {}\n" +
                "export class SOOrderHeader_Payments {\n    PaymentTotal: PXFieldState;\n}\n";

            // Another extension of ours, beside this one.
            files["/site/src/development/screens/SO/SO301000/extensions/SO301000_Other.ts"] =
                "import { SOOrderHeader } from \"src/screens/SO/SO301000/SO301000\";\n" +
                "export interface SOOrderHeader_Other extends SOOrderHeader {}\n" +
                "export class SOOrderHeader_Other {\n    UsrOther: PXFieldState;\n}\n";

            // One from another customization project.
            files["/site/src/customizationScreens/Shipping/SO/SO301000/extensions/SO301000_Shipping.ts"] =
                "import { SOOrderHeader } from \"src/screens/SO/SO301000/SO301000\";\n" +
                "export interface SOOrderHeader_Shipping extends SOOrderHeader {}\n" +
                "export class SOOrderHeader_Shipping {\n    UsrCarrier: PXFieldState;\n}\n";

            const string html =
                "<template>" +
                "<qp-grid id=\"g\" after=\"#x\" view.bind=\"Payments\"><field name=\"CuryAmt\"></field></qp-grid>" +
                "<qp-fieldset id=\"f\" after=\"#x\" view.bind=\"Document\">" +
                "<field name=\"PaymentTotal\"></field><field name=\"UsrOther\"></field><field name=\"UsrCarrier\"></field>" +
                "</qp-fieldset>" +
                "</template>";

            Assert.DoesNotContain(
                HtmlMergeScanner.Analyze(Extension, html, Reader(files), Lister(files)),
                x => x.Id == DiagnosticIds.BindingNotInTypeScript);

            // Without a way to list folders those files can't be found, and all four are reported.
            Assert.Equal(4, Scan(html, files).Count(x => x.Id == DiagnosticIds.BindingNotInTypeScript));
        }

        [Fact]
        public void ModifyAndRemove_AreLeftAlone()
        {
            const string html = "<template><qp-fieldset modify=\"#fs\" view.bind=\"Whatever\"></qp-fieldset><field remove=\"[name='X']\" name=\"Gone\"></field></template>";
            Assert.DoesNotContain(Scan(html), x => x.Id == DiagnosticIds.BindingNotInTypeScript);
        }

        [Fact]
        public void UnboundFields_AreOnNoView()
        {
            const string html = "<template><qp-fieldset id=\"fs\" after=\"#x\" view.bind=\"Document\"><field name=\"Placeholder\" unbound replace-content></field></qp-fieldset></template>";
            Assert.DoesNotContain(Scan(html), x => x.Id == DiagnosticIds.BindingNotInTypeScript);
        }

        [Fact]
        public void BindingExpressions_AreNotNames()
        {
            const string html = "<template><qp-grid id=\"g\" view.bind=\"Document && Transactions\"></qp-grid></template>";
            Assert.DoesNotContain(Scan(html), x => x.Id == DiagnosticIds.BindingNotInTypeScript);
        }

        [Fact]
        public void CommentsStringsDecoratorsAndMethodBodies_DeclareNothing()
        {
            Dictionary<string, string> files = Files();
            files[ViewsTs] =
                "export class SOLine extends PXView {\n" +
                "    // Ghost1: PXFieldState;\n" +
                "    /* Ghost2: PXFieldState; */\n" +
                "    @columnConfig({ hint: \"Ghost3: PXFieldState\", nested: { Ghost4: 1 } })\n" +
                "    InventoryID: PXFieldState;\n" +
                "    describe() {\n" +
                "        const Ghost5 = 1;\n" +
                "        return `Ghost6: ${Ghost5}`;\n" +
                "    }\n" +
                "}\n";

            for (int i = 1; i <= 6; i++)
            {
                string html = "<template><qp-grid id=\"g\" view.bind=\"Transactions\"><field name=\"Ghost" + i + "\"></field></qp-grid></template>";
                Assert.Contains(Scan(html, files), x => x.Id == DiagnosticIds.BindingNotInTypeScript);
            }

            Assert.DoesNotContain(
                Scan("<template><qp-grid id=\"g\" view.bind=\"Transactions\"><field name=\"InventoryID\"></field></qp-grid></template>", files),
                x => x.Id == DiagnosticIds.BindingNotInTypeScript);
        }

        [Fact]
        public void ViewClassWeCannotFollow_IsNotChecked()
        {
            Dictionary<string, string> files = Files();
            files[ViewsTs] = "import { LineBase } from \"../common/lines\";\nexport class SOLine extends LineBase {\n    OrderQty: PXFieldState;\n}\n";

            const string html = "<template><qp-grid id=\"g\" view.bind=\"Transactions\"><field name=\"InheritedFromLineBase\"></field></qp-grid></template>";
            Assert.DoesNotContain(Scan(html, files), x => x.Id == DiagnosticIds.BindingNotInTypeScript);

            // ...and it makes "is it on any view?" unanswerable too.
            Assert.DoesNotContain(Scan("<template><field name=\"Anything\" after=\"[name='X']\"></field></template>", files), x => x.Id == DiagnosticIds.BindingNotInTypeScript);
        }

        [Fact]
        public void BaseClassesAreFollowed()
        {
            Dictionary<string, string> files = Files();
            files[ViewsTs] = "import { LineBase } from \"../common/lines\";\nexport class SOLine extends LineBase {\n    OrderQty: PXFieldState;\n}\n";
            files["/site/src/screens/SO/common/lines.ts"] = "export class LineBase extends PXView<Foo> {\n    LineNbr: PXFieldState;\n}\n";

            Assert.DoesNotContain(Scan("<template><qp-grid id=\"g\" view.bind=\"Transactions\"><field name=\"LineNbr\"></field></qp-grid></template>", files), x => x.Id == DiagnosticIds.BindingNotInTypeScript);
            Assert.Contains(Scan("<template><qp-grid id=\"g\" view.bind=\"Transactions\"><field name=\"LineNumber\"></field></qp-grid></template>", files), x => x.Id == DiagnosticIds.BindingNotInTypeScript);
        }

        [Fact]
        public void NoTypeScriptOrNoScreen_MeansNoChecks()
        {
            const string html = "<template><qp-grid id=\"g\" view.bind=\"Nope\"></qp-grid></template>";
            Assert.DoesNotContain(Scan(html, new Dictionary<string, string>()), x => x.Id == DiagnosticIds.BindingNotInTypeScript);

            // The extension never imports the screen, so there is no screen class to check against.
            var orphan = new Dictionary<string, string> { [ExtensionTs] = "export class Helper {}" };
            Assert.DoesNotContain(Scan(html, orphan), x => x.Id == DiagnosticIds.BindingNotInTypeScript);
        }

        [Fact]
        public void StandaloneScreens_AreCheckedAgainstTheirOwnTs()
        {
            const string path = "/site/src/development/screens/XX/XX301000/XX301000.html";
            var files = new Dictionary<string, string>
            {
                ["/site/src/development/screens/XX/XX301000/XX301000.ts"] =
                    "export class XX301000 extends PXScreen {\n    Filter = createSingle(Filter);\n}\nexport class Filter extends PXView {\n    Date: PXFieldState;\n}\n",
            };

            IReadOnlyList<Diagnostic> results = HtmlMergeScanner.Analyze(
                path,
                "<template><qp-fieldset id=\"fs\" view.bind=\"Filter\"><field name=\"Date\"></field><field name=\"Data\"></field></qp-fieldset></template>",
                p => files.TryGetValue(p, out string? text) ? text : null);
            Diagnostic d = Assert.Single(results, x => x.Id == DiagnosticIds.BindingNotInTypeScript);
            Assert.Contains("'Data'", d.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void StockScreens_AreNotChecked()
        {
            var files = new Dictionary<string, string> { [StockTs] = Stock, [ViewsTs] = Views };
            IReadOnlyList<Diagnostic> results = HtmlMergeScanner.Analyze(
                "/site/src/screens/SO/SO301000/SO301000.html",
                "<template><qp-grid id=\"g\" view.bind=\"Nope\"></qp-grid></template>",
                p => files.TryGetValue(p, out string? text) ? text : null);
            Assert.DoesNotContain(results, x => x.Id == DiagnosticIds.BindingNotInTypeScript);
        }

        [Fact]
        public void QpControlWithoutId_SkipsExemptionsAndReferences()
        {
            const string html =
                "<template>\n" +
                "<qp-grid view.bind=\"A\"></qp-grid>\n" +
                "<qp-label></qp-label><qp-field name=\"X\"></qp-field><qp-include url=\"x.html\"></qp-include>\n" +
                "<qp-tab remove=\"#tab-X\"></qp-tab><qp-fieldset modify=\"#fs\"></qp-fieldset>\n" +
                "<qp-tab id=\"tab-Y\"></qp-tab>\n" +
                "</template>";
            Diagnostic d = Assert.Single(HtmlMergeScanner.Analyze("x.html", html), x => x.Id == DiagnosticIds.QpControlWithoutId);
            Assert.Equal(2, d.Line);
            Assert.Equal(Severity.Suggestion, d.Severity);
        }

        [Theory]
        [InlineData("{ a: 1, b: [1, 2] }", null)]
        [InlineData("gridConfig", null)]
        [InlineData("{ text: 'a } b' }", null)]
        [InlineData("{ a: 1", "unclosed '{'")]
        [InlineData("{ a: [1, 2 }", "unexpected '}'")]
        public void MalformedConfig_ChecksBalance(string config, string? problem)
        {
            string html = "<template><qp-grid id=\"g\" config.bind=\"" + config + "\"></qp-grid></template>";
            IReadOnlyList<Diagnostic> results = HtmlMergeScanner.Analyze("x.html", html);
            if (problem == null)
            {
                Assert.DoesNotContain(results, x => x.Id == DiagnosticIds.MalformedConfig);
            }
            else
            {
                Assert.Contains(problem, Assert.Single(results, x => x.Id == DiagnosticIds.MalformedConfig).Message, StringComparison.Ordinal);
            }
        }

        internal static Dictionary<string, string> Files()
        {
            return new Dictionary<string, string>
            {
                [StockTs] = Stock,
                [ViewsTs] = Views,
                [ExtensionTs] = Custom,
            };
        }

        internal static Func<string, string?> Reader(Dictionary<string, string> files)
        {
            return path => files.TryGetValue(path, out string? text) ? text : null;
        }

        /// <summary>What's directly inside a folder, files and subfolders, like Directory.GetFileSystemEntries.</summary>
        internal static Func<string, IEnumerable<string>> Lister(Dictionary<string, string> files)
        {
            return folder => files.Keys
                .Where(p => p.StartsWith(folder + "/", StringComparison.Ordinal))
                .Select(p => p.IndexOf('/', folder.Length + 1) < 0 ? p : p.Substring(0, p.IndexOf('/', folder.Length + 1)))
                .Distinct()
                .ToList();
        }

        private static IReadOnlyList<Diagnostic> Scan(string html, Dictionary<string, string>? files = null)
        {
            files ??= Files();
            return HtmlMergeScanner.Analyze(Extension, html, path => files.TryGetValue(path, out string? text) ? text : null);
        }
    }
}
