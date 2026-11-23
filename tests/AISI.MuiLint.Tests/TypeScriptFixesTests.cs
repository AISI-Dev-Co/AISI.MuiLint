using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace AISI.MuiLint.Tests
{
    public sealed class TypeScriptFixesTests
    {
        [Fact]
        public void DeclareField_GoesIntoTheExistingExtensionClass()
        {
            Dictionary<string, string> files = BindingTests.Files();
            files[BindingTests.ExtensionTs] =
                "import { PXFieldState } from \"client-controls\";\n" +
                "import { SOOrderHeader } from \"src/screens/SO/SO301000/SO301000\";\n" +
                "\n" +
                "export interface SOOrderHeader_Custom extends SOOrderHeader {}\n" +
                "export class SOOrderHeader_Custom {\n" +
                "    UsrPriority: PXFieldState;\n" +
                "}\n";

            string fixedTs = Fix("<template><qp-fieldset id=\"f\" after=\"#x\" view.bind=\"Document\"><field name=\"UsrRush\"></field></qp-fieldset></template>", files);

            Assert.Equal(
                "import { PXFieldState } from \"client-controls\";\n" +
                "import { SOOrderHeader } from \"src/screens/SO/SO301000/SO301000\";\n" +
                "\n" +
                "export interface SOOrderHeader_Custom extends SOOrderHeader {}\n" +
                "export class SOOrderHeader_Custom {\n" +
                "    UsrPriority: PXFieldState;\n" +
                "    UsrRush: PXFieldState;\n" +
                "}\n",
                fixedTs);
        }

        [Fact]
        public void DeclareField_WritesTheExtensionAndItsImportsWhenThereIsNone()
        {
            Dictionary<string, string> files = BindingTests.Files();
            files[BindingTests.ExtensionTs] =
                "import { SO301000 } from \"src/screens/SO/SO301000/SO301000\";\n" +
                "\n" +
                "export interface SO301000_Custom extends SO301000 {}\n" +
                "export class SO301000_Custom {}\n";

            // No view around the field: its anchor, OrderQty, is on Transactions (SOLine, declared in views.ts).
            string fixedTs = Fix("<template><field name=\"UsrNote\" after=\"[name='OrderQty']\"></field></template>", files);

            Assert.Equal(
                "import { SO301000 } from \"src/screens/SO/SO301000/SO301000\";\n" +
                "import { SOLine } from \"src/screens/SO/SO301000/views\";\n" +
                "import { PXFieldState } from \"client-controls\";\n" +
                "\n" +
                "export interface SO301000_Custom extends SO301000 {}\n" +
                "export class SO301000_Custom {}\n" +
                "\n" +
                "export interface SOLine_Custom extends SOLine {}\n" +
                "export class SOLine_Custom {\n" +
                "    UsrNote: PXFieldState;\n" +
                "}\n",
                fixedTs);
        }

        [Fact]
        public void DeclareField_AddsToAnImportFromTheSameModule()
        {
            Dictionary<string, string> files = BindingTests.Files();
            files[BindingTests.ExtensionTs] = "import { SO301000 } from \"src/screens/SO/SO301000/SO301000\";\nexport interface SO301000_Custom extends SO301000 {}\nexport class SO301000_Custom {}\n";

            string fixedTs = Fix("<template><qp-fieldset id=\"f\" after=\"#x\" view.bind=\"Document\"><field name=\"UsrRush\"></field></qp-fieldset></template>", files);

            Assert.StartsWith("import { SO301000, SOOrderHeader } from \"src/screens/SO/SO301000/SO301000\";\n", fixedTs, StringComparison.Ordinal);
        }

        [Fact]
        public void DeclareField_InAStandaloneScreenGoesIntoTheViewClassItself()
        {
            const string html = "/site/src/development/screens/XX/XX301000/XX301000.html";
            const string ts = "/site/src/development/screens/XX/XX301000/XX301000.ts";
            Dictionary<string, string> files = new Dictionary<string, string>
            {
                [ts] =
                    "export class XX301000 extends PXScreen {\n" +
                    "\tFilter = createSingle(XXFilter);\n" +
                    "}\n" +
                    "export class XXFilter extends PXView {}\n",
            };

            string fixedTs = Fix("<template><qp-fieldset id=\"f\" view.bind=\"Filter\"><field name=\"Date\"></field></qp-fieldset></template>", files, html);

            Assert.Contains("export class XXFilter extends PXView {\n\tDate: PXFieldState;\n}\n", fixedTs, StringComparison.Ordinal);
        }

        [Fact]
        public void DeclareField_NothingToPlanWithoutAView()
        {
            Dictionary<string, string> files = BindingTests.Files();
            const string html = "<template><field name=\"UsrNote\" after=\"#somewhere\"></field></template>";
            Diagnostic d = Assert.Single(Scan(html, files), x => x.Id == DiagnosticIds.BindingNotInTypeScript);
            Assert.Null(TypeScriptFixes.PlanFieldDeclaration(BindingTests.Extension, html, d, Reader(files)));
        }

        [Fact]
        public void DeclareField_StartsTheTsOfAnHtmlOnlyExtension()
        {
            // Acumatica ships extensions that are only HTML; the VSIX creates the empty .ts before applying this.
            Dictionary<string, string> files = BindingTests.Files();
            files.Remove(BindingTests.ExtensionTs);

            string fixedTs = Fix("<template><field name=\"UsrNote\" after=\"[name='OrderQty']\"></field></template>", files);

            Assert.Contains("import { SOLine } from \"src/screens/SO/SO301000/views\";", fixedTs, StringComparison.Ordinal);
            Assert.Contains("export interface SOLine_Custom extends SOLine {}", fixedTs, StringComparison.Ordinal);
            Assert.Contains("    UsrNote: PXFieldState;", fixedTs, StringComparison.Ordinal);
        }

        [Fact]
        public void ValueChangedHandler_WritesTheScreenExtensionAndItsImports()
        {
            Dictionary<string, string> files = BindingTests.Files();

            string fixedTs = Handle(DocumentField("UsrPriority"), files);

            Assert.Equal(
                "import { SO301000, SOOrderHeader } from \"src/screens/SO/SO301000/SO301000\";\n" +
                "import { handleEvent, CustomEventType, ValueChangedHandlerArgs } from \"client-controls\";\n" +
                "export interface SOOrderHeader_Custom extends SOOrderHeader {}\n" +
                "export class SOOrderHeader_Custom {\n" +
                "    UsrPriority: PXFieldState;\n" +
                "}\n" +
                "\n" +
                "export interface SO301000_Custom extends SO301000 {}\n" +
                "export class SO301000_Custom {\n" +
                "    @handleEvent(CustomEventType.ValueChanged, { view: \"Document\", field: \"UsrPriority\" })\n" +
                "    onUsrPriorityChanged(args: ValueChangedHandlerArgs) {\n" +
                "    }\n" +
                "}\n",
                fixedTs);
        }

        [Fact]
        public void ValueChangedHandler_JoinsTheScreenExtensionAlreadyThere()
        {
            Dictionary<string, string> files = BindingTests.Files();
            files[BindingTests.ExtensionTs] =
                "import { PXFieldState, handleEvent, CustomEventType } from \"client-controls\";\n" +
                "import { SO301000 } from \"src/screens/SO/SO301000/SO301000\";\n" +
                "\n" +
                "export interface SO301000_Custom extends SO301000 {}\n" +
                "export class SO301000_Custom {\n" +
                "\t@handleEvent(CustomEventType.RowSelected, { view: \"Document\" })\n" +
                "\tonDocumentSelected(args: RowSelectedHandlerArgs) {\n" +
                "\t}\n" +
                "}\n";

            string fixedTs = Handle(DocumentField("OrderDate"), files);

            Assert.Equal(
                "import { PXFieldState, handleEvent, CustomEventType, ValueChangedHandlerArgs } from \"client-controls\";\n" +
                "import { SO301000 } from \"src/screens/SO/SO301000/SO301000\";\n" +
                "\n" +
                "export interface SO301000_Custom extends SO301000 {}\n" +
                "export class SO301000_Custom {\n" +
                "\t@handleEvent(CustomEventType.RowSelected, { view: \"Document\" })\n" +
                "\tonDocumentSelected(args: RowSelectedHandlerArgs) {\n" +
                "\t}\n" +
                "\n" +
                "\t@handleEvent(CustomEventType.ValueChanged, { view: \"Document\", field: \"OrderDate\" })\n" +
                "\tonOrderDateChanged(args: ValueChangedHandlerArgs) {\n" +
                "\t}\n" +
                "}\n",
                fixedTs);
        }

        [Fact]
        public void ValueChangedHandler_InAStandaloneScreenGoesIntoTheScreenClass()
        {
            const string html = "/site/src/development/screens/XX/XX301000/XX301000.html";
            Dictionary<string, string> files = new Dictionary<string, string>
            {
                ["/site/src/development/screens/XX/XX301000/XX301000.ts"] =
                    "export class XX301000 extends PXScreen {\n" +
                    "\tFilter = createSingle(XXFilter);\n" +
                    "}\n" +
                    "export class XXFilter extends PXView {\n" +
                    "\tDate: PXFieldState;\n" +
                    "}\n",
            };

            string fixedTs = Handle("<template><qp-fieldset id=\"f\" view.bind=\"Filter\"><field name=\"Date\"></field></qp-fieldset></template>", files, html);

            Assert.StartsWith(
                "import { handleEvent, CustomEventType, ValueChangedHandlerArgs } from \"client-controls\";\n" +
                "export class XX301000 extends PXScreen {\n" +
                "\tFilter = createSingle(XXFilter);\n" +
                "\n" +
                "\t@handleEvent(CustomEventType.ValueChanged, { view: \"Filter\", field: \"Date\" })\n" +
                "\tonDateChanged(args: ValueChangedHandlerArgs) {\n" +
                "\t}\n" +
                "}\n",
                fixedTs,
                StringComparison.Ordinal);
        }

        [Fact]
        public void ValueChangedHandler_TakesTheViewIntoItsNameWhenThePlainOneIsTaken()
        {
            Dictionary<string, string> files = BindingTests.Files();
            files[BindingTests.ExtensionTs] =
                "import { SO301000 } from \"src/screens/SO/SO301000/SO301000\";\n" +
                "export interface SO301000_Custom extends SO301000 {}\n" +
                "export class SO301000_Custom {\n" +
                "    onOrderDateChanged() { }\n" +
                "}\n";

            Assert.Contains("    onDocumentOrderDateChanged(args: ValueChangedHandlerArgs) {\n", Handle(DocumentField("OrderDate"), files), StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(BindingTests.Extension, "UsrNope", "name=")]
        [InlineData(BindingTests.Extension, "OrderDate", "qp-fieldset")]
        [InlineData("/site/src/screens/SO/SO301000/SO301000.html", "OrderDate", "name=")]
        public void ValueChangedHandler_IsNotOfferedForAnUndeclaredFieldOffTheTagOrOnAStockScreen(string htmlPath, string field, string caretOn)
        {
            string html = DocumentField(field);
            int caret = html.IndexOf(caretOn, StringComparison.Ordinal);
            Assert.Null(TypeScriptFixes.PlanValueChangedHandler(htmlPath, html, caret, caret, Reader(BindingTests.Files())));
        }

        /// <summary>An extension that puts one field on the Document view.</summary>
        private static string DocumentField(string field)
        {
            return "<template><qp-fieldset id=\"f\" after=\"#x\" view.bind=\"Document\"><field name=\"" + field + "\"></field></qp-fieldset></template>";
        }

        /// <summary>Writes the handler for the only field tag, then checks the .ts is clean and the handler isn't offered twice.</summary>
        private static string Handle(string html, Dictionary<string, string> files, string htmlPath = BindingTests.Extension)
        {
            // Visual Studio asks with the whole line, not the caret.
            TsDeclaration? plan = TypeScriptFixes.PlanValueChangedHandler(htmlPath, html, 0, html.Length, Reader(files));
            Assert.NotNull(plan);

            string ts = files.TryGetValue(plan!.TsPath, out string? existing) ? existing : string.Empty;
            foreach (TextEdit edit in TypeScriptFixes.Declare(ts, plan))
            {
                ts = TestFiles.Apply(ts, edit);
            }

            files[plan.TsPath] = ts;
            Assert.Empty(TypeScriptScanner.Analyze(plan.TsPath, ts, Reader(files), null));
            Assert.Empty(TypeScriptFixes.Declare(ts, plan));
            Assert.Null(TypeScriptFixes.PlanValueChangedHandler(htmlPath, html, 0, html.Length, Reader(files)));
            return ts;
        }

        /// <summary>Applies the fix, then checks the HTML is clean against the fixed .ts.</summary>
        private static string Fix(string html, Dictionary<string, string> files, string htmlPath = BindingTests.Extension)
        {
            Diagnostic d = Assert.Single(HtmlMergeScanner.Analyze(htmlPath, html, Reader(files)), x => x.Id == DiagnosticIds.BindingNotInTypeScript);
            TsDeclaration? plan = TypeScriptFixes.PlanFieldDeclaration(htmlPath, html, d, Reader(files));
            Assert.NotNull(plan);

            string ts = files.TryGetValue(plan!.TsPath, out string? existing) ? existing : string.Empty;
            foreach (TextEdit edit in TypeScriptFixes.Declare(ts, plan))
            {
                ts = TestFiles.Apply(ts, edit);
            }

            files[plan.TsPath] = ts;
            Assert.DoesNotContain(HtmlMergeScanner.Analyze(htmlPath, html, Reader(files)), x => x.Id == DiagnosticIds.BindingNotInTypeScript);
            Assert.Empty(TypeScriptFixes.Declare(ts, plan));
            return ts;
        }

        private static IReadOnlyList<Diagnostic> Scan(string html, Dictionary<string, string> files)
        {
            return HtmlMergeScanner.Analyze(BindingTests.Extension, html, Reader(files)).ToList();
        }

        private static Func<string, string?> Reader(Dictionary<string, string> files)
        {
            return p => files.TryGetValue(p, out string? text) ? text : null;
        }
    }
}
