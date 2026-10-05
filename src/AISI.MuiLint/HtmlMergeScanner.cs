using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace AISI.MuiLint
{
    /// <summary>
    /// Raw-text scanner for Acumatica Modern UI HTML. No Roslyn compilation and no site: given a
    /// way to read files it also checks the HTML against the stock screen and the screen's .ts.
    /// </summary>
    public static partial class HtmlMergeScanner
    {
        internal static readonly Regex NameSelector = new Regex(
            "\\[name\\s*=\\s*(?:(['\"])(?<n>.*?)\\1|(?<n>[^\\s\\]]+))\\]",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly HashSet<string> MergeOperators = new HashSet<string>(
            new[] { "after", "before", "append", "prepend", "modify", "remove", "replace" },
            StringComparer.OrdinalIgnoreCase);

        private static readonly char[] IdSeparators = { ' ', '\t', '\r', '\n', ',' };

        // AcuMate's list: these are fine without an id.
        private static readonly HashSet<string> IdOptional = new HashSet<string>(
            new[] { "qp-field", "qp-label", "qp-include" },
            StringComparer.OrdinalIgnoreCase);

        private static readonly Regex SuppressionComment = new Regex(
            "<!--\\s*muilint-disable(?<next>-next-line)?(?<ids>(?:\\s[^>]*?)?)\\s*-->",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        /// <summary>
        /// Scans <paramref name="text"/> as if it lived at <paramref name="path"/>, looking at
        /// nothing but the text itself.
        /// </summary>
        /// <param name="path">File path (used for the path rules and for reporting).</param>
        /// <param name="text">Raw HTML.</param>
        /// <returns>Zero or more findings, in source order.</returns>
        public static IReadOnlyList<Diagnostic> Analyze(string path, string text)
        {
            return Analyze(path, text, null);
        }

        /// <summary>
        /// Scans <paramref name="text"/> as if it lived at <paramref name="path"/>.
        /// </summary>
        /// <param name="path">File path (used for the path rules and for reporting).</param>
        /// <param name="text">Raw HTML.</param>
        /// <param name="readFile">
        /// Returns the text of another file, or null when it does not exist. With it the scanner
        /// also checks the extension's .ts sibling and the stock screen HTML, and honours
        /// <c>dotnet_diagnostic.AISI*.severity</c> in .editorconfig. Null keeps the scan to
        /// <paramref name="text"/> alone.
        /// </param>
        /// <returns>Zero or more findings, in source order.</returns>
        public static IReadOnlyList<Diagnostic> Analyze(string path, string text, Func<string, string?>? readFile)
        {
            if (path is null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (text is null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            var results = new List<Diagnostic>();
            var lineMap = new LineMap(text);

            TryPath0003(path, lineMap, results);
            TryPath0004(path, lineMap, results);

            string masked = MaskComments(text);
            IReadOnlyList<HtmlTag> tags = HtmlTagReader.Read(masked);
            int[] parents = HtmlTagReader.Parents(tags);

            Scan0001(path, tags, lineMap, results);
            Scan0002(path, tags, lineMap, results);
            Scan0005(path, masked, tags, lineMap, results);
            Scan0006(path, tags, lineMap, results);
            Scan0008(path, tags, parents, lineMap, results);
            Scan0012(path, tags, lineMap, results);
            Scan0013(path, tags, lineMap, results);

            // Stock screens are what they are; checking them against their own .ts is just noise.
            ScreenModel? screen = readFile == null || IsStockScreensPath(path) ? null : ScreenModel.Read(path, readFile);
            if (screen != null)
            {
                Scan0011(path, tags, parents, screen, lineMap, results);
            }

            if (IsExtensionFile(path))
            {
                StockScreen? stock = null;
                if (readFile != null)
                {
                    TryPath0007(path, readFile, lineMap, results);
                    stock = StockScreen.Read(path, readFile);
                    if (stock != null)
                    {
                        Scan0009(path, tags, stock, lineMap, results);
                    }
                }

                Scan0010(path, tags, parents, stock, lineMap, results);
            }

            RemoveSuppressed(text, lineMap, results);
            if (readFile != null)
            {
                ApplyConfiguredSeverities(path, readFile, results);
            }

            results.Sort(CompareDiagnostics);
            return results;
        }

        private static void RemoveSuppressed(string text, LineMap lineMap, List<Diagnostic> results)
        {
            if (results.Count == 0 || text.IndexOf("muilint-disable", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return;
            }

            foreach (Match match in SuppressionComment.Matches(text))
            {
                var ids = new HashSet<string>(
                    match.Groups["ids"].Value.Split(IdSeparators, StringSplitOptions.RemoveEmptyEntries),
                    StringComparer.OrdinalIgnoreCase);

                int onlyLine = 0;
                if (match.Groups["next"].Success)
                {
                    lineMap.ToLineCol(match.Index + match.Length, out int commentLine, out _);
                    onlyLine = commentLine + 1;
                }

                results.RemoveAll(d => (ids.Count == 0 || ids.Contains(d.Id)) && (onlyLine == 0 || d.Line == onlyLine));
            }
        }

        private static void ApplyConfiguredSeverities(string path, Func<string, string?> readFile, List<Diagnostic> results)
        {
            if (results.Count == 0)
            {
                return;
            }

            IReadOnlyDictionary<string, string> configured = EditorConfig.ReadSeverities(path, readFile);
            for (int i = results.Count - 1; i >= 0; i--)
            {
                if (!configured.TryGetValue(results[i].Id, out string? value))
                {
                    continue;
                }

                switch (value)
                {
                    case "error":
                        results[i] = results[i].WithSeverity(Severity.Error);
                        break;
                    case "warning":
                        results[i] = results[i].WithSeverity(Severity.Warning);
                        break;
                    case "suggestion":
                        results[i] = results[i].WithSeverity(Severity.Suggestion);
                        break;
                    case "silent":
                    case "none":
                        results.RemoveAt(i);
                        break;
                }
            }
        }

        private static int CompareDiagnostics(Diagnostic a, Diagnostic b)
        {
            int byStart = a.Start.CompareTo(b.Start);
            if (byStart != 0)
            {
                return byStart;
            }

            return string.CompareOrdinal(a.Id, b.Id);
        }

        private static void TryPath0003(string path, LineMap lineMap, List<Diagnostic> results)
        {
            if (!IsStockScreensPath(path))
            {
                return;
            }

            results.Add(Create(
                DiagnosticIds.StockScreensPath,
                "This file is under stock src/screens. Put Modern UI customizations in development/screens (or customizationScreens), not the stock tree.",
                path,
                0,
                0,
                lineMap));
        }

        private static void TryPath0004(string path, LineMap lineMap, List<Diagnostic> results)
        {
            if (!IsExtensionNamedAsParentScreen(path, out string fileName, out string parentFolder))
            {
                return;
            }

            string message = string.Format(
                CultureInfo.InvariantCulture,
                "Extension file '{0}' uses the parent screen folder name '{1}'. Name extensions with a postfix (for example {1}_Custom.html), not {1}.html.",
                fileName,
                parentFolder);

            results.Add(Create(DiagnosticIds.ExtensionBasename, message, path, 0, 0, lineMap));
        }

        private static void Scan0001(string path, IReadOnlyList<HtmlTag> tags, LineMap lineMap, List<Diagnostic> results)
        {
            for (int i = 0; i < tags.Count; i++)
            {
                HtmlTag tag = tags[i];
                if (tag.IsEndTag || !tag.SelfClosing)
                {
                    continue;
                }

                if (!IsFieldTag(tag.Name) && !IsQpTag(tag.Name))
                {
                    continue;
                }

                string message = string.Format(
                    CultureInfo.InvariantCulture,
                    "Self-closing <{0}> is not valid for Acumatica Modern UI merge. Use <{0} ...></{0}>.",
                    tag.Name);

                results.Add(Create(DiagnosticIds.SelfClosing, message, path, tag.Start, tag.End - tag.Start, lineMap));
            }
        }

        private static void Scan0002(string path, IReadOnlyList<HtmlTag> tags, LineMap lineMap, List<Diagnostic> results)
        {
            var namesInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
                    if (string.Equals(attr.Name, "name", StringComparison.OrdinalIgnoreCase)
                        && attr.Value.Length > 0)
                    {
                        namesInFile.Add(attr.Value);
                    }
                }
            }

            if (namesInFile.Count == 0)
            {
                return;
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
                    if (!IsAfterOrBefore(attr.Name))
                    {
                        continue;
                    }

                    MatchCollection matches = NameSelector.Matches(attr.Value);
                    for (int m = 0; m < matches.Count; m++)
                    {
                        Match match = matches[m];
                        string referenced = match.Groups["n"].Value;
                        if (referenced.Length == 0 || !namesInFile.Contains(referenced))
                        {
                            continue;
                        }

                        int spanStart = attr.ValueStart + match.Index;
                        int spanLength = match.Length;
                        string message = string.Format(
                            CultureInfo.InvariantCulture,
                            "after/before selector [name='{0}'] targets a name defined in this same file. HTML merge sees only stock HTML, so this selector will not match.",
                            referenced);

                        results.Add(Create(DiagnosticIds.AfterBeforeSameFile, message, path, spanStart, spanLength, lineMap));
                    }
                }
            }
        }

        private static void Scan0005(
            string path,
            string masked,
            IReadOnlyList<HtmlTag> tags,
            LineMap lineMap,
            List<Diagnostic> results)
        {
            for (int i = 0; i < tags.Count; i++)
            {
                HtmlTag tag = tags[i];
                if (tag.IsEndTag || tag.SelfClosing || !IsQpFieldset(tag.Name))
                {
                    continue;
                }

                if (HasMergeOperation(tag))
                {
                    continue;
                }

                int depth = 1;
                int closeIndex = -1;
                for (int j = i + 1; j < tags.Count; j++)
                {
                    HtmlTag other = tags[j];
                    if (!IsQpFieldset(other.Name))
                    {
                        continue;
                    }

                    if (other.IsEndTag)
                    {
                        depth--;
                        if (depth == 0)
                        {
                            closeIndex = j;
                            break;
                        }
                    }
                    else if (!other.SelfClosing)
                    {
                        depth++;
                    }
                }

                if (closeIndex < 0)
                {
                    continue;
                }

                int innerStart = tag.End;
                int innerEnd = tags[closeIndex].Start;
                if (innerEnd < innerStart)
                {
                    continue;
                }

                if (!IsWhitespaceOnly(masked, innerStart, innerEnd))
                {
                    continue;
                }

                results.Add(Create(
                    DiagnosticIds.EmptyFieldset,
                    "qp-fieldset is empty (whitespace or comments only).",
                    path,
                    tag.Start,
                    tag.End - tag.Start,
                    lineMap));
            }
        }

        private static void Scan0006(string path, IReadOnlyList<HtmlTag> tags, LineMap lineMap, List<Diagnostic> results)
        {
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
                    if (!IsMergeOperator(attr.Name))
                    {
                        continue;
                    }

                    string? problem = FindBracketProblem(attr.Value);
                    if (problem == null)
                    {
                        continue;
                    }

                    string message = string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}=\"{1}\" is not a valid selector: {2}. The merge will not match anything.",
                        attr.Name,
                        attr.Value,
                        problem);

                    results.Add(Create(DiagnosticIds.MalformedSelector, message, path, attr.ValueStart, attr.Value.Length, lineMap));
                }
            }
        }

        /// <summary>Checks brackets, braces, parentheses and quotes balance. Returns null when they do.</summary>
        internal static string? FindBracketProblem(string value)
        {
            var open = new Stack<char>();
            char quote = '\0';
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (quote != '\0')
                {
                    if (c == quote)
                    {
                        quote = '\0';
                    }

                    continue;
                }

                switch (c)
                {
                    case '\'':
                    case '"':
                        quote = c;
                        break;
                    case '[':
                    case '(':
                    case '{':
                        open.Push(c);
                        break;
                    case ']':
                    case ')':
                    case '}':
                        char expected = c == ']' ? '[' : c == ')' ? '(' : '{';
                        if (open.Count == 0 || open.Pop() != expected)
                        {
                            return "unexpected '" + c + "'";
                        }

                        break;
                }
            }

            if (quote != '\0')
            {
                return "unclosed " + quote + " quote";
            }

            if (open.Count > 0)
            {
                return "unclosed '" + open.Peek() + "'";
            }

            return null;
        }

        private static void Scan0008(
            string path,
            IReadOnlyList<HtmlTag> tags,
            int[] parents,
            LineMap lineMap,
            List<Diagnostic> results)
        {
            var ids = new Dictionary<string, HtmlTag>(StringComparer.Ordinal);
            var names = new Dictionary<string, HtmlTag>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < tags.Count; i++)
            {
                HtmlTag tag = tags[i];

                // modify/remove point at an existing element; their own name/id is not a new one.
                if (tag.IsEndTag || tag.HasAttribute("modify") || tag.HasAttribute("remove"))
                {
                    continue;
                }

                string id = tag.GetAttribute("id");
                if (id.Length > 0)
                {
                    if (ids.TryGetValue(id, out HtmlTag first))
                    {
                        string message = string.Format(
                            CultureInfo.InvariantCulture,
                            "id '{0}' is already used on line {1}. Selectors will only ever find the first one.",
                            id,
                            LineOf(first, lineMap));
                        results.Add(Create(DiagnosticIds.DuplicateNameOrId, message, path, tag.Start, tag.End - tag.Start, lineMap));
                    }
                    else
                    {
                        ids.Add(id, tag);
                    }
                }

                string name = tag.GetAttribute("name");
                if (name.Length == 0 || !IsFieldTag(tag.Name))
                {
                    continue;
                }

                // The same field can legitimately show up once per view, so key on the view.
                string scope = FieldScope(tags, parents, i, out string scopeLabel);
                if (names.TryGetValue(scope + "\n" + name, out HtmlTag firstField))
                {
                    string message = string.Format(
                        CultureInfo.InvariantCulture,
                        "Field '{0}' already appears {1} on line {2}.",
                        name,
                        scopeLabel,
                        LineOf(firstField, lineMap));
                    results.Add(Create(DiagnosticIds.DuplicateNameOrId, message, path, tag.Start, tag.End - tag.Start, lineMap));
                }
                else
                {
                    names.Add(scope + "\n" + name, tag);
                }
            }
        }

        private static void Scan0012(string path, IReadOnlyList<HtmlTag> tags, LineMap lineMap, List<Diagnostic> results)
        {
            foreach (HtmlTag tag in tags)
            {
                if (tag.IsEndTag
                    || !IsQpTag(tag.Name)
                    || tag.HasAttribute("id")
                    || tag.HasAttribute("modify")
                    || tag.HasAttribute("remove")
                    || IdOptional.Contains(tag.Name))
                {
                    continue;
                }

                string message = string.Format(
                    CultureInfo.InvariantCulture,
                    "<{0}> has no id, so other customizations cannot target it with #id and tests cannot find it.",
                    tag.Name);
                results.Add(Create(DiagnosticIds.QpControlWithoutId, message, path, tag.Start, tag.End - tag.Start, lineMap));
            }
        }

        private static void Scan0013(string path, IReadOnlyList<HtmlTag> tags, LineMap lineMap, List<Diagnostic> results)
        {
            foreach (HtmlTag tag in tags)
            {
                for (int a = 0; a < tag.Attributes.Count && !tag.IsEndTag; a++)
                {
                    HtmlAttribute attr = tag.Attributes[a];
                    string? problem = string.Equals(attr.Name, "config.bind", StringComparison.OrdinalIgnoreCase)
                        ? FindBracketProblem(attr.Value)
                        : null;
                    if (problem == null)
                    {
                        continue;
                    }

                    string message = string.Format(
                        CultureInfo.InvariantCulture,
                        "config.bind does not parse: {0}. The binding expression is broken, so the control cannot get its settings.",
                        problem);
                    results.Add(Create(DiagnosticIds.MalformedConfig, message, path, attr.ValueStart, attr.Value.Length, lineMap));
                }
            }
        }

        /// <summary>The view.bind of the nearest enclosing element, or empty.</summary>
        internal static string NearestView(IReadOnlyList<HtmlTag> tags, int[] parents, int index)
        {
            for (int p = parents[index]; p >= 0; p = parents[p])
            {
                string view = tags[p].GetAttribute("view.bind");
                if (view.Length > 0)
                {
                    return view;
                }
            }

            return string.Empty;
        }

        private static string FieldScope(IReadOnlyList<HtmlTag> tags, int[] parents, int index, out string label)
        {
            for (int p = parents[index]; p >= 0; p = parents[p])
            {
                string view = tags[p].GetAttribute("view.bind");
                if (view.Length > 0)
                {
                    label = "in view '" + view + "'";
                    return "view:" + view;
                }

                for (int a = 0; a < tags[p].Attributes.Count; a++)
                {
                    HtmlAttribute attr = tags[p].Attributes[a];
                    if (IsMergeOperator(attr.Name))
                    {
                        label = "under " + attr.Name + "=\"" + attr.Value + "\"";
                        return "merge:" + attr.Name + "=" + attr.Value;
                    }
                }
            }

            label = "in this file";
            return string.Empty;
        }

        private static int LineOf(HtmlTag tag, LineMap lineMap)
        {
            lineMap.ToLineCol(tag.Start, out int line, out _);
            return line;
        }

        internal static bool IsStockScreensPath(string path)
        {
            string normalized = NormalizePath(path);
            if (normalized.IndexOf("/customizationscreens/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            if (normalized.IndexOf("/development/screens/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            return normalized.IndexOf("/src/screens/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static bool IsExtensionNamedAsParentScreen(string path, out string fileName, out string parentFolder)
        {
            fileName = Path.GetFileName(path) ?? string.Empty;
            parentFolder = string.Empty;
            string normalized = NormalizePath(path);
            if (normalized.Length == 0)
            {
                return false;
            }

            string[] parts = normalized.Split('/');
            int extensionsIndex = -1;
            for (int i = 0; i < parts.Length; i++)
            {
                if (string.Equals(parts[i], "extensions", StringComparison.OrdinalIgnoreCase))
                {
                    extensionsIndex = i;
                    break;
                }
            }

            if (extensionsIndex <= 0 || extensionsIndex >= parts.Length - 1)
            {
                return false;
            }

            parentFolder = parts[extensionsIndex - 1];
            if (parentFolder.Length == 0)
            {
                return false;
            }

            string baseName = Path.GetFileNameWithoutExtension(fileName);
            return string.Equals(baseName, parentFolder, StringComparison.OrdinalIgnoreCase);
        }

        internal static string NormalizePath(string path)
        {
            return path.Replace('\\', '/');
        }

        internal static string MaskComments(string text)
        {
            char[] chars = text.ToCharArray();
            int i = 0;
            int n = chars.Length;
            while (i < n)
            {
                if (i + 3 < n
                    && chars[i] == '<'
                    && chars[i + 1] == '!'
                    && chars[i + 2] == '-'
                    && chars[i + 3] == '-')
                {
                    int start = i;
                    i += 4;
                    while (i + 2 < n
                           && !(chars[i] == '-' && chars[i + 1] == '-' && chars[i + 2] == '>'))
                    {
                        i++;
                    }

                    int end = i + 2 < n ? i + 3 : n;
                    for (int j = start; j < end; j++)
                    {
                        if (chars[j] != '\n' && chars[j] != '\r')
                        {
                            chars[j] = ' ';
                        }
                    }

                    i = end;
                }
                else
                {
                    i++;
                }
            }

            return new string(chars);
        }

        internal static bool IsFieldTag(string name)
        {
            return string.Equals(name, "field", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsQpTag(string name)
        {
            return name.Length > 3
                   && (name[0] == 'q' || name[0] == 'Q')
                   && (name[1] == 'p' || name[1] == 'P')
                   && name[2] == '-';
        }

        private static bool IsQpFieldset(string name)
        {
            return string.Equals(name, "qp-fieldset", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsMergeOperator(string name)
        {
            return MergeOperators.Contains(name);
        }

        private static bool IsAfterOrBefore(string name)
        {
            return string.Equals(name, "after", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "before", StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasMergeOperation(HtmlTag tag)
        {
            return tag.HasAttribute("modify") || tag.HasAttribute("remove") || tag.HasAttribute("replace");
        }

        private static bool IsWhitespaceOnly(string text, int start, int end)
        {
            for (int i = start; i < end; i++)
            {
                if (!char.IsWhiteSpace(text[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static Diagnostic Create(string id, string message, string path, int start, int length, LineMap lineMap)
        {
            if (start < 0)
            {
                start = 0;
            }

            if (length < 0)
            {
                length = 0;
            }

            int end = start + length;
            if (end < start)
            {
                end = start;
            }

            lineMap.ToLineCol(start, out int line, out int column);
            lineMap.ToLineCol(end, out int endLine, out int endColumn);
            Rule? rule = Rules.Find(id);
            Severity severity = rule == null ? Severity.Error : rule.DefaultSeverity;
            return new Diagnostic(id, message, path, start, length, line, column, endLine, endColumn, severity);
        }
    }
}
