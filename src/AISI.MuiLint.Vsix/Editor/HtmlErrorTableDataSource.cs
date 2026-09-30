#nullable disable
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using AISI.MuiLint;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell.TableControl;
using Microsoft.VisualStudio.Shell.TableManager;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;

namespace AISI.MuiLint.Vsix
{
    /// <summary>
    /// Error List source for MuiLint findings on currently open HTML documents.
    /// </summary>
    [Export(typeof(HtmlErrorTableDataSource))]
    [Export(typeof(ITableDataSource))]
    [Name(HtmlErrorTableDataSource.SourceIdentifier)]
    internal sealed class HtmlErrorTableDataSource : ITableDataSource
    {
        internal const string SourceIdentifier = "AISI.MuiLint";
        internal const string SourceDisplayName = "AISI MuiLint";
        internal const string BuildToolName = "AISI.MuiLint";

        private readonly object _gate = new object();
        private readonly List<ITableDataSink> _sinks = new List<ITableDataSink>();
        private readonly Dictionary<ITextBuffer, HtmlErrorSnapshotFactory> _factories =
            new Dictionary<ITextBuffer, HtmlErrorSnapshotFactory>();

        [ImportingConstructor]
        public HtmlErrorTableDataSource(ITableManagerProvider tableManagerProvider)
        {
            if (tableManagerProvider is null)
            {
                throw new ArgumentNullException(nameof(tableManagerProvider));
            }

            ITableManager manager = tableManagerProvider.GetTableManager(StandardTables.ErrorsTable);
            manager.AddSource(
                this,
                StandardTableColumnDefinitions.DetailsExpander,
                StandardTableColumnDefinitions.ErrorSeverity,
                StandardTableColumnDefinitions.ErrorCode,
                StandardTableColumnDefinitions.ErrorSource,
                StandardTableColumnDefinitions.BuildTool,
                StandardTableColumnDefinitions.Text,
                StandardTableColumnDefinitions.DocumentName,
                StandardTableColumnDefinitions.Line,
                StandardTableColumnDefinitions.Column);
        }

        /// <inheritdoc />
        public string SourceTypeIdentifier
        {
            get { return StandardTableDataSources.ErrorTableDataSource; }
        }

        /// <inheritdoc />
        public string Identifier
        {
            get { return SourceIdentifier; }
        }

        /// <inheritdoc />
        public string DisplayName
        {
            get { return SourceDisplayName; }
        }

        /// <inheritdoc />
        public IDisposable Subscribe(ITableDataSink sink)
        {
            if (sink is null)
            {
                throw new ArgumentNullException(nameof(sink));
            }

            lock (_gate)
            {
                _sinks.Add(sink);
                foreach (HtmlErrorSnapshotFactory factory in _factories.Values)
                {
                    sink.AddFactory(factory);
                }

                sink.IsStable = true;
            }

            return new SinkSubscription(this, sink);
        }

