using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AISI.MuiLint
{
    /// <summary>A string literal in a decorator's arguments and where its contents start.</summary>
    internal readonly struct TsLiteral
    {
        public TsLiteral(string value, int start)
        {
            Value = value;
            Start = start;
        }

        public string Value { get; }

        public int Start { get; }
    }

    /// <summary>One <c>@handleEvent</c> decorator, its event type and properties, and the method under it.</summary>
    internal sealed class TsEventHandler
    {
        private static readonly string[] PropertyNames = { "view", "field", "column", "allColumns" };

        private static readonly Regex Decorator = new Regex(
            "@handleEvent\\s*\\(",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex EventType = new Regex(
            "\\G\\s*CustomEventType\\s*\\.\\s*(?<e>[A-Za-z_$][\\w$]*)\\s*(?<comma>,)?",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex Method = new Regex(
            "\\G\\s*(?:@[\\w$.]+\\s*(?:\\((?>[^()]+|\\((?<d>)|\\)(?<-d>))*(?(d)(?!))\\))?\\s*)*(?:(?:public|private|protected|async|override)\\s+)*(?<name>[A-Za-z_$][\\w$]*)\\s*\\(\\s*(?:[A-Za-z_$][\\w$]*\\s*\\??\\s*:\\s*(?<type>[A-Za-z_$][\\w$]*))?",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private readonly HashSet<string> _properties = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, TsLiteral> _literals = new Dictionary<string, TsLiteral>(StringComparer.Ordinal);

        private TsEventHandler(int decoratorStart, string eventName)
        {
            DecoratorStart = decoratorStart;
            Event = eventName;
        }

        /// <summary>Offset of the <c>@</c>.</summary>
        public int DecoratorStart { get; }

        /// <summary>The <c>CustomEventType</c> member, for example <c>RowSelected</c>.</summary>
        public string Event { get; }

        /// <summary>False when the second argument is something other than an object literal, so its properties can't be read.</summary>
        public bool PropertiesKnown { get; private set; }

        /// <summary>The decorated method's name, or empty when what follows isn't a method.</summary>
        public string MethodName { get; private set; } = string.Empty;

        public int MethodStart { get; private set; } = -1;

        /// <summary>The type of the method's first parameter, or empty.</summary>
        public string ArgsType { get; private set; } = string.Empty;

        public int ArgsTypeStart { get; private set; } = -1;

        public static IReadOnlyList<TsEventHandler> Read(TsModule module)
        {
            List<TsEventHandler> handlers = new List<TsEventHandler>();
            foreach (Match decorator in Decorator.Matches(module.Code))
            {
                int open = decorator.Index + decorator.Length - 1;
                int close = TsModule.MatchingParen(module.Code, open);
                Match eventType = close < 0 ? Match.Empty : EventType.Match(module.Code, open + 1);
                if (!eventType.Success || eventType.Index + eventType.Length > close)
                {
                    continue;
                }

                TsEventHandler handler = new TsEventHandler(decorator.Index, eventType.Groups["e"].Value);
                int start = eventType.Index + eventType.Length;
                string properties = module.Code.Substring(start, close - start).Trim();
                handler.PropertiesKnown = !eventType.Groups["comma"].Success || properties.Length == 0 || properties.StartsWith("{", StringComparison.Ordinal);
                if (handler.PropertiesKnown)
                {
                    string withStrings = module.CodeWithStrings.Substring(start, close - start);
                    foreach (string name in PropertyNames)
                    {
                        if (Regex.IsMatch(properties, "\\b" + name + "\\b", RegexOptions.CultureInvariant))
                        {
                            handler._properties.Add(name);
                        }

                        Match literal = Regex.Match(withStrings, "\\b" + name + "\\s*:\\s*(['\"])(?<v>[^'\"\\r\\n]*)\\1", RegexOptions.CultureInvariant);
                        if (literal.Success)
                        {
                            handler._literals[name] = new TsLiteral(literal.Groups["v"].Value, start + literal.Groups["v"].Index);
                        }
                    }
                }

                Match method = Method.Match(module.Code, close + 1);
                if (method.Success)
                {
                    handler.MethodName = method.Groups["name"].Value;
                    handler.MethodStart = method.Groups["name"].Index;
                    if (method.Groups["type"].Success)
                    {
                        handler.ArgsType = method.Groups["type"].Value;
                        handler.ArgsTypeStart = method.Groups["type"].Index;
                    }
                }

                handlers.Add(handler);
            }

            return handlers;
        }

        /// <summary>Whether the decorator's properties mention <paramref name="name"/>, as a key or in shorthand.</summary>
        public bool Has(string name)
        {
            return _properties.Contains(name);
        }

        /// <summary>The string literal given for <paramref name="name"/>, or null when it is missing or not a literal.</summary>
        public TsLiteral? Literal(string name)
        {
            return _literals.TryGetValue(name, out TsLiteral literal) ? literal : (TsLiteral?)null;
        }
    }
}
