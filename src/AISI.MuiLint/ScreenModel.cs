using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace AISI.MuiLint
{
    /// <summary>A place in a file, 1-based.</summary>
    internal readonly struct SourceLocation
    {
        public SourceLocation(string path, int line, int column)
        {
            Path = path;
            Line = line;
            Column = column;
        }

        public string Path { get; }

        public int Line { get; }

        public int Column { get; }
    }

    /// <summary>
    /// What a screen's TypeScript declares: its views, the fields of each view's class, and its
    /// other members (actions mostly). Read from the .ts next to the HTML and every module it
    /// imports, with extension interfaces (<c>interface SOLine_Ext extends SOLine {}</c>) merged
    /// into the class they extend. Anything we cannot follow is left unknown, never guessed.
    /// </summary>
    internal sealed class ScreenModel
    {
        private const int MaxModules = 64;

        private static readonly Regex ModuleSpecifier = new Regex(
            "\\b(?:from|import)\\s*(['\"])(?<spec>[^'\"\\r\\n]+)\\1",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex ClassHeader = new Regex(
            "\\bclass\\s+(?<name>[A-Za-z_$][\\w$]*)\\s*(?:<[^{]*?>)?\\s*(?:extends\\s+(?<base>[^{]*?))?\\s*(?:implements\\s+[^{]*)?\\{",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex InterfaceHeader = new Regex(
            "\\binterface\\s+(?<name>[A-Za-z_$][\\w$]*)\\s*(?:<[^{]*?>)?\\s*extends\\s+(?<bases>[^{]+)\\{",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex ViewInitializer = new Regex(
            "(?<name>[A-Za-z_$][\\w$]*)\\s*[?!]?\\s*(?::[^=;{}]*)?=\\s*create(?:Single|Collection)\\s*\\(\\s*(?<class>[A-Za-z_$][\\w$]*)",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex MemberStart = new Regex(
            "^(?:@[\\w$.]+\\s*(?:\\(\\s*\\))?\\s*)*(?:(?:public|private|protected|static|readonly|declare|override|abstract|async|get|set)\\s+)*(?<name>[A-Za-z_$][\\w$]*)\\s*[?!]?\\s*(?<op>[:=(<])\\s*(?<type>[\\w$]*)",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex GenericSuffix = new Regex("<.*>$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private readonly Dictionary<string, ClassDecl> _classes = new Dictionary<string, ClassDecl>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> _mergedInto = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, Dictionary<string, Member>?> _resolved = new Dictionary<string, Dictionary<string, Member>?>(StringComparer.Ordinal);
        private Dictionary<string, Member> _screen = new Dictionary<string, Member>(StringComparer.Ordinal);

        private ScreenModel()
        {
        }

        /// <summary>Name of the screen class, for example <c>SO301000</c>.</summary>
        public string ScreenClass { get; private set; } = string.Empty;

        /// <summary>View names, in declaration order.</summary>
        public IReadOnlyList<string> Views { get; private set; } = Array.Empty<string>();

        /// <summary>Members typed <c>PXActionState</c>.</summary>
        public IReadOnlyList<string> Actions { get; private set; } = Array.Empty<string>();

        /// <summary>
        /// Reads the screen behind <paramref name="htmlPath"/>, or returns null when there is no
        /// .ts beside it or no single screen class can be found in what it imports.
        /// </summary>
        public static ScreenModel? Read(string htmlPath, Func<string, string?> readFile)
        {
            string tsPath = HtmlMergeScanner.NormalizePath(Path.ChangeExtension(htmlPath, ".ts"));
            string? text = readFile(tsPath);
            if (text == null)
            {
                return null;
            }

            var model = new ScreenModel();
            var modules = new Queue<Module>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { tsPath };
            modules.Enqueue(new Module(tsPath, text));
            while (modules.Count > 0)
            {
                Module module = modules.Dequeue();
                model.Collect(module);
                foreach (string import in module.Imports)
                {
                    foreach (string candidate in Candidates(module.Path, import))
                    {
                        if (seen.Count >= MaxModules || !seen.Add(candidate))
                        {
                            continue;
                        }

                        string? imported = readFile(candidate);
                        if (imported != null)
                        {
                            modules.Enqueue(new Module(candidate, imported));
                            break;
                        }
                    }
                }
            }

            return model.FindScreen(ScreenIdFor(htmlPath)) ? model : null;
        }

        /// <summary>Gets a member of the screen class (a view, an action, anything declared).</summary>
        public bool TryGetMember(string name, out SourceLocation location)
        {
            location = default;
            if (!_screen.TryGetValue(name, out Member member))
            {
                return false;
            }

            location = member.Location;
            return true;
        }

        /// <summary>
        /// Field names of a view's class, or null when the view is unknown or its class could not
        /// be followed all the way to <c>PXView</c>.
        /// </summary>
        public ICollection<string>? FieldsOf(string view)
        {
            return FieldMembers(view)?.Keys;
        }

        /// <summary>The class a view is created from, or null for an unknown view.</summary>
        public string? ClassOf(string view)
        {
            return _screen.TryGetValue(view, out Member member) ? member.ViewClass : null;
        }

        /// <summary>Finds where a field is declared in <paramref name="view"/>'s class.</summary>
        public bool TryGetField(string view, string field, out SourceLocation location)
        {
            location = default;
            Dictionary<string, Member>? fields = FieldMembers(view);
            if (fields == null || !fields.TryGetValue(field, out Member member))
            {
                return false;
            }

            location = member.Location;
            return true;
        }

        /// <summary>
        /// Every field of every view, or null if any view's class is unknown. Used when the HTML
        /// does not say which view a field belongs to.
        /// </summary>
        public ICollection<string>? AllFields()
        {
            var all = new HashSet<string>(StringComparer.Ordinal);
            foreach (string view in Views)
            {
                ICollection<string>? fields = FieldsOf(view);
                if (fields == null)
                {
                    return null;
                }

                all.UnionWith(fields);
            }

            return all;
        }

        /// <summary>The first view whose class declares <paramref name="field"/>.</summary>
        public string? FindViewWithField(string field)
        {
            foreach (string view in Views)
            {
                if (FieldMembers(view)?.ContainsKey(field) == true)
                {
                    return view;
                }
            }

            return null;
        }

        private Dictionary<string, Member>? FieldMembers(string view)
        {
            return _screen.TryGetValue(view, out Member member) && member.ViewClass != null
                ? Resolve(member.ViewClass)
                : null;
        }

        private static string ScreenIdFor(string htmlPath)
        {
            string[] parts = HtmlMergeScanner.NormalizePath(htmlPath).Split('/');
            if (parts.Length >= 3 && string.Equals(parts[parts.Length - 2], "extensions", StringComparison.OrdinalIgnoreCase))
            {
                return parts[parts.Length - 3];
            }

            return Path.GetFileNameWithoutExtension(htmlPath);
        }

        private static IEnumerable<string> Candidates(string fromPath, string specifier)
        {
            string resolved;
            if (specifier.StartsWith("./", StringComparison.Ordinal) || specifier.StartsWith("../", StringComparison.Ordinal))
            {
                resolved = Combine(fromPath, specifier);
            }
            else if (specifier.StartsWith("src/", StringComparison.Ordinal))
            {
                // "src/screens/..." is rooted at the folder that holds src, i.e. FrontendSources/screen.
                int src = fromPath.LastIndexOf("/src/", StringComparison.OrdinalIgnoreCase);
                if (src < 0)
                {
                    yield break;
                }

                resolved = fromPath.Substring(0, src + 1) + specifier;
            }
            else
            {
                // client-controls and other packages: nothing of ours in there.
                yield break;
            }

            if (resolved.EndsWith(".ts", StringComparison.OrdinalIgnoreCase))
            {
                yield return resolved;
                yield break;
            }

            yield return resolved + ".ts";
            yield return resolved + "/index.ts";
        }

        internal static string Combine(string fromFile, string relative)
        {
            var parts = new List<string>(HtmlMergeScanner.NormalizePath(fromFile).Split('/'));
            parts.RemoveAt(parts.Count - 1);
            foreach (string part in HtmlMergeScanner.NormalizePath(relative).Split('/'))
            {
                if (part == "..")
                {
                    if (parts.Count > 0)
                    {
                        parts.RemoveAt(parts.Count - 1);
                    }
                }
                else if (part.Length > 0 && part != ".")
                {
                    parts.Add(part);
                }
            }

            return string.Join("/", parts);
        }

        private void Collect(Module module)
        {
            foreach (Match header in ClassHeader.Matches(module.Code))
            {
                string name = header.Groups["name"].Value;
                int open = header.Index + header.Length - 1;
                int close = MatchingBrace(module.Code, open);
                if (close < 0 || _classes.ContainsKey(name))
                {
                    continue;
                }

                var decl = new ClassDecl(BaseName(header.Groups["base"].Value));
                ReadMembers(module, open + 1, close, decl.Members);
                _classes.Add(name, decl);
            }

            foreach (Match header in InterfaceHeader.Matches(module.Code))
            {
                foreach (string raw in header.Groups["bases"].Value.Split(','))
                {
                    string target = BaseName(raw);
                    if (!_mergedInto.TryGetValue(target, out List<string>? sources))
                    {
                        sources = new List<string>();
                        _mergedInto.Add(target, sources);
                    }

                    sources.Add(header.Groups["name"].Value);
                }
            }
        }

        private static void ReadMembers(Module module, int start, int end, Dictionary<string, Member> members)
        {
            var views = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Match m in ViewInitializer.Matches(module.Code.Substring(start, end - start)))
            {
                views[m.Groups["name"].Value] = m.Groups["class"].Value;
            }

            // Look only at the class body's own level; decorator arguments, method bodies and
            // object literals are blanked so their contents never look like members.
            string flat = FlattenNested(module.Code, start, end);
            int segment = 0;
            for (int i = 0; i <= flat.Length; i++)
            {
                if (i < flat.Length && flat[i] != ';' && flat[i] != '\n')
                {
                    continue;
                }

                string text = flat.Substring(segment, i - segment);
                int lead = text.Length - text.TrimStart().Length;
                Match m = MemberStart.Match(text.Substring(lead));
                if (m.Success)
                {
                    string name = m.Groups["name"].Value;
                    if (!members.ContainsKey(name))
                    {
                        string type = m.Groups["op"].Value == ":" ? m.Groups["type"].Value : string.Empty;
                        views.TryGetValue(name, out string? viewClass);
                        int offset = start + segment + lead + m.Groups["name"].Index;
                        members.Add(name, new Member(type, viewClass, module.Locate(offset)));
                    }
                }

                segment = i + 1;
            }
        }

        private bool FindScreen(string screenId)
        {
            string? found = null;
            foreach (KeyValuePair<string, ClassDecl> c in _classes)
            {
                if (!ExtendsScreen(c.Key, 0))
                {
                    continue;
                }

                if (string.Equals(c.Key, screenId, StringComparison.OrdinalIgnoreCase))
                {
                    found = c.Key;
                    break;
                }

                found = found == null ? c.Key : string.Empty;
            }

            Dictionary<string, Member>? screen = string.IsNullOrEmpty(found) ? null : Resolve(found!);
            if (screen == null)
            {
                return false;
            }

            _screen = screen;
            ScreenClass = found!;
            var views = new List<string>();
            var actions = new List<string>();
            foreach (KeyValuePair<string, Member> member in screen)
            {
                if (member.Value.ViewClass != null)
                {
                    views.Add(member.Key);
                }
                else if (member.Value.Type == "PXActionState")
                {
                    actions.Add(member.Key);
                }
            }

            Views = views;
            Actions = actions;
            return true;
        }

        private bool ExtendsScreen(string name, int depth)
        {
            if (name == "PXScreen")
            {
                return true;
            }

            return depth < 16 && _classes.TryGetValue(name, out ClassDecl? decl) && ExtendsScreen(decl.Base, depth + 1);
        }

        /// <summary>Members of a class, its bases and its extensions; null if the chain breaks.</summary>
        private Dictionary<string, Member>? Resolve(string name)
        {
            if (_resolved.TryGetValue(name, out Dictionary<string, Member>? done))
            {
                return done;
            }

            // Guards against a class that (indirectly) extends itself.
            _resolved[name] = null;
            if (!_classes.TryGetValue(name, out ClassDecl? decl))
            {
                return null;
            }

            var members = new Dictionary<string, Member>(StringComparer.Ordinal);
            if (_mergedInto.TryGetValue(name, out List<string>? extensions))
            {
                foreach (string extension in extensions)
                {
                    if (_classes.TryGetValue(extension, out ClassDecl? ext))
                    {
                        AddMissing(members, ext.Members);
                    }
                }
            }

            AddMissing(members, decl.Members);
            if (decl.Base != "PXView" && decl.Base != "PXScreen")
            {
                Dictionary<string, Member>? inherited = Resolve(decl.Base);
                if (inherited == null)
                {
                    return null;
                }

                AddMissing(members, inherited);
            }

            _resolved[name] = members;
            return members;
        }

        private static void AddMissing(Dictionary<string, Member> into, Dictionary<string, Member> from)
        {
            foreach (KeyValuePair<string, Member> member in from)
            {
                if (!into.ContainsKey(member.Key))
                {
                    into.Add(member.Key, member.Value);
                }
            }
        }

        private static string BaseName(string raw)
        {
            string name = GenericSuffix.Replace(raw.Trim(), string.Empty).Trim();
            for (int i = 0; i < name.Length; i++)
            {
                if (!char.IsLetterOrDigit(name[i]) && name[i] != '_' && name[i] != '$')
                {
                    // mixin(...) or anything else we cannot follow.
                    return "?";
                }
            }

            return name.Length == 0 ? "?" : name;
        }

        private static int MatchingBrace(string code, int open)
        {
            int depth = 0;
            for (int i = open; i < code.Length; i++)
            {
                if (code[i] == '{')
                {
                    depth++;
                }
                else if (code[i] == '}' && --depth == 0)
                {
                    return i;
                }
            }

            return -1;
        }

        private static string FlattenNested(string code, int start, int end)
        {
            var sb = new StringBuilder(end - start);
            int depth = 0;
            for (int i = start; i < end; i++)
            {
                char c = code[i];
                bool opens = c == '{' || c == '(' || c == '[';
                bool closes = c == '}' || c == ')' || c == ']';
                if (closes && depth > 0)
                {
                    depth--;
                }

                sb.Append(depth > 0 && c != '\n' ? ' ' : c);
                if (opens)
                {
                    depth++;
                }
            }

            return sb.ToString();
        }

        /// <summary>Blanks comments and, unless <paramref name="keepStrings"/>, string contents.</summary>
        private static string Mask(string text, bool keepStrings)
        {
            char[] chars = text.ToCharArray();
            int i = 0;
            while (i < chars.Length)
            {
                char c = chars[i];
                int end;
                if (c == '/' && i + 1 < chars.Length && chars[i + 1] == '/')
                {
                    end = text.IndexOf('\n', i);
                    end = end < 0 ? chars.Length : end;
                }
                else if (c == '/' && i + 1 < chars.Length && chars[i + 1] == '*')
                {
                    end = text.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    end = end < 0 ? chars.Length : end + 2;
                }
                else if (c == '"' || c == '\'' || c == '`')
                {
                    end = i + 1;
                    while (end < chars.Length && chars[end] != c && (c == '`' || chars[end] != '\n'))
                    {
                        end += chars[end] == '\\' ? 2 : 1;
                    }

                    if (!keepStrings)
                    {
                        // Keep the quotes so the shape of the code survives.
                        Blank(chars, i + 1, Math.Min(end, chars.Length));
                    }

                    i = end + 1;
                    continue;
                }
                else
                {
                    i++;
                    continue;
                }

                Blank(chars, i, end);
                i = end;
            }

            return new string(chars);
        }

        private static void Blank(char[] chars, int start, int end)
        {
            for (int i = start; i < end; i++)
            {
                if (chars[i] != '\n')
                {
                    chars[i] = ' ';
                }
            }
        }

        private sealed class Module
        {
            private LineMap? _lines;

            public Module(string path, string text)
            {
                Path = path;
                Text = text;
                Code = Mask(text, keepStrings: false);

                var imports = new List<string>();
                foreach (Match m in ModuleSpecifier.Matches(Mask(text, keepStrings: true)))
                {
                    imports.Add(m.Groups["spec"].Value);
                }

                Imports = imports;
            }

            public string Path { get; }

            public string Text { get; }

            /// <summary>The text with comments and string contents blanked, offsets unchanged.</summary>
            public string Code { get; }

            public IReadOnlyList<string> Imports { get; }

            public SourceLocation Locate(int offset)
            {
                _lines ??= new LineMap(Text);
                _lines.ToLineCol(offset, out int line, out int column);
                return new SourceLocation(Path, line, column);
            }
        }

        private sealed class ClassDecl
        {
            public ClassDecl(string baseName)
            {
                Base = baseName;
            }

            public string Base { get; }

            public Dictionary<string, Member> Members { get; } = new Dictionary<string, Member>(StringComparer.Ordinal);
        }

        private readonly struct Member
        {
            public Member(string type, string? viewClass, SourceLocation location)
            {
                Type = type;
                ViewClass = viewClass;
                Location = location;
            }

            public string Type { get; }

            public string? ViewClass { get; }

            public SourceLocation Location { get; }
        }
    }
}
