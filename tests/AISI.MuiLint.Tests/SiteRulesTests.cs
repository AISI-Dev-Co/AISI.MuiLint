using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace AISI.MuiLint.Tests
{
    /// <summary>The checks against the site's Bin, with the metadata made by hand.</summary>
    public sealed class SiteRulesTests
    {
        private const string Screen = "/site/src/development/screens/XX/XX301000/XX301000.ts";
        private const string ExtensionTs = "/site/src/development/screens/XX/XX301000/extensions/XX301000_AISI.ts";

        private const string ScreenTs =
            "import { PXScreen, PXView, PXFieldState, PXActionState, createSingle, createCollection, graphInfo, linkCommand, featureInstalled } from \"client-controls\";\n"
            + "@graphInfo({ graphType: \"PX.Objects.XX.XXEntry\", primaryView: \"Document\" })\n"
            + "export class XX301000 extends PXScreen {\n"
            + "    Release: PXActionState;\n"
            + "    Relase: PXActionState;\n"
            + "    Document = createSingle(XXDocument);\n"
            + "    Lines = createCollection(XXLine);\n"
            + "    Lnes = createCollection(XXLine);\n"
            + "    _Generated_CurrencyInfo_ = createSingle(XXLine);\n"
            + "}\n"
            + "export class XXDocument extends PXView {\n"
            + "    RefNbr: PXFieldState;\n"
            + "    Status: PXFieldState;\n"
            + "    Stauts: PXFieldState;\n"
            + "    Customer__AcctName: PXFieldState;\n"
            + "    @linkCommand(\"Release\") Description: PXFieldState;\n"
            + "    @linkCommand(\"Relese\") Note: PXFieldState;\n"
            + "}\n"
            + "export class XXLine extends PXView {\n"
            + "    LineNbr: PXFieldState;\n"
            + "}\n";

        [Fact]
        public void ViewsActionsFieldsAndLinks_AreCheckedAgainstTheGraph()
        {
            IReadOnlyList<Diagnostic> found = Scan(ScreenTs, Site(complete: true));

            Assert.Equal(new[] { "Relase", "Lnes" }, Spans(found, DiagnosticIds.MemberNotInGraph));
            Assert.Equal(new[] { "Stauts" }, Spans(found, DiagnosticIds.FieldNotInView));
            Assert.Equal(new[] { "Relese" }, Spans(found, DiagnosticIds.LinkCommandUnknownAction));
            Assert.Contains("XXEntry has no action called 'Relase'", found.First(d => d.Id == DiagnosticIds.MemberNotInGraph).Message, StringComparison.Ordinal);
            Assert.Contains("XXDocument has no field 'Stauts'", found.Single(d => d.Id == DiagnosticIds.FieldNotInView).Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AnIncompleteGraph_ProvesNothingMissing()
        {
            IReadOnlyList<Diagnostic> found = Scan(ScreenTs, Site(complete: false));
            Assert.DoesNotContain(found, d => d.Id == DiagnosticIds.MemberNotInGraph || d.Id == DiagnosticIds.LinkCommandUnknownAction);

            // Fields are the DAC's business, not the graph's.
            Assert.Equal(new[] { "Stauts" }, Spans(found, DiagnosticIds.FieldNotInView));
        }

        [Fact]
        public void AnUnknownGraph_IsReportedWhereItIsNamed()
        {
            string ts = ScreenTs.Replace("PX.Objects.XX.XXEntry", "PX.Objects.XX.XXEntyr");
            IReadOnlyList<Diagnostic> found = Scan(ts, Site(complete: true));
            Diagnostic d = Assert.Single(found, x => x.Id == DiagnosticIds.GraphNotInSite);
            Assert.Equal("PX.Objects.XX.XXEntyr", ts.Substring(d.Start, d.Length));
            Assert.DoesNotContain(found, x => x.Id == DiagnosticIds.MemberNotInGraph || x.Id == DiagnosticIds.FieldNotInView);

            // An empty Bin (nothing could be read) says nothing about any graph.
            Assert.DoesNotContain(Scan(ts, new SiteMetadata(Array.Empty<GraphMetadata>(), null)), x => x.Id == DiagnosticIds.GraphNotInSite);
        }

        [Fact]
        public void Extensions_AreCheckedAgainstTheScreensGraph()
        {
            const string extension =
                "import { XX301000, XXDocument } from \"../XX301000\";\n"
                + "@featureInstalled('PX.Objects.CS.FeaturesSet+Multicurrency')\n"
                + "export interface XX301000_AISI extends XX301000 {}\n"
                + "@featureInstalled('PX.Objects.CS.FeaturesSet+Multicurency')\n"
                + "export class XX301000_AISI {\n"
                + "    UsrApprove: PXActionState;\n"
                + "    UsrAprove: PXActionState;\n"
                + "}\n"
                + "export interface XXDocument_AISI extends XXDocument {}\n"
                + "export class XXDocument_AISI {\n"
                + "    UsrPriority: PXFieldState;\n"
                + "    UsrPriorty: PXFieldState;\n"
                + "}\n";
            var files = new Dictionary<string, string> { [Screen] = ScreenTs, [ExtensionTs] = extension };
            IReadOnlyList<Diagnostic> found = TypeScriptScanner.Analyze(ExtensionTs, extension, BindingTests.Reader(files), BindingTests.Lister(files), Site(complete: true));

            Assert.Equal(new[] { "UsrAprove" }, Spans(found, DiagnosticIds.MemberNotInGraph, extension));
            Assert.Equal(new[] { "UsrPriorty" }, Spans(found, DiagnosticIds.FieldNotInView, extension));
            Assert.Equal(new[] { "PX.Objects.CS.FeaturesSet+Multicurency" }, Spans(found, DiagnosticIds.FeatureNotInSite, extension));
        }

        [Fact]
        public void FindGraph_TakesNestedTypesEitherWay()
        {
            var site = new SiteMetadata(new[] { new GraphMetadata("PX.Objects.XX.Outer+Inner", Array.Empty<ViewMetadata>(), Array.Empty<string>(), true) }, null);
            Assert.NotNull(site.FindGraph("PX.Objects.XX.Outer+Inner"));
            Assert.NotNull(site.FindGraph("PX.Objects.XX.Outer.Inner"));
            Assert.NotNull(site.FindGraph("px.objects.xx.outer.inner"));
            Assert.Null(site.FindGraph("PX.Objects.XX.Inner"));
        }

        private static IReadOnlyList<Diagnostic> Scan(string ts, SiteMetadata site)
        {
            var files = new Dictionary<string, string> { [Screen] = ts };
            return TypeScriptScanner.Analyze(Screen, ts, BindingTests.Reader(files), BindingTests.Lister(files), site);
        }

        private static string[] Spans(IReadOnlyList<Diagnostic> found, string id, string? text = null)
        {
            return found.Where(d => d.Id == id).Select(d => (text ?? ScreenTs).Substring(d.Start, d.Length)).ToArray();
        }

        private static SiteMetadata Site(bool complete)
        {
            string[] document = { "RefNbr", "Status", "Description", "Note", "UsrPriority" };
            var graph = new GraphMetadata(
                "PX.Objects.XX.XXEntry",
                new[]
                {
                    new ViewMetadata("Document", "PX.Objects.XX.XXDocument", document),
                    new ViewMetadata("Lines", "PX.Objects.XX.XXLine", null),
                },
                new[] { "Release", "Save", "Cancel", "UsrApprove" },
                complete);
            return new SiteMetadata(new[] { graph }, new[] { "PX.Objects.CS.FeaturesSet+Multicurrency" });
        }
    }
}
