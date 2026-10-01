using System;

namespace AISI.MuiLint
{
    /// <summary>Replace <see cref="Length"/> characters at <see cref="Start"/> with <see cref="NewText"/>.</summary>
    public readonly struct TextEdit
    {
        /// <summary>Creates an edit.</summary>
        public TextEdit(int start, int length, string newText)
        {
            Start = start;
            Length = length;
            NewText = newText;
        }

        /// <summary>Gets the 0-based offset.</summary>
        public int Start { get; }

        /// <summary>Gets how many characters are replaced (0 for an insert).</summary>
        public int Length { get; }

        /// <summary>Gets the replacement text.</summary>
        public string NewText { get; }
    }

    /// <summary>
    /// Quick fixes as plain text edits, computed against the same text the diagnostic came from.
    /// The editor just applies them.
    /// </summary>
    public static class MuiLintFixes
    {
        /// <summary><c>&lt;field name="X" /&gt;</c> becomes <c>&lt;field name="X"&gt;&lt;/field&gt;</c> (AISI0001).</summary>
        /// <returns>The edit, or null if the span no longer holds a self-closing tag.</returns>
        public static TextEdit? ExpandSelfClosing(string text, Diagnostic diagnostic)
        {
            string tag = Slice(text, diagnostic);
            if (tag.Length < 4 || tag[0] != '<' || !tag.EndsWith("/>", StringComparison.Ordinal))
            {
                return null;
            }

            int nameEnd = 1;
            while (nameEnd < tag.Length && !char.IsWhiteSpace(tag[nameEnd]) && tag[nameEnd] != '/' && tag[nameEnd] != '>')
            {
                nameEnd++;
            }

            string name = tag.Substring(1, nameEnd - 1);
            string open = tag.Substring(0, tag.Length - 2).TrimEnd();
            return new TextEdit(diagnostic.Start, diagnostic.Length, open + "></" + name + ">");
        }

        /// <summary>Deletes an empty <c>qp-fieldset</c> along with its now-blank lines (AISI0005).</summary>
        /// <returns>The edit, or null if the closing tag cannot be found.</returns>
        public static TextEdit? RemoveEmptyFieldset(string text, Diagnostic diagnostic)
        {
            int openEnd = diagnostic.Start + diagnostic.Length;
            if (!Slice(text, diagnostic).StartsWith("<qp-fieldset", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            int close = text.IndexOf("</qp-fieldset", openEnd, StringComparison.OrdinalIgnoreCase);
            int closeEnd = close < 0 ? -1 : text.IndexOf('>', close);
            if (closeEnd < 0)
            {
                return null;
            }

            int start = diagnostic.Start;
            int end = closeEnd + 1;
            int lineStart = LineStart(text, start);
            int lineEnd = text.IndexOf('\n', end);
            if (lineEnd < 0)
            {
                lineEnd = text.Length;
            }

            // Take the whole line when the fieldset had it to itself.
            if (IsBlank(text, lineStart, start) && IsBlank(text, end, lineEnd))
            {
                start = lineStart;
                end = Math.Min(text.Length, lineEnd + 1);
            }

            return new TextEdit(start, end - start, string.Empty);
        }

        /// <summary>
        /// Inserts <c>&lt;!-- muilint-disable-next-line ID --&gt;</c> above the finding, or a
        /// file-wide <c>muilint-disable</c> at the top for rules about the file itself.
        /// </summary>
        public static TextEdit Suppress(string text, Diagnostic diagnostic)
        {
            string newline = text.IndexOf("\r\n", StringComparison.Ordinal) >= 0 ? "\r\n" : "\n";
            if (IsFileLevel(diagnostic))
            {
                return new TextEdit(0, 0, "<!-- muilint-disable " + diagnostic.Id + " -->" + newline);
            }

            int lineStart = LineStart(text, Math.Min(diagnostic.Start, text.Length));
            int indentEnd = lineStart;
            while (indentEnd < text.Length && (text[indentEnd] == ' ' || text[indentEnd] == '\t'))
            {
                indentEnd++;
            }

            string indent = text.Substring(lineStart, indentEnd - lineStart);
            return new TextEdit(lineStart, 0, indent + "<!-- muilint-disable-next-line " + diagnostic.Id + " -->" + newline);
        }

        /// <summary>True for findings about the file as a whole (its path or its neighbours), not a span.</summary>
        public static bool IsFileLevel(Diagnostic diagnostic)
        {
            return diagnostic.Start == 0 && diagnostic.Length == 0;
        }

        private static string Slice(string text, Diagnostic diagnostic)
        {
            if (diagnostic.Start < 0 || diagnostic.Start + diagnostic.Length > text.Length)
            {
                return string.Empty;
            }

            return text.Substring(diagnostic.Start, diagnostic.Length);
        }

        private static int LineStart(string text, int index)
        {
            return index == 0 ? 0 : text.LastIndexOf('\n', index - 1) + 1;
        }

        private static bool IsBlank(string text, int start, int end)
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
    }
}
