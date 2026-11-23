using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace AISI.MuiLint.Tests
{
    public sealed class EventHandlerTests
    {
        private const string ScreenTs = "/site/src/development/screens/XX/XX301000/XX301000.ts";
        private const string StockTs = "/site/src/screens/SO/SO301000/SO301000.ts";
        private const string ExtensionTs = "/site/src/development/screens/SO/SO301000/extensions/SO301000_Custom.ts";
        private const string OtherExtensionTs = "/site/src/development/screens/SO/SO301000/extensions/SO301000_Other.ts";

        private const string Screen =
            "export class XX301000 extends PXScreen {\n" +
            "    Filter = createSingle(XXFilter);\n" +
            "    Lines = createCollection(XXLine);\n" +
            "{0}" +
            "}\n" +
            "export class XXFilter extends PXView { StartDate: PXFieldState; }\n" +
            "export class XXLine extends PXView { Qty: PXFieldState; }\n";

        private const string Stock =
            "export class SO301000 extends PXScreen {\n" +
            "    Document = createSingle(SOOrder);\n" +
            "    @handleEvent(CustomEventType.RowSelected, { view: \"Document\" })\n" +
            "    onDocumentSelected(args: RowSelectedHandlerArgs) {}\n" +
            "    @viewInfo({ containerName: \"Totals\" })\n" +
            "    Totals = createSingle(SOOrder);\n" +
            "    refreshTotals() {}\n" +
            "}\n" +
            "export class SOOrder extends PXView { OrderNbr: PXFieldState; }\n";

        [Theory]
        [InlineData("@handleEvent(CustomEventType.RowSelected)", "names no view")]
        [InlineData("@handleEvent(CustomEventType.GetRowCss, { order: 1 })", "names no view")]
        [InlineData("@handleEvent(CustomEventType.ValueChanged, { view: \"Filter\" })", "needs the field")]
        [InlineData("@handleEvent(CustomEventType.GetCellCss, { view: \"Lines\" })", "needs a column")]
        public void MissingTarget_IsReportedOnTheDecorator(string decorator, string why)
        {
            string ts = Screen.Replace("{0}", "    " + decorator + "\n    handler() {}\n", StringComparison.Ordinal);
            Diagnostic d = Assert.Single(Scan(ScreenTs, ts), x => x.Id == DiagnosticIds.HandlerMissingTarget);
            Assert.Equal("@handleEvent", ts.Substring(d.Start, d.Length));
            Assert.Contains(why, d.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("@handleEvent(CustomEventType.CallbackCompleted)")]
        [InlineData("@handleEvent(CustomEventType.GetCellCss, { view: \"Lines\", allColumns: true })")]
        [InlineData("@handleEvent(CustomEventType.ValueChanged, {\n        view: \"Filter\",\n        field: \"StartDate\",\n    })")]
        [InlineData("@handleEvent(CustomEventType.ValueChanged, { view, field })")]
        [InlineData("@handleEvent(CustomEventType.RowSelected, sharedProps)")]
        [InlineData("@handleEvent(CustomEventType.SomethingNew)")]
        [InlineData("// @handleEvent(CustomEventType.RowSelected)")]
        public void MissingTarget_LeavesCompleteAndUnreadableDecoratorsAlone(string decorator)
        {
            string ts = Screen.Replace("{0}", "    " + decorator + "\n    handler() {}\n", StringComparison.Ordinal);
            Assert.DoesNotContain(Scan(ScreenTs, ts), x => x.Id == DiagnosticIds.HandlerMissingTarget);
        }

        [Theory]
        [InlineData("@handleEvent(CustomEventType.ValueChanged, { view: \"Filter\", field: \"StartDat\" })", "StartDat", "field", "XXFilter")]
        [InlineData("@handleEvent(CustomEventType.GetCellCss, { view: \"Lines\", column: \"Quantity\" })", "Quantity", "column", "XXLine")]
        public void UnknownField_IsReportedOnTheName(string decorator, string name, string kind, string viewClass)
        {
            string ts = Screen.Replace("{0}", "    " + decorator + "\n    handler() {}\n", StringComparison.Ordinal);
            Diagnostic d = Assert.Single(Scan(ScreenTs, ts), x => x.Id == DiagnosticIds.HandlerFieldNotDeclared);
            Assert.Equal(name, ts.Substring(d.Start, d.Length));
            Assert.Contains("the " + kind + " '" + name + "'", d.Message, StringComparison.Ordinal);
            Assert.Contains(viewClass, d.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("@handleEvent(CustomEventType.ValueChanged, { view: \"Filter\", field: \"startdate\" })")]
        [InlineData("@handleEvent(CustomEventType.ValueChanged, { view: \"Missing\", field: \"Anything\" })")]
        [InlineData("@handleEvent(CustomEventType.RowSelected, { view: \"Filter\", field: \"Ignored\" })")]
        public void UnknownField_StaysQuietWhenItCannotTell(string decorator)
        {
            string ts = Screen.Replace("{0}", "    " + decorator + "\n    handler() {}\n", StringComparison.Ordinal);
            Assert.DoesNotContain(Scan(ScreenTs, ts), x => x.Id == DiagnosticIds.HandlerFieldNotDeclared);
        }

        [Fact]
        public void UnknownField_NeedsTheScreen()
        {
            string ts = Screen.Replace("{0}", "    @handleEvent(CustomEventType.ValueChanged, { view: \"Filter\", field: \"StartDat\" })\n    handler() {}\n", StringComparison.Ordinal);
            Assert.DoesNotContain(TypeScriptScanner.Analyze(ScreenTs, ts, null), x => x.Id == DiagnosticIds.HandlerFieldNotDeclared);
        }

        [Theory]
        [InlineData("RowSelected, { view: \"Filter\" }", "ValueChangedHandlerArgs", "RowSelectedHandlerArgs")]
        [InlineData("ValueChanged, { view: \"Filter\", field: \"StartDate\" }", "RowSelectedHandlerArgs<XXFilter>", "ValueChangedHandlerArgs")]
        [InlineData("GetCellCss, { view: \"Lines\", allColumns: true }", "RowCssHandlerArgs", "CellCssHandlerArgs")]
        [InlineData("CallbackCompleted", "CurrentRowChangedHandlerArgs", "CallbackCompletedHandlerArgs")]
        public void ArgsOfAnotherEvent_AreReportedOnTheType(string decorator, string argsType, string expected)
        {
            string ts = Screen.Replace(
                "{0}",
                "    @handleEvent(CustomEventType." + decorator + ")\n    @someOtherDecorator({ a: (1) })\n    protected async handler(args: " + argsType + ") {}\n",
                StringComparison.Ordinal);
            Diagnostic d = Assert.Single(Scan(ScreenTs, ts), x => x.Id == DiagnosticIds.HandlerArgsOfAnotherEvent);
            Assert.Equal(argsType.Split('<')[0], ts.Substring(d.Start, d.Length));
            Assert.EndsWith("Use " + expected + ".", d.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("RowSelected, { view: \"Filter\" }", "args: CurrentRowChangedHandlerArgs")]
        [InlineData("CurrentRowChanged, { view: \"Filter\" }", "args: RowSelectedHandlerArgs<XXFilter>")]
        [InlineData("RowSelected, { view: \"Filter\" }", "args: any")]
        [InlineData("RowSelected, { view: \"Filter\" }", "args")]
        [InlineData("RowSelected, { view: \"Filter\" }", "")]
        [InlineData("RowSelected, { view: \"Filter\" }", "args: MyOwnArgs")]
        public void ArgsOfAnotherEvent_AcceptsMatchingAndUnknownTypes(string decorator, string parameter)
        {
            string ts = Screen.Replace("{0}", "    @handleEvent(CustomEventType." + decorator + ")\n    handler(" + parameter + ") {}\n", StringComparison.Ordinal);
            Assert.DoesNotContain(Scan(ScreenTs, ts), x => x.Id == DiagnosticIds.HandlerArgsOfAnotherEvent);
        }

        [Fact]
        public void MethodCollision_WithTheScreen_IsReported()
        {
            const string ts =
                "import { SO301000 } from \"src/screens/SO/SO301000/SO301000\";\n" +
                "export interface SO301000_Custom extends SO301000 {}\n" +
                "export class SO301000_Custom {\n" +
                "    refreshTotals() {}\n" +
                "    refreshMyTotals() {}\n" +
                "}\n";
            Diagnostic d = Assert.Single(Scan(ExtensionTs, ts, (StockTs, Stock)), x => x.Id == DiagnosticIds.ExtensionMethodCollision);
            Assert.Equal("refreshTotals", ts.Substring(d.Start, d.Length));
            Assert.Contains("a method of SO301000.", d.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void MethodCollision_WithAnotherExtension_IsReported()
        {
            const string ts =
                "import { SO301000 } from \"src/screens/SO/SO301000/SO301000\";\n" +
                "export interface SO301000_Custom extends SO301000 {}\n" +
                "export class SO301000_Custom {\n" +
                "    setVisibility() {}\n" +
                "}\n";
            const string other =
                "import { SO301000 } from \"src/screens/SO/SO301000/SO301000\";\n" +
                "export interface SO301000_Other extends SO301000 {}\n" +
                "export class SO301000_Other {\n" +
                "    setVisibility() {}\n" +
                "}\n";
            Diagnostic d = Assert.Single(Scan(ExtensionTs, ts, (StockTs, Stock), (OtherExtensionTs, other)), x => x.Id == DiagnosticIds.ExtensionMethodCollision);
            Assert.Contains("a method of SO301000_Other.", d.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void MethodCollision_LeavesHandlerPairsViewsAndOtherClassesAlone()
        {
            const string ts =
                "import { SO301000, SOOrder } from \"src/screens/SO/SO301000/SO301000\";\n" +
                "export interface SO301000_Custom extends SO301000 {}\n" +
                "export class SO301000_Custom {\n" +
                "    @handleEvent(CustomEventType.RowSelected, { view: \"Document\" })\n" +
                "    onDocumentSelected(args: RowSelectedHandlerArgs) {}\n" +
                "    @viewInfo({ containerName: \"Totals\" })\n" +
                "    Totals = createSingle(SOOrder);\n" +
                "    constructor() {}\n" +
                "}\n" +
                "export class Helper {\n" +
                "    refreshTotals() {}\n" +
                "}\n";
            Assert.DoesNotContain(Scan(ExtensionTs, ts, (StockTs, Stock)), x => x.Id == DiagnosticIds.ExtensionMethodCollision);
        }

        [Fact]
        public void MethodCollision_AHandlerMeetingAPlainMethod_IsReported()
        {
            const string ts =
                "import { SO301000 } from \"src/screens/SO/SO301000/SO301000\";\n" +
                "export interface SO301000_Custom extends SO301000 {}\n" +
                "export class SO301000_Custom {\n" +
                "    @handleEvent(CustomEventType.RowSelected, { view: \"Document\" })\n" +
                "    refreshTotals() {}\n" +
                "}\n";
            Assert.Single(Scan(ExtensionTs, ts, (StockTs, Stock)), x => x.Id == DiagnosticIds.ExtensionMethodCollision);
        }

        [Fact]
        public void NewRules_AreWarnings()
        {
            string[] ids =
            {
                DiagnosticIds.HandlerMissingTarget,
                DiagnosticIds.HandlerFieldNotDeclared,
                DiagnosticIds.HandlerArgsOfAnotherEvent,
                DiagnosticIds.ExtensionMethodCollision,
            };
            Assert.All(ids, id => Assert.Equal(Severity.Warning, Rules.Find(id)!.DefaultSeverity));
        }

        /// <summary>Scans <paramref name="ts"/> against an in-memory site holding it and <paramref name="others"/>.</summary>
        private static IReadOnlyList<Diagnostic> Scan(string path, string ts, params (string Path, string Text)[] others)
        {
            Dictionary<string, string> files = others.ToDictionary(o => o.Path, o => o.Text, StringComparer.Ordinal);
            files[path] = ts;
            return TypeScriptScanner.Analyze(
                path,
                ts,
                p => files.TryGetValue(p, out string? text) ? text : null,
                folder => files.Keys.Where(p => p.StartsWith(folder.TrimEnd('/') + "/", StringComparison.Ordinal) && p.IndexOf('/', folder.TrimEnd('/').Length + 1) < 0));
        }
    }
}
