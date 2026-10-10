using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace AISI.MuiLint.Tests
{
    /// <summary>The checks AcuMate has, as MuiLint does them.</summary>
    public sealed class AcuMateParityTests
    {
        private const string Stock = "/site/src/screens/SO/SO301000/SO301000.html";

        [Fact]
        public void FieldStateBind_IsCheckedAsAField()
        {
            // On anything but a button, state.bind names a field: View.Field, or one of the view around it.
            Assert.Collection(
                Bindings(
                    "<template><qp-rich-text-editor id=\"a\" state.bind=\"Transactions.OrderQty\"></qp-rich-text-editor>"
                    + "<qp-rich-text-editor id=\"b\" state.bind=\"Transactions.Nope\"></qp-rich-text-editor>"
                    + "<qp-fieldset id=\"f\" after=\"#x\" view.bind=\"Document\"><qp-mail-editor id=\"m\" state.bind=\"OrderDate\"></qp-mail-editor></qp-fieldset></template>"),
                d => Assert.Contains("Field 'Nope' is not declared on view 'Transactions'", d.Message, StringComparison.Ordinal));
        }

        [Fact]
        public void ControlStateBind_IsCheckedAsAField()
        {
            Assert.Collection(
                Bindings(
                    "<template><qp-fieldset id=\"f\" after=\"#x\" view.bind=\"Document\">"
                    + "<qp-field control-state.bind=\"Document.OrderNbr\"></qp-field>"
                    + "<qp-field control-state.bind=\"OrderType\"></qp-field>"
                    + "<qp-field control-state.bind=\"Document.OrderNbrr\"></qp-field></qp-fieldset></template>"),
                d => Assert.Contains("'OrderNbrr'", d.Message, StringComparison.Ordinal));
        }

        [Fact]
        public void ButtonStateBind_TakesAQualifiedAction()
        {
            Assert.Collection(
                Bindings("<template><qp-button id=\"b1\" state.bind=\"Transactions.InventoryID\"></qp-button><qp-button id=\"b2\" state.bind=\"Transactions.Release\"></qp-button></template>"),
                d => Assert.Contains("no action called 'Transactions.Release'", d.Message, StringComparison.Ordinal));
        }

        [Fact]
        public void IncludeParameters_RequiredAndUndeclared()
        {
            var files = new Dictionary<string, string>
            {
                [Stock] = "<template></template>",
                ["/site/src/screens/common/address.html"] =
                    "<template><qp-include-parameters id.required address-view.required caption=\"Address\"></qp-include-parameters></template>",
            };

            IReadOnlyList<Diagnostic> found = Scan(
                "<template><qp-include url=\"../../common/address.html\" id=\"formA\" captoin=\"Billing\" class=\"x\" data-x=\"1\"></qp-include></template>",
                files,
                "/site/src/screens/SO/SO301000/SO301000.html");
            Assert.Collection(
                found.Where(d => d.Id == DiagnosticIds.IncludeParameters),
                d => Assert.Contains("missing the required parameter 'address-view'", d.Message, StringComparison.Ordinal),
                d => Assert.Contains("declares no parameter 'captoin'", d.Message, StringComparison.Ordinal));

            // An include that declares no parameters takes anything, as before parameters existed.
            files["/site/src/screens/common/address.html"] = "<template></template>";
            Assert.DoesNotContain(
                Scan("<template><qp-include url=\"../../common/address.html\" foo=\"1\"></qp-include></template>", files, "/site/src/screens/SO/SO301000/SO301000.html"),
                d => d.Id == DiagnosticIds.IncludeParameters);
        }

        [Fact]
        public void UnbalancedTags_StrayAndUnclosed()
        {
            // Both straight out of Acumatica's 24R1 screens: FS300100's stray </field>, PM506000's "/qp-grid>".
            IReadOnlyList<Diagnostic> found = HtmlMergeScanner.Analyze(
                "/x.html",
                "<template><qp-fieldset id=\"f\"><field name=\"Mode\"></field></field></qp-fieldset>"
                + "<qp-grid id=\"grid\" view.bind=\"Items\">/qp-grid>\n</template>");
            Assert.Collection(
                found.Where(d => d.Id == DiagnosticIds.UnbalancedTag),
                d => Assert.Contains("</field> has no <field> to close", d.Message, StringComparison.Ordinal),
                d => Assert.Contains("<qp-grid> is never closed", d.Message, StringComparison.Ordinal));

            Assert.DoesNotContain(
                HtmlMergeScanner.Analyze("/x.html", "<template><div><qp-fieldset id=\"f\"><br><field name=\"A\"></field></qp-fieldset></div><img src=\"x\"></template>"),
                d => d.Id == DiagnosticIds.UnbalancedTag);
        }

        [Fact]
        public void ClientControls_TemplatesControlTypesAndConfigKeys()
        {
            Dictionary<string, string> files = Package("/cc1");
            files["/cc1/src/screens/SO/SO301000/SO301000.html"] = "<template></template>";
            IReadOnlyList<Diagnostic> found = Scan(
                "<template>"
                + "<qp-template id=\"t\" name=\"17-17-14\"></qp-template><qp-template id=\"u\" name=\"17-17-41\"></qp-template>"
                + "<field name=\"A\" control-type=\"qp-selector\"></field><field name=\"B\" control-type=\"qp-slector\"></field>"
                + "<qp-grid id=\"g\" config.bind=\"{ allowInsert: false, 'syncPosition': true, adjustPageSise: true }\"></qp-grid>"
                + "<qp-grid id=\"h\" config.bind=\"gridConfig\"></qp-grid>"
                + "<qp-fieldset id=\"f\" config.bind=\"{ anything: 1 }\"></qp-fieldset>"
                + "</template>",
                files,
                "/cc1/src/screens/SO/SO301000/SO301000.html");

            Assert.Equal("'17-17-41'", Between(Assert.Single(found, d => d.Id == DiagnosticIds.UnknownTemplate).Message));
            Assert.Equal("'qp-slector'", Between(Assert.Single(found, d => d.Id == DiagnosticIds.UnknownControlType).Message));
            Diagnostic key = Assert.Single(found, d => d.Id == DiagnosticIds.UnknownConfigKey);
            Assert.Contains("<qp-grid> config has no 'adjustPageSise'", key.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ClientControls_StayQuietWithoutThePackage()
        {
            var files = new Dictionary<string, string> { ["/cc2/src/screens/SO/SO301000/SO301000.html"] = "<template></template>" };
            IReadOnlyList<Diagnostic> found = Scan(
                "<template><qp-template id=\"u\" name=\"nonsense\"></qp-template><field name=\"B\" control-type=\"qp-nonsense\"></field></template>",
                files,
                "/cc2/src/screens/SO/SO301000/SO301000.html");
            Assert.DoesNotContain(found, d => d.Id == DiagnosticIds.UnknownTemplate || d.Id == DiagnosticIds.UnknownControlType);
        }

        [Fact]
        public void ClientControls_ReadsInheritedConfigsAndGivesUpOnWhatItCantSee()
        {
            ClientControls controls = ClientControls.Read("/cc3/node_modules/client-controls/", BindingTests.Reader(Package("/cc3")), BindingTests.Lister(Package("/cc3")))!;
            Assert.Contains("captureClick", controls.Controls["qp-button"]!);
            Assert.Contains("allowInsert", controls.Controls["qp-grid"]!);
            Assert.Contains("id", controls.Controls["qp-grid"]!);

            // qp-mail-editor's config comes from the class it extends.
            Assert.Contains("readOnly", controls.Controls["qp-mail-editor"]!);

            // An index signature, or a base interface from another package, could allow any key.
            Assert.Null(controls.Controls["qp-chart"]);
            Assert.Null(controls.Controls["qp-panel"]);
        }

        [Theory]
        [InlineData("{ id: 'a', b: { c: 1 }, \"d\": [1, 2], e }", "id,b,d,e")]
        [InlineData("{ a: f(1, 2), b: 'x, y: z' }", "a,b")]
        [InlineData("gridConfig", "")]
        [InlineData("{ ...defaults, a: 1 }", "a")]
        public void ObjectKeys_AreTheTopLevelOnes(string value, string keys)
        {
            Assert.Equal(keys, string.Join(",", HtmlMergeScanner.ObjectKeys(value).Select(k => k.Key)));
            foreach ((string key, int offset) in HtmlMergeScanner.ObjectKeys(value))
            {
                Assert.Equal(key, value.Substring(offset, key.Length));
            }
        }

        [Fact]
        public void Selectors_ThatMatchSeveralStockElements()
        {
            var files = new Dictionary<string, string>
            {
                [Stock] = "<template><qp-fieldset id=\"fsA\"><field name=\"Status\"></field><field name=\"OrderDate\"></field></qp-fieldset>"
                    + "<qp-fieldset id=\"fsB\"><field name=\"Status\"></field></qp-fieldset></template>",
                ["/site/src/development/screens/SO/SO301000/extensions/SO301000_Other.html"] = "<template><field append=\"#fsB\" name=\"UsrAdded\"></field></template>",
            };

            IReadOnlyList<Diagnostic> found = Scan(
                "<template>"
                + "<field after=\"[name='Status']\" name=\"UsrA\"></field>"
                + "<field after=\"#fsA [name='Status']\" name=\"UsrB\"></field>"
                + "<field after=\"#fsB [name='OrderDate']\" name=\"UsrC\"></field>"
                + "<field after=\"#fsA [name='UsrAdded']\" name=\"UsrD\"></field>"
                + "<field after=\"#fsA [name='UsrA']\" name=\"UsrE\"></field>"
                + "</template>",
                files,
                BindingTests.Extension,
                BindingTests.Lister(files));

            Diagnostic several = Assert.Single(found, d => d.Id == DiagnosticIds.SelectorMatchesSeveral);
            Assert.Contains("[name='Status'] matches 2 elements", several.Message, StringComparison.Ordinal);

            // OrderDate is in the stock screen, just not in fsB. UsrAdded comes from another extension and
            // UsrA from above, so where they end up isn't ours to know.
            Diagnostic notInside = Assert.Single(found, d => d.Id == DiagnosticIds.SelectorNotInStock);
            Assert.Contains("[name='OrderDate'] is in the stock SO301000.html, but not inside #fsB", notInside.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void GraphInfoAndGridConfig_Decorators()
        {
            const string ts =
                "@graphInfo({ primaryView: 'Document' })\nexport class XX301000 extends PXScreen {}\n"
                + "@gridConfig({ preset: GridPreset.Details })\nexport class A extends PXView {}\n"
                + "@gridConfig({ syncPosition: true })\nexport class B extends PXView {}\n"
                + "@gridConfig()\nexport class C extends PXView {}\n"
                + "@gridConfig(sharedConfig)\nexport class D extends PXView {}\n";
            IReadOnlyList<Diagnostic> found = TypeScriptScanner.Analyze("/site/src/development/screens/XX/XX301000/XX301000.ts", ts, null);

            Assert.Equal("@graphInfo", Text(ts, Assert.Single(found, d => d.Id == DiagnosticIds.GraphInfoWithoutGraphType)));
            Assert.Equal(2, found.Count(d => d.Id == DiagnosticIds.GridWithoutPreset));
            Assert.All(found.Where(d => d.Id == DiagnosticIds.GridWithoutPreset), d => Assert.Equal(Severity.Suggestion, d.Severity));
        }

        private static IReadOnlyList<Diagnostic> Bindings(string html)
        {
            return HtmlMergeScanner.Analyze(BindingTests.Extension, html, BindingTests.Reader(BindingTests.Files()))
                .Where(d => d.Id == DiagnosticIds.BindingNotInTypeScript)
                .ToList();
        }

        private static IReadOnlyList<Diagnostic> Scan(string html, Dictionary<string, string> files, string path, Func<string, IEnumerable<string>>? lister = null)
        {
            return HtmlMergeScanner.Analyze(path, html, BindingTests.Reader(files), lister ?? BindingTests.Lister(files));
        }

        private static string Between(string message)
        {
            return message.Substring(0, message.IndexOf('\'', 1) + 1);
        }

        private static string Text(string source, Diagnostic d)
        {
            return source.Substring(d.Start, d.Length);
        }

        // A client-controls package cut down to what the checks read, laid out as the real one is.
        private static Dictionary<string, string> Package(string site)
        {
            string root = site + "/node_modules/client-controls/";
            return new Dictionary<string, string>
            {
                [root + "package.json"] = "{ \"name\": \"client-controls\" }",
                [root + "controls/container/template/qp-template.js"] =
                    "ScreenTemplates.set(\"17-17-14\", x);\nScreenTemplates.set('1-1', y);\nScreenTemplates.set(`record-1`, z);",
                [root + "descriptors/controls.d.ts"] =
                    "export interface IBaseControlConfig { id?: string; enabled?: boolean; /** readOnly: not a key */ }\n"
                    + "export interface IViewCollectionPresentationConfig<TView extends PXView = PXView> {\n  allowInsert?: boolean;\n  syncPosition?: boolean;\n  onRowSelected?(args: IRowArgs<TView>): void;\n}\n"
                    + "export interface IGridConfig<TView extends PXView = PXView> extends IBaseControlConfig, IViewCollectionPresentationConfig<TView> {\n  preset?: GridPreset;\n  'adjustPageSize'?: boolean;\n}\n"
                    + "export interface IButtonConfig extends IBaseControlConfig { captureClick?: boolean; }\n"
                    + "export interface ITextEditorConfig extends IBaseControlConfig { readOnly?: boolean; }\n"
                    + "export interface IChartConfig { [key: string]: any; }\n"
                    + "export interface IPanelConfig extends SomethingFromAurelia { caption?: string; }\n",
                [root + "controls/compound/grid/qp-grid.d.ts"] =
                    "/**\n * @customElement('qp-not-me')\n */\n@customElement(\"qp-grid\")\n@autoinject\nexport declare class QpGridCustomElement implements IDataComponent {\n    config: IGridConfig<PXView>;\n    configChanged(newConfig?: IGridConfig<PXView>): void;\n}\n",
                [root + "controls/simple/button/qp-button.d.ts"] =
                    "@customElement('qp-button')\nexport declare class QpButtonCustomElement {\n    config?: IButtonConfig;\n}\n"
                    + "export declare class NotAControl {\n    config: IChartConfig;\n}\n",
                [root + "controls/simple/text-editor/qp-text-editor.d.ts"] =
                    "@customElement('qp-text-editor')\nexport declare class QpTextEditorCustomElement {\n    config: ITextEditorConfig;\n}\n"
                    + "@customElement('qp-mail-editor')\nexport declare class QpMailEditorCustomElement extends QpTextEditorCustomElement {\n    static mailDefaultConfig: {\n        activeButton: boolean;\n    };\n}\n",
                [root + "controls/simple/selector/qp-selector.d.ts"] = "@customElement('qp-selector')\nexport declare class QpSelectorCustomElement {}\n",
                [root + "controls/chart/qp-chart.d.ts"] = "@customElement('qp-chart')\nexport declare class QpChartCustomElement { config: IChartConfig; }\n",
                [root + "controls/container/panel/qp-panel.d.ts"] = "@customElement({ name: 'qp-panel' })\nexport declare class QpPanelCustomElement { config: IPanelConfig; }\n",
                [root + "controls/container/fieldset/qp-fieldset.d.ts"] = "@customElement('qp-fieldset')\nexport declare class QpFieldsetCustomElement {}\n",
            };
        }
    }
}
