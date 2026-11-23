using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace AISI.MuiLint
{
    /// <summary>A member to add to a class in a .ts file: into the class when it is ours, through an extension of it when it isn't.</summary>
    internal sealed class TsDeclaration
    {
        private readonly string _what;

        public TsDeclaration(string tsPath, string member, IReadOnlyList<string> lines, IReadOnlyList<string> imports, string what, string owner, string ownerModule, bool inOwner)
        {
            TsPath = tsPath;
            Member = member;
            Lines = lines;
            Imports = imports;
            _what = what;
            Owner = owner;
            OwnerModule = ownerModule;
            InOwner = inOwner;
        }

        public string TsPath { get; }

        /// <summary>The member's name: a field, or a handler's method.</summary>
        public string Member { get; }

        /// <summary>The member's source, one entry per line, not indented.</summary>
        public IReadOnlyList<string> Lines { get; }

        /// <summary>What the member needs from <c>client-controls</c>.</summary>
        public IReadOnlyList<string> Imports { get; }

        public string Owner { get; }

        /// <summary>Import specifier for <see cref="Owner"/>, such as <c>src/screens/SO/SO301000/SO301000</c>.</summary>
        public string OwnerModule { get; }

        /// <summary>True when the owner is ours and lives in <see cref="TsPath"/>; false to go through an extension.</summary>
        public bool InOwner { get; }

        /// <summary>The class the member ends up in.</summary>
        public string TargetClass
        {
            get
            {
                if (InOwner)
                {
                    return Owner;
                }

                string file = Path.GetFileNameWithoutExtension(TsPath);
                return Owner + "_" + file.Substring(file.IndexOf('_') + 1);
            }
        }

        /// <summary>The lightbulb's text.</summary>
        public string Title
        {
            get { return _what + " in " + TargetClass + " (" + Path.GetFileName(TsPath) + ")"; }
        }
    }

    /// <summary>Quick fixes that write TypeScript rather than HTML.</summary>
    internal static class TypeScriptFixes
    {
        private static readonly Regex NamedImport = new Regex(
            "\\bimport\\s*\\{(?<names>[^}]*)\\}\\s*from\\s*(?<q>['\"])(?<spec>[^'\"\\r\\n]+)\\k<q>\\s*;?",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly string[] FieldImports = { "PXFieldState" };
        private static readonly string[] HandlerImports = { "handleEvent", "CustomEventType", "ValueChangedHandlerArgs" };

        /// <summary>Works out where the field behind an AISI0011 finding should be declared, or null when no view class can take it.</summary>
        public static TsDeclaration? PlanFieldDeclaration(string htmlPath, string htmlText, Diagnostic diagnostic, Func<string, string?> readFile, Func<string, IEnumerable<string>>? listFolder = null)
        {
            (ScreenModel Screen, string View, string Field)? found = FieldAt(htmlPath, htmlText, diagnostic.Start, diagnostic.Start, readFile, listFolder);
            if (found == null)
            {
                return null;
            }

            (ScreenModel screen, string view, string field) = found.Value;
            return Plan(htmlPath, screen, screen.ClassOf(view), field, new[] { field + ": PXFieldState;" }, FieldImports, "Declare " + field);
        }

        /// <summary>Plans a ValueChanged handler for the first field tag the range touches, or null when the field isn't declared, is already handled, or the screen isn't ours to write to.</summary>
        public static TsDeclaration? PlanValueChangedHandler(string htmlPath, string htmlText, int start, int end, Func<string, string?> readFile, Func<string, IEnumerable<string>>? listFolder = null)
        {
            (ScreenModel Screen, string View, string Field)? found = FieldAt(htmlPath, htmlText, start, end, readFile, listFolder);
            if (found == null)
            {
                return null;
            }

            (ScreenModel screen, string view, string field) = found.Value;
            if (!screen.TryGetField(view, field, out _) || screen.HandlesValueChanged(view, field))
            {
                return null;
            }

            // The plain name may already belong to a handler for the same field of another view.
            string method = "on" + field + "Changed";
            if (screen.TryGetMember(method, out _))
            {
                method = "on" + view + field + "Changed";
            }

            if (screen.TryGetMember(method, out _))
            {
                return null;
            }

            string[] lines =
            {
                "@handleEvent(CustomEventType.ValueChanged, { view: \"" + view + "\", field: \"" + field + "\" })",
                method + "(args: ValueChangedHandlerArgs) {",
                "}",
            };
            return Plan(htmlPath, screen, screen.ScreenClass, method, lines, HandlerImports, "Handle changes to " + field);
        }

        /// <summary>The edits that add <see cref="TsDeclaration.Member"/> to <paramref name="tsText"/>, last first.</summary>
        public static IReadOnlyList<TextEdit> Declare(string tsText, TsDeclaration plan)
        {
            TsModule module = new TsModule(plan.TsPath, tsText);
            string newLine = tsText.IndexOf("\r\n", StringComparison.Ordinal) >= 0 ? "\r\n" : "\n";
            string indent = "    ";
            foreach (string line in tsText.Split('\n'))
            {
                if (line.Trim().Length > 0 && (line[0] == '\t' || line[0] == ' '))
                {
                    indent = line[0] == '\t' ? "\t" : line.Substring(0, line.Length - line.TrimStart(' ').Length);
                    break;
                }
            }

            string member = indent + string.Join(newLine + indent, plan.Lines);
            List<TextEdit> edits = new List<TextEdit>();
            List<string> imports = new List<string>();

            TsClass? target = plan.InOwner ? module.FindClass(plan.Owner) : null;
            for (int i = 0; target == null && !plan.InOwner && i < module.Interfaces.Count; i++)
            {
                TsInterface extension = module.Interfaces[i];
                target = extension.Bases.Contains(plan.Owner) ? module.FindClass(extension.Name) : null;
            }

            if (target != null)
            {
                if (target.Members.ContainsKey(plan.Member))
                {
                    return edits;
                }

                // A method stands a blank line clear of the members above it.
                if (plan.Lines.Count > 1 && target.Members.Count > 0)
                {
                    member = newLine + member;
                }

                // "class X {}" on one line opens up; otherwise the member goes on its own line above the "}".
                int lineStart = tsText.LastIndexOf('\n', target.ClosingBrace - 1) + 1;
                bool braceAlone = tsText.Substring(lineStart, target.ClosingBrace - lineStart).Trim().Length == 0;
                edits.Add(braceAlone
                    ? new TextEdit(lineStart, 0, member + newLine)
                    : new TextEdit(target.ClosingBrace, 0, newLine + member + newLine));
            }
            else if (plan.InOwner)
            {
                return edits;
            }
            else
            {
                string name = plan.TargetClass;
                string pair =
                    newLine +
                    "export interface " + name + " extends " + plan.Owner + " {}" + newLine +
                    "export class " + name + " {" + newLine +
                    member + newLine +
                    "}" + newLine;
                string body = tsText.TrimEnd();
                edits.Add(new TextEdit(body.Length, tsText.Length - body.Length, newLine + pair));
                AddImports(module, new[] { plan.Owner }, plan.OwnerModule, edits, imports);
            }

            AddImports(module, plan.Imports, "client-controls", edits, imports);
            if (imports.Count > 0)
            {
                // New import lines go after the last import, or at the top.
                int after = 0;
                foreach (Match import in NamedImport.Matches(module.CodeWithStrings))
                {
                    after = import.Index + import.Length;
                }

                string lines = string.Join(newLine, imports);
                edits.Add(after == 0 ? new TextEdit(0, 0, lines + newLine) : new TextEdit(after, 0, newLine + lines));
            }

            edits.Sort((a, b) => b.Start.CompareTo(a.Start));
            return edits;
        }

        /// <summary>The first field tag the range touches, with its screen and its view (empty when none can be worked out), or null.</summary>
        private static (ScreenModel Screen, string View, string Field)? FieldAt(string htmlPath, string htmlText, int start, int end, Func<string, string?> readFile, Func<string, IEnumerable<string>>? listFolder)
        {
            IReadOnlyList<HtmlTag> tags = HtmlTagReader.Read(HtmlMergeScanner.MaskComments(htmlText));
            for (int i = 0; i < tags.Count; i++)
            {
                HtmlTag tag = tags[i];
                if (tag.IsEndTag || !HtmlMergeScanner.IsFieldTag(tag.Name) || end < tag.Start || start > tag.End)
                {
                    continue;
                }

                ScreenModel? screen = ScreenModel.Read(htmlPath, readFile, listFolder: listFolder);
                string name = tag.GetAttribute("name");
                if (screen == null || name.Length == 0)
                {
                    return null;
                }

                string view = HtmlMergeScanner.NearestView(tags, HtmlTagReader.Parents(tags), i);
                int dot = name.IndexOf('.');
                if (dot > 0)
                {
                    view = name.Substring(0, dot);
                    name = name.Substring(dot + 1);
                }

                // A field merged in with after/before and no view around it belongs to the view its anchor is on.
                for (int a = 0; view.Length == 0 && a < tag.Attributes.Count; a++)
                {
                    MatchCollection? anchors = HtmlMergeScanner.IsMergeOperator(tag.Attributes[a].Name) ? HtmlMergeScanner.NameSelector.Matches(tag.Attributes[a].Value) : null;
                    if (anchors != null && anchors.Count > 0)
                    {
                        view = screen.FindViewWithField(anchors[anchors.Count - 1].Groups["n"].Value) ?? string.Empty;
                        break;
                    }
                }

                return (screen, view, name);
            }

            return null;
        }

        /// <summary>Where <paramref name="member"/> can be written: into <paramref name="owner"/> in the HTML's own .ts, or into an extension of it there.</summary>
        private static TsDeclaration? Plan(string htmlPath, ScreenModel screen, string? owner, string member, string[] lines, string[] imports, string what)
        {
            string? ownerFile = owner == null ? null : screen.FileOf(owner);
            if (owner == null || ownerFile == null)
            {
                return null;
            }

            string tsPath = HtmlMergeScanner.NormalizePath(Path.ChangeExtension(htmlPath, ".ts"));
            if (string.Equals(ownerFile, tsPath, StringComparison.OrdinalIgnoreCase) && !HtmlMergeScanner.IsStockScreensPath(tsPath))
            {
                return new TsDeclaration(tsPath, member, lines, imports, what, owner, string.Empty, inOwner: true);
            }

            // Someone else's class: only an extension can add to it, importing it by its path from src.
            string specifier = HtmlMergeScanner.NormalizePath(Path.ChangeExtension(ownerFile, null));
            int src = specifier.LastIndexOf("/src/", StringComparison.OrdinalIgnoreCase);
            if (!HtmlMergeScanner.IsExtensionFile(htmlPath) || src < 0)
            {
                return null;
            }

            return new TsDeclaration(tsPath, member, lines, imports, what, owner, specifier.Substring(src + 1), inOwner: false);
        }

        /// <summary>Adds the names not yet imported to an import from the same module, or queues a new import line.</summary>
        private static void AddImports(TsModule module, IEnumerable<string> names, string specifier, List<TextEdit> edits, List<string> newImports)
        {
            List<string> missing = new List<string>(names);
            Match? sameModule = null;
            foreach (Match import in NamedImport.Matches(module.CodeWithStrings))
            {
                foreach (string imported in import.Groups["names"].Value.Split(','))
                {
                    string local = imported.Trim();
                    int alias = local.IndexOf(" as ", StringComparison.Ordinal);
                    missing.Remove(alias >= 0 ? local.Substring(alias + 4).Trim() : local);
                }

                if (import.Groups["spec"].Value == specifier)
                {
                    sameModule = import;
                }
            }

            if (missing.Count == 0)
            {
                return;
            }

            string added = string.Join(", ", missing);
            if (sameModule != null)
            {
                Group existing = sameModule.Groups["names"];
                string trimmed = existing.Value.TrimEnd();
                string separator = trimmed.EndsWith(",", StringComparison.Ordinal) ? " " : ", ";
                edits.Add(new TextEdit(existing.Index + trimmed.Length, 0, separator + added));
                return;
            }

            newImports.Add("import { " + added + " } from \"" + specifier + "\";");
        }
    }
}
