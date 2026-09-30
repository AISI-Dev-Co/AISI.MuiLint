using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace AISI.MuiLint
{
    public static partial class HtmlMergeScanner
    {
        private static readonly Regex IdSelector = new Regex(
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

            string[] parts = NormalizePath(extensionHtmlPath).Split('/');
            int extensions = parts.Length - 2;
            int module = extensions - 2;
            if (module < 1 || !string.Equals(parts[extensions], "extensions", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            // Walk back from the module folder to the start of the screens tree.
            int root = -1;
            for (int i = module - 1; i >= 0 && i >= module - 3; i--)
            {
                if (string.Equals(parts[i], "development", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(parts[i], "customizationScreens", StringComparison.OrdinalIgnoreCase))
                {
                    root = i;
                    break;
                }
            }

            if (root < 0 && string.Equals(parts[module - 1], "screens", StringComparison.OrdinalIgnoreCase))
            {
                root = module - 1;
            }

            if (root < 0)
            {
                return null;
            }

            string screen = parts[extensions - 1];
            string prefix = root == 0 ? string.Empty : string.Join("/", parts, 0, root) + "/";
            return prefix + "screens/" + parts[module] + "/" + screen + "/" + screen + ".html";
        }

        private static bool IsExtensionFile(string path)
        {
            string directory = Path.GetFileName(Path.GetDirectoryName(NormalizePath(path)) ?? string.Empty);
            return string.Equals(directory, "extensions", StringComparison.OrdinalIgnoreCase);
        }

        private static void TryPath0007(string path, Func<string, string?> readFile, LineMap lineMap, List<Diagnostic> results)
        {
            string tsPath = Path.ChangeExtension(path, ".ts");
            if (readFile(tsPath) != null)
            {
                return;
            }

            string message = string.Format(
                CultureInfo.InvariantCulture,
                "There is no {0} next to this file. Modern UI loads extension HTML through the TypeScript extension of the same name, so this HTML is never merged.",
                Path.GetFileName(tsPath));
            results.Add(Create(DiagnosticIds.ExtensionWithoutTypeScript, message, path, 0, 0, lineMap));
        }

        private static void Scan0009(
            string path,
            IReadOnlyList<HtmlTag> tags,
            StockScreen stock,
            LineMap lineMap,
            List<Diagnostic> results)
        {
            // Same-file targets are AISI0002's business, not this rule's.
            var localNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var localIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (HtmlTag tag in tags)
            {
                localNames.Add(tag.GetAttribute("name"));
                localIds.Add(tag.GetAttribute("id"));
            }

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
                    if (!IsMergeOperator(attr.Name) || FindSelectorProblem(attr.Value) != null)
                    {
                        continue;
                    }

                    foreach (Match match in NameSelector.Matches(attr.Value))
                    {
                        string name = match.Groups["n"].Value;
                        if (name.Length > 0 && !localNames.Contains(name) && !stock.Names.Contains(name))
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
                        if (!localIds.Contains(id) && !stock.Ids.Contains(id))
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
                }
            }
        }

        private static string NotInStockMessage(string target, StockScreen stock)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} is not in the stock {1}, so the merge has nothing to attach to. Check the spelling; if another extension adds it, suppress this.",
                target,
                stock.FileName);
        }

        private static void Scan0010(
            string path,
            IReadOnlyList<HtmlTag> tags,
            int[] parents,
            StockScreen? stock,
            LineMap lineMap,
            List<Diagnostic> results)
        {
            for (int i = 0; i < tags.Count; i++)
            {
                HtmlTag tag = tags[i];
                string name = tag.GetAttribute("name");
                if (tag.IsEndTag || !IsFieldTag(tag.Name) || name.Length == 0 || !IsAddedByExtension(tags, parents, i))
                {
                    continue;
                }

                string field = name.Substring(name.LastIndexOf('.') + 1);
                if (field.StartsWith("Usr", StringComparison.Ordinal) || (stock != null && stock.Names.Contains(name)))
                {
                    continue;
                }

                string message = string.Format(
                    CultureInfo.InvariantCulture,
                    "Field '{0}' is added by this extension but is not Usr-prefixed. Fine if you are moving a stock field; a custom DAC field needs the Usr prefix.",
                    name);
                results.Add(Create(DiagnosticIds.FieldWithoutUsrPrefix, message, path, tag.Start, tag.End - tag.Start, lineMap));
            }
        }

        private static bool IsAddedByExtension(IReadOnlyList<HtmlTag> tags, int[] parents, int index)
        {
            for (int t = index; t >= 0; t = parents[t])
            {
                HtmlTag tag = tags[t];
                if (tag.HasAttribute("after")
                    || tag.HasAttribute("before")
                    || tag.HasAttribute("append")
                    || tag.HasAttribute("prepend")
                    || tag.HasAttribute("replace"))
                {
                    return true;
                }
            }

            return false;
        }

        private static string BlankBracketsAndQuotes(string selector)
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
        private sealed class StockScreen
        {
            private StockScreen(string fileName)
            {
                FileName = fileName;
            }

            public string FileName { get; }

            public HashSet<string> Names { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            public HashSet<string> Ids { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            public static StockScreen? Read(string extensionPath, Func<string, string?> readFile)
            {
                string? stockPath = StockHtmlPath(extensionPath);
                string? html = stockPath == null ? null : readFile(stockPath);
                if (stockPath == null || html == null)
                {
                    return null;
                }

                var stock = new StockScreen(Path.GetFileName(stockPath));
                return stock.Collect(stockPath, html, readFile, 0) ? stock : null;
            }

            // False when an include cannot be followed. Then we do not know every target, and a
            // half-known stock screen would only produce false alarms.
            private bool Collect(string filePath, string html, Func<string, string?> readFile, int depth)
            {
                IReadOnlyList<HtmlTag> tags = HtmlTagReader.Read(MaskComments(html));
                for (int i = 0; i < tags.Count; i++)
                {
                    HtmlTag tag = tags[i];
                    if (tag.IsEndTag)
                    {
                        continue;
                    }

                    Names.Add(tag.GetAttribute("name"));
                    Ids.Add(tag.GetAttribute("id"));

                    if (!string.Equals(tag.Name, "qp-include", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string url = tag.GetAttribute("url");
                    if (url.Length == 0 || depth >= 4)
                    {
                        return false;
                    }

                    string includePath = CombinePath(filePath, url);
                    string? included = readFile(includePath);
                    if (included == null || !Collect(includePath, included, readFile, depth + 1))
                    {
                        return false;
                    }
                }

                return true;
            }

            private static string CombinePath(string fromFile, string relative)
            {
                var parts = new List<string>(NormalizePath(fromFile).Split('/'));
                parts.RemoveAt(parts.Count - 1);
                foreach (string part in NormalizePath(relative).Split('/'))
                {
                    if (part == "..")
                    {
                        if (parts.Count > 0)
                        {
                            parts.RemoveAt(parts.Count - 1);
                        }
                    }
                    else if (part.Length > 0 && part != ".")
                    {
                        parts.Add(part);
                    }
                }

                return string.Join("/", parts);
            }
        }
    }
}
