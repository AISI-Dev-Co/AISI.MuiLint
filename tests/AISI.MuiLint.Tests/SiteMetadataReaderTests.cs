using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using AISI.MuiLint.Site;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace AISI.MuiLint.Tests
{
    public sealed class SiteMetadataReaderTests : IClassFixture<SiteMetadataReaderTests.FakeSite>
    {
        private readonly FakeSite _site;

        public SiteMetadataReaderTests(FakeSite site)
        {
            _site = site;
        }

        private SiteMetadata Site
        {
            get { return _site.Metadata; }
        }

        [Fact]
        public void FindsConcreteGraphsOnly()
        {
            Assert.Equal(
                new[]
                {
                    "Orphan.OrphanGraph",
                    "Orphan.PartialGraph",
                    "PX.Objects.SO.SOGraphs+Nested",
                    "PX.Objects.SO.SOOrderEntry",
                    "PX.Objects.SO.SOOrderEntryEx",
                },
                Site.Graphs.Keys.OrderBy(name => name, StringComparer.Ordinal));
            Assert.True(Site.Graphs["PX.Objects.SO.SOOrderEntry"].Complete);
            Assert.True(Site.Graphs["PX.Objects.SO.SOOrderEntryEx"].Complete);
        }

        [Fact]
        public void ViewsNameTheirDac()
        {
            GraphMetadata graph = Site.Graphs["PX.Objects.SO.SOOrderEntry"];

            Assert.Equal("PX.Objects.SO.SOOrder", graph.Views["Document"].Dac);
            Assert.Equal("PX.Objects.SO.SOOrder", graph.Views["CurrentDocument"].Dac);
            Assert.Equal("PX.Objects.SO.SOLine", graph.Views["Transactions"].Dac);
            Assert.Equal("PX.Objects.SO.SOLine", graph.Views["Lines"].Dac);
            Assert.Equal("PX.Objects.SO.SOLine", graph.Views["JoinedLines"].Dac);
            Assert.Equal("PX.Objects.SO.SOSetup", graph.Views["sosetup"].Dac);
            Assert.Equal("PX.Objects.SO.SOOrderEntry+AddLineFilter", graph.Views["addFilter"].Dac);
            Assert.Equal(new[] { "Qty" }, graph.Views["addFilter"].Fields);
            Assert.Equal("PX.Objects.SO.SOOrder", graph.Views["document"].Dac);
        }

        [Fact]
        public void OnlyPublicInstanceViewFieldsAreViews()
        {
            GraphMetadata graph = Site.Graphs["PX.Objects.SO.SOOrderEntry"];

            foreach (string name in new[] { "StaticView", "NotAView", "ArrayOfViews", "protectedAction" })
            {
                Assert.False(graph.Views.ContainsKey(name), name);
                Assert.DoesNotContain(name, graph.Actions);
            }
        }

        [Fact]
        public void FieldsIncludeBaseDacAndCacheExtensionProperties()
        {
            ICollection<string>? fields = Site.Graphs["PX.Objects.SO.SOOrderEntry"].Views["Document"].Fields;

            Assert.NotNull(fields);
            Assert.Equal(
                new[] { "OrderNbr", "OrderTotal", "OrderType", "UsrPriority", "UsrSecond" },
                fields!.OrderBy(name => name, StringComparer.Ordinal));
            Assert.Contains("orderNbr", fields);
            Assert.DoesNotContain("ghostField", fields);
            Assert.DoesNotContain("Base", fields);

            Assert.Equal(
                new[] { "LineNbr", "OrderNbr" },
                Site.Graphs["PX.Objects.SO.SOOrderEntry"].Views["Transactions"].Fields!.OrderBy(name => name, StringComparer.Ordinal));
        }

        [Fact]
        public void PrimaryGraphDeclaresTheStandardActions()
        {
            ICollection<string> actions = Site.Graphs["PX.Objects.SO.SOOrderEntry"].Actions;

            foreach (string name in new[] { "Save", "Cancel", "Insert", "Delete", "CopyPaste", "First", "Previous", "Next", "Last", "release" })
            {
                Assert.Contains(name, actions);
            }

            Assert.Contains("save", actions);
            Assert.DoesNotContain("Save", Site.Graphs["PX.Objects.SO.SOGraphs+Nested"].Actions);
        }

        [Fact]
        public void ExtensionsAddViewsAndActions()
        {
            GraphMetadata graph = Site.Graphs["PX.Objects.SO.SOOrderEntry"];

            // The generic MultiCurrencyGraph<TGraph, TPrimary>, applied through SOOrderEntry.MultiCurrency.
            Assert.Equal("PX.Objects.CM.CurrencyInfo", graph.Views["currencyinfo"].Dac);
            Assert.Equal(new[] { "CuryID", "CuryInfoID" }, graph.Views["currencyinfo"].Fields!.OrderBy(name => name, StringComparer.Ordinal));
            Assert.Equal("PX.Objects.SO.SOOrder", graph.Views["currencyDocument"].Dac);
            Assert.Contains("UsrPriority", graph.Views["currencyDocument"].Fields!);
            Assert.Contains("currencyView", graph.Actions);

            // Custom.dll: PXGraphExtension<G>, <E, G> and <E2, E1, G>.
            Assert.Equal("PX.Objects.SO.SOOrder", graph.Views["UsrNotes"].Dac);
            Assert.Contains("usrApprove", graph.Actions);
            Assert.Contains("usrSecond", graph.Actions);
            Assert.Contains("usrThird", graph.Actions);

            Assert.DoesNotContain("abstractOnly", graph.Actions);
            Assert.DoesNotContain("exOnly", graph.Actions);
            Assert.DoesNotContain("exAction", graph.Actions);
        }

        [Fact]
        public void DerivedGraphInheritsTheBaseGraphsExtensions()
        {
            GraphMetadata graph = Site.Graphs["PX.Objects.SO.SOOrderEntryEx"];

            foreach (string view in new[] { "Document", "Transactions", "currencyinfo", "UsrNotes" })
            {
                Assert.True(graph.Views.ContainsKey(view), view);
            }

            foreach (string action in new[] { "Save", "release", "currencyView", "usrApprove", "usrThird", "exAction", "exOnly" })
            {
                Assert.Contains(action, graph.Actions);
            }
        }

        [Fact]
        public void FindGraphAcceptsDotOrPlusBeforeANestedType()
        {
            Assert.Same(Site.Graphs["PX.Objects.SO.SOOrderEntry"], Site.FindGraph("PX.Objects.SO.SOOrderEntry"));
            Assert.Same(Site.Graphs["PX.Objects.SO.SOGraphs+Nested"], Site.FindGraph("PX.Objects.SO.SOGraphs+Nested"));
            Assert.Same(Site.Graphs["PX.Objects.SO.SOGraphs+Nested"], Site.FindGraph("PX.Objects.SO.SOGraphs.Nested"));
            Assert.Null(Site.FindGraph("PX.Objects.SO.SOOrderEntry+MultiCurrency"));
            Assert.Null(Site.FindGraph("PX.Objects.CM.MultiCurrencyGraph"));
        }

        [Fact]
        public void FeaturesIncludeCacheExtensionFeatures()
        {
            Assert.NotNull(Site.Features);
            Assert.Equal(
                new[]
                {
                    "PX.Objects.CS.FeaturesSet+Inventory",
                    "PX.Objects.CS.FeaturesSet+Multicurrency",
                    "PX.Objects.CS.FeaturesSet+UsrCustomFeature",
                },
                Site.Features!.OrderBy(name => name, StringComparer.Ordinal));
        }

        [Fact]
        public void WhatCannotBeReadMakesAGraphIncomplete()
        {
            // OrphanGraph's base class lives in Middle.dll, which isn't in the Bin.
            GraphMetadata orphan = Site.Graphs["Orphan.OrphanGraph"];
            Assert.False(orphan.Complete);
            Assert.Equal("PX.Objects.SO.SOLine", orphan.Views["Lines"].Dac);
            Assert.False(Site.Graphs.ContainsKey("Orphan.OrphanHelper"));

            // PartialGraph reaches PXGraph, but one field's type is from Middle.dll, and so is a DAC's base.
            GraphMetadata partial = Site.Graphs["Orphan.PartialGraph"];
            Assert.False(partial.Complete);
            Assert.False(partial.Views.ContainsKey("MiddleLines"));
            Assert.Equal("PX.Objects.SO.SOOrder", partial.Views["Orders"].Dac);
            Assert.Equal("Orphan.DerivedDac", partial.Views["Derived"].Dac);
            Assert.Null(partial.Views["Derived"].Fields);
        }

        [Fact]
        public void NoFeaturesSetMeansNoFeatures()
        {
            SiteMetadata? site = SiteMetadataReader.Read(_site.DataOnlyBin);

            Assert.NotNull(site);
            Assert.Empty(site!.Graphs);
            Assert.Null(site.Features);
        }

        [Fact]
        public void SourceCodeBesideBin_CountsAsDeclared()
        {
            Assert.Contains("UsrRuntimeOnly", Site.SourceNames);
            Assert.Contains("usrruntimeonly", Site.SourceNames);
        }

        [Fact]
        public void AScreenInTheSite_IsCheckedAgainstItsBin()
        {
            string folder = Path.Combine(_site.Root, "FrontendSources", "screen", "src", "development", "screens", "SO", "SO301000");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "SO301000.ts");
            const string ts =
                "@graphInfo({ graphType: \"PX.Objects.SO.SOOrderEntry\", primaryView: \"Document\" })\n"
                + "export class SO301000 extends PXScreen {\n"
                + "    Release: PXActionState;\n"
                + "    Relase: PXActionState;\n"
                + "    Document = createSingle(SOOrder);\n"
                + "}\n"
                + "export class SOOrder extends PXView {\n"
                + "    OrderNbr: PXFieldState;\n"
                + "    OrderType: PXFieldState;\n"
                + "    UsrRuntimeOnly: PXFieldState;\n"
                + "    OrderNmbr: PXFieldState;\n"
                + "}\n";
            File.WriteAllText(path, ts);

            Assert.Equal(Path.GetFullPath(_site.Bin), SiteCache.BinFor(path));
            Assert.Null(SiteCache.BinFor(Path.Combine(_site.Root, "elsewhere", "SO301000.ts")));
            IReadOnlyList<AISI.MuiLint.Diagnostic> found = MuiLinter.Analyze(path, ts, p => File.Exists(p) ? File.ReadAllText(p) : null, d => Directory.Exists(d) ? Directory.GetFileSystemEntries(d) : Array.Empty<string>(), SiteCache.ForFile(path));

            Assert.Equal(
                new[] { DiagnosticIds.MemberNotInGraph + " Relase", DiagnosticIds.FieldNotInView + " OrderNmbr" },
                found.Where(d => d.Id == DiagnosticIds.MemberNotInGraph || d.Id == DiagnosticIds.FieldNotInView).Select(d => d.Id + " " + ts.Substring(d.Start, d.Length)));
            Assert.Same(SiteCache.ForFile(path), SiteCache.ForFile(path));
        }

        [Fact]
        public void JunkFilesAreSkipped()
        {
            Assert.Null(SiteMetadataReader.Read(_site.JunkBin));
        }

        [Fact]
        public void MissingFolderReadsAsNull()
        {
            Assert.Null(SiteMetadataReader.Read(Path.Combine(_site.Root, "no-such-folder")));
            Assert.Null(SiteMetadataReader.Read(string.Empty));
        }

        [Fact]
        public void ReadingTheFakeBinIsQuick()
        {
            Stopwatch watch = Stopwatch.StartNew();
            SiteMetadata? site = SiteMetadataReader.Read(_site.Bin);
            watch.Stop();

            Assert.NotNull(site);
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), "Took " + watch.Elapsed);
        }

        /// <summary>Compiles fake PX.Data, PX.Objects and customisation assemblies into a temporary Bin.</summary>
        public sealed class FakeSite : IDisposable
        {
            private const string PXData = @"
using System;
using PX.Data;

namespace PX.Data
{
    public interface IBqlTable { }
    public abstract class PXBqlTable : IBqlTable { }
    public interface IBqlWhere { }
    public interface IBqlJoin { }
    public interface IBqlField { }
    public class Where<TCondition> : IBqlWhere { }
    public class Equal<TOperand> { }
    public class Current<TField> where TField : IBqlField { }
    public class InnerJoin<TTable, TOn> : IBqlJoin { }
    public class On<TCondition> { }

    public class PXGraph { }
    public class PXGraph<TGraph> : PXGraph where TGraph : PXGraph { }
    public class PXGraph<TGraph, TPrimary> : PXGraph<TGraph>
        where TGraph : PXGraph
        where TPrimary : class, IBqlTable, new()
    {
        public PXSave<TPrimary> Save;
        public PXCancel<TPrimary> Cancel;
        public PXInsert<TPrimary> Insert;
        public PXDelete<TPrimary> Delete;
        public PXCopyPasteAction<TPrimary> CopyPaste;
        public PXFirst<TPrimary> First;
        public PXPrevious<TPrimary> Previous;
        public PXNext<TPrimary> Next;
        public PXLast<TPrimary> Last;
    }

    public abstract class PXSelectBase { }
    public abstract class PXSelectBase<Table> : PXSelectBase where Table : class, IBqlTable, new() { }
    public class PXSelect<Table> : PXSelectBase<Table> where Table : class, IBqlTable, new() { }
    public class PXSelect<Table, Where> : PXSelectBase<Table> where Table : class, IBqlTable, new() where Where : IBqlWhere { }
    public class PXSelectJoin<Table, Join, Where> : PXSelectBase<Table> where Table : class, IBqlTable, new() where Join : IBqlJoin where Where : IBqlWhere { }
    public class PXSelectReadonly<Table> : PXSelectBase<Table> where Table : class, IBqlTable, new() { }
    public class PXSetup<Table> : PXSelectReadonly<Table> where Table : class, IBqlTable, new() { }
    public class PXFilter<Table> : PXSelectBase<Table> where Table : class, IBqlTable, new() { }

    public abstract class PXAction { }
    public class PXAction<TNode> : PXAction where TNode : class, IBqlTable, new() { }
    public class PXSave<TNode> : PXAction<TNode> where TNode : class, IBqlTable, new() { }
    public class PXCancel<TNode> : PXAction<TNode> where TNode : class, IBqlTable, new() { }
    public class PXInsert<TNode> : PXAction<TNode> where TNode : class, IBqlTable, new() { }
    public class PXDelete<TNode> : PXAction<TNode> where TNode : class, IBqlTable, new() { }
    public class PXCopyPasteAction<TNode> : PXAction<TNode> where TNode : class, IBqlTable, new() { }
    public class PXFirst<TNode> : PXAction<TNode> where TNode : class, IBqlTable, new() { }
    public class PXPrevious<TNode> : PXAction<TNode> where TNode : class, IBqlTable, new() { }
    public class PXNext<TNode> : PXAction<TNode> where TNode : class, IBqlTable, new() { }
    public class PXLast<TNode> : PXAction<TNode> where TNode : class, IBqlTable, new() { }

    public abstract class PXGraphExtension { }
    public abstract class PXGraphExtension<Graph> : PXGraphExtension where Graph : PXGraph
    {
        public Graph Base { get { return null; } }
    }
    public abstract class PXGraphExtension<Extension1, Graph> : PXGraphExtension<Graph> where Graph : PXGraph
    {
        public Extension1 Base1 { get { return default(Extension1); } }
    }
    public abstract class PXGraphExtension<Extension2, Extension1, Graph> : PXGraphExtension<Extension1, Graph> where Graph : PXGraph { }

    public abstract class PXCacheExtension { }
    public abstract class PXCacheExtension<Table> : PXCacheExtension where Table : IBqlTable
    {
        public Table Base { get { return default(Table); } }
    }
    public abstract class PXCacheExtension<Extension1, Table> : PXCacheExtension<Table> where Table : IBqlTable { }
}

namespace PX.Data.BQL
{
    public abstract class BqlString
    {
        public abstract class Field<TSelf> : IBqlField where TSelf : Field<TSelf> { }
    }
}

namespace PX.Data.BQL.Fluent
{
    public class PXViewOf<TTable> where TTable : class, IBqlTable, new()
    {
        public class BasedOn<TSelect> : PXSelectBase<TTable> { }
    }

    public abstract class FbqlSelect<TSelf, TTable> where TTable : class, IBqlTable, new()
    {
        public class View : PXViewOf<TTable>.BasedOn<TSelf> { }
    }

    public class SelectFrom<TTable> where TTable : class, IBqlTable, new()
    {
        public class Where<TCondition> : FbqlSelect<Where<TCondition>, TTable> { }
    }
}
";

            private const string PXObjects = @"
using PX.Data;
using PX.Data.BQL;
using PX.Data.BQL.Fluent;
using PX.Objects.CM;

namespace PX.Objects.SO
{
    public abstract class SOOrderBase : PXBqlTable
    {
        public abstract class orderType : BqlString.Field<orderType> { }
        public virtual string OrderType { get; set; }
    }

    public class SOOrder : SOOrderBase, IBqlTable
    {
        public abstract class orderNbr : BqlString.Field<orderNbr> { }
        public virtual string OrderNbr { get; set; }
        public abstract class ghostField : BqlString.Field<ghostField> { }
        public virtual decimal? OrderTotal { get; set; }
        public static string StaticProperty { get; set; }
        protected virtual string ProtectedProperty { get; set; }
        public string PublicField;
    }

    public class SOLine : PXBqlTable, IBqlTable
    {
        public abstract class orderNbr : BqlString.Field<orderNbr> { }
        public virtual string OrderNbr { get; set; }
        public virtual int? LineNbr { get; set; }
    }

    public class SOSetup : PXBqlTable, IBqlTable
    {
        public virtual bool? RequireControlTotal { get; set; }
    }

    public class SOLinesOfCurrentOrder : PXSelect<SOLine, Where<Equal<Current<SOOrder.orderNbr>>>> { }

    public abstract class SOGraphBase : PXGraph<SOGraphBase>
    {
        public PXSelect<SOLine> BaseLines;
    }

    public class SOOrderEntry : PXGraph<SOOrderEntry, SOOrder>
    {
        public PXSelect<SOOrder> Document;
        public PXSelect<SOOrder, Where<Equal<Current<SOOrder.orderNbr>>>> CurrentDocument;
        public SelectFrom<SOLine>.Where<Equal<Current<SOOrder.orderNbr>>>.View Transactions;
        public SOLinesOfCurrentOrder Lines;
        public PXSelectJoin<SOLine, InnerJoin<SOOrder, On<Equal<Current<SOOrder.orderNbr>>>>, Where<Equal<Current<SOOrder.orderNbr>>>> JoinedLines;
        public PXSetup<SOSetup> sosetup;
        public PXFilter<AddLineFilter> addFilter;
        public PXAction<SOOrder> release;
        public static PXSelect<SOLine> StaticView;
        protected PXAction<SOOrder> protectedAction;
        public string NotAView;
        public PXSelect<SOLine>[] ArrayOfViews;

        public class AddLineFilter : PXBqlTable, IBqlTable
        {
            public virtual int? Qty { get; set; }
        }

        public class MultiCurrency : MultiCurrencyGraph<SOOrderEntry, SOOrder> { }
    }

    public class SOOrderEntryEx : SOOrderEntry
    {
        public PXAction<SOOrder> exAction;
    }

    public static class SOGraphs
    {
        public class Nested : PXGraph<Nested>
        {
            public PXSelect<SOSetup> Setup;
        }
    }
}

namespace PX.Objects.CM
{
    public class CurrencyInfo : PXBqlTable, IBqlTable
    {
        public virtual long? CuryInfoID { get; set; }
        public virtual string CuryID { get; set; }
    }

    public abstract class MultiCurrencyGraph<TGraph, TPrimary> : PXGraphExtension<TGraph>
        where TGraph : PXGraph
        where TPrimary : class, IBqlTable, new()
    {
        public PXSelect<CurrencyInfo> currencyinfo;
        public PXSelect<TPrimary> currencyDocument;
        public PXAction<TPrimary> currencyView;
    }
}

namespace PX.Objects.CS
{
    public class FeaturesSet : PXBqlTable, IBqlTable
    {
        public virtual bool? Multicurrency { get; set; }
        public virtual bool? Inventory { get; set; }
    }
}
";

            private const string Custom = @"
using PX.Data;
using PX.Data.BQL;
using PX.Objects.CS;
using PX.Objects.SO;

namespace Custom
{
    public sealed class SOOrderExt : PXCacheExtension<SOOrder>
    {
        public abstract class usrPriority : BqlString.Field<usrPriority> { }
        public string UsrPriority { get; set; }
    }

    public sealed class SOOrderExt2 : PXCacheExtension<SOOrderExt, SOOrder>
    {
        public string UsrSecond { get; set; }
    }

    public sealed class FeaturesSetExt : PXCacheExtension<FeaturesSet>
    {
        public bool? UsrCustomFeature { get; set; }
    }

    public class SOOrderEntry_Ext : PXGraphExtension<SOOrderEntry>
    {
        public PXSelect<SOOrder> UsrNotes;
        public PXAction<SOOrder> usrApprove;
    }

    public class SOOrderEntry_Ext2 : PXGraphExtension<SOOrderEntry_Ext, SOOrderEntry>
    {
        public PXAction<SOOrder> usrSecond;
    }

    public class SOOrderEntry_Ext3 : PXGraphExtension<SOOrderEntry_Ext2, SOOrderEntry_Ext, SOOrderEntry>
    {
        public PXAction<SOOrder> usrThird;
    }

    public class SOOrderEntryEx_Ext : PXGraphExtension<SOOrderEntryEx>
    {
        public PXAction<SOOrder> exOnly;
    }

    public abstract class AbstractExt : PXGraphExtension<SOOrderEntry>
    {
        public PXAction<SOOrder> abstractOnly;
    }
}
";

            private const string Middle = @"
using PX.Data;
using PX.Objects.SO;

namespace Middle
{
    public class MiddleGraph : PXGraph<MiddleGraph, SOOrder>
    {
        public PXAction<SOOrder> middleAction;
    }

    public class MiddleView : PXSelect<SOLine> { }

    public class MiddleHelper { }

    public class MiddleDac : PXBqlTable, IBqlTable
    {
        public virtual string MiddleField { get; set; }
    }
}
";

            private const string Orphan = @"
using PX.Data;
using PX.Objects.SO;

namespace Orphan
{
    public class OrphanGraph : Middle.MiddleGraph
    {
        public PXSelect<SOLine> Lines;
    }

    public class OrphanHelper : Middle.MiddleHelper
    {
        public string Text;
    }

    public class DerivedDac : Middle.MiddleDac
    {
        public virtual string Own { get; set; }
    }

    public class PartialGraph : PXGraph<PartialGraph>
    {
        public Middle.MiddleView MiddleLines;
        public PXSelect<SOOrder> Orders;
        public PXSelect<DerivedDac> Derived;
    }
}
";

            private SiteMetadata? _metadata;

            public FakeSite()
            {
                Root = Path.Combine(Path.GetTempPath(), "muilint-site-" + Guid.NewGuid().ToString("N"));
                Bin = Path.Combine(Root, "Bin");
                DataOnlyBin = Path.Combine(Root, "DataOnly");
                JunkBin = Path.Combine(Root, "Junk");
                string refs = Path.Combine(Root, "Refs");
                foreach (string folder in new[] { Bin, DataOnlyBin, JunkBin, refs })
                {
                    Directory.CreateDirectory(folder);
                }

                var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
                    .Split(Path.PathSeparator)
                    .Where(path => path.Length > 0)
                    .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
                    .ToList();

                references.Add(Compile("PX.Data", PXData, references, Bin, DataOnlyBin));
                references.Add(Compile("PX.Objects", PXObjects, references, Bin));
                references.Add(Compile("Custom", Custom, references, Bin));
                references.Add(Compile("Middle", Middle, references, refs));
                Compile("Orphan", Orphan, references, Bin);

                Directory.CreateDirectory(Path.Combine(Root, "App_RuntimeCode", "Nested"));
                File.WriteAllText(
                    Path.Combine(Root, "App_RuntimeCode", "Nested", "SOOrderExt.cs"),
                    "public class SOOrderExt : PXCacheExtension<SOOrder> { public string UsrRuntimeOnly { get; set; } }");

                foreach (string folder in new[] { Bin, JunkBin })
                {
                    File.WriteAllText(Path.Combine(folder, "x.dll"), "not an assembly");
                    File.WriteAllBytes(Path.Combine(folder, "empty.dll"), Array.Empty<byte>());
                }
            }

            public string Root { get; }

            public string Bin { get; }

            public string DataOnlyBin { get; }

            public string JunkBin { get; }

            public SiteMetadata Metadata
            {
                get { return _metadata ??= SiteMetadataReader.Read(Bin) ?? throw new InvalidOperationException("Nothing read."); }
            }

            public void Dispose()
            {
                try
                {
                    Directory.Delete(Root, recursive: true);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

            private static MetadataReference Compile(string name, string source, IEnumerable<MetadataReference> references, params string[] folders)
            {
                CSharpCompilation compilation = CSharpCompilation.Create(
                    name,
                    new[] { CSharpSyntaxTree.ParseText(source) },
                    references,
                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

                using var image = new MemoryStream();
                Microsoft.CodeAnalysis.Emit.EmitResult result = compilation.Emit(image);
                if (!result.Success)
                {
                    throw new InvalidOperationException(
                        name + " didn't compile:\n" + string.Join("\n", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
                }

                byte[] bytes = image.ToArray();
                foreach (string folder in folders)
                {
                    File.WriteAllBytes(Path.Combine(folder, name + ".dll"), bytes);
                }

                return MetadataReference.CreateFromImage(bytes);
            }
        }
    }
}
