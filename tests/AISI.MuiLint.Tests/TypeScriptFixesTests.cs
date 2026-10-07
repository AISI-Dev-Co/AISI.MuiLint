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
            var files = new Dictionary<string, string>
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
        public void NewExtensionTypeScript_ImportsTheStockScreen()
        {
            Assert.Equal(
                "import { SO301000 } from \"src/screens/SO/SO301000/SO301000\";\n" +
                "\n" +
                "export interface SO301000_AISI extends SO301000 {}\n" +
                "export class SO301000_AISI {}\n",
                TypeScriptFixes.NewExtensionTypeScript("/site/src/development/screens/SO/SO301000/extensions/SO301000_AISI.html"));

            Assert.Null(TypeScriptFixes.NewExtensionTypeScript("/site/src/development/screens/SO/SO301000/SO301000.html"));
        }

        /// <summary>Applies the fix, then checks the HTML is clean against the fixed .ts.</summary>
        private static string Fix(string html, Dictionary<string, string> files, string htmlPath = BindingTests.Extension)
        {
            Diagnostic d = Assert.Single(HtmlMergeScanner.Analyze(htmlPath, html, Reader(files)), x => x.Id == DiagnosticIds.BindingNotInTypeScript);
            FieldDeclaration? plan = TypeScriptFixes.PlanFieldDeclaration(htmlPath, html, d, Reader(files));
            Assert.NotNull(plan);

            string ts = files[plan!.TsPath];
            foreach (TextEdit edit in TypeScriptFixes.DeclareField(ts, plan))
            {
                ts = TestFiles.Apply(ts, edit);
            }

            files[plan.TsPath] = ts;
            Assert.DoesNotContain(HtmlMergeScanner.Analyze(htmlPath, html, Reader(files)), x => x.Id == DiagnosticIds.BindingNotInTypeScript);
            Assert.Empty(TypeScriptFixes.DeclareField(ts, plan));
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
