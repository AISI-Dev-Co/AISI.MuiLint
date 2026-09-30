using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace AISI.MuiLint
{
    /// <summary>
    /// Just enough .editorconfig to read <c>dotnet_diagnostic.&lt;id&gt;.severity</c>, the same
    /// keys Roslyn uses, so one setting drives the analyzer, the CLI and the VSIX alike.
    /// </summary>
    internal static class EditorConfig
    {
        private const string KeyPrefix = "dotnet_diagnostic.";
        private const string KeySuffix = ".severity";

        private static readonly char[] CommentStarts = { '#', ';' };

        /// <summary>Diagnostic id to lower-cased severity word, for the file at <paramref name="path"/>.</summary>
        public static IReadOnlyDictionary<string, string> ReadSeverities(string path, Func<string, string?> readFile)
        {
            string file = HtmlMergeScanner.NormalizePath(path);

            // Nearest first; stop at root = true.
            var configs = new List<KeyValuePair<string, string>>();
            string? directory = Path.GetDirectoryName(file);
            while (!string.IsNullOrEmpty(directory))
            {
                string dir = HtmlMergeScanner.NormalizePath(directory!);
                string? text = readFile(dir.TrimEnd('/') + "/.editorconfig");
                if (text != null)
                {
                    configs.Add(new KeyValuePair<string, string>(dir, text));
                    if (IsRoot(text))
                    {
                        break;
                    }
                }

                directory = Path.GetDirectoryName(directory);
            }

            var severities = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = configs.Count - 1; i >= 0; i--)
            {
                string relative = file.Substring(configs[i].Key.TrimEnd('/').Length).TrimStart('/');
                Apply(configs[i].Value, relative, severities);
            }

            return severities;
        }

        private static bool IsRoot(string text)
        {
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.StartsWith("[", StringComparison.Ordinal))
                {
                    return false;
                }

                if (TrySplit(line, out string key, out string value)
                    && string.Equals(key, "root", StringComparison.OrdinalIgnoreCase))
                {
                    return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
                }
            }

            return false;
        }

        private static void Apply(string text, string relativePath, Dictionary<string, string> severities)
        {
            bool inMatchingSection = false;
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#' || line[0] == ';')
                {
                    continue;
                }

                if (line[0] == '[' && line[line.Length - 1] == ']')
                {
                    inMatchingSection = Matches(line.Substring(1, line.Length - 2), relativePath);
                    continue;
                }

                if (!inMatchingSection || !TrySplit(line, out string key, out string value))
                {
                    continue;
                }

                if (key.StartsWith(KeyPrefix, StringComparison.OrdinalIgnoreCase)
                    && key.EndsWith(KeySuffix, StringComparison.OrdinalIgnoreCase)
                    && key.Length > KeyPrefix.Length + KeySuffix.Length)
                {
                    // Trailing comments are not in the spec, but people write them anyway.
                    int comment = value.IndexOfAny(CommentStarts);
                    string id = key.Substring(KeyPrefix.Length, key.Length - KeyPrefix.Length - KeySuffix.Length);
                    severities[id] = (comment < 0 ? value : value.Substring(0, comment)).Trim().ToLowerInvariant();
                }
            }
        }

        private static bool TrySplit(string line, out string key, out string value)
        {
            int eq = line.IndexOf('=');
            key = eq < 0 ? string.Empty : line.Substring(0, eq).Trim();
            value = eq < 0 ? string.Empty : line.Substring(eq + 1).Trim();
            return eq > 0;
        }

        internal static bool Matches(string glob, string relativePath)
        {
            // Per the spec, a glob without a slash matches the file name at any depth.
            if (glob.IndexOf('/') < 0)
            {
                glob = "**/" + glob;
            }
            else if (glob[0] == '/')
            {
                glob = glob.Substring(1);
            }

            string pattern = "^" + Translate(glob) + "$";
            return Regex.IsMatch(relativePath, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        private static string Translate(string glob)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < glob.Length; i++)
            {
                char c = glob[i];
                switch (c)
                {
                    case '*':
                        if (i + 1 < glob.Length && glob[i + 1] == '*')
                        {
                            i++;
                            if (i + 1 < glob.Length && glob[i + 1] == '/')
                            {
                                i++;
                                sb.Append("(?:.*/)?");
                            }
                            else
                            {
                                sb.Append(".*");
                            }
                        }
                        else
                        {
                            sb.Append("[^/]*");
                        }

                        break;
                    case '?':
                        sb.Append("[^/]");
                        break;
                    case '{':
                        int close = glob.IndexOf('}', i);
                        if (close < 0)
                        {
                            sb.Append("\\{");
                            break;
                        }

                        string[] options = glob.Substring(i + 1, close - i - 1).Split(',');
                        sb.Append("(?:");
                        for (int o = 0; o < options.Length; o++)
                        {
                            sb.Append(o == 0 ? string.Empty : "|").Append(Translate(options[o]));
                        }

                        sb.Append(')');
                        i = close;
                        break;
                    case '[':
                        int end = glob.IndexOf(']', i + 1);
                        if (end < 0)
                        {
                            sb.Append("\\[");
                            break;
                        }

                        string set = glob.Substring(i + 1, end - i - 1);
                        sb.Append('[').Append(set.StartsWith("!", StringComparison.Ordinal) ? "^" + set.Substring(1) : set).Append(']');
                        i = end;
                        break;
                    case '\\':
                        if (i + 1 < glob.Length)
                        {
                            i++;
                            sb.Append(Regex.Escape(glob[i].ToString()));
                        }

                        break;
                    default:
                        sb.Append(Regex.Escape(c.ToString()));
                        break;
                }
            }

            return sb.ToString();
        }
    }
}