        internal void Update(ITextBuffer buffer, string path, IReadOnlyList<Diagnostic> diagnostics)
        {
            if (buffer is null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (path is null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (diagnostics is null)
            {
                throw new ArgumentNullException(nameof(diagnostics));
            }

            lock (_gate)
            {
                HtmlErrorSnapshotFactory factory;
                if (!_factories.TryGetValue(buffer, out factory))
                {
                    factory = new HtmlErrorSnapshotFactory();
                    _factories.Add(buffer, factory);
                    factory.Update(path, diagnostics);
                    for (int i = 0; i < _sinks.Count; i++)
                    {
                        _sinks[i].AddFactory(factory);
                    }
                }
                else
                {
                    factory.Update(path, diagnostics);
                    for (int i = 0; i < _sinks.Count; i++)
                    {
                        _sinks[i].FactorySnapshotChanged(factory);
                    }
                }
            }
        }

        internal void Remove(ITextBuffer buffer)
        {
            if (buffer is null)
            {
                return;
            }

            lock (_gate)
            {
                HtmlErrorSnapshotFactory factory;
                if (!_factories.TryGetValue(buffer, out factory))
                {
                    return;
                }

                _factories.Remove(buffer);
                for (int i = 0; i < _sinks.Count; i++)
                {
                    _sinks[i].RemoveFactory(factory);
                }

                factory.Dispose();
            }
        }

        private void RemoveSink(ITableDataSink sink)
        {
            lock (_gate)
            {
                _sinks.Remove(sink);
            }
        }

        private sealed class SinkSubscription : IDisposable
        {
            private HtmlErrorTableDataSource _source;
            private readonly ITableDataSink _sink;

            public SinkSubscription(HtmlErrorTableDataSource source, ITableDataSink sink)
            {
                _source = source;
                _sink = sink;
            }

            public void Dispose()
            {
                HtmlErrorTableDataSource source = Interlocked.Exchange(ref _source, null);
                if (source != null)
                {
                    source.RemoveSink(_sink);
                }
            }
        }
    }

    internal sealed class HtmlErrorSnapshotFactory : TableEntriesSnapshotFactoryBase
    {
        private HtmlErrorSnapshot _current = new HtmlErrorSnapshot(0, "buffer.html", Array.Empty<Diagnostic>());

        public override int CurrentVersionNumber
        {
            get { return _current.VersionNumber; }
        }

        public override ITableEntriesSnapshot GetCurrentSnapshot()
        {
            return _current;
        }

        public override ITableEntriesSnapshot GetSnapshot(int versionNumber)
        {
            HtmlErrorSnapshot current = _current;
            if (current.VersionNumber == versionNumber)
            {
                return current;
            }

            return null;
        }

        public void Update(string path, IReadOnlyList<Diagnostic> diagnostics)
        {
            _current = new HtmlErrorSnapshot(_current.VersionNumber + 1, path, diagnostics);
        }
    }

    internal sealed class HtmlErrorSnapshot : TableEntriesSnapshotBase
    {
        private readonly string _path;
        private readonly IReadOnlyList<Diagnostic> _diagnostics;

        public HtmlErrorSnapshot(int versionNumber, string path, IReadOnlyList<Diagnostic> diagnostics)
        {
            VersionNumber = versionNumber;
            _path = path;
            _diagnostics = diagnostics;
        }

        public override int Count
        {
            get { return _diagnostics.Count; }
        }

        public override int VersionNumber { get; }

        public override bool TryGetValue(int index, string keyName, out object content)
        {
            content = null;
            if (index < 0 || index >= _diagnostics.Count)
            {
                return false;
            }

            Diagnostic diagnostic = _diagnostics[index];
            switch (keyName)
            {
                case StandardTableKeyNames.ErrorSeverity:
                    content = Category(diagnostic.Severity);
                    return true;
                case StandardTableKeyNames.ErrorCode:
                    content = diagnostic.Id;
                    return true;
                case StandardTableKeyNames.HelpLink:
                    content = Rules.Find(diagnostic.Id)?.HelpUri;
                    return content != null;
                case StandardTableKeyNames.Text:
                    content = diagnostic.Id + ": " + diagnostic.Message;
                    return true;
                case StandardTableKeyNames.DocumentName:
                    content = string.IsNullOrEmpty(diagnostic.Path) ? _path : diagnostic.Path;
                    return true;
                case StandardTableKeyNames.Line:
                    content = Math.Max(0, diagnostic.Line - 1);
                    return true;
                case StandardTableKeyNames.Column:
                    content = Math.Max(0, diagnostic.Column - 1);
                    return true;
                case StandardTableKeyNames.ErrorSource:
                    content = ErrorSource.Other;
                    return true;
                case StandardTableKeyNames.BuildTool:
                    content = HtmlErrorTableDataSource.BuildToolName;
                    return true;
                case StandardTableKeyNames.ErrorCategory:
                    content = HtmlErrorTableDataSource.SourceDisplayName;
                    return true;
                default:
                    return false;
            }
        }

        private static __VSERRORCATEGORY Category(Severity severity)
        {
            switch (severity)
            {
                case Severity.Warning:
                    return __VSERRORCATEGORY.EC_WARNING;
                case Severity.Suggestion:
                    return __VSERRORCATEGORY.EC_MESSAGE;
                default:
                    return __VSERRORCATEGORY.EC_ERROR;
            }
        }
    }
}
