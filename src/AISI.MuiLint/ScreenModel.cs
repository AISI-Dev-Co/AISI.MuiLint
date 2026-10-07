using System;
using System.Collections.Generic;
using System.IO;

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
        private const int MaxModules = 128;

        private readonly Dictionary<string, (TsClass Class, TsModule Module)> _classes = new Dictionary<string, (TsClass, TsModule)>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> _mergedInto = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, Dictionary<string, TsMember>?> _resolved = new Dictionary<string, Dictionary<string, TsMember>?>(StringComparer.Ordinal);
        private Dictionary<string, TsMember> _screen = new Dictionary<string, TsMember>(StringComparer.Ordinal);

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
        /// Reads the screen behind <paramref name="path"/> (an HTML file or its .ts), or returns
        /// null when there is no .ts or no single screen class can be found in what it reads.
        /// </summary>
        /// <param name="path">The HTML or .ts being checked.</param>
        /// <param name="readFile">Returns a file's text, or null.</param>
        /// <param name="tsText">The .ts itself when it's already in hand, such as an editor buffer with unsaved changes.</param>
        /// <param name="listFolder">
        /// Lists what's directly inside a folder. With it, every extension of the screen is read too,
        /// wherever it lives (see <see cref="HtmlMergeScanner.ScreenExtensions"/>).
        /// </param>
        public static ScreenModel? Read(string path, Func<string, string?> readFile, string? tsText = null, Func<string, IEnumerable<string>>? listFolder = null)
        {
            string tsPath = HtmlMergeScanner.NormalizePath(Path.ChangeExtension(path, ".ts"));
            string? text = tsText ?? readFile(tsPath);
            if (text == null)
            {
                return null;
            }

            var model = new ScreenModel();
            var modules = new Queue<TsModule>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { tsPath };
            modules.Enqueue(new TsModule(tsPath, text));
            foreach (string extension in HtmlMergeScanner.ScreenExtensions(tsPath, ".ts", listFolder))
            {
                string? extensionText = seen.Count < MaxModules && seen.Add(extension) ? readFile(extension) : null;
                if (extensionText != null)
                {
                    modules.Enqueue(new TsModule(extension, extensionText));
                }
            }

            while (modules.Count > 0)
            {
                TsModule module = modules.Dequeue();
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
                            modules.Enqueue(new TsModule(candidate, imported));
                            break;
                        }
                    }
                }
            }

            return model.FindScreen(ScreenIdFor(path)) ? model : null;
        }

        /// <summary>Gets a member of the screen class (a view, an action, anything declared).</summary>
        public bool TryGetMember(string name, out SourceLocation location)
        {
            location = default;
            if (!_screen.TryGetValue(name, out TsMember member))
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
            return _screen.TryGetValue(view, out TsMember member) ? member.ViewClass : null;
        }

        /// <summary>The file that declares <paramref name="className"/>, or null when we never saw it.</summary>
        public string? FileOf(string className)
        {
            return _classes.TryGetValue(className, out (TsClass Class, TsModule Module) found) ? found.Module.Path : null;
        }

        /// <summary>Finds where a field is declared in <paramref name="view"/>'s class.</summary>
        public bool TryGetField(string view, string field, out SourceLocation location)
        {
            location = default;
            Dictionary<string, TsMember>? fields = FieldMembers(view);
            if (fields == null || !fields.TryGetValue(field, out TsMember member))
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

        /// <summary>
        /// Whether <paramref name="className"/> ends up at <c>PXView</c>: false when its chain ends at
        /// a class with no base (or at <c>PXScreen</c>), null when it runs into something we can't see.
        /// </summary>
        public bool? IsView(string className)
        {
            string name = className;
            for (int depth = 0; depth < 16; depth++)
            {
                if (name == "PXView")
                {
                    return true;
                }

                if (name == "PXScreen" || name.Length == 0)
                {
                    return false;
                }

                if (!_classes.TryGetValue(name, out (TsClass Class, TsModule Module) found))
                {
                    return null;
                }

                name = found.Class.Base;
            }

            return null;
        }

        private Dictionary<string, TsMember>? FieldMembers(string view)
        {
            return _screen.TryGetValue(view, out TsMember member) && member.ViewClass != null
                ? Resolve(member.ViewClass)
                : null;
        }

        private static string ScreenIdFor(string path)
        {
            string[] parts = HtmlMergeScanner.NormalizePath(path).Split('/');
            if (parts.Length >= 3 && string.Equals(parts[parts.Length - 2], "extensions", StringComparison.OrdinalIgnoreCase))
            {
                return parts[parts.Length - 3];
            }

            return Path.GetFileNameWithoutExtension(path);
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

        private void Collect(TsModule module)
        {
            foreach (TsClass c in module.Classes)
            {
                if (!_classes.ContainsKey(c.Name))
                {
                    _classes.Add(c.Name, (c, module));
                }
            }

            foreach (TsInterface i in module.Interfaces)
            {
                foreach (string target in i.Bases)
                {
                    if (!_mergedInto.TryGetValue(target, out List<string>? sources))
                    {
                        sources = new List<string>();
                        _mergedInto.Add(target, sources);
                    }

                    sources.Add(i.Name);
                }
            }
        }

        private bool FindScreen(string screenId)
        {
            string? found = null;
            foreach (string name in _classes.Keys)
            {
                if (!ExtendsScreen(name, 0))
                {
                    continue;
                }

                if (string.Equals(name, screenId, StringComparison.OrdinalIgnoreCase))
                {
                    found = name;
                    break;
                }

                found = found == null ? name : string.Empty;
            }

            Dictionary<string, TsMember>? screen = string.IsNullOrEmpty(found) ? null : Resolve(found!);
            if (screen == null)
            {
                return false;
            }

            _screen = screen;
            ScreenClass = found!;
            var views = new List<string>();
            var actions = new List<string>();
            foreach (KeyValuePair<string, TsMember> member in screen)
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

            return depth < 16 && _classes.TryGetValue(name, out (TsClass Class, TsModule Module) found) && ExtendsScreen(found.Class.Base, depth + 1);
        }

        /// <summary>Members of a class, its bases and its extensions; null if the chain breaks.</summary>
        private Dictionary<string, TsMember>? Resolve(string name)
        {
            if (_resolved.TryGetValue(name, out Dictionary<string, TsMember>? done))
            {
                return done;
            }

            // Guards against a class that (indirectly) extends itself.
            _resolved[name] = null;
            if (!_classes.TryGetValue(name, out (TsClass Class, TsModule Module) found))
            {
                return null;
            }

            var members = new Dictionary<string, TsMember>(StringComparer.Ordinal);
            if (_mergedInto.TryGetValue(name, out List<string>? extensions))
            {
                foreach (string extension in extensions)
                {
                    if (_classes.TryGetValue(extension, out (TsClass Class, TsModule Module) ext))
                    {
                        AddMissing(members, ext.Class.Members);
                    }
                }
            }

            AddMissing(members, found.Class.Members);
            string baseName = found.Class.Base;
            if (baseName != "PXView" && baseName != "PXScreen")
            {
                Dictionary<string, TsMember>? inherited = Resolve(baseName);
                if (inherited == null)
                {
                    return null;
                }

                AddMissing(members, inherited);
            }

            _resolved[name] = members;
            return members;
        }

        private static void AddMissing(Dictionary<string, TsMember> into, Dictionary<string, TsMember> from)
        {
            foreach (KeyValuePair<string, TsMember> member in from)
            {
                if (!into.ContainsKey(member.Key))
                {
                    into.Add(member.Key, member.Value);
                }
            }
        }
    }
}
