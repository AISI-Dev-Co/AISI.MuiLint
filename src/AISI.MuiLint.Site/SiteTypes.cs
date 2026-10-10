using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text.RegularExpressions;

namespace AISI.MuiLint.Site
{
    /// <summary>Every type definition in a Bin folder, indexed by full name, and what Acumatica makes of them.</summary>
    internal sealed class SiteTypes
    {
        private const string PXGraph = "PX.Data.PXGraph";
        private const string GraphExtension = "PX.Data.PXGraphExtension`";
        private const string CacheExtension = "PX.Data.PXCacheExtension`";
        private const string PXSelectBase = "PX.Data.PXSelectBase`1";
        private const string PXAction = "PX.Data.PXAction";
        private const string FeaturesSet = "PX.Objects.CS.FeaturesSet";
        private const int MaxDepth = 64;

        private static readonly Regex Arity = new Regex("`[0-9]+", RegexOptions.CultureInvariant);

        private readonly Dictionary<string, TypeEntry> _types = new Dictionary<string, TypeEntry>(StringComparer.Ordinal);

        // Keyed by the target's metadata name; each entry is an extension's own levels, below PXGraphExtension.
        private readonly Dictionary<string, List<Extension>> _graphExtensions = new Dictionary<string, List<Extension>>(StringComparer.Ordinal);

        // Keyed by the DAC's metadata name; each entry is an extension's own levels, below PXCacheExtension.
        private readonly Dictionary<string, List<List<Level>>> _cacheExtensions = new Dictionary<string, List<List<Level>>>(StringComparer.Ordinal);

        private readonly Dictionary<string, FieldKind> _fieldKinds = new Dictionary<string, FieldKind>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>?> _dacFields = new Dictionary<string, List<string>?>(StringComparer.Ordinal);

        public SiteTypes(IEnumerable<MetadataReader> readers)
        {
            foreach (MetadataReader reader in readers)
            {
                foreach (TypeDefinitionHandle handle in reader.TypeDefinitions)
                {
                    try
                    {
                        string name = TypeSigProvider.FullName(reader, handle);
                        if (!_types.ContainsKey(name))
                        {
                            _types.Add(name, new TypeEntry(reader, reader.GetTypeDefinition(handle)));
                        }
                    }
                    catch (BadImageFormatException)
                    {
                        // An unreadable type is a type we don't know; whatever needs it reports itself incomplete.
                    }
                }
            }
        }

        public SiteMetadata Build(IEnumerable<string> sourceNames)
        {
            var graphs = new List<KeyValuePair<string, Ancestry>>();
            var unsure = new List<KeyValuePair<string, Ancestry>>();
            foreach (KeyValuePair<string, TypeEntry> type in _types)
            {
                if (!type.Value.IsConcreteClass)
                {
                    continue;
                }

                Ancestry ancestry = Walk(new NamedSig(type.Key, ImmutableArray<TypeSig>.Empty));
                List<Level> levels = ancestry.Levels;
                int marker = Marker(levels);
                if (marker < 0)
                {
                    // Its ancestry broke before showing what it is: maybe a graph built on a missing assembly.
                    if (!ancestry.Complete)
                    {
                        unsure.Add(new KeyValuePair<string, Ancestry>(type.Key, ancestry));
                    }

                    continue;
                }

                string name = levels[marker].Sig.Name;
                if (name == PXGraph)
                {
                    graphs.Add(new KeyValuePair<string, Ancestry>(type.Key, ancestry));
                    continue;
                }

                // PXGraphExtension<TExt, TGraph>, PXCacheExtension<TExt, TTable>: the last argument is the target.
                ImmutableArray<TypeSig> args = levels[marker].Sig.Args;
                if (args.Length == 0 || !(args[args.Length - 1] is NamedSig target))
                {
                    continue;
                }

                List<Level> own = levels.GetRange(0, marker);
                if (name.StartsWith(GraphExtension, StringComparison.Ordinal))
                {
                    ListFor(_graphExtensions, target.Name).Add(new Extension(own));
                }
                else
                {
                    ListFor(_cacheExtensions, target.Name).Add(own);
                }
            }

            var result = new List<GraphMetadata>();
            foreach (KeyValuePair<string, Ancestry> graph in graphs)
            {
                Members members = GraphMembers(graph.Value);
                result.Add(new GraphMetadata(Display(graph.Key), members.Views.Values, members.Actions, members.Complete));
            }

            foreach (KeyValuePair<string, Ancestry> type in unsure)
            {
                // Only what an extension targets, or what declares views or actions, is taken for a graph.
                Members members = GraphMembers(type.Value);
                if (_graphExtensions.ContainsKey(type.Key) || members.Views.Count > 0 || members.Actions.Count > 0)
                {
                    result.Add(new GraphMetadata(Display(type.Key), members.Views.Values, members.Actions, false));
                }
            }

            return new SiteMetadata(result, Features(), sourceNames);
        }

