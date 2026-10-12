using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace AISI.MuiLint
{
    public static partial class TypeScriptScanner
    {
        private static readonly Regex GraphTypeLiteral = new Regex(
            "@graphInfo\\s*\\(\\s*\\{[^)]*?\\bgraphType\\s*:\\s*(['\"`])(?<v>[^'\"`]+)\\1",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex LinkCommand = new Regex(
            "@linkCommand\\s*\\(\\s*(['\"`])(?<v>[^'\"`]+)\\1",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex FeatureInstalled = new Regex(
            "@featureInstalled\\s*\\(\\s*(['\"`])(?<v>[^'\"`]+)\\1",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        // Added at run time, so no Bin declares them: PXProcessing's actions and PXNote's fields.
        private static readonly HashSet<string> RuntimeNames = new HashSet<string>(
            new[] { "Process", "ProcessAll", "NoteText", "NoteFiles", "NotePopupText" },
            StringComparer.OrdinalIgnoreCase);

        // Checks against what the site's Bin says. Each one stays quiet where the Bin couldn't say.
        private static void ScanSite(string path, TsModule module, ScreenModel screen, SiteMetadata site, LineMap lineMap, List<Diagnostic> results)
        {
            foreach (Match feature in FeatureInstalled.Matches(module.CodeWithStrings))
            {
                Group value = feature.Groups["v"];
                if (site.Features != null && !site.Features.Contains(value.Value) && !Declared(site, Short(value.Value)))
                {
                    string message = string.Format(
                        CultureInfo.InvariantCulture,
                        "'{0}' isn't a feature in this site's FeaturesSet, so the class is never switched on.",
                        value.Value);
                    results.Add(HtmlMergeScanner.Create(DiagnosticIds.FeatureNotInSite, message, path, value.Index, value.Length, lineMap));
                }
            }

            GraphMetadata? graph = screen.GraphType == null ? null : site.FindGraph(screen.GraphType);
            if (graph == null)
            {
                Match declared = GraphTypeLiteral.Match(module.CodeWithStrings);
                if (declared.Success && screen.GraphType != null && site.Graphs.Count > 0 && !Declared(site, Short(screen.GraphType)))
                {
                    Group value = declared.Groups["v"];
                    string message = string.Format(
                        CultureInfo.InvariantCulture,
                        "No assembly in this site's Bin defines the graph '{0}'. Check the spelling, or build the assembly into Bin.",
                        value.Value);
                    results.Add(HtmlMergeScanner.Create(DiagnosticIds.GraphNotInSite, message, path, value.Index, value.Length, lineMap));
                }

                return;
            }

            if (graph.Complete)
            {
                ScanMembers(path, module, screen, site, graph, lineMap, results);
                foreach (Match link in LinkCommand.Matches(module.CodeWithStrings))
                {
                    Group value = link.Groups["v"];
                    if (!graph.Actions.Contains(value.Value) && !Declared(site, value.Value))
                    {
                        string message = string.Format(
                            CultureInfo.InvariantCulture,
                            "{0} has no action '{1}', so the link does nothing.",
                            Short(graph.FullName),
                            value.Value);
                        results.Add(HtmlMergeScanner.Create(DiagnosticIds.LinkCommandUnknownAction, message, path, value.Index, value.Length, lineMap));
                    }
                }
            }

            ScanFields(path, module, screen, site, graph, lineMap, results);
        }

        // The screen's views and actions declared in this file, against the graph's.
        private static void ScanMembers(string path, TsModule module, ScreenModel screen, SiteMetadata site, GraphMetadata graph, LineMap lineMap, List<Diagnostic> results)
        {
            foreach (TsClass c in module.Classes)
            {
                if (!screen.IsPartOf(c.Name, screen.ScreenClass))
                {
                    continue;
                }

                foreach (KeyValuePair<string, TsMember> member in c.Members)
                {
                    bool view = member.Value.ViewClass != null;
                    bool action = member.Value.Type == "PXActionState";
                    if ((!view && !action) || member.Key.StartsWith("_", StringComparison.Ordinal)
                        || (view && graph.Views.ContainsKey(member.Key)) || (action && graph.Actions.Contains(member.Key)) || Declared(site, member.Key))
                    {
                        continue;
                    }

                    string message = string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} has no {1} called '{2}', so this binds to nothing.",
                        Short(graph.FullName),
                        view ? "view" : "action",
                        member.Key);
                    results.Add(HtmlMergeScanner.Create(DiagnosticIds.MemberNotInGraph, message, path, member.Value.Offset, member.Key.Length, lineMap));
                }
            }
        }

        // A view class's fields declared in this file, against the DACs of the views made from it.
        private static void ScanFields(string path, TsModule module, ScreenModel screen, SiteMetadata site, GraphMetadata graph, LineMap lineMap, List<Diagnostic> results)
        {
            foreach (TsClass c in module.Classes)
            {
                var dacs = new List<ViewMetadata>();
                bool unknown = false;
                foreach (string view in screen.Views)
                {
                    string? viewClass = screen.ClassOf(view);
                    if (viewClass == null || !screen.IsPartOf(c.Name, viewClass))
                    {
                        continue;
                    }

                    // A view the graph doesn't have is reported on its own; its fields can't be judged.
                    if (!graph.Views.TryGetValue(view, out ViewMetadata? backend) || backend.Fields == null)
                    {
                        unknown = true;
                        break;
                    }

                    dacs.Add(backend);
                }

                if (unknown || dacs.Count == 0)
                {
                    continue;
                }

                foreach (KeyValuePair<string, TsMember> member in c.Members)
                {
                    // Customer__AcctName: a field of a joined DAC, which the view's own DAC doesn't list.
                    if (member.Value.Type != "PXFieldState" || member.Key.Contains("__") || dacs.Exists(d => d.Fields!.Contains(member.Key)) || Declared(site, member.Key))
                    {
                        continue;
                    }

                    string message = string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} has no field '{1}', so this binds to nothing.",
                        Short(dacs[0].Dac ?? dacs[0].Name),
                        member.Key);
                    results.Add(HtmlMergeScanner.Create(DiagnosticIds.FieldNotInView, message, path, member.Value.Offset, member.Key.Length, lineMap));
                }
            }
        }

        private static bool Declared(SiteMetadata site, string name)
        {
            return RuntimeNames.Contains(name) || site.SourceNames.Contains(name);
        }

        private static string Short(string fullName)
        {
            return fullName.Substring(Math.Max(fullName.LastIndexOf('.'), fullName.LastIndexOf('+')) + 1);
        }
    }
}
