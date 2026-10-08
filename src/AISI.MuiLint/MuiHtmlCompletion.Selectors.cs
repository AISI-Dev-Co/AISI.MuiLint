using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AISI.MuiLint
{
    public static partial class MuiHtmlCompletion
    {
        /// <summary>The one expansion in this slice: a Usr field after a stock field.</summary>
        public static MuiCompletionItem UsrFieldSnippet()
        {
            return new MuiCompletionItem(
                "field-usr",
                UsrFieldSnippetText,
                MuiCompletionKind.Snippet,
                "Usr field after a stock selector. Replace StockField and UsrMyField; the anchor must exist in the stock HTML.");
        }

        /// <summary>
        /// Attributes the stock screen uses on <paramref name="tagName"/> that <see cref="GetAttributes"/>
        /// doesn't already offer. Empty when there's no stock screen to learn from.
        /// </summary>
        public static IReadOnlyList<MuiCompletionItem> GetStockAttributes(string tagName, string htmlPath, Func<string, string?> readFile, Func<string, IEnumerable<string>>? listFolder = null)
        {
            var items = new List<MuiCompletionItem>();
            HtmlMergeScanner.StockScreen? stock = HtmlMergeScanner.StockScreen.Read(htmlPath, readFile, allowPartial: true, listFolder: listFolder);
            if (stock == null || !stock.Attributes.TryGetValue(tagName, out Dictionary<string, List<string>>? attributes))
            {
                return items;
            }

            var offered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (MuiCompletionItem item in GetAttributes(tagName))
            {
                offered.Add(item.DisplayText);
            }

            foreach (string attribute in attributes.Keys)
            {
                if (offered.Add(attribute))
                {
                    items.Add(new MuiCompletionItem(attribute, attribute + "=\"\"", MuiCompletionKind.Attribute, "used on <" + tagName + "> in " + stock.FileName));
                }
            }

            return items;
        }

        /// <summary>
        /// Targets for a merge selector: fields and ids this file adds above the caret, then the stock
        /// screen's <c>#ids</c> and <c>[name='…']</c> values. Once the selector names a container
        /// (<c>#fsColumnA-Order [name='|</c>), only the stock names inside that container are offered.
        /// </summary>
        private static List<MuiCompletionItem> GetSelectorValues(string htmlPath, string text, int caret, Func<string, string?> readFile, Func<string, IEnumerable<string>>? listFolder)
        {
            var items = new List<MuiCompletionItem>();
            string above = text.Substring(0, text.LastIndexOf('<', caret - 1) + 1);
            foreach (HtmlTag tag in HtmlTagReader.Read(HtmlMergeScanner.MaskComments(above)))
            {
                string name = tag.GetAttribute("name");
                string id = tag.GetAttribute("id");
                if (HtmlMergeScanner.IsFieldTag(tag.Name) && name.Length > 0)
                {
                    items.Add(new MuiCompletionItem("[name='" + name + "']", "[name='" + name + "']", MuiCompletionKind.SelectorValue, "added in this file"));
                }

                if (id.Length > 0)
                {
                    items.Add(new MuiCompletionItem("#" + id, "#" + id, MuiCompletionKind.SelectorValue, "added in this file"));
                }
            }

            HtmlMergeScanner.StockScreen? stock = HtmlMergeScanner.StockScreen.Read(htmlPath, readFile, allowPartial: true, listFolder: listFolder);
            if (stock == null)
            {
                return items;
            }

            int valueStart = ValueStart(text, caret);
            string earlier = text.Substring(valueStart, ApplicableStart(text, caret, MuiCompletionTarget.SelectorValue) - valueStart);
            MatchCollection containers = HtmlMergeScanner.IdSelector.Matches(HtmlMergeScanner.BlankBracketsAndQuotes(earlier));
            string container = containers.Count > 0 ? containers[containers.Count - 1].Groups["id"].Value : string.Empty;

            IEnumerable<string> names = stock.Names.Keys;
            string where = "in " + stock.FileName;
            if (container.Length > 0 && stock.NamesUnder.TryGetValue(container, out List<string>? inside))
            {
                names = inside;
                where = "inside #" + container;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string name in names)
            {
                if (name.Length > 0 && !LooksLikeTemplateName(name) && seen.Add(name))
                {
                    items.Add(new MuiCompletionItem("[name='" + name + "']", "[name='" + name + "']", MuiCompletionKind.SelectorValue, where));
                }
            }

            foreach (string id in stock.Ids.Keys)
            {
                if (id.Length > 0)
                {
                    items.Add(new MuiCompletionItem("#" + id, "#" + id, MuiCompletionKind.SelectorValue, "id in " + stock.FileName));
                }
            }

            return items;
        }

        /// <summary>Values the stock screen gives <paramref name="attribute"/> on <paramref name="tagName"/>.</summary>
        private static List<MuiCompletionItem> GetAttributeValues(string tagName, string attribute, string htmlPath, Func<string, string?> readFile, Func<string, IEnumerable<string>>? listFolder)
        {
            var items = new List<MuiCompletionItem>();
            HtmlMergeScanner.StockScreen? stock = HtmlMergeScanner.StockScreen.Read(htmlPath, readFile, allowPartial: true, listFolder: listFolder);
            if (stock != null
                && stock.Attributes.TryGetValue(tagName, out Dictionary<string, List<string>>? attributes)
                && attributes.TryGetValue(attribute, out List<string>? values))
            {
                foreach (string value in values)
                {
                    items.Add(new MuiCompletionItem(value, value, MuiCompletionKind.AttributeValue, "used on <" + tagName + "> in " + stock.FileName));
                }
            }

            return items;
        }

        private static bool IsViewBound(string tagName)
        {
            if (string.IsNullOrEmpty(tagName))
            {
                return false;
            }

            return string.Equals(tagName, "qp-fieldset", StringComparison.OrdinalIgnoreCase)
                || string.Equals(tagName, "qp-grid", StringComparison.OrdinalIgnoreCase)
                || string.Equals(tagName, "qp-template", StringComparison.OrdinalIgnoreCase)
                || string.Equals(tagName, "qp-panel", StringComparison.OrdinalIgnoreCase)
                || string.Equals(tagName, "qp-tree", StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasCaption(string tagName)
        {
            return string.Equals(tagName, "qp-fieldset", StringComparison.OrdinalIgnoreCase)
                || string.Equals(tagName, "qp-tab", StringComparison.OrdinalIgnoreCase)
                || string.Equals(tagName, "qp-panel", StringComparison.OrdinalIgnoreCase);
        }

        private static bool LooksLikeTemplateName(string value)
        {
            // qp-template name="7-10-7" is a layout, not a field selector target.
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (!char.IsDigit(c) && c != '-')
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>The attribute whose quoted value is still open at the end of <paramref name="inTag"/>, and where its quote is.</summary>
        private static string? TryGetOpenQuotedAttribute(string inTag, out int quoteIndex)
        {
            quoteIndex = -1;
            int i = 0;
            int n = inTag.Length;
            while (i < n)
            {
                char c = inTag[i];
                if (c != '"' && c != '\'')
                {
                    i++;
                    continue;
                }

                char quote = c;
                string attribute = ReadAttributeNameBefore(inTag, i);
                int j = i + 1;
                while (j < n && inTag[j] != quote)
                {
                    j++;
                }

                if (j >= n)
                {
                    quoteIndex = i;
                    return attribute;
                }

                i = j + 1;
            }

            return null;
        }

        private static string ReadAttributeNameBefore(string inTag, int quoteIndex)
        {
            int i = quoteIndex - 1;
            while (i >= 0 && char.IsWhiteSpace(inTag[i]))
            {
                i--;
            }

            if (i < 0 || inTag[i] != '=')
            {
                return string.Empty;
            }

            i--;
            while (i >= 0 && char.IsWhiteSpace(inTag[i]))
            {
                i--;
            }

            int end = i + 1;
            while (i >= 0 && (char.IsLetterOrDigit(inTag[i]) || inTag[i] == '-' || inTag[i] == '_' || inTag[i] == '.' || inTag[i] == ':'))
            {
                i--;
            }

            int start = i + 1;
            if (end <= start)
            {
                return string.Empty;
            }

            return inTag.Substring(start, end - start);
        }
    }
}
