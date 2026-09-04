using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AISI.MuiLint
{
    public static partial class MuiHtmlCompletion
    {
        /// <summary>
        /// Values for <c>after=</c> / <c>before=</c>. Base-HTML names come first because a
        /// selector must match the original screen; same-file names are still listed but
        /// marked, since AISI0002 flags them. Sibling <c>*.ts</c> supplies PXFieldState names.
        /// </summary>
        /// <param name="currentHtml">Text of the HTML being edited. May be null.</param>
        /// <param name="baseHtml">Text of the resolved base screen HTML. May be null.</param>
        /// <param name="siblingTypeScript">Text of the sibling screen/extension TS. May be null.</param>
        public static IReadOnlyList<MuiCompletionItem> GetSelectorValues(
            string currentHtml,
            string baseHtml,
            string siblingTypeScript)
        {
            var items = new List<MuiCompletionItem>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string name in ReadHtmlNames(baseHtml))
            {
                Add(items, seen, name, "base HTML [name] — valid after/before target");
            }

            foreach (string name in ReadPxFieldStateNames(siblingTypeScript))
            {
                Add(items, seen, name, "sibling .ts PXFieldState");
            }

            foreach (string name in ReadHtmlNames(currentHtml))
            {
                Add(items, seen, name, "same file — HTML merge sees only stock HTML (AISI0002)");
            }

            return items;
        }

        /// <summary>The one expansion in this slice: a Usr field after a stock field.</summary>
        public static MuiCompletionItem UsrFieldSnippet()
        {
            return new MuiCompletionItem(
                "field-usr",
                UsrFieldSnippetText,
                MuiCompletionKind.Snippet,
                "Usr field after a stock selector. Replace StockField and UsrMyField; the anchor must exist in the stock HTML.");
        }

        /// <summary>Every <c>name=</c> attribute value in <paramref name="html"/>, comments masked.</summary>
        public static IReadOnlyList<string> ReadHtmlNames(string html)
        {
            var names = new List<string>();
            if (string.IsNullOrEmpty(html))
            {
                return names;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            IReadOnlyList<HtmlTag> tags = HtmlTagReader.Read(HtmlMergeScanner.MaskComments(html));
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
                    if (!string.Equals(attr.Name, "name", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (attr.Value.Length > 0 && !LooksLikeTemplateName(attr.Value) && seen.Add(attr.Value))
                    {
                        names.Add(attr.Value);
                    }
                }
            }

            return names;
        }

        /// <summary>Field names declared as <c>Foo: PXFieldState</c> in a screen TS.</summary>
        public static IReadOnlyList<string> ReadPxFieldStateNames(string typeScript)
        {
            var names = new List<string>();
            if (string.IsNullOrEmpty(typeScript))
            {
                return names;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            MatchCollection matches = PxFieldStateField.Matches(typeScript);
            for (int i = 0; i < matches.Count; i++)
            {
                string name = matches[i].Groups["n"].Value;
                if (name.Length > 0 && seen.Add(name))
                {
                    names.Add(name);
                }
            }

            return names;
        }

        private static void Add(List<MuiCompletionItem> items, HashSet<string> seen, string name, string description)
        {
            if (name.Length == 0 || !seen.Add(name))
            {
                return;
            }

            items.Add(new MuiCompletionItem(
                "[name='" + name + "']",
                "[name='" + name + "']",
                MuiCompletionKind.SelectorValue,
                description));
        }

        private static bool IsViewBound(string tagName)
        {
            if (string.IsNullOrEmpty(tagName))
            {
                return false;
            }

            return string.Equals(tagName, "qp-fieldset", StringComparison.OrdinalIgnoreCase)
                || string.Equals(tagName, "qp-grid", StringComparison.OrdinalIgnoreCase)
                || string.Equals(tagName, "qp-template", StringComparison.OrdinalIgnoreCase);
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

        private static string? TryGetOpenQuotedAttribute(string inTag)
        {
            int i = 0;
            int n = inTag.Length;
            string? attributeOfOpenQuote = null;
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
                    attributeOfOpenQuote = attribute;
                    break;
                }

                i = j + 1;
            }

            return attributeOfOpenQuote;
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
