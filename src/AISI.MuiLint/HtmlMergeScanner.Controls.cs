using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace AISI.MuiLint
{
    public static partial class HtmlMergeScanner
    {
        private static readonly Regex IncludeParameters = new Regex(
            "<qp-include-parameters\\b(?<attrs>(?:[^>\"']|\"[^\"]*\"|'[^']*')*)>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex ParameterName = new Regex(
            "(?<=^|\\s)(?<name>[\\w-]+)(?<required>\\.required)?(?=\\s|=|$)",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        // Attributes any element takes, which a qp-include passes on rather than to its parameters.
        private static readonly HashSet<string> IncludeOwnAttributes = new HashSet<string>(
            new[] { "url", "id", "class", "style", "slot" },
            StringComparer.OrdinalIgnoreCase);

        private static void Scan0019(string path, IReadOnlyList<HtmlTag> tags, Func<string, string?> readFile, LineMap lineMap, List<Diagnostic> results)
        {
            foreach (HtmlTag tag in tags)
            {
                string url = tag.GetAttribute("url");
                if (tag.IsEndTag || !Is(tag.Name, "qp-include") || url.Length == 0 || url.Contains("{{") || url.Contains("${"))
                {
                    continue;
                }

                string? included = readFile(ScreenModel.Combine(NormalizePath(path), url));
                Match declared = included == null ? Match.Empty : IncludeParameters.Match(MaskComments(included));
                if (!declared.Success)
                {
                    continue;
                }

                var parameters = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                foreach (Match p in ParameterName.Matches(BlankQuoted(declared.Groups["attrs"].Value)))
                {
                    string name = p.Groups["name"].Value;
                    parameters[name] = p.Groups["required"].Success || (parameters.TryGetValue(name, out bool required) && required);
                }

                foreach (KeyValuePair<string, bool> parameter in parameters)
                {
                    if (parameter.Value && !tag.HasAttribute(parameter.Key))
                    {
                        string message = string.Format(
                            CultureInfo.InvariantCulture,
                            "qp-include of {0} is missing the required parameter '{1}'.",
                            url.Substring(url.LastIndexOf('/') + 1),
                            parameter.Key);
                        results.Add(Create(DiagnosticIds.IncludeParameters, message, path, tag.Start, tag.End - tag.Start, lineMap));
                    }
                }

                foreach (HtmlAttribute attr in tag.Attributes)
                {
                    bool passedOn = IncludeOwnAttributes.Contains(attr.Name) || attr.Name.IndexOf('.') >= 0
                        || attr.Name.StartsWith("data-", StringComparison.OrdinalIgnoreCase)
                        || attr.Name.StartsWith("aria-", StringComparison.OrdinalIgnoreCase);
                    if (!passedOn && !parameters.ContainsKey(attr.Name))
                    {
                        string message = string.Format(
                            CultureInfo.InvariantCulture,
                            "{0} declares no parameter '{1}', so this value goes nowhere.",
                            url.Substring(url.LastIndexOf('/') + 1),
                            attr.Name);
                        results.Add(Create(DiagnosticIds.IncludeParameters, message, path, attr.NameStart, attr.Name.Length, lineMap));
                    }
                }
            }
        }

        private static void ScanClientControls(string path, IReadOnlyList<HtmlTag> tags, ClientControls controls, LineMap lineMap, List<Diagnostic> results)
        {
            foreach (HtmlTag tag in tags)
            {
                if (tag.IsEndTag)
                {
                    continue;
                }

                foreach (HtmlAttribute attr in tag.Attributes)
                {
                    if (attr.Value.Contains("{{") || attr.Value.Contains("${"))
                    {
                        continue;
                    }

                    if (Is(tag.Name, "qp-template") && Is(attr.Name, "name") && controls.Templates != null && !controls.Templates.Contains(attr.Value))
                    {
                        string message = string.Format(
                            CultureInfo.InvariantCulture,
                            "'{0}' isn't one of the screen templates in this site's client-controls, so the template renders nothing.",
                            attr.Value);
                        results.Add(Create(DiagnosticIds.UnknownTemplate, message, path, attr.ValueStart, attr.Value.Length, lineMap));
                    }
                    else if (Is(attr.Name, "control-type") && attr.Value.StartsWith("qp-", StringComparison.OrdinalIgnoreCase) && !controls.Controls.ContainsKey(attr.Value))
                    {
                        string message = string.Format(
                            CultureInfo.InvariantCulture,
                            "'{0}' isn't a control in this site's client-controls.",
                            attr.Value);
                        results.Add(Create(DiagnosticIds.UnknownControlType, message, path, attr.ValueStart, attr.Value.Length, lineMap));
                    }
                    else if (Is(attr.Name, "config.bind") && controls.Controls.TryGetValue(tag.Name, out ICollection<string>? keys) && keys != null && FindBracketProblem(attr.Value) == null)
                    {
                        foreach ((string key, int offset) in ObjectKeys(attr.Value))
                        {
                            if (!keys.Contains(key))
                            {
                                string message = string.Format(
                                    CultureInfo.InvariantCulture,
                                    "<{0}> config has no '{1}' in this site's client-controls, so it is ignored.",
                                    tag.Name,
                                    key);
                                results.Add(Create(DiagnosticIds.UnknownConfigKey, message, path, attr.ValueStart + offset, key.Length, lineMap));
                            }
                        }
                    }
                }
            }
        }

        private static void Scan0021(string path, IReadOnlyList<HtmlTag> tags, int[] parents, LineMap lineMap, List<Diagnostic> results)
        {
            for (int i = 0; i < tags.Count; i++)
            {
                HtmlTag tag = tags[i];
                HtmlAttribute name = Find(tag, "name");
                if (tag.IsEndTag || !Is(tag.Name, "qp-template") || name.Value == null || !name.Value.StartsWith("record-", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                bool inFeed = false;
                for (int p = parents[i]; p >= 0 && !inFeed; p = parents[p])
                {
                    inFeed = Is(tags[p].Name, "qp-data-feed");
                }

                if (!inFeed)
                {
                    string message = string.Format(
                        CultureInfo.InvariantCulture,
                        "The {0} template lays out the records of a qp-data-feed, and this one isn't inside one.",
                        name.Value);
                    results.Add(Create(DiagnosticIds.RecordTemplateOutsideDataFeed, message, path, name.ValueStart, name.Value.Length, lineMap));
                }
            }
        }

        private static void Scan0024(string path, IReadOnlyList<HtmlTag> tags, LineMap lineMap, List<Diagnostic> results)
        {
            var open = new List<HtmlTag>();
            foreach (HtmlTag tag in tags)
            {
                if (!NeedsEndTag(tag.Name) || tag.SelfClosing)
                {
                    continue;
                }

                if (!tag.IsEndTag)
                {
                    open.Add(tag);
                    continue;
                }

                int match = open.FindLastIndex(t => Is(t.Name, tag.Name));
                if (match < 0)
                {
                    string message = string.Format(CultureInfo.InvariantCulture, "</{0}> has no <{0}> to close.", tag.Name);
                    results.Add(Create(DiagnosticIds.UnbalancedTag, message, path, tag.Start, tag.End - tag.Start, lineMap));
                    continue;
                }

                for (int i = open.Count - 1; i > match; i--)
                {
                    Unclosed(open[i], path, lineMap, results);
                }

                open.RemoveRange(match, open.Count - match);
            }

            foreach (HtmlTag tag in open)
            {
                Unclosed(tag, path, lineMap, results);
            }
        }

        private static void Unclosed(HtmlTag tag, string path, LineMap lineMap, List<Diagnostic> results)
        {
            string message = string.Format(
                CultureInfo.InvariantCulture,
                "<{0}> is never closed, so everything after it ends up inside it. Add </{0}>.",
                tag.Name);
            results.Add(Create(DiagnosticIds.UnbalancedTag, message, path, tag.Start, tag.End - tag.Start, lineMap));
        }

        // The elements HTML won't close for you: Acumatica's custom ones.
        private static bool NeedsEndTag(string name)
        {
            return name.StartsWith("qp-", StringComparison.OrdinalIgnoreCase)
                || Is(name, "field")
                || Is(name, "template")
                || Is(name, "using");
        }

        /// <summary>The top-level keys of an object literal, with their offsets; nothing for anything else.</summary>
        internal static IEnumerable<(string Key, int Offset)> ObjectKeys(string value)
        {
            string trimmed = value.Trim();
            if (!trimmed.StartsWith("{", StringComparison.Ordinal) || !trimmed.EndsWith("}", StringComparison.Ordinal))
            {
                yield break;
            }

            int open = value.IndexOf('{');
            int close = value.LastIndexOf('}');
            int depth = 0;
            char quote = '\0';
            int start = open + 1;
            bool inKey = true;
            for (int i = open + 1; i <= close; i++)
            {
                char c = value[i];
                if (quote != '\0')
                {
                    quote = c == quote && value[i - 1] != '\\' ? '\0' : quote;
                    continue;
                }

                if (c == '\'' || c == '"' || c == '`')
                {
                    quote = c;
                }
                else if (c == '{' || c == '[' || c == '(')
                {
                    depth++;
                }
                else if ((c == '}' || c == ']' || c == ')') && depth > 0)
                {
                    depth--;
                }
                else if (depth == 0 && inKey && c == ':')
                {
                    (string key, int offset) = KeyAt(value, start, i);
                    if (key.Length > 0)
                    {
                        yield return (key, offset);
                    }

                    inKey = false;
                }
                else if (depth == 0 && (c == ',' || i == close))
                {
                    // { id } shorthand: the name is the key.
                    if (inKey)
                    {
                        (string key, int offset) = KeyAt(value, start, i);
                        if (key.Length > 0)
                        {
                            yield return (key, offset);
                        }
                    }

                    start = i + 1;
                    inKey = true;
                }
            }
        }

        private static (string Key, int Offset) KeyAt(string value, int start, int end)
        {
            string raw = value.Substring(start, end - start);
            string key = raw.Trim().Trim('\'', '"');
            int offset = start + raw.IndexOf(key, StringComparison.Ordinal);
            return IsIdentifier(key.Replace("-", string.Empty)) ? (key, offset) : (string.Empty, 0);
        }

        private static string BlankQuoted(string attrs)
        {
            return Regex.Replace(attrs, "\"[^\"]*\"|'[^']*'", m => new string(' ', m.Length));
        }
    }
}
