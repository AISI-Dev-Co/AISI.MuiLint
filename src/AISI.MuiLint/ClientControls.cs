using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AISI.MuiLint
{
    /// <summary>
    /// What the site's own <c>client-controls</c> package (FrontendSources/screen/node_modules) says:
    /// the qp-* controls, the keys their config takes, and the screen templates. Read from its
    /// .d.ts files, so it matches the site's version of Acumatica.
    /// </summary>
    internal sealed class ClientControls
    {
        private static readonly ConcurrentDictionary<string, ClientControls?> Cache =
            new ConcurrentDictionary<string, ClientControls?>(StringComparer.OrdinalIgnoreCase);

        private static readonly char[] BodyChars = { '{', '}', ';' };

        private static readonly char[] NotASimpleType = { '|', '&', '{', '[', '(' };

        private static readonly string[] PackageFolders = { "node_modules/client-controls/", "node_modules/@acumatica/client-controls/" };

        private static readonly Regex CustomElement = new Regex(
            "@customElement\\(\\s*(?:\\{[^}]*?name\\s*:\\s*)?['\"](?<tag>qp-[\\w-]+)['\"]",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex ClassHeader = new Regex(
            "\\bclass\\s+(?<name>[A-Za-z_$][\\w$]*)",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex InterfaceHeader = new Regex(
            "\\binterface\\s+(?<name>[A-Za-z_$][\\w$]*)",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex Member = new Regex(
            "^\\s*(?:readonly\\s+)?(?<name>[A-Za-z_$][\\w$]*|'[^']*'|\"[^\"]*\")\\s*\\??\\s*(?<kind>[:(<])",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex Template = new Regex(
            "ScreenTemplates\\.set\\(\\s*([\"'`])(?<name>[^\"'`]+)\\1",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private ClientControls(Dictionary<string, ICollection<string>?> controls, ICollection<string>? templates)
        {
            Controls = controls;
            Templates = templates;
        }

        /// <summary>Gets the qp-* tags, each with its config keys, or null where they can't be worked out.</summary>
        public IReadOnlyDictionary<string, ICollection<string>?> Controls { get; }

        /// <summary>Gets the screen template names, or null when qp-template.js wasn't found.</summary>
        public ICollection<string>? Templates { get; }

        /// <summary>The package for the screen <paramref name="path"/> belongs to, or null when it isn't installed.</summary>
        public static ClientControls? For(string path, Func<string, string?> readFile, Func<string, IEnumerable<string>>? listFolder)
        {
            string normalized = HtmlMergeScanner.NormalizePath(path);
            int src = normalized.LastIndexOf("/src/", StringComparison.OrdinalIgnoreCase);
            if (src < 0 || listFolder == null)
            {
                return null;
            }

            foreach (string folder in PackageFolders)
            {
                string root = normalized.Substring(0, src + 1) + folder;
                ClientControls? found = Cache.GetOrAdd(root, r => Read(r, readFile, listFolder));
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        internal static ClientControls? Read(string root, Func<string, string?> readFile, Func<string, IEnumerable<string>> listFolder)
        {
            var interfaces = new Dictionary<string, (List<string> Keys, List<string> Bases, bool Open)>(StringComparer.Ordinal);
            var classes = new Dictionary<string, (string? Tag, string? Base, string? Config)>(StringComparer.Ordinal);
            int files = 0;
            foreach (string file in DeclarationFiles(root, listFolder))
            {
                string? text = readFile(file);
                if (text != null)
                {
                    files++;
                    Collect(TsModule.Mask(text, keepStrings: true), interfaces, classes);
                }
            }

            if (files == 0)
            {
                return null;
            }

            var resolved = new Dictionary<string, List<string>?>(StringComparer.Ordinal);
            var controls = new Dictionary<string, ICollection<string>?>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, (string? Tag, string? Base, string? Config)> c in classes)
            {
                if (c.Value.Tag != null && !controls.ContainsKey(c.Value.Tag))
                {
                    List<string>? keys = Keys(ConfigType(c.Key, classes), interfaces, resolved, new HashSet<string>());
                    controls[c.Value.Tag] = keys == null ? null : new HashSet<string>(keys, StringComparer.Ordinal);
                }
            }

            string? templateJs = readFile(root + "controls/container/template/qp-template.js");
            HashSet<string>? templates = null;
            if (templateJs != null)
            {
                templates = new HashSet<string>(StringComparer.Ordinal);
                foreach (Match m in Template.Matches(templateJs))
                {
                    templates.Add(m.Groups["name"].Value);
                }
            }

            return new ClientControls(controls, templates);
        }

        private static IEnumerable<string> DeclarationFiles(string folder, Func<string, IEnumerable<string>> listFolder)
        {
            var pending = new Stack<string>();
            pending.Push(folder.TrimEnd('/', '\\'));
            while (pending.Count > 0)
            {
                foreach (string entry in listFolder(pending.Pop()))
                {
                    string name = entry.Substring(HtmlMergeScanner.NormalizePath(entry).LastIndexOf('/') + 1);
                    if (name.EndsWith(".d.ts", StringComparison.OrdinalIgnoreCase))
                    {
                        yield return entry;
                    }
                    else if (name.IndexOf('.') < 0 && name != "node_modules")
                    {
                        pending.Push(entry);
                    }
                }
            }
        }

        private static void Collect(
            string code,
            Dictionary<string, (List<string> Keys, List<string> Bases, bool Open)> interfaces,
            Dictionary<string, (string? Tag, string? Base, string? Config)> classes)
        {
            foreach (Match header in InterfaceHeader.Matches(code))
            {
                int open = BodyStart(code, header.Index + header.Length, out string heritage);
                if (open < 0)
                {
                    continue;
                }

                var keys = new List<string>();
                bool isOpen = false;
                foreach (string member in Members(code, open))
                {
                    Match m = Member.Match(member);
                    if (m.Success)
                    {
                        keys.Add(m.Groups["name"].Value.Trim('\'', '"'));
                    }
                    else if (member.TrimStart().StartsWith("[", StringComparison.Ordinal))
                    {
                        // [key: string]: any — anything goes.
                        isOpen = true;
                    }
                }

                string name = header.Groups["name"].Value;
                List<string> bases = Bases(heritage, "extends");
                if (interfaces.TryGetValue(name, out (List<string> Keys, List<string> Bases, bool Open) existing))
                {
                    // Declaration merging across files.
                    existing.Keys.AddRange(keys);
                    existing.Bases.AddRange(bases);
                    interfaces[name] = (existing.Keys, existing.Bases, existing.Open || isOpen);
                }
                else
                {
                    interfaces[name] = (keys, bases, isOpen);
                }
            }

            foreach (Match header in ClassHeader.Matches(code))
            {
                int open = BodyStart(code, header.Index + header.Length, out string heritage);
                if (open < 0)
                {
                    continue;
                }

                string? config = null;
                foreach (string member in Members(code, open))
                {
                    Match m = Member.Match(member);
                    if (m.Success && m.Groups["name"].Value == "config" && m.Groups["kind"].Value == ":")
                    {
                        config = member.Substring(member.IndexOf(':') + 1).Trim();
                    }
                }

                // The decorator sits just before the class, with only other decorators in between.
                Match? tag = null;
                int window = Math.Max(0, header.Index - 400);
                string before = code.Substring(window, header.Index - window);
                foreach (Match candidate in CustomElement.Matches(before))
                {
                    string between = Regex.Replace(before.Substring(candidate.Index + candidate.Length), "^['\"]?\\s*\\}?\\s*\\)", string.Empty);
                    if (between.IndexOfAny(BodyChars) < 0)
                    {
                        tag = candidate;
                    }
                }

                List<string> baseClass = Bases(heritage, "extends");
                classes[header.Groups["name"].Value] = (tag?.Groups["tag"].Value, baseClass.Count > 0 ? baseClass[0] : null, config);
            }
        }

        // The class's own config type, or the nearest base class's.
        private static string? ConfigType(string className, Dictionary<string, (string? Tag, string? Base, string? Config)> classes)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (string? name = className; name != null && seen.Add(name) && classes.TryGetValue(name, out (string? Tag, string? Base, string? Config) c); name = c.Base)
            {
                if (c.Config != null)
                {
                    return c.Config;
                }
            }

            return null;
        }

        private static List<string>? Keys(
            string? type,
            Dictionary<string, (List<string> Keys, List<string> Bases, bool Open)> interfaces,
            Dictionary<string, List<string>?> resolved,
            HashSet<string> active)
        {
            string? name = SimpleName(type);
            if (name == null)
            {
                return null;
            }

            if (resolved.TryGetValue(name, out List<string>? done))
            {
                return done;
            }

            if (!interfaces.TryGetValue(name, out (List<string> Keys, List<string> Bases, bool Open) declared) || declared.Open || !active.Add(name))
            {
                return null;
            }

            var keys = new List<string>(declared.Keys);
            foreach (string baseType in declared.Bases)
            {
                // A base we can't see could add any key, so we can't say what's unknown.
                List<string>? inherited = Keys(baseType, interfaces, resolved, active);
                if (inherited == null)
                {
                    keys = null;
                    break;
                }

                keys.AddRange(inherited);
            }

            active.Remove(name);
            resolved[name] = keys;
            return keys;
        }

        // IGridConfig<PXView>, Partial<IFoo>, ns.IFoo → the interface's name; unions and literals → null.
        private static string? SimpleName(string? type)
        {
            if (type == null)
            {
                return null;
            }

            string t = type.Trim().TrimEnd(';').Trim();
            if (t.StartsWith("Partial<", StringComparison.Ordinal) || t.StartsWith("Readonly<", StringComparison.Ordinal))
            {
                t = t.Substring(t.IndexOf('<') + 1).TrimEnd('>');
            }

            if (t.IndexOfAny(NotASimpleType) >= 0)
            {
                return null;
            }

            int generic = t.IndexOf('<');
            if (generic >= 0)
            {
                t = t.Substring(0, generic);
            }

            t = t.Substring(t.LastIndexOf('.') + 1).Trim();
            return HtmlMergeScanner.IsIdentifier(t) ? t : null;
        }

        // From the end of "class X" / "interface X" to its '{', skipping generics; null-safe on odd input.
        private static int BodyStart(string code, int from, out string heritage)
        {
            int angle = 0;
            for (int i = from; i < code.Length; i++)
            {
                char c = code[i];
                if (c == '<')
                {
                    angle++;
                }
                else if (c == '>' && angle > 0)
                {
                    angle--;
                }
                else if (angle == 0 && (c == ';' || c == '='))
                {
                    break;
                }
                else if (c == '{' && angle == 0)
                {
                    heritage = code.Substring(from, i - from);
                    return i;
                }
            }

            heritage = string.Empty;
            return -1;
        }

        private static List<string> Bases(string heritage, string keyword)
        {
            // <TView extends PXView = PXView> is the type's own parameters, not what it extends.
            heritage = heritage.TrimStart();
            if (heritage.StartsWith("<", StringComparison.Ordinal))
            {
                int depth = 0;
                for (int i = 0; i < heritage.Length; i++)
                {
                    depth += heritage[i] == '<' ? 1 : heritage[i] == '>' ? -1 : 0;
                    if (depth == 0)
                    {
                        heritage = heritage.Substring(i + 1);
                        break;
                    }
                }
            }

            var bases = new List<string>();
            Match m = Regex.Match(heritage, "\\b" + keyword + "\\b(?<list>.*?)(?:\\bimplements\\b|$)", RegexOptions.Singleline);
            if (!m.Success)
            {
                return bases;
            }

            int angle = 0;
            int start = 0;
            string list = m.Groups["list"].Value;
            for (int i = 0; i <= list.Length; i++)
            {
                char c = i < list.Length ? list[i] : ',';
                angle += c == '<' ? 1 : c == '>' ? -1 : 0;
                if (c == ',' && angle == 0)
                {
                    string part = list.Substring(start, i - start).Trim();
                    if (part.Length > 0)
                    {
                        bases.Add(part);
                    }

                    start = i + 1;
                }
            }

            return bases;
        }

        // The top-level members of the body that opens at <paramref name="open"/>.
        private static IEnumerable<string> Members(string code, int open)
        {
            int close = TsModule.MatchingBrace(code, open);
            if (close < 0)
            {
                yield break;
            }

            int depth = 0;
            int start = open + 1;
            for (int i = open + 1; i < close; i++)
            {
                char c = code[i];
                if (c == '{' || c == '(' || c == '[' || c == '<')
                {
                    depth++;
                }
                else if ((c == '}' || c == ')' || c == ']' || c == '>') && depth > 0)
                {
                    depth--;
                }
                else if (depth == 0 && (c == ';' || c == '\n'))
                {
                    if (i > start)
                    {
                        yield return code.Substring(start, i - start);
                    }

                    start = i + 1;
                }
            }

            if (close > start)
            {
                yield return code.Substring(start, close - start);
            }
        }
    }
}
