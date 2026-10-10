using System;
using System.Collections.Generic;
using System.IO;

namespace AISI.MuiLint.Site
{
    /// <summary>The metadata of the site a screen file belongs to, read once and re-read when the site changes.</summary>
    public static class SiteCache
    {
        private static readonly TimeSpan CheckEvery = TimeSpan.FromSeconds(30);
        private static readonly Dictionary<string, Entry> Entries = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The metadata for the site holding <paramref name="path"/>: the Bin beside the FrontendSources
        /// folder the file sits in. Null when the file isn't in a site or the Bin can't be read.
        /// </summary>
        public static SiteMetadata? ForFile(string path)
        {
            string? bin = BinFor(path);
            if (bin == null)
            {
                return null;
            }

            lock (Entries)
            {
                DateTime now = DateTime.UtcNow;
                if (Entries.TryGetValue(bin, out Entry? entry) && now - entry.CheckedAt < CheckEvery)
                {
                    return entry.Metadata;
                }

                DateTime stamp = Stamp(bin);
                if (entry == null || entry.Stamp != stamp)
                {
                    entry = new Entry(SiteMetadataReader.Read(bin), stamp);
                    Entries[bin] = entry;
                }

                entry.CheckedAt = now;
                return entry.Metadata;
            }
        }

        /// <summary>The Bin folder for a file under &lt;site&gt;/FrontendSources, or null.</summary>
        public static string? BinFor(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            string full = path.Replace('\\', '/');
            int at = full.IndexOf("/FrontendSources/", StringComparison.OrdinalIgnoreCase);
            if (at < 0)
            {
                return null;
            }

            string bin = Path.Combine(full.Substring(0, at), "Bin");
            return Directory.Exists(bin) ? Path.GetFullPath(bin) : null;
        }

        // The newest write among the assemblies and the source code beside them: what a rebuild or publish touches.
        private static DateTime Stamp(string bin)
        {
            DateTime newest = DateTime.MinValue;
            try
            {
                foreach (string file in Directory.GetFiles(bin, "*.dll"))
                {
                    newest = Max(newest, File.GetLastWriteTimeUtc(file));
                }

                string runtimeCode = Path.Combine(Path.GetDirectoryName(bin) ?? bin, "App_RuntimeCode");
                if (Directory.Exists(runtimeCode))
                {
                    foreach (string file in Directory.GetFiles(runtimeCode, "*.cs", SearchOption.AllDirectories))
                    {
                        newest = Max(newest, File.GetLastWriteTimeUtc(file));
                    }
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // Mid-publish: try again next time round.
                return DateTime.MaxValue;
            }

            return newest;
        }

        private static DateTime Max(DateTime a, DateTime b)
        {
            return a > b ? a : b;
        }

        private sealed class Entry
        {
            public Entry(SiteMetadata? metadata, DateTime stamp)
            {
                Metadata = metadata;
                Stamp = stamp;
            }

            public SiteMetadata? Metadata { get; }

            public DateTime Stamp { get; }

            public DateTime CheckedAt { get; set; }
        }
    }
}
