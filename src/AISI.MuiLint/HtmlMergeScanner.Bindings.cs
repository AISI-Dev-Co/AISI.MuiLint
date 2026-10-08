using System;
using System.Collections.Generic;
using System.Globalization;

namespace AISI.MuiLint
{
    public static partial class HtmlMergeScanner
    {
        private static void Scan0011(
            string path,
            IReadOnlyList<HtmlTag> tags,
            int[] parents,
            ScreenModel screen,
            LineMap lineMap,
            List<Diagnostic> results)
        {
            for (int i = 0; i < tags.Count; i++)
            {
                HtmlTag tag = tags[i];

                // These point at existing markup; whatever they say was checked where it was written.
                if (tag.IsEndTag || tag.HasAttribute("modify") || tag.HasAttribute("remove"))
                {
                    continue;
                }

                foreach (HtmlAttribute attr in tag.Attributes)
                {
                    // Anything but a plain name is an expression, and not ours to judge. Names with a leading
                    // underscore (_PayBillsFilter_CurrencyInfo_) are views the backend makes up.
                    if (!IsIdentifier(attr.Value) || attr.Value[0] == '_')
                    {
                        continue;
                    }

                    string? message = null;
                    if (Is(attr.Name, "view.bind") && !screen.TryGetMember(attr.Value, out _))
                    {
                        message = "{0} has no view called '{1}'. Declare it in the .ts (createSingle/createCollection) or fix the spelling.";
                    }
                    else if (Is(attr.Name, "state.bind") && Is(tag.Name, "qp-button") && !HasAction(screen, attr.Value, NearestView(tags, parents, i)))
                    {
                        // On anything but a button, state.bind is a field (<qp-mail-editor state.bind="Email">).
                        message = "{0} has no action called '{1}'. Declare '{1}: PXActionState' in the .ts or fix the spelling.";
                    }

                    if (message != null)
                    {
                        string text = string.Format(CultureInfo.InvariantCulture, message, screen.ScreenClass, attr.Value);
                        results.Add(Create(DiagnosticIds.BindingNotInTypeScript, text, path, attr.ValueStart, attr.Value.Length, lineMap));
                    }
                }

                // unbound fields are placeholders for custom content and live on no view.
                string name = tag.GetAttribute("name");
                if (IsFieldTag(tag.Name) && name.Length > 0 && !tag.HasAttribute("unbound"))
                {
                    CheckField(path, tag, name, NearestView(tags, parents, i), screen, lineMap, results);
                }
            }
        }

        /// <summary>An action on the screen, or on the class of the view the button sits in.</summary>
        private static bool HasAction(ScreenModel screen, string action, string view)
        {
            if (screen.TryGetMember(action, out _))
            {
                return true;
            }

            if (view.Length == 0)
            {
                return false;
            }

            // A view we can't follow stays quiet, as fields do.
            ICollection<string>? members = screen.FieldsOf(view);
            return members == null || members.Contains(action);
        }

        private static void CheckField(
            string path,
            HtmlTag tag,
            string name,
            string view,
            ScreenModel screen,
            LineMap lineMap,
            List<Diagnostic> results)
        {
            // name="Document.OrderNbr" names its own view.
            int dot = name.IndexOf('.');
            string field = name;
            if (dot > 0)
            {
                view = name.Substring(0, dot);
                field = name.Substring(dot + 1);
            }

            if (!IsIdentifier(field) || (view.Length > 0 && !IsIdentifier(view)))
            {
                return;
            }

            string message;
            if (view.Length == 0)
            {
                // Merged in with after/before and no view around it: it has to be on some view.
                ICollection<string>? all = screen.AllFields();
                if (all == null || all.Contains(field))
                {
                    return;
                }

                message = string.Format(
                    CultureInfo.InvariantCulture,
                    "Field '{0}' is not declared on any view of {1}. Add '{0}: PXFieldState' to the view's class (or an extension of it) in the .ts, or it won't show up.",
                    field,
                    screen.ScreenClass);
            }
            else
            {
                // Unknown views are reported on their view.bind; a class we can't follow is not our business.
                ICollection<string>? fields = screen.FieldsOf(view);
                if (fields == null || fields.Contains(field))
                {
                    return;
                }

                message = string.Format(
                    CultureInfo.InvariantCulture,
                    "Field '{0}' is not declared on view '{1}' ({2}). Add '{0}: PXFieldState' to {2} (or an extension of it) in the .ts, or it won't show up.",
                    field,
                    view,
                    screen.ClassOf(view));
            }

            HtmlAttribute attr = Find(tag, "name");
            results.Add(Create(DiagnosticIds.BindingNotInTypeScript, message, path, attr.ValueStart, attr.Value.Length, lineMap));
        }

        private static HtmlAttribute Find(HtmlTag tag, string name)
        {
            foreach (HtmlAttribute attr in tag.Attributes)
            {
                if (Is(attr.Name, name))
                {
                    return attr;
                }
            }

            return default;
        }

        private static bool Is(string a, string b)
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsIdentifier(string value)
        {
            if (value.Length == 0 || char.IsDigit(value[0]))
            {
                return false;
            }

            foreach (char c in value)
            {
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '$')
                {
                    return false;
                }
            }

            return true;
        }
    }
}