        private static int Marker(List<Level> levels)
        {
            for (int i = 1; i < levels.Count; i++)
            {
                string name = levels[i].Sig.Name;
                if (name == PXGraph
                    || name.StartsWith(GraphExtension, StringComparison.Ordinal)
                    || name.StartsWith(CacheExtension, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private static List<T> ListFor<T>(Dictionary<string, List<T>> lists, string key)
        {
            if (!lists.TryGetValue(key, out List<T>? list))
            {
                list = new List<T>();
                lists.Add(key, list);
            }

            return list;
        }

        private static string Display(string metadataName)
        {
            return metadataName.IndexOf('`') < 0 ? metadataName : Arity.Replace(metadataName, string.Empty);
        }

        // Types the Bin never holds; their ancestry is no business of a view, action or DAC.
        private static bool IsFramework(string name)
        {
            return name.StartsWith("System.", StringComparison.Ordinal)
                || name.StartsWith("Microsoft.Win32.", StringComparison.Ordinal)
                || name.StartsWith("Microsoft.CSharp.", StringComparison.Ordinal)
                || name.StartsWith("Microsoft.VisualBasic.", StringComparison.Ordinal);
        }

        private static void AddProperties(Level level, List<string> names)
        {
            MetadataReader reader = level.Entry.Reader;
            foreach (PropertyDefinitionHandle handle in level.Entry.Definition.GetProperties())
            {
                PropertyDefinition property = reader.GetPropertyDefinition(handle);
                PropertyAccessors accessors = property.GetAccessors();
                MethodDefinitionHandle accessor = accessors.Getter.IsNil ? accessors.Setter : accessors.Getter;
                if (accessor.IsNil)
                {
                    continue;
                }

                MethodAttributes attributes = reader.GetMethodDefinition(accessor).Attributes;
                if ((attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Public
                    && (attributes & MethodAttributes.Static) == 0)
                {
                    names.Add(reader.GetString(property.Name));
                }
            }
        }

        /// <summary>The base-type chain from <paramref name="start"/>, generic arguments substituted at each step.</summary>
        private Ancestry Walk(NamedSig start)
        {
            var levels = new List<Level>();
            NamedSig current = start;
            try
            {
                for (int depth = 0; depth < MaxDepth; depth++)
                {
                    if (!_types.TryGetValue(current.Name, out TypeEntry? entry))
                    {
                        return new Ancestry(levels, IsFramework(current.Name));
                    }

                    levels.Add(new Level(entry, current));
                    EntityHandle baseType = entry.Definition.BaseType;
                    if (baseType.IsNil)
                    {
                        return new Ancestry(levels, true);
                    }

                    if (!(TypeSigProvider.Instance.Decode(entry.Reader, baseType).Substitute(current.Args) is NamedSig next))
                    {
                        break;
                    }

                    current = next;
                }
            }
            catch (BadImageFormatException)
            {
                // Falls through to an incomplete ancestry.
            }

            return new Ancestry(levels, false);
        }

        private Members GraphMembers(Ancestry ancestry)
        {
            var members = new Members(ancestry.Complete);
            AddFields(ancestry.Levels, members);

            // Base classes first, so a derived graph's own and its extensions' members win a clash of names.
            for (int i = ancestry.Levels.Count - 1; i >= 0; i--)
            {
                if (_graphExtensions.TryGetValue(ancestry.Levels[i].Sig.Name, out List<Extension>? extensions))
                {
                    foreach (Extension extension in extensions)
                    {
                        if (extension.Members == null)
                        {
                            extension.Members = new Members(true);
                            AddFields(extension.Levels, extension.Members);
                        }

                        members.Add(extension.Members);
                    }
                }
            }

            return members;
        }

        private void AddFields(List<Level> levels, Members members)
        {
            for (int i = levels.Count - 1; i >= 0; i--)
            {
                Level level = levels[i];
                MetadataReader reader = level.Entry.Reader;
                foreach (FieldDefinitionHandle handle in level.Entry.Definition.GetFields())
                {
                    try
                    {
                        FieldDefinition field = reader.GetFieldDefinition(handle);
                        if ((field.Attributes & FieldAttributes.FieldAccessMask) != FieldAttributes.Public
                            || (field.Attributes & FieldAttributes.Static) != 0)
                        {
                            continue;
                        }

                        string name = reader.GetString(field.Name);
                        TypeSig type = field.DecodeSignature(TypeSigProvider.Instance, null).Substitute(level.Sig.Args);
                        FieldKind kind = Kind(type);
                        if (kind.IsView)
                        {
                            members.Views[name] = new ViewMetadata(name, kind.Dac, kind.Fields);
                        }
                        else if (kind.IsAction)
                        {
                            members.Actions.Add(name);
                        }
                        else if (kind.IsUnknown)
                        {
                            members.Complete = false;
                        }
                    }
                    catch (BadImageFormatException)
                    {
                        members.Complete = false;
                    }
                }
            }
        }

        private FieldKind Kind(TypeSig type)
        {
            if (!(type is NamedSig named))
            {
                return FieldKind.None;
            }

            string key = named.ToString();
            if (_fieldKinds.TryGetValue(key, out FieldKind? kind))
            {
                return kind;
            }

            Ancestry ancestry = Walk(named);
            kind = ancestry.Complete ? FieldKind.None : FieldKind.Unknown;
            foreach (Level level in ancestry.Levels)
            {
                if (level.Sig.Name == PXAction)
                {
                    kind = FieldKind.Action;
                    break;
                }

                if (level.Sig.Name == PXSelectBase)
                {
                    kind = level.Sig.Args.Length == 1 && level.Sig.Args[0] is NamedSig dac
                        ? FieldKind.View(Display(dac.Name), DacFields(dac))
                        : FieldKind.View(null, null);
                    break;
                }
            }

            _fieldKinds.Add(key, kind);
            return kind;
        }

        /// <summary>A DAC's public instance properties, inherited ones and its cache extensions' included.</summary>
        private List<string>? DacFields(NamedSig dac)
        {
            string key = dac.ToString();
            if (_dacFields.TryGetValue(key, out List<string>? fields))
            {
                return fields;
            }

            fields = null;
            Ancestry ancestry = Walk(dac);
            if (ancestry.Complete && ancestry.Levels.Count > 0)
            {
                try
                {
                    var names = new List<string>();
                    foreach (Level level in ancestry.Levels)
                    {
                        AddProperties(level, names);
                        if (_cacheExtensions.TryGetValue(level.Sig.Name, out List<List<Level>>? extensions))
                        {
                            foreach (List<Level> extension in extensions)
                            {
                                foreach (Level extensionLevel in extension)
                                {
                                    AddProperties(extensionLevel, names);
                                }
                            }
                        }
                    }

                    fields = names;
                }
                catch (BadImageFormatException)
                {
                    fields = null;
                }
            }

            _dacFields.Add(key, fields);
            return fields;
        }

        private List<string>? Features()
        {
            if (!_types.ContainsKey(FeaturesSet))
            {
                return null;
            }

            List<string>? fields = DacFields(new NamedSig(FeaturesSet, ImmutableArray<TypeSig>.Empty));
            return fields?.ConvertAll(field => FeaturesSet + "+" + field);
        }

        private sealed class TypeEntry
        {
            public TypeEntry(MetadataReader reader, TypeDefinition definition)
            {
                Reader = reader;
                Definition = definition;
            }

            public MetadataReader Reader { get; }

            public TypeDefinition Definition { get; }

            // Abstract graphs and extensions only lend their members; generic ones (including types
            // nested in generic types) are only ever used through a concrete class.
            public bool IsConcreteClass
            {
                get
                {
                    return (Definition.Attributes & (TypeAttributes.Interface | TypeAttributes.Abstract)) == 0
                        && Definition.GetGenericParameters().Count == 0;
                }
            }
        }

        private readonly struct Level
        {
            public Level(TypeEntry entry, NamedSig sig)
            {
                Entry = entry;
                Sig = sig;
            }

            public TypeEntry Entry { get; }

            public NamedSig Sig { get; }
        }

        private sealed class Ancestry
        {
            public Ancestry(List<Level> levels, bool complete)
            {
                Levels = levels;
                Complete = complete;
            }

            /// <summary>Gets the resolved types, the start first.</summary>
            public List<Level> Levels { get; }

            /// <summary>Gets a value indicating whether the chain ended at System.Object or another framework type.</summary>
            public bool Complete { get; }
        }

        private sealed class Extension
        {
            public Extension(List<Level> levels)
            {
                Levels = levels;
            }

            public List<Level> Levels { get; }

            public Members? Members { get; set; }
        }

        private sealed class Members
        {
            public Members(bool complete)
            {
                Complete = complete;
            }

            public Dictionary<string, ViewMetadata> Views { get; } = new Dictionary<string, ViewMetadata>(StringComparer.OrdinalIgnoreCase);

            public HashSet<string> Actions { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            public bool Complete { get; set; }

            public void Add(Members other)
            {
                foreach (KeyValuePair<string, ViewMetadata> view in other.Views)
                {
                    Views[view.Key] = view.Value;
                }

                Actions.UnionWith(other.Actions);
                Complete &= other.Complete;
            }
        }

        private sealed class FieldKind
        {
            public static readonly FieldKind None = new FieldKind(false, false, false, null, null);
            public static readonly FieldKind Unknown = new FieldKind(false, false, true, null, null);
            public static readonly FieldKind Action = new FieldKind(false, true, false, null, null);

            private FieldKind(bool isView, bool isAction, bool isUnknown, string? dac, List<string>? fields)
            {
                IsView = isView;
                IsAction = isAction;
                IsUnknown = isUnknown;
                Dac = dac;
                Fields = fields;
            }

            public bool IsView { get; }

            public bool IsAction { get; }

            /// <summary>Gets a value indicating whether the type's ancestry broke before it showed what it is.</summary>
            public bool IsUnknown { get; }

            public string? Dac { get; }

            public List<string>? Fields { get; }

            public static FieldKind View(string? dac, List<string>? fields)
            {
                return new FieldKind(true, false, false, dac, fields);
            }
        }
    }
}
