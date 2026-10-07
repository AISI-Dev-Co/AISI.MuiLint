using System;
using System.Collections.Generic;
using Xunit;

namespace AISI.MuiLint.Tests
{
    public sealed class TypeScriptScannerTests
    {
        private const string ExtensionTs = "/site/src/development/screens/SO/SO301000/extensions/SO301000_Custom.ts";
        private const string ScreenTs = "/site/src/development/screens/XX/XX301000/XX301000.ts";

        [Fact]
        public void HalfAnExtension_ReportsBothHalves()
        {
            const string ts =
                "export interface SOLine_Custom extends SOLine {}\n" +
                "export class SOLineCustom {\n" +
                "    UsrNote: PXFieldState;\n" +
                "}\n";
            IReadOnlyList<Diagnostic> results = TypeScriptScanner.Analyze(ExtensionTs, ts, null);
            Assert.Collection(
                results,
                d => Assert.Equal("SOLine_Custom", ts.Substring(d.Start, d.Length)),
                d => Assert.Equal("SOLineCustom", ts.Substring(d.Start, d.Length)));
            Assert.All(results, d => Assert.Equal(DiagnosticIds.HalfAnExtension, d.Id));
        }

        [Fact]
        public void HalfAnExtension_LeavesHelpersAndWholePairsAlone()
        {
            const string ts =
                "export interface SOLine_Custom extends SOLine {}\n" +
                "export class SOLine_Custom { UsrNote: PXFieldState; }\n" +
                "export class Formatter { format(x: string) { return x; } }\n" +
                "export class Lines extends PXView { UsrA: PXFieldState; }\n";
            Assert.Empty(TypeScriptScanner.Analyze(ExtensionTs, ts, null));
        }

        [Fact]
        public void HalfAnExtension_OnlyAppliesToExtensionFiles()
        {
            const string ts = "export class Helper { UsrA: PXFieldState; }\n";
            Assert.Empty(TypeScriptScanner.Analyze(ScreenTs, ts, null));
        }

        [Fact]
        public void Decorators_MustNameARealView()
        {
            const string ts =
                "// primaryView: \"Ignored\" in a comment\n" +
                "@graphInfo({ graphType: \"X.Graph\", primaryView: \"Filtre\" })\n" +
                "export class XX301000 extends PXScreen {\n" +
                "    Filter = createSingle(XXFilter);\n" +
                "    @handleEvent(CustomEventType.RowSelected, { view: \"Filter\" })\n" +
                "    ok() {}\n" +
                "    @handleEvent(CustomEventType.RowSelected, { view: \"Lines\" })\n" +
                "    notOk() {}\n" +
                "}\n" +
                "export class XXFilter extends PXView { Date: PXFieldState; }\n";

            IReadOnlyList<Diagnostic> results = Scan(ScreenTs, ts, new Dictionary<string, string>());
            Assert.Collection(
                results,
                d => Assert.Equal("Filtre", ts.Substring(d.Start, d.Length)),
                d => Assert.Equal("Lines", ts.Substring(d.Start, d.Length)));
            Assert.All(results, d => Assert.Equal(DiagnosticIds.DecoratorViewNotDeclared, d.Id));
        }

        [Fact]
        public void ViewFromNonView_FlagsExtensionClassesButNotUnknownBases()
        {
            const string ts =
                "export class XX301000 extends PXScreen {\n" +
                "    Lines = createCollection(Line_Custom);\n" +
                "    Mystery = createCollection(Imported);\n" +
                "    Good = createSingle(Header);\n" +
                "}\n" +
                "export class Line_Custom { UsrA: PXFieldState; }\n" +
                "export class Imported extends SomethingFromALibrary {}\n" +
                "export class Header extends PXView { A: PXFieldState; }\n";

            Diagnostic d = Assert.Single(Scan(ScreenTs, ts, new Dictionary<string, string>()), x => x.Id == DiagnosticIds.ViewFromNonView);
            Assert.Equal("Lines", ts.Substring(d.Start, d.Length));
            Assert.Equal(Severity.Error, d.Severity);
        }

        [Fact]
        public void LineCommentsSuppress()
        {
            const string ts =
                "// muilint-disable-next-line AISI0014\n" +
                "export interface SOLine_Custom extends SOLine {}\n" +
                "export interface SOLine_Other extends SOLine {}\n";
            Diagnostic d = Assert.Single(TypeScriptScanner.Analyze(ExtensionTs, ts, null));
            Assert.Equal(3, d.Line);
        }

        [Fact]
        public void OnlyModernUiScreensAreChecked()
        {
            Assert.Empty(TypeScriptScanner.Analyze("/site/src/screens/SO/SO301000/extensions/X.ts", "export interface A extends B {}", null));
            Assert.Empty(TypeScriptScanner.Analyze("/work/some-web-app/src/extensions/X.ts", "export interface A extends B {}", null));
            Assert.False(MuiLinter.CanScan("/site/node_modules/client-controls/index.d.ts"));
            Assert.True(MuiLinter.CanScan(ExtensionTs));
        }

        private static IReadOnlyList<Diagnostic> Scan(string path, string ts, Dictionary<string, string> files)
        {
            files[path] = ts;
            return TypeScriptScanner.Analyze(path, ts, p => files.TryGetValue(p, out string? text) ? text : null);
        }
    }
}
