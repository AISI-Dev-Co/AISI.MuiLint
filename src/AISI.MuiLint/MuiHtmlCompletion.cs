using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AISI.MuiLint
{
    /// <summary>
    /// Completion model for Acumatica Modern UI HTML. Raw text in, candidates out:
    /// no Visual Studio types, no PX types, no site metadata, no MuiMerge ordering.
    /// The VS 2022 MEF source is a thin adapter over this.
    /// </summary>
    public static partial class MuiHtmlCompletion
    {
        /// <summary>Insert text of the Usr field expansion.</summary>
        public const string UsrFieldSnippetText =
            "<field after=\"[name='StockField']\" name=\"UsrMyField\"></field>";

        private static readonly Regex PxFieldStateField = new Regex(
            @"(?<n>[A-Za-z_][A-Za-z0-9_]*)\s*:\s*PXFieldState",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex NameSelectorValue = new Regex(
            "\\[name\\s*=\\s*(?:(['\"])(?<n>.*?)\\1|(?<n>[^\\s\\]]+))\\]",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        /// <summary>
        /// Decides what the caret is asking for. <paramref name="caret"/> is a character
        /// offset into <paramref name="text"/>.
        /// </summary>
        public static MuiCompletionTarget Classify(string text, int caret)
        {
            if (text is null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            if (caret < 0)
            {
                caret = 0;
            }

            if (caret > text.Length)
            {
                caret = text.Length;
            }

            int open = text.LastIndexOf('<', Math.Max(0, caret - 1));
            int close = text.LastIndexOf('>', Math.Max(0, caret - 1));
            if (open < 0 || (close >= 0 && close > open))
            {
                return MuiCompletionTarget.None;
            }

            if (open + 1 < text.Length && (text[open + 1] == '/' || text[open + 1] == '!' || text[open + 1] == '?'))
            {
                return MuiCompletionTarget.None;
            }

            string inTag = text.Substring(open, caret - open);
            if (inTag.IndexOf(' ') < 0 && inTag.IndexOf('\t') < 0 && inTag.IndexOf('\n') < 0)
            {
                return MuiCompletionTarget.TagName;
            }

            string? quotedAttribute = TryGetOpenQuotedAttribute(inTag);
            if (quotedAttribute == null)
            {
                return MuiCompletionTarget.AttributeName;
            }

            if (string.Equals(quotedAttribute, "after", StringComparison.OrdinalIgnoreCase)
                || string.Equals(quotedAttribute, "before", StringComparison.OrdinalIgnoreCase))
            {
                return MuiCompletionTarget.SelectorValue;
            }

            return MuiCompletionTarget.None;
        }

        /// <summary>Modern UI element names this slice completes.</summary>
        public static IReadOnlyList<MuiCompletionItem> GetTags()
        {
            var items = new List<MuiCompletionItem>
            {
                new MuiCompletionItem("field", "field", MuiCompletionKind.Tag, "Modern UI field. Never self-closing (AISI0001)."),
                new MuiCompletionItem("qp-fieldset", "qp-fieldset", MuiCompletionKind.Tag, "Fieldset container. Empty one is AISI0005."),
                new MuiCompletionItem("qp-grid", "qp-grid", MuiCompletionKind.Tag, "Grid bound with view.bind."),
                new MuiCompletionItem("qp-template", "qp-template", MuiCompletionKind.Tag, "Layout template, for example name=\"7-10-7\"."),
                new MuiCompletionItem("qp-include", "qp-include", MuiCompletionKind.Tag, "Shared template include (extension-name)."),
            };

            return items;
        }

        /// <summary>
        /// Attribute names for <paramref name="tagName"/>. Merge operators are offered on
        /// every Modern UI tag; <c>view.bind</c> only where a view is bound.
        /// </summary>
        public static IReadOnlyList<MuiCompletionItem> GetAttributes(string tagName)
        {
            var items = new List<MuiCompletionItem>
            {
                new MuiCompletionItem("after", "after=\"\"", MuiCompletionKind.Attribute, "Insert after a stock selector. Must match the original screen HTML."),
                new MuiCompletionItem("before", "before=\"\"", MuiCompletionKind.Attribute, "Insert before a stock selector. Must match the original screen HTML."),
                new MuiCompletionItem("append", "append=\"\"", MuiCompletionKind.Attribute, "Append inside an existing node."),
                new MuiCompletionItem("prepend", "prepend=\"\"", MuiCompletionKind.Attribute, "Prepend inside an existing node."),
                new MuiCompletionItem("modify", "modify=\"\"", MuiCompletionKind.Attribute, "Change attributes of an existing node."),
                new MuiCompletionItem("remove", "remove=\"\"", MuiCompletionKind.Attribute, "Remove an existing node."),
                new MuiCompletionItem("replace", "replace=\"\"", MuiCompletionKind.Attribute, "Replace an existing node."),
                new MuiCompletionItem("name", "name=\"\"", MuiCompletionKind.Attribute, "Field or template name."),
                new MuiCompletionItem("id", "id=\"\"", MuiCompletionKind.Attribute, "Element id used by selectors."),
            };

            if (IsViewBound(tagName))
            {
                items.Add(new MuiCompletionItem("view.bind", "view.bind=\"\"", MuiCompletionKind.Attribute, "Bind the container to a view declared in the screen TS."));
                items.Add(new MuiCompletionItem("slot", "slot=\"\"", MuiCompletionKind.Attribute, "Template slot, for example slot=\"A\"."));
            }

            return items;
        }
    }
}
