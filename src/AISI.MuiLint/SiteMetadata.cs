using System;
using System.Collections.Generic;

namespace AISI.MuiLint
{
    /// <summary>
    /// What a site's compiled code says about its graphs, views, actions, DAC fields and features,
    /// read from its Bin folder. The rules that use it stay quiet wherever it doesn't know.
    /// </summary>
    public sealed class SiteMetadata
    {
        private readonly Dictionary<string, GraphMetadata> _graphs;

        /// <summary>Initializes a new instance of the <see cref="SiteMetadata"/> class.</summary>
        /// <param name="graphs">Every graph, keyed by full name (nested types joined with '+').</param>
        /// <param name="features">
        /// Feature names as <c>@featureInstalled</c> spells them
        /// (<c>PX.Objects.CS.FeaturesSet+Multicurrency</c>), or null when FeaturesSet wasn't found.
        /// </param>
        public SiteMetadata(IEnumerable<GraphMetadata> graphs, IEnumerable<string>? features)
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
        }

        /// <summary>Gets the graphs, keyed by full name.</summary>
        public IReadOnlyDictionary<string, GraphMetadata> Graphs
        {
            get { return _graphs; }
        }

        /// <summary>Gets the known feature names, or null when the site's FeaturesSet wasn't found.</summary>
        public ICollection<string>? Features { get; }

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
        /// <summary>Initializes a new instance of the <see cref="GraphMetadata"/> class.</summary>
        /// <param name="fullName">Full type name, nested types joined with '+'.</param>
        /// <param name="views">Views by name: the graph's own, inherited, and from its extensions.</param>
        /// <param name="actions">Action names, likewise.</param>
        /// <param name="complete">
        /// False when part of the graph's ancestry or extensions couldn't be read, so a missing view
        /// or action proves nothing.
        /// </param>
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

        /// <summary>Gets the full type name.</summary>
        public string FullName { get; }

        /// <summary>Gets the views by name (case-insensitive, as the backend matches them).</summary>
        public IReadOnlyDictionary<string, ViewMetadata> Views { get; }

        /// <summary>Gets the action names (case-insensitive).</summary>
        public ICollection<string> Actions { get; }

        /// <summary>Gets a value indicating whether everything the graph is made of could be read.</summary>
        public bool Complete { get; }
    }

    /// <summary>A view of a graph and the DAC behind it.</summary>
    public sealed class ViewMetadata
    {
        /// <summary>Initializes a new instance of the <see cref="ViewMetadata"/> class.</summary>
        /// <param name="name">The field name of the view on the graph.</param>
        /// <param name="dac">Full name of the main DAC, or null when it couldn't be worked out.</param>
        /// <param name="fields">
        /// The DAC's fields with its cache extensions, or null when the DAC or part of its ancestry
        /// couldn't be read.
        /// </param>
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
