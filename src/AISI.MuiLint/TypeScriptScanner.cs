using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace AISI.MuiLint
{
    /// <summary>
    /// Checks a Modern UI .ts file: one under a <c>screens</c> or <c>customizationScreens</c> folder
    /// that isn't a stock screen.
    /// </summary>
    public static partial class TypeScriptScanner
    {
        private static readonly Regex SuppressionComment = new Regex(
            "//[ \\t]*muilint-disable(?<next>-next-line)?(?<ids>[^\\r\\n]*)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex PrimaryView = new Regex(
            "\\bprimaryView\\s*:\\s*(['\"])(?<v>[^'\"\\r\\n]*)\\1",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex HandleEvent = new Regex(
            "@handleEvent\\s*\\(",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex ConfigDecorator = new Regex(
            "@(?<name>graphInfo|gridConfig)\\s*\\(",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex ViewArgument = new Regex(
            "\\bview\\s*:\\s*(['\"])(?<v>[^'\"\\r\\n]*)\\1",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        /// <summary>Scans <paramref name="text"/> as the .ts at <paramref name="path"/>.</summary>
        /// <param name="path">File path, for the path checks and for reporting.</param>
        /// <param name="text">The TypeScript.</param>
        /// <param name="readFile">
        /// Returns another file's text, or null when it does not exist. Needed to follow imports
        /// and to read .editorconfig; null limits the scan to this file.
        /// </param>
        /// <param name="listFolder">Lists what's directly inside a folder, so every extension of the screen is read too. Optional.</param>
        /// <param name="site">What the site's Bin says about its graphs and features, for the checks against the backend. Optional.</param>
        /// <returns>Zero or more findings, in source order.</returns>
        public static IReadOnlyList<Diagnostic> Analyze(
            string path,
            string text,
            Func<string, string?>? readFile,
            Func<string, IEnumerable<string>>? listFolder = null,
            SiteMetadata? site = null)
        {
            if (path is null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (text is null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            // Only Modern UI screens: VS hands us every .ts in every project, and stock screens are what they are.
            var results = new List<Diagnostic>();
            string normalized = HtmlMergeScanner.NormalizePath(path);
            bool modernUi = normalized.IndexOf("/screens/", StringComparison.OrdinalIgnoreCase) >= 0
                || normalized.IndexOf("/customizationScreens/", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!modernUi || HtmlMergeScanner.IsStockScreensPath(path))
            {
                return results;
            }

            var lineMap = new LineMap(text);
            var module = new TsModule(path, text);
            if (HtmlMergeScanner.IsExtensionFile(path))
            {
                Scan0014(path, module, lineMap, results);
            }

            HtmlMergeScanner.TryPath0017(path, lineMap, results);

            ScanDecoratorConfigs(path, module, lineMap, results);

            ScreenModel? screen = readFile == null ? null : ScreenModel.Read(path, readFile, text, listFolder);
            if (screen != null)
            {
                Scan0015(path, module, screen, lineMap, results);
                Scan0016(path, module, screen, lineMap, results);
                if (site != null)
                {
                    ScanSite(path, module, screen, site, lineMap, results);
                }
            }

            HtmlMergeScanner.RemoveSuppressed(text, SuppressionComment, lineMap, results);
            if (readFile != null)
            {
                HtmlMergeScanner.ApplyConfiguredSeverities(path, readFile, results);
            }

            results.Sort(HtmlMergeScanner.CompareDiagnostics);
            return results;
        }

        private static void Scan0014(string path, TsModule module, LineMap lineMap, List<Diagnostic> results)
        {
            var interfaces = new HashSet<string>(StringComparer.Ordinal);
            foreach (TsInterface i in module.Interfaces)
            {
                interfaces.Add(i.Name);

                // An interface with a body is a type of its own (a control config, say), not half an extension.
                if (i.Empty && module.FindClass(i.Name) == null)
                {
                    string message = string.Format(
                        CultureInfo.InvariantCulture,
                        "interface {0} has no class {0} beside it, so nothing is added to {1}. Put the members in 'export class {0} {{ }}'.",
                        i.Name,
                        string.Join(", ", i.Bases));
                    results.Add(HtmlMergeScanner.Create(DiagnosticIds.HalfAnExtension, message, path, i.NameStart, i.Name.Length, lineMap));
                }
            }

            foreach (TsClass c in module.Classes)
            {
                // A class with no base and no interface is fine as a helper; it's only half an
                // extension when it declares the things an extension adds.
                if (c.Base.Length > 0 || interfaces.Contains(c.Name) || !DeclaresModernUiMembers(c))
                {
                    continue;
                }

                string message = string.Format(
                    CultureInfo.InvariantCulture,
                    "class {0} extends nothing and no interface {0} merges it anywhere, so its members never reach a view or the screen. Add 'export interface {0} extends <the class you are extending> {{}}'.",
                    c.Name);
                results.Add(HtmlMergeScanner.Create(DiagnosticIds.HalfAnExtension, message, path, c.NameStart, c.Name.Length, lineMap));
            }
        }

        private static bool DeclaresModernUiMembers(TsClass c)
        {
            foreach (TsMember member in c.Members.Values)
            {
                if (member.ViewClass != null || member.Type == "PXFieldState" || member.Type == "PXActionState")
                {
                    return true;
                }
            }

            return false;
        }

        private static void Scan0015(string path, TsModule module, ScreenModel screen, LineMap lineMap, List<Diagnostic> results)
        {
            foreach (Match match in PrimaryView.Matches(module.CodeWithStrings))
            {
                CheckView(path, match.Groups["v"], "primaryView", screen, lineMap, results);
            }

            foreach (Match handler in HandleEvent.Matches(module.CodeWithStrings))
            {
                int open = handler.Index + handler.Length - 1;
                int close = MatchingParen(module.Code, open);
                if (close < 0)
                {
                    continue;
                }

                Match view = ViewArgument.Match(module.CodeWithStrings, open, close - open);
                if (view.Success)
                {
                    CheckView(path, view.Groups["v"], "@handleEvent", screen, lineMap, results);
                }
            }
        }

        // @graphInfo without graphType, and @gridConfig without preset.
        private static void ScanDecoratorConfigs(string path, TsModule module, LineMap lineMap, List<Diagnostic> results)
        {
            foreach (Match decorator in ConfigDecorator.Matches(module.Code))
            {
                int open = decorator.Index + decorator.Length - 1;
                int close = MatchingParen(module.Code, open);
                if (close < 0)
                {
                    continue;
                }

                string args = module.Code.Substring(open + 1, close - open - 1).Trim();
                Group name = decorator.Groups["name"];
                if (name.Value == "graphInfo" && args.StartsWith("{", StringComparison.Ordinal) && !Regex.IsMatch(args, "\\bgraphType\\s*:"))
                {
                    results.Add(HtmlMergeScanner.Create(
                        DiagnosticIds.GraphInfoWithoutGraphType,
                        "@graphInfo has no graphType, so the screen has no graph to talk to.",
                        path,
                        name.Index - 1,
                        name.Length + 1,
                        lineMap));
                }
                else if (name.Value == "gridConfig" && (args.Length == 0 || args.StartsWith("{", StringComparison.Ordinal)) && !Regex.IsMatch(args, "\\bpreset\\s*:"))
                {
                    results.Add(HtmlMergeScanner.Create(
                        DiagnosticIds.GridWithoutPreset,
                        "@gridConfig has no preset. Acumatica's docs ask for one on every grid: GridPreset.Primary, Inquiry, Processing, Details and so on.",
                        path,
                        name.Index - 1,
                        name.Length + 1,
                        lineMap));
                }
            }
        }

        private static void CheckView(string path, Group value, string where, ScreenModel screen, LineMap lineMap, List<Diagnostic> results)
        {
            // The backend matches view names case-insensitively: CA506500 has primaryView 'filter' for its Filter view.
            if (!HtmlMergeScanner.IsIdentifier(value.Value) || screen.TryGetMember(value.Value, out _) || screen.HasMemberIgnoringCase(value.Value))
            {
                return;
            }

            string message = string.Format(
                CultureInfo.InvariantCulture,
                "{0} names '{1}', but {2} has no view called '{1}'.",
                where,
                value.Value,
                screen.ScreenClass);
            results.Add(HtmlMergeScanner.Create(DiagnosticIds.DecoratorViewNotDeclared, message, path, value.Index, value.Length, lineMap));
        }

        private static void Scan0016(string path, TsModule module, ScreenModel screen, LineMap lineMap, List<Diagnostic> results)
        {
            foreach (TsClass c in module.Classes)
            {
                foreach (KeyValuePair<string, TsMember> member in c.Members)
                {
                    string? viewClass = member.Value.ViewClass;
                    if (viewClass == null || screen.IsView(viewClass) != false)
                    {
                        continue;
                    }

                    string message = string.Format(
                        CultureInfo.InvariantCulture,
                        "View '{0}' is created from {1}, which does not extend PXView. Acumatica's docs give createSingle and createCollection a view class that extends PXView, not an extension of one.",
                        member.Key,
                        viewClass);
                    results.Add(HtmlMergeScanner.Create(DiagnosticIds.ViewFromNonView, message, path, member.Value.Offset, member.Key.Length, lineMap));
                }
            }
        }

        private static int MatchingParen(string code, int open)
        {
            int depth = 0;
            for (int i = open; i < code.Length; i++)
            {
                if (code[i] == '(')
                {
                    depth++;
                }
                else if (code[i] == ')' && --depth == 0)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
