using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace AISI.MuiLint
{
    /// <summary>Raw-text scanner for Acumatica Modern UI HTML that needs no Roslyn compilation and no site.</summary>
    public static partial class HtmlMergeScanner
    {
        internal static readonly Regex NameSelector = new Regex(
            "\\[name\\s*=\\s*(?:(['\"])(?<n>.*?)\\1|(?<n>[^\\s\\]]+))\\]",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly HashSet<string> MergeOperators = new HashSet<string>(
            new[] { "after", "before", "append", "prepend", "modify", "remove", "replace" },
            StringComparer.OrdinalIgnoreCase);

        private static readonly char[] IdSeparators = { ' ', '\t', '\r', '\n', ',' };

        // Controls Acumatica's own screens never give an id.
        private static readonly HashSet<string> IdOptional = new HashSet<string>(
            new[]
            {
                "qp-field", "qp-data-component", "qp-data-components", "qp-label", "qp-include", "qp-informer-rack",
                "qp-longrun-indicator", "qp-nested-screen", "qp-screen-configuration-menu", "qp-translation-validation",
                "qp-wait-cursor", "qp-wiki-tooltip", "qp-address-lookup", "qp-hyper-icon", "qp-caption",
            },
            StringComparer.OrdinalIgnoreCase);

        private static readonly Regex ConfigId = new Regex("[{,]\\s*['\"]?id['\"]?\\s*:", RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex SuppressionComment = new Regex(
            "<!--\\s*muilint-disable(?<next>-next-line)?(?<ids>(?:\\s[^>]*?)?)\\s*-->",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        /// <summary>Scans <paramref name="text"/> as if it lived at <paramref name="path"/>, looking at nothing but the text itself.</summary>
        public static IReadOnlyList<Diagnostic> Analyze(string path, string text)
        {
            return Analyze(path, text, null);
        }

        /// <summary>Scans <paramref name="text"/> as if it lived at <paramref name="path"/>, also checking the screen's .ts, stock HTML, other extensions and .editorconfig through <paramref name="readFile"/> and <paramref name="listFolder"/>.</summary>
        public static IReadOnlyList<Diagnostic> Analyze(
            string path,
            string text,
            Func<string, string?>? readFile,
            Func<string, IEnumerable<string>>? listFolder = null)
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
            TryPath0017(path, lineMap, results);

            Scan0001(path, tags, lineMap, results);
            Scan0005(path, masked, tags, lineMap, results);
            Scan0006(path, tags, lineMap, results);
            Scan0008(path, tags, parents, lineMap, results);
            Scan0012(path, tags, lineMap, results);
            Scan0013(path, tags, lineMap, results);
            Scan0018(path, tags, parents, lineMap, results);
            ScanUnbalancedTags(path, tags, lineMap, results);
            if (readFile != null)
            {
                ScanIncludes(path, tags, readFile, lineMap, results);
                ClientControls? controls = ClientControls.For(path, readFile, listFolder);
                if (controls != null)
                {
                    ScanClientControls(path, tags, controls, lineMap, results);
                }
            }

            // Stock screens are what they are; checking them against their own .ts is just noise.
            ScreenModel? screen = readFile == null || IsStockScreensPath(path) ? null : ScreenModel.Read(path, readFile, listFolder: listFolder);
            if (screen != null)
            {
                Scan0011(path, tags, parents, screen, lineMap, results);
            }

            StockScreen? stock = readFile != null && IsExtensionFile(path) ? StockScreen.Read(path, readFile, listFolder: listFolder) : null;
            if (stock != null)
            {
                Scan0009(path, tags, stock, lineMap, results);
            }

            RemoveSuppressed(text, SuppressionComment, lineMap, results);
            if (readFile != null)
            {
                ApplyConfiguredSeverities(path, readFile, results);
            }

            results.Sort(CompareDiagnostics);
            return results;
        }

        internal static void RemoveSuppressed(string text, Regex comments, LineMap lineMap, List<Diagnostic> results)
        {
            if (results.Count == 0 || text.IndexOf("muilint-disable", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return;
            }

            foreach (Match match in comments.Matches(text))
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

        internal static void ApplyConfiguredSeverities(string path, Func<string, string?> readFile, List<Diagnostic> results)
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

        internal static int CompareDiagnostics(Diagnostic a, Diagnostic b)
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
                "This file is under src/screens, which holds Acumatica's own screen sources. Put customizations in src/development/screens: that's where a customization project picks them up, and publishing leaves them alone.",
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
                "Extension file '{0}' is named after the screen itself. Acumatica names extensions <ScreenID>_<postfix>, for example {1}_Custom.html.",
                fileName,
                parentFolder);

            results.Add(Create(DiagnosticIds.ExtensionBasename, message, path, 0, 0, lineMap));
        }

        /// <summary>AISI0017 for HTML and TypeScript alike, judged by the ScreenID_postfix name since screens reusing another through qp-include carry merge attributes too.</summary>
        internal static void TryPath0017(string path, LineMap lineMap, List<Diagnostic> results)
        {
            string normalized = NormalizePath(path);
            bool customTree = normalized.IndexOf("/development/screens/", StringComparison.OrdinalIgnoreCase) >= 0
                || normalized.IndexOf("/customizationScreens/", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!customTree || IsExtensionFile(path))
            {
                return;
            }

            string fileName = Path.GetFileName(normalized);
            string folder = Path.GetFileName(Path.GetDirectoryName(normalized) ?? string.Empty);
            if (!fileName.StartsWith(folder + "_", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string message = string.Format(
                CultureInfo.InvariantCulture,
                "{0} is named like an extension of {1}, but it isn't in an extensions folder. Acumatica keeps a screen's extensions in {1}/extensions/.",
                fileName,
                folder);
            results.Add(Create(DiagnosticIds.ExtensionOutsideExtensions, message, path, 0, 0, lineMap));
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
                    "<{0}/> can't be self-closing: HTML only allows that on a few standard tags, so whatever follows ends up inside it. Use <{0} ...></{0}>.",
                    tag.Name);

                results.Add(Create(DiagnosticIds.SelfClosing, message, path, tag.Start, tag.End - tag.Start, lineMap));
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

                // Stock leaves fieldsets empty on purpose: hidden ones, wg-containers filled at run time,
                // and ones later elements append to.
                if (HasMergeOperation(tag)
                    || tag.HasAttribute("wg-container")
                    || (" " + tag.GetAttribute("class") + " ").IndexOf(" hidden ", StringComparison.OrdinalIgnoreCase) >= 0
                    || IsTargeted(tags, tag.GetAttribute("id")))
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

                if (!IsWhitespaceOnly(masked, tag.End, tags[closeIndex].Start))
                {
                    continue;
                }

                results.Add(Create(
                    DiagnosticIds.EmptyFieldset,
                    "qp-fieldset has nothing in it, and nothing in this file adds to it.",
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
                        "{0}=\"{1}\" is not a valid CSS selector: {2}. The Modern UI build fails on a selector that doesn't match exactly one element.",
                        attr.Name,
                        attr.Value,
                        problem);

                    results.Add(Create(DiagnosticIds.MalformedSelector, message, path, attr.ValueStart, attr.Value.Length, lineMap));
                }
            }
        }

        /// <summary>Describes the first unbalanced bracket, brace, parenthesis or quote, or null when they all balance.</summary>
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

                // Stock repeats ids like btnOK in every dialog; only twice in one container is a slip.
                string scope = parents[i].ToString(CultureInfo.InvariantCulture);
                string id = tag.GetAttribute("id");
                if (id.Length > 0)
                {
                    if (ids.TryGetValue(scope + "\n" + id, out HtmlTag first))
                    {
                        string message = string.Format(
                            CultureInfo.InvariantCulture,
                            "id '{0}' is already used in this container on line {1}. #{0} matches both, and the build fails on a selector that matches more than one element.",
                            id,
                            LineOf(first, lineMap));
                        results.Add(Create(DiagnosticIds.DuplicateNameOrId, message, path, tag.Start, tag.End - tag.Start, lineMap));
                    }
                    else
                    {
                        ids.Add(scope + "\n" + id, tag);
                    }
                }

                string name = tag.GetAttribute("name");
                if (name.Length == 0 || !IsFieldTag(tag.Name))
                {
                    continue;
                }

                // Stock shows the same field twice in a view on purpose (AP301000's CuryTaxTotal), but
                // never twice in one container.
                if (names.TryGetValue(scope + "\n" + name, out HtmlTag firstField))
                {
                    string message = string.Format(
                        CultureInfo.InvariantCulture,
                        "Field '{0}' already appears in this container on line {1}.",
                        name,
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
                    || HasConfigId(tag.GetAttribute("config.bind"))
                    || tag.HasAttribute("modify")
                    || tag.HasAttribute("remove")
                    || IdOptional.Contains(tag.Name))
                {
                    continue;
                }

                string message = string.Format(
                    CultureInfo.InvariantCulture,
                    "<{0}> has no id, so customizations can't target it with #id.",
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
                        "config.bind is not a valid expression: {0}.",
                        problem);
                    results.Add(Create(DiagnosticIds.MalformedConfig, message, path, attr.ValueStart, attr.Value.Length, lineMap));
                }
            }
        }

        /// <summary>The view around a tag: a view.bind, a qp-panel's id, or a &lt;using view&gt;.</summary>
        internal static string NearestView(IReadOnlyList<HtmlTag> tags, int[] parents, int index)
        {
            for (int p = parents[index]; p >= 0; p = parents[p])
            {
                HtmlTag tag = tags[p];
                string view = tag.GetAttribute("view.bind");
                if (view.Length == 0 && string.Equals(tag.Name, "qp-panel", StringComparison.OrdinalIgnoreCase))
                {
                    view = tag.GetAttribute("id");
                }

                if (view.Length == 0 && string.Equals(tag.Name, "using", StringComparison.OrdinalIgnoreCase))
                {
                    view = tag.GetAttribute("view");
                }

                if (view.Length > 0)
                {
                    return view;
                }
            }

            return string.Empty;
        }

        private static void Scan0018(string path, IReadOnlyList<HtmlTag> tags, int[] parents, LineMap lineMap, List<Diagnostic> results)
        {
            for (int i = 0; i < tags.Count; i++)
            {
                HtmlTag tag = tags[i];
                string? attribute = tag.IsEndTag ? null : FirstMergeAttribute(tag);
                int parent = parents[i];
                if (attribute == null || parent < 0 || (IsTemplate(tags[parent]) && parents[parent] < 0))
                {
                    continue;
                }

                bool inInclude = false;
                for (int p = parent; p >= 0 && !inInclude; p = parents[p])
                {
                    inInclude = string.Equals(tags[p].Name, "qp-include", StringComparison.OrdinalIgnoreCase);
                }

                if (inInclude)
                {
                    continue;
                }

                string message = string.Format(
                    CultureInfo.InvariantCulture,
                    "<{0} {1}=...> is inside <{2}>. Tags that customize the original HTML have to be directly in the top-level <template>.",
                    tag.Name,
                    attribute,
                    tags[parent].Name);
                results.Add(Create(DiagnosticIds.MergeTagNotAtTopLevel, message, path, tag.Start, tag.End - tag.Start, lineMap));
            }
        }

        private static string? FirstMergeAttribute(HtmlTag tag)
        {
            foreach (HtmlAttribute attr in tag.Attributes)
            {
                if (IsMergeOperator(attr.Name))
                {
                    return attr.Name;
                }
            }

            return null;
        }

        private static bool IsTemplate(HtmlTag tag)
        {
            return string.Equals(tag.Name, "template", StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasConfigId(string config)
        {
            // Not an object literal (a property or a call) can carry an id we can't see.
            return config.Length > 0 && (!config.TrimStart().StartsWith("{", StringComparison.Ordinal) || ConfigId.IsMatch(config));
        }

        private static bool IsTargeted(IReadOnlyList<HtmlTag> tags, string id)
        {
            if (id.Length == 0)
            {
                return false;
            }

            foreach (HtmlTag tag in tags)
            {
                foreach (HtmlAttribute attr in tag.Attributes)
                {
                    if (IsMergeOperator(attr.Name) && Regex.IsMatch(attr.Value, "#" + Regex.Escape(id) + "(?![\\w-])"))
                    {
                        return true;
                    }
                }
            }

            return false;
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
            string[] parts = NormalizePath(path).Split('/');
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

        internal static Diagnostic Create(string id, string message, string path, int start, int length, LineMap lineMap)
        {
            if (start < 0)
            {
                start = 0;
            }

            if (length < 0)
            {
                length = 0;
            }

            lineMap.ToLineCol(start, out int line, out int column);
            lineMap.ToLineCol(start + length, out int endLine, out int endColumn);
            Rule? rule = Rules.Find(id);
            Severity severity = rule == null ? Severity.Error : rule.DefaultSeverity;
            return new Diagnostic(id, message, path, start, length, line, column, endLine, endColumn, severity);
        }
    }
}
