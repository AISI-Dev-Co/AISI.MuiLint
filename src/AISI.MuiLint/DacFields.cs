using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AISI.MuiLint
{
    /// <summary>A field declared in a C# DAC extension.</summary>
    public readonly struct DacField
    {
        /// <summary>Creates one.</summary>
        public DacField(string name, string extension, string dac)
        {
            Name = name;
            Extension = extension;
            Dac = dac;
        }

        /// <summary>Gets the property name, for example <c>UsrPriority</c>.</summary>
        public string Name { get; }

        /// <summary>Gets the extension class, for example <c>SOOrderExt</c>.</summary>
        public string Extension { get; }

        /// <summary>Gets the DAC it extends, for example <c>SOOrder</c>.</summary>
        public string Dac { get; }
    }

    /// <summary>Reads the properties of <c>PXCacheExtension</c> classes out of C# source, no compiler involved.</summary>
    public static class DacFields
    {
        private static readonly Regex CacheExtension = new Regex(
            "\\bclass\\s+(?<ext>[A-Za-z_]\\w*)\\s*:\\s*PXCacheExtension\\s*<(?<args>[^>{]+)>[^{]*\\{",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex Property = new Regex(
            "\\bpublic\\s+(?:(?:virtual|new|override|abstract)\\s+)*(?<type>[\\w.]+(?:<[^>]*>)?\\??(?:\\[\\])?)\\s+(?<name>[A-Za-z_]\\w*)\\s*\\{",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        /// <summary>Every property declared directly in a cache extension in <paramref name="csharp"/>.</summary>
        public static IReadOnlyList<DacField> Read(string csharp)
        {
            if (csharp is null)
            {
                throw new ArgumentNullException(nameof(csharp));
            }

            var fields = new List<DacField>();
            if (csharp.IndexOf("PXCacheExtension", StringComparison.Ordinal) < 0)
            {
                return fields;
            }

            string code = TsModule.Mask(csharp, keepStrings: false);
            foreach (Match header in CacheExtension.Matches(code))
            {
                int open = header.Index + header.Length - 1;
                int close = TsModule.MatchingBrace(code, open);
                if (close < 0)
                {
                    continue;
                }

                // PXCacheExtension<SOOrderExt, SOOrder>: the DAC is the last argument.
                string[] args = header.Groups["args"].Value.Split(',');
                string dac = args[args.Length - 1].Trim();
                int dot = dac.LastIndexOf('.');
                dac = dot >= 0 ? dac.Substring(dot + 1) : dac;

                string body = code.Substring(open + 1, close - open - 1);
                foreach (Match property in Property.Matches(body))
                {
                    string type = property.Groups["type"].Value;
                    if (type != "class" && type != "struct" && type != "interface" && type != "enum" && type != "record")
                    {
                        fields.Add(new DacField(property.Groups["name"].Value, header.Groups["ext"].Value, dac));
                    }
                }
            }

            return fields;
        }
    }
}
