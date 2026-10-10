using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace AISI.MuiLint
{
    public static partial class HtmlMergeScanner
    {
        // The selector shapes extensions actually use: [name='X'], #id, #id [name='X'].
        private static readonly Regex SimpleSelector = new Regex(
            "^\\s*(?:#(?<id>[A-Za-z_][\\w-]*))?\\s*(?:\\[name\\s*=\\s*(['\"]?)(?<n>[^'\"\\]]+)\\1\\s*\\])?\\s*$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        internal static readonly Regex IdSelector = new Regex(
            "#(?<id>[A-Za-z_][A-Za-z0-9_-]*)",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        /// <summary>
        /// Where the stock screen for an extension lives. For
        /// <c>src/development/screens/SO/SO301000/extensions/SO301000_Custom.html</c> (or the same
        /// under <c>customizationScreens/&lt;Project&gt;</c>) that is
        /// <c>src/screens/SO/SO301000/SO301000.html</c>.
        /// </summary>
        /// <returns>The stock path with forward slashes, or null if the path is not an extension.</returns>
        public static string? StockHtmlPath(string extensionHtmlPath)
        {
            if (extensionHtmlPath is null)
            {
                throw new ArgumentNullException(nameof(extensionHtmlPath));
            }

            if (!IsExtensionFile(extensionHtmlPath) || !TryLocateScreen(extensionHtmlPath, out string src, out string module, out string screen))
            {
                return null;
            }

            return src + "screens/" + module + "/" + screen + "/" + screen + ".html";
        }

        /// <summary>
        /// Every extension of the screen <paramref name="path"/> belongs to, wherever it lives: the
        /// stock screen's own extensions folder, development/screens, and each project under
        /// customizationScreens. Nothing imports these; the build stitches them all in.
        /// </summary>
        /// <param name="path">Any file of the screen, or of one of its extensions.</param>
        /// <param name="fileExtension">".ts" or ".html".</param>
        /// <param name="listFolder">Lists what's directly inside a folder, files and subfolders. Null finds nothing.</param>
        internal static IEnumerable<string> ScreenExtensions(string path, string fileExtension, Func<string, IEnumerable<string>>? listFolder)
        {
            if (listFolder == null || !TryLocateScreen(path, out string src, out string module, out string screen))
            {
                yield break;
            }

            string tail = module + "/" + screen + "/extensions";
            var folders = new List<string> { src + "screens/" + tail, src + "development/screens/" + tail };
            foreach (string project in listFolder(src + "customizationScreens"))
            {
                string projectPath = NormalizePath(project);
                folders.Add(projectPath + "/" + tail);
                folders.Add(projectPath + "/screens/" + tail);
            }

            foreach (string folder in folders)
            {
                foreach (string file in listFolder(folder))
                {
                    string filePath = NormalizePath(file);
                    if (filePath.EndsWith(fileExtension, StringComparison.OrdinalIgnoreCase) && !filePath.EndsWith(".d.ts", StringComparison.OrdinalIgnoreCase))
                    {
                        yield return filePath;
                    }
                }
            }
        }

        /// <summary>
        /// Finds the screen a file belongs to: the folder that holds the screens trees (usually
        /// <c>…/src/</c>, with a trailing slash), the module and the screen id. Works for the stock
        /// screen, a development screen, a customizationScreens project, and their extensions.
        /// </summary>
        internal static bool TryLocateScreen(string path, out string src, out string module, out string screen)
        {
            src = module = screen = string.Empty;
            string[] parts = NormalizePath(path).Split('/');
            int screenIndex = IsExtensionFile(path) ? parts.Length - 3 : parts.Length - 2;
            int moduleIndex = screenIndex - 1;
            if (moduleIndex < 1)
            {
                return false;
            }

            // Walk back from the module folder to the start of the screens tree.
            int root = -1;
            for (int i = moduleIndex - 1; i >= 0 && i >= moduleIndex - 3; i--)
            {
                if (string.Equals(parts[i], "development", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(parts[i], "customizationScreens", StringComparison.OrdinalIgnoreCase))
                {
                    root = i;
                    break;
                }
            }

            if (root < 0 && string.Equals(parts[moduleIndex - 1], "screens", StringComparison.OrdinalIgnoreCase))
            {
                root = moduleIndex - 1;
            }

            if (root < 0)
            {
                return false;
            }

            src = root == 0 ? string.Empty : string.Join("/", parts, 0, root) + "/";
            module = parts[moduleIndex];
            screen = parts[screenIndex];
            return true;
        }

        internal static bool IsExtensionFile(string path)
        {
            string directory = Path.GetFileName(Path.GetDirectoryName(NormalizePath(path)) ?? string.Empty);
            return string.Equals(directory, "extensions", StringComparison.OrdinalIgnoreCase);
        }

        private static void Scan0009(
            string path,
            IReadOnlyList<HtmlTag> tags,
            StockScreen stock,
            LineMap lineMap,
            List<Diagnostic> results)
        {
            // Anchoring on something this file adds above is fine: the merge applies elements in order.
            var localNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var localIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < tags.Count; i++)
            {
                HtmlTag tag = tags[i];
                if (tag.IsEndTag)
                {
                    continue;
                }

                for (int a = 0; a < tag.Attributes.Count; a++)
                {
                    HtmlAttribute attr = tag.Attributes[a];
                    if (!IsMergeOperator(attr.Name) || FindBracketProblem(attr.Value) != null)
                    {
                        continue;
                    }

                    foreach (Match match in NameSelector.Matches(attr.Value))
                    {
                        string name = match.Groups["n"].Value;
                        if (name.Length > 0 && !localNames.Contains(name) && !stock.Names.ContainsKey(name))
                        {
                            results.Add(Create(
                                DiagnosticIds.SelectorNotInStock,
                                NotInStockMessage("[name='" + name + "']", stock),
                                path,
                                attr.ValueStart + match.Index,
                                match.Length,
                                lineMap));
                        }
                    }

                    foreach (Match match in IdSelector.Matches(BlankBracketsAndQuotes(attr.Value)))
                    {
                        string id = match.Groups["id"].Value;
                        if (!localIds.Contains(id) && !stock.Ids.ContainsKey(id))
                        {
                            results.Add(Create(
                                DiagnosticIds.SelectorNotInStock,
                                NotInStockMessage("#" + id, stock),
                                path,
                                attr.ValueStart + match.Index,
                                match.Length,
                                lineMap));
                        }
                    }

                    CountMatches(path, attr, stock, localNames, lineMap, results);
                }

                localNames.Add(tag.GetAttribute("name"));
                localIds.Add(tag.GetAttribute("id"));
            }
        }

        // A selector of a shape we can count: more than one stock match fails the build, and so does
        // #id [name='X'] when X is in the stock screen but not inside #id.
        private static void CountMatches(string path, HtmlAttribute attr, StockScreen stock, HashSet<string> localNames, LineMap lineMap, List<Diagnostic> results)
        {
            Match m = SimpleSelector.Match(attr.Value);
            string id = m.Groups["id"].Value;
            string name = m.Groups["n"].Value;
            if (!m.Success || (id.Length == 0 && name.Length == 0))
            {
                return;
            }

            string selector = (id.Length > 0 ? "#" + id : string.Empty) + (id.Length > 0 && name.Length > 0 ? " " : string.Empty) + (name.Length > 0 ? "[name='" + name + "']" : string.Empty);
            stock.StockMatches.TryGetValue(selector, out int count);
            if (count > 1)
            {
                string message = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} matches {1} elements in the stock {2}, and the Modern UI build fails on a selector that matches more than one.{3}",
                    selector,
                    count,
                    stock.FileName,
                    id.Length == 0 ? " Qualify it with the #id of the container you mean." : string.Empty);
                results.Add(Create(DiagnosticIds.SelectorMatchesSeveral, message, path, attr.ValueStart, attr.Value.Length, lineMap));
            }
            else if (count == 0 && id.Length > 0 && name.Length > 0 && stock.Ids.ContainsKey(id) && stock.StockMatches.ContainsKey("[name='" + name + "']")
                && !stock.ExtensionNames.Contains(name) && !localNames.Contains(name))
            {
                string message = string.Format(
                    CultureInfo.InvariantCulture,
                    "[name='{0}'] is in the stock {1}, but not inside #{2}, so the selector matches nothing. The Modern UI build fails on a selector that matches nothing.",
                    name,
                    stock.FileName,
                    id);
                results.Add(Create(DiagnosticIds.SelectorNotInStock, message, path, attr.ValueStart, attr.Value.Length, lineMap));
            }
        }

        private static string NotInStockMessage(string target, StockScreen stock)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} is not in the stock {1}, any extension of it, or above in this file. The Modern UI build fails on a selector that matches nothing.",
                target,
                stock.FileName);
        }

        internal static string BlankBracketsAndQuotes(string selector)
        {
            char[] chars = selector.ToCharArray();
            int depth = 0;
            char quote = '\0';
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (quote != '\0')
                {
                    quote = c == quote ? '\0' : quote;
                    chars[i] = ' ';
                }
                else if (c == '\'' || c == '"')
                {
                    quote = c;
                    chars[i] = ' ';
                }
                else if (c == '[')
                {
                    depth++;
                }
                else if (c == ']')
                {
                    depth--;
                }
                else if (depth > 0)
                {
                    chars[i] = ' ';
                }
            }

            return new string(chars);
        }

        /// <summary>Names and ids of a stock screen, including anything it pulls in with qp-include.</summary>
        internal sealed class StockScreen
        {
            private bool _complete = true;

            private StockScreen(string fileName)
            {
                FileName = fileName;
            }

            public string FileName { get; }

            /// <summary>Each name, and where it first appears.</summary>
            public Dictionary<string, SourceLocation> Names { get; } = new Dictionary<string, SourceLocation>(StringComparer.OrdinalIgnoreCase);

            /// <summary>Each id, and where it first appears.</summary>
            public Dictionary<string, SourceLocation> Ids { get; } = new Dictionary<string, SourceLocation>(StringComparer.OrdinalIgnoreCase);

            /// <summary>For each id, the names inside that element (includes followed).</summary>
            public Dictionary<string, List<string>> NamesUnder { get; } = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            /// <summary>How many elements of the stock screen itself (not its extensions) match each simple selector.</summary>
            public Dictionary<string, int> StockMatches { get; } = new Dictionary<string, int>(StringComparer.Ordinal);

            /// <summary>Names the other extensions add, wherever they put them.</summary>
            public HashSet<string> ExtensionNames { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            /// <summary>Tag → attribute → the values the stock screen uses for it, first seen first.</summary>
            public Dictionary<string, Dictionary<string, List<string>>> Attributes { get; } =
                new Dictionary<string, Dictionary<string, List<string>>>(StringComparer.OrdinalIgnoreCase);

            /// <summary>
            /// Reads the stock screen. Null when there is none, or when an include can't be followed and
            /// <paramref name="allowPartial"/> is false: a half-known screen only produces false alarms,
            /// though it is still good enough to complete from.
            /// </summary>
            public static StockScreen? Read(
                string extensionPath,
                Func<string, string?> readFile,
                bool allowPartial = false,
                Func<string, IEnumerable<string>>? listFolder = null)
            {
                string? stockPath = StockHtmlPath(extensionPath);
                string? html = stockPath == null ? null : readFile(stockPath);
                if (stockPath == null || html == null)
                {
                    return null;
                }

                var stock = new StockScreen(Path.GetFileName(stockPath));
                stock.Collect(stockPath, html, readFile, 0, Array.Empty<string>(), isStock: true);

                // Every other extension of the screen is merged in too, so what they add can be targeted.
                string self = NormalizePath(extensionPath);
                foreach (string path in ScreenExtensions(extensionPath, ".html", listFolder))
                {
                    string? extension = string.Equals(path, self, StringComparison.OrdinalIgnoreCase) ? null : readFile(path);
                    if (extension != null)
                    {
                        stock.Collect(path, extension, readFile, 0, Array.Empty<string>(), isStock: false);
                    }
                }

                return stock._complete || allowPartial ? stock : null;
            }

            private void Count(string? selector)
            {
                if (selector != null)
                {
                    StockMatches[selector] = StockMatches.TryGetValue(selector, out int n) ? n + 1 : 1;
                }
            }

            private void Collect(string filePath, string html, Func<string, string?> readFile, int depth, IReadOnlyList<string> outerIds, bool isStock)
            {
                IReadOnlyList<HtmlTag> tags = HtmlTagReader.Read(MaskComments(html));
                int[] parents = HtmlTagReader.Parents(tags);
                var lineMap = new LineMap(html);
                for (int i = 0; i < tags.Count; i++)
                {
                    HtmlTag tag = tags[i];
                    if (tag.IsEndTag)
                    {
                        continue;
                    }

                    lineMap.ToLineCol(tag.Start, out int line, out int column);
                    var location = new SourceLocation(filePath, line, column);
                    string name = tag.GetAttribute("name");
                    string id = tag.GetAttribute("id");
                    if (!Names.ContainsKey(name))
                    {
                        Names.Add(name, location);
                    }

                    if (!Ids.ContainsKey(id))
                    {
                        Ids.Add(id, location);
                    }

                    List<string> ids = EnclosingIds(tags, parents, i, outerIds);
                    if (isStock)
                    {
                        Count(name.Length > 0 ? "[name='" + name + "']" : null);
                        Count(id.Length > 0 ? "#" + id : null);
                        foreach (string container in ids)
                        {
                            Count(name.Length > 0 ? "#" + container + " [name='" + name + "']" : null);
                        }
                    }
                    else if (name.Length > 0)
                    {
                        ExtensionNames.Add(name);
                    }

                    if (name.Length > 0)
                    {
                        foreach (string container in ids)
                        {
                            if (!NamesUnder.TryGetValue(container, out List<string>? names))
                            {
                                names = new List<string>();
                                NamesUnder.Add(container, names);
                            }

                            names.Add(name);
                        }
                    }

                    RecordAttributes(tag);
                    if (!string.Equals(tag.Name, "qp-include", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string url = tag.GetAttribute("url");
                    string? included = url.Length == 0 || depth >= 4 ? null : readFile(ScreenModel.Combine(filePath, url));
                    if (included == null)
                    {
                        _complete = false;
                        continue;
                    }

                    if (id.Length > 0)
                    {
                        ids.Add(id);
                    }

                    Collect(ScreenModel.Combine(filePath, url), included, readFile, depth + 1, ids, isStock);
                }
            }

            private static List<string> EnclosingIds(IReadOnlyList<HtmlTag> tags, int[] parents, int index, IReadOnlyList<string> outerIds)
            {
                var ids = new List<string>(outerIds);
                for (int p = parents[index]; p >= 0; p = parents[p])
                {
                    string id = tags[p].GetAttribute("id");
                    if (id.Length > 0)
                    {
                        ids.Add(id);
                    }
                }

                return ids;
            }

            private void RecordAttributes(HtmlTag tag)
            {
                if (!Attributes.TryGetValue(tag.Name, out Dictionary<string, List<string>>? attributes))
                {
                    attributes = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                    Attributes.Add(tag.Name, attributes);
                }

                foreach (HtmlAttribute attr in tag.Attributes)
                {
                    if (!attributes.TryGetValue(attr.Name, out List<string>? values))
                    {
                        values = new List<string>();
                        attributes.Add(attr.Name, values);
                    }

                    if (attr.Value.Length > 0 && !values.Contains(attr.Value))
                    {
                        values.Add(attr.Value);
                    }
                }
            }
        }
    }
}
