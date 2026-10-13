using System;
using System.Collections.Generic;

namespace AISI.MuiLint
{
    /// <summary>What a site's Bin folder says about its graphs, views, actions, DAC fields and features.</summary>
    public sealed class SiteMetadata
    {
        private readonly Dictionary<string, GraphMetadata> _graphs;

        /// <summary>Creates the metadata from every graph, the feature names as <c>@featureInstalled</c> spells them, and the identifiers in App_RuntimeCode.</summary>
        public SiteMetadata(IEnumerable<GraphMetadata> graphs, IEnumerable<string>? features, IEnumerable<string>? sourceNames = null)
        {
            if (graphs is null)
            {
                throw new ArgumentNullException(nameof(graphs));
            }

            _graphs = new Dictionary<string, GraphMetadata>(StringComparer.OrdinalIgnoreCase);
            foreach (GraphMetadata graph in graphs)
            {
                _graphs[graph.FullName] = graph;
            }

            Features = features == null ? null : new HashSet<string>(features, StringComparer.OrdinalIgnoreCase);
            SourceNames = new HashSet<string>(sourceNames ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>Gets the graphs, keyed by full name.</summary>
        public IReadOnlyDictionary<string, GraphMetadata> Graphs
        {
            get { return _graphs; }
        }

        /// <summary>Gets the known feature names, or null when the site's FeaturesSet wasn't found.</summary>
        public ICollection<string>? Features { get; }

        /// <summary>Gets the identifiers in source code the Bin doesn't hold (case-insensitive).</summary>
        public ICollection<string> SourceNames { get; }

        /// <summary>The graph a <c>graphType</c> names, accepting '.' or '+' before a nested type.</summary>
        public GraphMetadata? FindGraph(string graphType)
        {
            if (string.IsNullOrEmpty(graphType))
            {
                return null;
            }

            if (_graphs.TryGetValue(graphType, out GraphMetadata? graph))
            {
                return graph;
            }

            // PX.Objects.SO.SOOrderEntry.Nested written with a dot: try each dot as a '+'.
            for (int i = graphType.LastIndexOf('.'); i > 0; i = graphType.LastIndexOf('.', i - 1))
            {
                string nested = graphType.Substring(0, i) + "+" + graphType.Substring(i + 1);
                if (_graphs.TryGetValue(nested, out graph))
                {
                    return graph;
                }
            }

            return null;
        }
    }

    /// <summary>A graph with its extensions folded in.</summary>
    public sealed class GraphMetadata
    {
        /// <summary>Creates a graph from its own, inherited and extension views and actions.</summary>
        public GraphMetadata(string fullName, IEnumerable<ViewMetadata> views, IEnumerable<string> actions, bool complete)
        {
            FullName = fullName ?? throw new ArgumentNullException(nameof(fullName));
            var byName = new Dictionary<string, ViewMetadata>(StringComparer.OrdinalIgnoreCase);
            foreach (ViewMetadata view in views ?? throw new ArgumentNullException(nameof(views)))
            {
                byName[view.Name] = view;
            }

            Views = byName;
            Actions = new HashSet<string>(actions ?? throw new ArgumentNullException(nameof(actions)), StringComparer.OrdinalIgnoreCase);
            Complete = complete;
        }

        /// <summary>Gets the full type name, nested types joined with '+'.</summary>
        public string FullName { get; }

        /// <summary>Gets the views by name (case-insensitive, as the backend matches them).</summary>
        public IReadOnlyDictionary<string, ViewMetadata> Views { get; }

        /// <summary>Gets the action names (case-insensitive).</summary>
        public ICollection<string> Actions { get; }

        /// <summary>Gets a value indicating whether everything the graph is made of could be read, so a missing view or action proves something.</summary>
        public bool Complete { get; }
    }

    /// <summary>A view of a graph and the DAC behind it.</summary>
    public sealed class ViewMetadata
    {
        /// <summary>Creates a view from its name on the graph, its main DAC and that DAC's fields with its cache extensions.</summary>
        public ViewMetadata(string name, string? dac, IEnumerable<string>? fields)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Dac = dac;
            Fields = fields == null ? null : new HashSet<string>(fields, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>Gets the view's name.</summary>
        public string Name { get; }

        /// <summary>Gets the main DAC's full name, or null.</summary>
        public string? Dac { get; }

        /// <summary>Gets the DAC's field names (case-insensitive), or null when unknown.</summary>
        public ICollection<string>? Fields { get; }
    }
}
