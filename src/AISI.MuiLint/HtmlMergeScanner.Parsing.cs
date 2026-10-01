using System;
using System.Collections.Generic;

namespace AISI.MuiLint
{
    internal readonly struct HtmlAttribute
    {
        public HtmlAttribute(string name, string value, int valueStart)
        {
            Name = name;
            Value = value;
            ValueStart = valueStart;
        }

        public string Name { get; }

        public string Value { get; }

        public int ValueStart { get; }
    }

    internal readonly struct HtmlTag
    {
        public HtmlTag(
            string name,
            int start,
            int end,
            bool selfClosing,
            bool isEndTag,
            IReadOnlyList<HtmlAttribute> attributes)
        {
            Name = name;
            Start = start;
            End = end;
            SelfClosing = selfClosing;
            IsEndTag = isEndTag;
            Attributes = attributes;
        }

        public string Name { get; }

        public int Start { get; }

        public int End { get; }

        public bool SelfClosing { get; }

        public bool IsEndTag { get; }

        public IReadOnlyList<HtmlAttribute> Attributes { get; }

        public bool HasAttribute(string name)
        {
            for (int i = 0; i < Attributes.Count; i++)
            {
                if (string.Equals(Attributes[i].Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Value of the first attribute called <paramref name="name"/>, or empty.</summary>
        public string GetAttribute(string name)
        {
            for (int i = 0; i < Attributes.Count; i++)
            {
                if (string.Equals(Attributes[i].Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return Attributes[i].Value;
                }
            }

            return string.Empty;
        }
    }

    internal static class HtmlTagReader
    {
        public static IReadOnlyList<HtmlTag> Read(string text)
        {
            var tags = new List<HtmlTag>();
            int i = 0;
            int n = text.Length;
            while (i < n)
            {
                if (text[i] != '<')
                {
                    i++;
                    continue;
                }

                int tagStart = i;
                i++;
                if (i >= n)
                {
                    break;
                }

                bool isEnd = false;
                char c = text[i];
                if (c == '/')
                {
                    isEnd = true;
                    i++;
                }
                else if (c == '!' || c == '?')
                {
                    while (i < n && text[i] != '>')
                    {
                        i++;
                    }

                    if (i < n)
                    {
                        i++;
                    }

                    continue;
                }

                int nameStart = i;
                while (i < n && IsNameChar(text[i]))
                {
                    i++;
                }

                if (i == nameStart)
                {
                    continue;
                }

                string name = text.Substring(nameStart, i - nameStart);
                var attrs = new List<HtmlAttribute>();
                bool selfClosing = false;

                while (i < n)
                {
                    SkipWs(text, ref i);
                    if (i >= n)
                    {
                        break;
                    }

                    if (text[i] == '>')
                    {
                        i++;
                        break;
                    }

                    if (text[i] == '/' && i + 1 < n && text[i + 1] == '>')
                    {
                        selfClosing = true;
                        i += 2;
                        break;
                    }

                    if (!IsNameChar(text[i]))
                    {
                        i++;
                        continue;
                    }

                    int attrNameStart = i;
                    while (i < n && IsNameChar(text[i]))
                    {
                        i++;
                    }

                    string attrName = text.Substring(attrNameStart, i - attrNameStart);
                    SkipWs(text, ref i);
                    string attrValue = string.Empty;
                    int valueStart = i;
                    if (i < n && text[i] == '=')
                    {
                        i++;
                        SkipWs(text, ref i);
                        if (i < n && (text[i] == '"' || text[i] == '\''))
                        {
                            char quote = text[i];
                            i++;
                            valueStart = i;
                            while (i < n && text[i] != quote)
                            {
                                i++;
                            }

                            attrValue = text.Substring(valueStart, i - valueStart);
                            if (i < n)
                            {
                                i++;
                            }
                        }
                        else
                        {
                            valueStart = i;
                            while (i < n && !char.IsWhiteSpace(text[i]) && text[i] != '>' && text[i] != '/')
                            {
                                i++;
                            }

                            attrValue = text.Substring(valueStart, i - valueStart);
                        }
                    }

                    attrs.Add(new HtmlAttribute(attrName, attrValue, valueStart));
                }

                tags.Add(new HtmlTag(name, tagStart, i, selfClosing, isEnd, attrs));
            }

            return tags;
        }

        /// <summary>
        /// Index of each tag's enclosing start tag, or -1 at the top level. Forgiving: an end tag
        /// closes the nearest open tag with its name, and unclosed tags simply stay open.
        /// </summary>
        public static int[] Parents(IReadOnlyList<HtmlTag> tags)
        {
            var parents = new int[tags.Count];
            var open = new List<int>();
            for (int i = 0; i < tags.Count; i++)
            {
                HtmlTag tag = tags[i];
                parents[i] = open.Count > 0 ? open[open.Count - 1] : -1;
                if (!tag.IsEndTag)
                {
                    if (!tag.SelfClosing)
                    {
                        open.Add(i);
                    }

                    continue;
                }

                for (int o = open.Count - 1; o >= 0; o--)
                {
                    if (string.Equals(tags[open[o]].Name, tag.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        open.RemoveRange(o, open.Count - o);
                        break;
                    }
                }
            }

            return parents;
        }

        private static bool IsNameChar(char c)
        {
            return (c >= 'A' && c <= 'Z')
                   || (c >= 'a' && c <= 'z')
                   || (c >= '0' && c <= '9')
                   || c == '-'
                   || c == '_'
                   || c == ':'
                   || c == '.';
        }

        private static void SkipWs(string text, ref int i)
        {
            while (i < text.Length && char.IsWhiteSpace(text[i]))
            {
                i++;
            }
        }
    }

    internal sealed class LineMap
    {
        private readonly int[] _starts;
        private readonly int _length;

        public LineMap(string text)
        {
            _length = text.Length;
            var starts = new List<int> { 0 };
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    starts.Add(i + 1);
                }
            }

            _starts = starts.ToArray();
        }

        public void ToLineCol(int index, out int line, out int column)
        {
            if (index < 0)
            {
                index = 0;
            }

            if (index > _length)
            {
                index = _length;
            }

            int lineIndex = Array.BinarySearch(_starts, index);
            if (lineIndex < 0)
            {
                lineIndex = ~lineIndex - 1;
            }

            if (lineIndex < 0)
            {
                lineIndex = 0;
            }

            line = lineIndex + 1;
            column = index - _starts[lineIndex] + 1;
        }
    }
}
