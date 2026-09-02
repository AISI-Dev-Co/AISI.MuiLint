using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using RoslynDiagnostic = Microsoft.CodeAnalysis.Diagnostic;

namespace AISI.MuiLint
{
    /// <summary>
    /// Roslyn additional-file analyzer that runs <see cref="HtmlMergeScanner"/> on <c>.html</c>
    /// files. The scanner itself does not need a compilation; this wrapper is how Visual Studio
    /// and `dotnet` surface the five HTML merge diagnostics on C# projects that list HTML as
    /// additional files.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class Analyzer : DiagnosticAnalyzer
    {
        private static readonly DiagnosticDescriptor SelfClosing = new DiagnosticDescriptor(
            DiagnosticIds.SelfClosing,
            "Self-closing Modern UI tag",
            "{0}",
            "HTML Merge",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Acumatica Modern UI merge does not treat self-closing <field> or <qp-*> tags as a full start/end pair. Use explicit end tags.");

        private static readonly DiagnosticDescriptor AfterBeforeSameFile = new DiagnosticDescriptor(
            DiagnosticIds.AfterBeforeSameFile,
            "after/before name selector defined in this file",
            "{0}",
            "HTML Merge",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "HTML merge only sees stock HTML. An after/before [name='X'] selector cannot target a name introduced in the same extension file.");

        private static readonly DiagnosticDescriptor StockScreensPath = new DiagnosticDescriptor(
            DiagnosticIds.StockScreensPath,
            "Stock src/screens path",
            "{0}",
            "HTML Merge",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Custom and customized Modern UI source belongs in development/screens or customizationScreens, not the stock src/screens tree.");

        private static readonly DiagnosticDescriptor ExtensionBasename = new DiagnosticDescriptor(
            DiagnosticIds.ExtensionBasename,
            "Extension named as the parent screen",
            "{0}",
            "HTML Merge",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "An extensions/*.html file must not use the parent screen folder as its basename (SO301000/extensions/SO301000.html). Add a postfix.");

        private static readonly DiagnosticDescriptor EmptyFieldset = new DiagnosticDescriptor(
            DiagnosticIds.EmptyFieldset,
            "Empty qp-fieldset",
            "{0}",
            "HTML Merge",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "A qp-fieldset whose body is only whitespace or comments will merge as an empty fieldset. Merge-operation fieldsets (modify/remove/replace) are allowed to be empty.");

        private static readonly ImmutableArray<DiagnosticDescriptor> Supported = ImmutableArray.Create(
            SelfClosing,
            AfterBeforeSameFile,
            StockScreensPath,
            ExtensionBasename,
            EmptyFieldset);

        /// <inheritdoc />
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => Supported;

        /// <summary>
        /// Independent raw-HTML scan. Used by the CLI, the VSIX package stub, and tests.
        /// Does not require a Roslyn compilation.
        /// </summary>
        /// <param name="path">File path (for AISI0003/AISI0004 and reporting).</param>
        /// <param name="text">Raw HTML.</param>
        /// <returns>Findings in source order.</returns>
        public static IReadOnlyList<Diagnostic> Scan(string path, string text)
        {
            return HtmlMergeScanner.Analyze(path, text);
        }

        /// <inheritdoc />
        public override void Initialize(AnalysisContext context)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationAction(AnalyzeCompilation);
        }

        private static void AnalyzeCompilation(CompilationAnalysisContext context)
        {
            ImmutableArray<AdditionalText> files = context.Options.AdditionalFiles;
            for (int i = 0; i < files.Length; i++)
            {
                AdditionalText file = files[i];
                if (file.Path is null || !IsHtml(file.Path))
                {
                    continue;
                }

                SourceText? source = file.GetText(context.CancellationToken);
                if (source is null)
                {
                    continue;
                }

                IReadOnlyList<Diagnostic> findings = Scan(file.Path, source.ToString());
                for (int f = 0; f < findings.Count; f++)
                {
                    Diagnostic finding = findings[f];
                    DiagnosticDescriptor? descriptor = DescriptorFor(finding.Id);
                    if (descriptor is null)
                    {
                        continue;
                    }

                    context.ReportDiagnostic(ToRoslyn(finding, descriptor));
                }
            }
        }

        private static bool IsHtml(string path)
        {
            return path.EndsWith(".html", StringComparison.OrdinalIgnoreCase);
        }

        private static DiagnosticDescriptor? DescriptorFor(string id)
        {
            if (string.Equals(id, DiagnosticIds.SelfClosing, StringComparison.Ordinal))
            {
                return SelfClosing;
            }

            if (string.Equals(id, DiagnosticIds.AfterBeforeSameFile, StringComparison.Ordinal))
            {
                return AfterBeforeSameFile;
            }

            if (string.Equals(id, DiagnosticIds.StockScreensPath, StringComparison.Ordinal))
            {
                return StockScreensPath;
            }

            if (string.Equals(id, DiagnosticIds.ExtensionBasename, StringComparison.Ordinal))
            {
                return ExtensionBasename;
            }

            if (string.Equals(id, DiagnosticIds.EmptyFieldset, StringComparison.Ordinal))
            {
                return EmptyFieldset;
            }

            return null;
        }

        private static RoslynDiagnostic ToRoslyn(Diagnostic finding, DiagnosticDescriptor descriptor)
        {
            int start = finding.Start;
            int length = finding.Length;
            if (start < 0)
            {
                start = 0;
            }

            if (length < 0)
            {
                length = 0;
            }

            var span = new TextSpan(start, length);
            var lineSpan = new LinePositionSpan(
                new LinePosition(Math.Max(0, finding.Line - 1), Math.Max(0, finding.Column - 1)),
                new LinePosition(Math.Max(0, finding.EndLine - 1), Math.Max(0, finding.EndColumn - 1)));
            Location location = Location.Create(finding.Path, span, lineSpan);
            return RoslynDiagnostic.Create(descriptor, location, finding.Message);
        }
    }
}
