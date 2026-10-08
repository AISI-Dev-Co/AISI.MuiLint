using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace AISI.MuiLint
{
    /// <summary>
    /// One TypeScript file, read just far enough for Modern UI: its imports, its classes with
    /// their members, and its <c>interface X extends Y</c> declarations. Comments and strings are
    /// blanked first so nothing inside them looks like code.
    /// </summary>
    internal sealed class TsModule
    {
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

        private LineMap? _lines;

        public TsModule(string path, string text)
        {
            Path = path;
            Text = text;
            Code = Mask(text, keepStrings: false);
            CodeWithStrings = Mask(text, keepStrings: true);

            var imports = new List<string>();
            foreach (Match m in ModuleSpecifier.Matches(CodeWithStrings))
            {
                imports.Add(m.Groups["spec"].Value);
            }

            Imports = imports;

            var classes = new List<TsClass>();
            foreach (Match header in ClassHeader.Matches(Code))
            {
                int open = header.Index + header.Length - 1;
                int close = MatchingBrace(Code, open);
                if (close >= 0)
                {
                    Group name = header.Groups["name"];
                    classes.Add(new TsClass(name.Value, BaseName(header.Groups["base"].Value), name.Index, close, ReadMembers(open + 1, close)));
                }
            }

            Classes = classes;

            var interfaces = new List<TsInterface>();
            foreach (Match header in InterfaceHeader.Matches(Code))
            {
                var bases = new List<string>();
                foreach (string raw in header.Groups["bases"].Value.Split(','))
                {
                    bases.Add(BaseName(raw));
                }

                Group name = header.Groups["name"];
                int open = header.Index + header.Length - 1;
                int close = MatchingBrace(Code, open);
                bool empty = close > open && Code.Substring(open + 1, close - open - 1).Trim().Length == 0;
                interfaces.Add(new TsInterface(name.Value, bases, name.Index, empty));
            }

            Interfaces = interfaces;
        }

        public string Path { get; }

        public string Text { get; }

        /// <summary>The text with comments and string contents blanked, offsets unchanged.</summary>
        public string Code { get; }

        /// <summary>The text with only comments blanked.</summary>
        public string CodeWithStrings { get; }

        public IReadOnlyList<string> Imports { get; }

        public IReadOnlyList<TsClass> Classes { get; }

        public IReadOnlyList<TsInterface> Interfaces { get; }

        public SourceLocation Locate(int offset)
        {
            _lines ??= new LineMap(Text);
            _lines.ToLineCol(offset, out int line, out int column);
            return new SourceLocation(Path, line, column);
        }

        /// <summary>The class called <paramref name="name"/> in this file, or null.</summary>
        public TsClass? FindClass(string name)
        {
            foreach (TsClass c in Classes)
            {
                if (c.Name == name)
                {
                    return c;
                }
            }

            return null;
        }

        /// <summary>
        /// The base class named in an extends clause: empty when there is none, <c>?</c> when it
        /// is something we cannot follow, such as <c>mixin(...)</c>.
        /// </summary>
        private static string BaseName(string raw)
        {
            string name = GenericSuffix.Replace(raw.Trim(), string.Empty).Trim();
            foreach (char c in name)
            {
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '$')
                {
                    return "?";
                }
            }

            return name;
        }

        private Dictionary<string, TsMember> ReadMembers(int start, int end)
        {
            var members = new Dictionary<string, TsMember>(StringComparer.Ordinal);
            var views = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Match m in ViewInitializer.Matches(Code.Substring(start, end - start)))
            {
                views[m.Groups["name"].Value] = m.Groups["class"].Value;
            }

            // Look only at the class body's own level; decorator arguments, method bodies and
            // object literals are blanked so their contents never look like members.
            string flat = FlattenNested(Code, start, end);
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
                if (m.Success && !members.ContainsKey(m.Groups["name"].Value))
                {
                    string name = m.Groups["name"].Value;
                    string type = m.Groups["op"].Value == ":" ? m.Groups["type"].Value : string.Empty;
                    views.TryGetValue(name, out string? viewClass);
                    int offset = start + segment + lead + m.Groups["name"].Index;
                    members.Add(name, new TsMember(type, viewClass, offset, Locate(offset)));
                }

                segment = i + 1;
            }

            return members;
        }

        internal static int MatchingBrace(string code, int open)
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

        /// <summary>
        /// Blanks comments and, unless <paramref name="keepStrings"/>, string contents. Close enough
        /// for C# too, which is how <see cref="DacFields"/> uses it.
        /// </summary>
        internal static string Mask(string text, bool keepStrings)
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
    }

    internal sealed class TsClass
    {
        public TsClass(string name, string baseName, int nameStart, int closingBrace, Dictionary<string, TsMember> members)
        {
            Name = name;
            Base = baseName;
            NameStart = nameStart;
            ClosingBrace = closingBrace;
            Members = members;
        }

        public string Name { get; }

        /// <summary>See <see cref="TsModule"/>: empty for no extends clause, <c>?</c> for one we can't follow.</summary>
        public string Base { get; }

        public int NameStart { get; }

        public int ClosingBrace { get; }

        public Dictionary<string, TsMember> Members { get; }
    }

    internal sealed class TsInterface
    {
        public TsInterface(string name, IReadOnlyList<string> bases, int nameStart, bool empty)
        {
            Name = name;
            Bases = bases;
            NameStart = nameStart;
            Empty = empty;
        }

        public string Name { get; }

        public IReadOnlyList<string> Bases { get; }

        public int NameStart { get; }

        /// <summary>Gets a value indicating whether the body is <c>{}</c>, as an extension's interface is.</summary>
        public bool Empty { get; }
    }

    internal readonly struct TsMember
    {
        public TsMember(string type, string? viewClass, int offset, SourceLocation location)
        {
            Type = type;
            ViewClass = viewClass;
            Offset = offset;
            Location = location;
        }

        /// <summary>The declared type's name, for example <c>PXFieldState</c>; empty for initialisers and methods.</summary>
        public string Type { get; }

        /// <summary>For <c>X = createSingle(SOLine)</c>, <c>SOLine</c>.</summary>
        public string? ViewClass { get; }

        /// <summary>Where the member's name starts in its file.</summary>
        public int Offset { get; }

        public SourceLocation Location { get; }
    }
}
