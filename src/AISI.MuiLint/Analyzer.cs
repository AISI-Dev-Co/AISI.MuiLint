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
    /// and `dotnet` surface the HTML merge diagnostics on C# projects that list HTML as
    /// additional files. Analyzers may not touch the disk, so the rules that read neighbouring
    /// files (AISI0009, AISI0011) only run in the CLI and the VSIX; Roslyn applies
    /// .editorconfig severities itself.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class Analyzer : DiagnosticAnalyzer
    {
        private static readonly ImmutableArray<DiagnosticDescriptor> Supported = Describe();

        /// <inheritdoc/>
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => Supported;

        /// <summary>
        /// Scans one HTML file's text on its own, as the analyzer does. No compilation needed.
        /// </summary>
        /// <param name="path">File path (for AISI0003/AISI0004 and reporting).</param>
        /// <param name="text">Raw HTML.</param>
        /// <returns>Findings in source order.</returns>
        public static IReadOnlyList<Diagnostic> Scan(string path, string text)
        {
            return HtmlMergeScanner.Analyze(path, text);
        }

        /// <inheritdoc/>
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
            for (int i = 0; i < Supported.Length; i++)
            {
                if (string.Equals(Supported[i].Id, id, StringComparison.Ordinal))
                {
                    return Supported[i];
                }
            }

            return null;
        }

        private static ImmutableArray<DiagnosticDescriptor> Describe()
        {
            ImmutableArray<DiagnosticDescriptor>.Builder descriptors = ImmutableArray.CreateBuilder<DiagnosticDescriptor>(Rules.All.Count);
            foreach (Rule rule in Rules.All)
            {
                descriptors.Add(new DiagnosticDescriptor(
                    rule.Id,
                    rule.Title,
                    "{0}",
                    "HTML Merge",
                    ToRoslyn(rule.DefaultSeverity),
                    isEnabledByDefault: true,
                    description: rule.Description,
                    helpLinkUri: rule.HelpUri));
            }

            return descriptors.MoveToImmutable();
        }

        private static DiagnosticSeverity ToRoslyn(Severity severity)
        {
            switch (severity)
            {
                case Severity.Warning:
                    return DiagnosticSeverity.Warning;
                case Severity.Suggestion:
                    return DiagnosticSeverity.Info;
                default:
                    return DiagnosticSeverity.Error;
            }
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
