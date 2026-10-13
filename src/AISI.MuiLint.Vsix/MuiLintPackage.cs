using System;
using System.Collections.Generic;
using System.IO;
using AISI.MuiLint;
using AISI.MuiLint.Site;

namespace AISI.MuiLint.Vsix
{
    /// <summary>Visual Studio entry point for AISI.MuiLint analysis.</summary>
    public static class MuiLintPackage
    {
        /// <summary>Lints an HTML or TypeScript buffer and returns its findings in source order.</summary>
        public static IReadOnlyList<Diagnostic> Analyze(string path, string text)
        {
            // A buffer with no real file behind it has no neighbours to look at.
            return Path.IsPathRooted(path) ? MuiLinter.Analyze(path, text, TryReadFile, TryListFolder, TrySite(path)) : MuiLinter.Analyze(path, text, null);
        }

        /// <summary>The site's metadata, or null if the Bin reader can't load in this Visual Studio.</summary>
        public static SiteMetadata? TrySite(string path)
        {
            // System.Reflection.Metadata comes from devenv; without it only the Bin rules go quiet, not the rest.
            try
            {
                return SiteCache.ForFile(path);
            }
            catch (FileNotFoundException)
            {
                return null;
            }
            catch (FileLoadException)
            {
                return null;
            }
        }

        /// <summary>What's directly inside a folder, or nothing if it's missing or locked.</summary>
        public static IEnumerable<string> TryListFolder(string path)
        {
            try
            {
                return Directory.Exists(path) ? Directory.GetFileSystemEntries(path) : Array.Empty<string>();
            }
            catch (IOException)
            {
                return Array.Empty<string>();
            }
            catch (UnauthorizedAccessException)
            {
                return Array.Empty<string>();
            }
        }

        /// <summary>Reads a file, or returns null if it is missing or locked.</summary>
        public static string? TryReadFile(string path)
        {
            try
            {
                return File.Exists(path) ? File.ReadAllText(path) : null;
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }
    }
}
