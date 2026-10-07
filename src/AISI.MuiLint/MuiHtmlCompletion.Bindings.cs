using System;
using System.Collections.Generic;

namespace AISI.MuiLint
{
    public static partial class MuiHtmlCompletion
    {
        /// <summary>
        /// Suggestions for the attribute value under the caret: stock targets in a selector; views,
        /// fields and actions from the screen's TypeScript (fields from the view of the nearest
        /// enclosing <c>view.bind</c>, or every view when there is none); otherwise the values the
        /// stock screen uses for the same attribute.
        /// </summary>
        /// <param name="htmlPath">Path of the HTML being edited.</param>
        /// <param name="text">Its current text.</param>
        /// <param name="caret">Caret offset into <paramref name="text"/>.</param>
        /// <param name="target">What <see cref="Classify"/> said the caret is in.</param>
        /// <param name="readFile">Returns a file's text, or null when it does not exist.</param>
        /// <param name="listFolder">Lists what's directly inside a folder, so every extension of the screen counts. Optional.</param>
        public static IReadOnlyList<MuiCompletionItem> GetValues(
            string htmlPath,
            string text,
            int caret,
            MuiCompletionTarget target,
            Func<string, string?> readFile,
            Func<string, IEnumerable<string>>? listFolder = null)
        {
            if (target == MuiCompletionTarget.SelectorValue)
            {
                return GetSelectorValues(htmlPath, text, caret, readFile, listFolder);
            }

            if (target == MuiCompletionTarget.AttributeValue)
            {
                int open = text.LastIndexOf('<', Math.Max(0, caret - 1));
                string inTag = open < 0 ? string.Empty : text.Substring(open, caret - open);
                string attribute = TryGetOpenQuotedAttribute(inTag, out _) ?? string.Empty;
                return GetAttributeValues(ReadTagName(inTag), attribute, htmlPath, readFile, listFolder);
            }

            var items = new List<MuiCompletionItem>();
            ScreenModel? screen = ScreenModel.Read(htmlPath, readFile, listFolder: listFolder);
            if (screen == null)
            {
                return items;
            }

            switch (target)
            {
                case MuiCompletionTarget.ViewValue:
                    foreach (string name in screen.Views)
                    {
                        items.Add(Binding(name, "view (" + screen.ClassOf(name) + ")"));
                    }

                    break;
                case MuiCompletionTarget.ActionValue:
                    foreach (string action in screen.Actions)
                    {
                        items.Add(Binding(action, "action on " + screen.ScreenClass));
                    }

                    break;
                case MuiCompletionTarget.FieldValue:
                    string view = EnclosingView(text, caret);
                    ICollection<string>? fields = view.Length > 0 ? screen.FieldsOf(view) : screen.AllFields();
                    foreach (string field in fields ?? Array.Empty<string>())
                    {
                        items.Add(Binding(field, view.Length > 0 ? "field of " + view : "field"));
                    }

                    break;
            }

            return items;
        }

        private static MuiCompletionItem Binding(string name, string description)
        {
            return new MuiCompletionItem(name, name, MuiCompletionKind.BindingValue, description);
        }

        private static string EnclosingView(string text, int caret)
        {
            int open = text.LastIndexOf('<', Math.Max(0, Math.Min(caret, text.Length) - 1));
            IReadOnlyList<HtmlTag> tags = HtmlTagReader.Read(HtmlMergeScanner.MaskComments(text));
            for (int i = 0; i < tags.Count; i++)
            {
                if (tags[i].Start == open)
                {
                    return HtmlMergeScanner.NearestView(tags, HtmlTagReader.Parents(tags), i);
                }
            }

            return string.Empty;
        }

        private static string ReadTagName(string inTag)
        {
            if (inTag.Length == 0)
            {
                return string.Empty;
            }

            int end = 1;
            while (end < inTag.Length && (char.IsLetterOrDigit(inTag[end]) || inTag[end] == '-' || inTag[end] == '_'))
            {
                end++;
            }

            return inTag.Substring(1, end - 1);
        }
    }
}
