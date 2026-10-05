using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AISI.MuiLint
{
    /// <summary>
    /// Go to definition for Modern UI HTML: a selector's <c>[name='X']</c> or <c>#id</c> goes to the
    /// stock screen HTML, and <c>view.bind</c>, <c>state.bind</c>, a field's name or a qp-panel id
    /// go to their declaration in the TypeScript.
    /// </summary>
    internal static class MuiNavigation
    {
        public static SourceLocation? FindDefinition(string path, string text, int caret, Func<string, string?> readFile)
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
                        return Resolve(path, tags, i, attr, offset, readFile);
                    }
                }

                return null;
            }

            return null;
        }

        private static SourceLocation? Resolve(
            string path,
            IReadOnlyList<HtmlTag> tags,
            int index,
            HtmlAttribute attr,
            int offset,
            Func<string, string?> readFile)
        {
            if (HtmlMergeScanner.IsMergeOperator(attr.Name))
            {
                return FindInStock(path, attr.Value, offset, readFile);
            }

            HtmlTag tag = tags[index];
            bool member = Is(attr.Name, "view.bind") || Is(attr.Name, "state.bind") || (Is(attr.Name, "id") && Is(tag.Name, "qp-panel"));
            bool field = Is(attr.Name, "name") && HtmlMergeScanner.IsFieldTag(tag.Name);
            ScreenModel? screen = member || field ? ScreenModel.Read(path, readFile) : null;
            if (screen == null)
            {
                return null;
            }

            SourceLocation location;
            if (member)
            {
                return screen.TryGetMember(attr.Value, out location) ? location : (SourceLocation?)null;
            }

            // name="Document.OrderNbr": the part under the caret decides where we go.
            string view = HtmlMergeScanner.NearestView(tags, HtmlTagReader.Parents(tags), index);
            string name = attr.Value;
            int dot = name.IndexOf('.');
            if (dot > 0)
            {
                view = name.Substring(0, dot);
                name = name.Substring(dot + 1);
                if (offset <= dot)
                {
                    return screen.TryGetMember(view, out location) ? location : (SourceLocation?)null;
                }
            }

            if (view.Length == 0)
            {
                view = screen.FindViewWithField(name) ?? string.Empty;
            }

            return screen.TryGetField(view, name, out location) ? location : (SourceLocation?)null;
        }

        private static SourceLocation? FindInStock(string path, string selector, int offset, Func<string, string?> readFile)
        {
            HtmlMergeScanner.StockScreen? stock = HtmlMergeScanner.StockScreen.Read(path, readFile);
            if (stock == null)
            {
                return null;
            }

            SourceLocation location;
            foreach (Match match in HtmlMergeScanner.NameSelector.Matches(selector))
            {
                if (Covers(match, offset) && stock.Names.TryGetValue(match.Groups["n"].Value, out location))
                {
                    return location;
                }
            }

            foreach (Match match in HtmlMergeScanner.IdSelector.Matches(HtmlMergeScanner.BlankBracketsAndQuotes(selector)))
            {
                if (Covers(match, offset) && stock.Ids.TryGetValue(match.Groups["id"].Value, out location))
                {
                    return location;
                }
            }

            return null;
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
