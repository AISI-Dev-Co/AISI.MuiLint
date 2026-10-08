using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace AISI.MuiLint
{
    /// <summary>What a name in the HTML points at: where it's declared and how to describe it.</summary>
    internal readonly struct NavigationTarget
    {
        public NavigationTarget(int start, int length, SourceLocation location, string description)
        {
            Start = start;
            Length = length;
            Location = location;
            Description = description;
        }

        /// <summary>The span in the HTML the target was found from.</summary>
        public int Start { get; }

        public int Length { get; }

        public SourceLocation Location { get; }

        /// <summary>One line for a hover.</summary>
        public string Description { get; }
    }

    /// <summary>
    /// Go to definition and hover for Modern UI HTML: a selector's <c>[name='X']</c> or <c>#id</c>
    /// goes to the stock screen HTML, and <c>view.bind</c>, <c>state.bind</c>, a field's name or a
    /// qp-panel id go to their declaration in the TypeScript.
    /// </summary>
    internal static class MuiNavigation
    {
        public static SourceLocation? FindDefinition(string path, string text, int caret, Func<string, string?> readFile, Func<string, IEnumerable<string>>? listFolder = null)
        {
            return Find(path, text, caret, readFile, listFolder)?.Location;
        }

        public static NavigationTarget? Find(string path, string text, int caret, Func<string, string?> readFile, Func<string, IEnumerable<string>>? listFolder = null)
        {
            IReadOnlyList<HtmlTag> tags = HtmlTagReader.Read(HtmlMergeScanner.MaskComments(text));
            for (int i = 0; i < tags.Count; i++)
            {
                HtmlTag tag = tags[i];
                if (tag.IsEndTag || caret < tag.Start || caret > tag.End)
                {
                    continue;
                }

                foreach (HtmlAttribute attr in tag.Attributes)
                {
                    int offset = caret - attr.ValueStart;
                    if (attr.Value.Length > 0 && offset >= 0 && offset <= attr.Value.Length)
                    {
                        return Resolve(path, tags, i, attr, offset, readFile, listFolder);
                    }
                }

                return null;
            }

            return null;
        }

        private static NavigationTarget? Resolve(
            string path,
            IReadOnlyList<HtmlTag> tags,
            int index,
            HtmlAttribute attr,
            int offset,
            Func<string, string?> readFile,
            Func<string, IEnumerable<string>>? listFolder)
        {
            if (HtmlMergeScanner.IsMergeOperator(attr.Name))
            {
                return FindInStock(path, attr, offset, readFile, listFolder);
            }

            HtmlTag tag = tags[index];
            bool view = Is(attr.Name, "view.bind") || (Is(attr.Name, "id") && Is(tag.Name, "qp-panel"));
            bool action = Is(attr.Name, "state.bind");
            bool field = Is(attr.Name, "name") && HtmlMergeScanner.IsFieldTag(tag.Name);
            ScreenModel? screen = view || action || field ? ScreenModel.Read(path, readFile, listFolder: listFolder) : null;
            if (screen == null)
            {
                return null;
            }

            SourceLocation location;
            if (view || action)
            {
                if (!screen.TryGetMember(attr.Value, out location))
                {
                    return null;
                }

                string what = view
                    ? "View " + attr.Value + ": " + screen.ClassOf(attr.Value)
                    : attr.Value + " on " + screen.ScreenClass;
                return new NavigationTarget(attr.ValueStart, attr.Value.Length, location, what + ", " + Where(location));
            }

            // name="Document.OrderNbr": the part under the caret decides where we go.
            string viewName = HtmlMergeScanner.NearestView(tags, HtmlTagReader.Parents(tags), index);
            string name = attr.Value;
            int start = attr.ValueStart;
            int dot = name.IndexOf('.');
            if (dot > 0)
            {
                viewName = name.Substring(0, dot);
                name = name.Substring(dot + 1);
                if (offset <= dot)
                {
                    return screen.TryGetMember(viewName, out location)
                        ? new NavigationTarget(start, dot, location, "View " + viewName + ": " + screen.ClassOf(viewName) + ", " + Where(location))
                        : (NavigationTarget?)null;
                }

                start += dot + 1;
            }

            if (viewName.Length == 0)
            {
                viewName = screen.FindViewWithField(name) ?? string.Empty;
            }

            return screen.TryGetField(viewName, name, out location)
                ? new NavigationTarget(start, name.Length, location, name + " on " + viewName + " (" + screen.ClassOf(viewName) + "), " + Where(location))
                : (NavigationTarget?)null;
        }

        private static NavigationTarget? FindInStock(string path, HtmlAttribute attr, int offset, Func<string, string?> readFile, Func<string, IEnumerable<string>>? listFolder)
        {
            HtmlMergeScanner.StockScreen? stock = HtmlMergeScanner.StockScreen.Read(path, readFile, allowPartial: true, listFolder: listFolder);
            if (stock == null)
            {
                return null;
            }

            SourceLocation location;
            foreach (Match match in HtmlMergeScanner.NameSelector.Matches(attr.Value))
            {
                if (Covers(match, offset) && stock.Names.TryGetValue(match.Groups["n"].Value, out location))
                {
                    return new NavigationTarget(attr.ValueStart + match.Index, match.Length, location, match.Value + " in the stock " + Where(location));
                }
            }

            foreach (Match match in HtmlMergeScanner.IdSelector.Matches(HtmlMergeScanner.BlankBracketsAndQuotes(attr.Value)))
            {
                if (Covers(match, offset) && stock.Ids.TryGetValue(match.Groups["id"].Value, out location))
                {
                    return new NavigationTarget(attr.ValueStart + match.Index, match.Length, location, match.Value + " in the stock " + Where(location));
                }
            }

            return null;
        }

        private static string Where(SourceLocation location)
        {
            return Path.GetFileName(location.Path) + " line " + location.Line;
        }

        private static bool Covers(Match match, int offset)
        {
            return offset >= match.Index && offset <= match.Index + match.Length;
        }

        private static bool Is(string a, string b)
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }
    }
}
