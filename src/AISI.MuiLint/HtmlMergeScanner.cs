using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace AISI.MuiLint
{
    /// <summary>
    /// Independent raw-HTML text scanner for Acumatica Modern UI merge traps. No Roslyn
    /// compilation, no view.bind analysis, no PX types.
    /// </summary>
    public static partial class HtmlMergeScanner
    {
        private static readonly Regex NameSelector = new Regex(
            "\\[name\\s*=\\s*(?:(['\"])(?<n>.*?)\\1|(?<n>[^\\s\\]]+))\\]",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        /// <summary>
        /// Scans <paramref name="text"/> as if it lived at <paramref name="path"/>.
        /// Path-based rules use <paramref name="path"/> only; tag-based rules use the text.
        /// </summary>
        /// <param name="path">File path (used for AISI0003 and AISI0004, and for reporting).</param>
        /// <param name="text">Raw HTML.</param>
        /// <returns>Zero or more findings, in source order.</returns>
        public static IReadOnlyList<Diagnostic> Analyze(string path, string text)
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

            Scan0001(path, tags, lineMap, results);
            Scan0002(path, tags, lineMap, results);
            Scan0005(path, masked, tags, lineMap, results);

            results.Sort(CompareDiagnostics);
            return results;
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

        private static bool IsFieldTag(string name)
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

        private static bool IsAfterOrBefore(string name)
        {
            return string.Equals(name, "after", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "before", StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasMergeOperation(HtmlTag tag)
        {
            for (int i = 0; i < tag.Attributes.Count; i++)
            {
                string name = tag.Attributes[i].Name;
                if (string.Equals(name, "modify", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(name, "remove", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(name, "replace", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
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
            return new Diagnostic(id, message, path, start, length, line, column, endLine, endColumn);
        }
    }
}
