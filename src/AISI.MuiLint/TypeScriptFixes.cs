using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace AISI.MuiLint
{
    /// <summary>Where and how to declare a field the HTML uses but the TypeScript doesn't (AISI0011).</summary>
    internal sealed class FieldDeclaration
    {
        public FieldDeclaration(string tsPath, string field, string viewClass, string viewClassModule, bool inViewClass)
        {
            TsPath = tsPath;
            Field = field;
            ViewClass = viewClass;
            ViewClassModule = viewClassModule;
            InViewClass = inViewClass;
        }

        /// <summary>The .ts to edit.</summary>
        public string TsPath { get; }

        public string Field { get; }

        public string ViewClass { get; }

        /// <summary>Import specifier for <see cref="ViewClass"/>, e.g. <c>src/screens/SO/SO301000/SO301000</c>.</summary>
        public string ViewClassModule { get; }

        /// <summary>True when the view class is ours and lives in <see cref="TsPath"/>; false to go through an extension.</summary>
        public bool InViewClass { get; }

        /// <summary>The class the field ends up in, for the lightbulb's title.</summary>
        public string TargetClass
        {
            get { return InViewClass ? ViewClass : ViewClass + "_" + ExtensionSuffix(TsPath); }
        }

        internal static string ExtensionSuffix(string tsPath)
        {
            string name = Path.GetFileNameWithoutExtension(tsPath);
            int underscore = name.IndexOf('_');
            return underscore >= 0 ? name.Substring(underscore + 1) : name;
        }
    }

    /// <summary>Quick fixes that write TypeScript rather than HTML.</summary>
    internal static class TypeScriptFixes
    {
        private static readonly Regex NamedImport = new Regex(
            "\\bimport\\s*\\{(?<names>[^}]*)\\}\\s*from\\s*(?<q>['\"])(?<spec>[^'\"\\r\\n]+)\\k<q>\\s*;?",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        /// <summary>
        /// Works out where the field behind an AISI0011 "not declared on view" finding should go,
        /// or returns null when we can't say (no view, a view class we can't find, a stock screen).
        /// </summary>
        public static FieldDeclaration? PlanFieldDeclaration(string htmlPath, string htmlText, Diagnostic diagnostic, Func<string, string?> readFile, Func<string, IEnumerable<string>>? listFolder = null)
        {
            IReadOnlyList<HtmlTag> tags = HtmlTagReader.Read(HtmlMergeScanner.MaskComments(htmlText));
            for (int i = 0; i < tags.Count; i++)
            {
                HtmlTag tag = tags[i];
                if (tag.IsEndTag || !HtmlMergeScanner.IsFieldTag(tag.Name) || diagnostic.Start < tag.Start || diagnostic.Start > tag.End)
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

                if (view.Length == 0)
                {
                    view = ViewOfAnchor(tag, screen) ?? string.Empty;
                }

                return Plan(htmlPath, name, view, screen);
            }

            return null;
        }

        /// <summary>The edits that declare <see cref="FieldDeclaration.Field"/> in <paramref name="tsText"/>, last first.</summary>
        public static IReadOnlyList<TextEdit> DeclareField(string tsText, FieldDeclaration plan)
        {
            var module = new TsModule(plan.TsPath, tsText);
            string newLine = tsText.IndexOf("\r\n", StringComparison.Ordinal) >= 0 ? "\r\n" : "\n";
            string indent = DetectIndent(tsText);
            var edits = new List<TextEdit>();
            var imports = new List<string>();

            TsClass? target = plan.InViewClass ? module.FindClass(plan.ViewClass) : FindExtensionOf(module, plan.ViewClass);
            if (target != null)
            {
                if (target.Members.ContainsKey(plan.Field))
                {
                    return edits;
                }

                edits.Add(InsertMember(tsText, target, indent + plan.Field + ": PXFieldState;", newLine));
            }
            else if (plan.InViewClass)
            {
                return edits;
            }
            else
            {
                string name = plan.TargetClass;
                string pair =
                    newLine +
                    "export interface " + name + " extends " + plan.ViewClass + " {}" + newLine +
                    "export class " + name + " {" + newLine +
                    indent + plan.Field + ": PXFieldState;" + newLine +
                    "}" + newLine;
                string body = tsText.TrimEnd();
                edits.Add(new TextEdit(body.Length, tsText.Length - body.Length, newLine + pair));
                AddImport(module, plan.ViewClass, plan.ViewClassModule, edits, imports);
            }

            AddImport(module, "PXFieldState", "client-controls", edits, imports);
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

        private static FieldDeclaration? Plan(string htmlPath, string field, string view, ScreenModel screen)
        {
            string? viewClass = view.Length == 0 ? null : screen.ClassOf(view);
            string? classFile = viewClass == null ? null : screen.FileOf(viewClass);
            if (viewClass == null || classFile == null)
            {
                return null;
            }

            string tsPath = HtmlMergeScanner.NormalizePath(Path.ChangeExtension(htmlPath, ".ts"));
            if (string.Equals(classFile, tsPath, StringComparison.OrdinalIgnoreCase) && !HtmlMergeScanner.IsStockScreensPath(tsPath))
            {
                return new FieldDeclaration(tsPath, field, viewClass, string.Empty, inViewClass: true);
            }

            // Someone else's class: only an extension can add to it.
            string? specifier = SrcSpecifier(classFile);
            if (!HtmlMergeScanner.IsExtensionFile(htmlPath) || specifier == null)
            {
                return null;
            }

            return new FieldDeclaration(tsPath, field, viewClass, specifier, inViewClass: false);
        }

        /// <summary>For a field merged in with after/before and no view around it, the view its anchor is on.</summary>
        private static string? ViewOfAnchor(HtmlTag tag, ScreenModel screen)
        {
            foreach (HtmlAttribute attr in tag.Attributes)
            {
                if (!HtmlMergeScanner.IsMergeOperator(attr.Name))
                {
                    continue;
                }

                MatchCollection names = HtmlMergeScanner.NameSelector.Matches(attr.Value);
                if (names.Count > 0)
                {
                    return screen.FindViewWithField(names[names.Count - 1].Groups["n"].Value);
                }
            }

            return null;
        }

        private static TsClass? FindExtensionOf(TsModule module, string viewClass)
        {
            foreach (TsInterface i in module.Interfaces)
            {
                foreach (string baseName in i.Bases)
                {
                    TsClass? c = baseName == viewClass ? module.FindClass(i.Name) : null;
                    if (c != null)
                    {
                        return c;
                    }
                }
            }

            return null;
        }

        private static TextEdit InsertMember(string text, TsClass target, string member, string newLine)
        {
            // "class X {}" on one line opens up; otherwise the member goes on its own line above the "}".
            int lineStart = text.LastIndexOf('\n', target.ClosingBrace - 1) + 1;
            bool braceAlone = text.Substring(lineStart, target.ClosingBrace - lineStart).Trim().Length == 0;
            return braceAlone
                ? new TextEdit(lineStart, 0, member + newLine)
                : new TextEdit(target.ClosingBrace, 0, newLine + member + newLine);
        }

        /// <summary>Adds <paramref name="name"/> to an import from the same module, or queues a new import line.</summary>
        private static void AddImport(TsModule module, string name, string specifier, List<TextEdit> edits, List<string> newImports)
        {
            Match? sameModule = null;
            foreach (Match import in NamedImport.Matches(module.CodeWithStrings))
            {
                foreach (string imported in import.Groups["names"].Value.Split(','))
                {
                    string local = imported.Trim();
                    int alias = local.IndexOf(" as ", StringComparison.Ordinal);
                    if ((alias >= 0 ? local.Substring(alias + 4).Trim() : local) == name)
                    {
                        return;
                    }
                }

                if (import.Groups["spec"].Value == specifier)
                {
                    sameModule = import;
                }
            }

            if (sameModule != null)
            {
                Group names = sameModule.Groups["names"];
                string trimmed = names.Value.TrimEnd();
                string separator = trimmed.TrimEnd().EndsWith(",", StringComparison.Ordinal) ? " " : ", ";
                edits.Add(new TextEdit(names.Index + trimmed.Length, 0, separator + name));
                return;
            }

            newImports.Add("import { " + name + " } from \"" + specifier + "\";");
        }

        private static string DetectIndent(string text)
        {
            foreach (string line in text.Split('\n'))
            {
                if (line.StartsWith("\t", StringComparison.Ordinal) && line.Trim().Length > 0)
                {
                    return "\t";
                }

                if (line.StartsWith(" ", StringComparison.Ordinal) && line.Trim().Length > 0)
                {
                    return line.Substring(0, line.Length - line.TrimStart(' ').Length);
                }
            }

            return "    ";
        }

        /// <summary><c>…/src/screens/SO/SO301000/SO301000.ts</c> → <c>src/screens/SO/SO301000/SO301000</c>.</summary>
        private static string? SrcSpecifier(string file)
        {
            string path = HtmlMergeScanner.NormalizePath(file);
            int src = path.LastIndexOf("/src/", StringComparison.OrdinalIgnoreCase);
            if (src < 0)
            {
                return null;
            }

            string specifier = path.Substring(src + 1);
            int dot = specifier.LastIndexOf('.');
            return dot > specifier.LastIndexOf('/') ? specifier.Substring(0, dot) : specifier;
        }
    }
}
