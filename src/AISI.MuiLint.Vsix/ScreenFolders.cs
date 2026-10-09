using System;
using System.Collections.Generic;
using System.IO;
using AISI.MuiLint;

namespace AISI.MuiLint.Vsix
{
    /// <summary>Finds the Modern UI files to lint on disk for Extensions › AISI MuiLint › Lint Modern UI Screens.</summary>
    internal static class ScreenFolders
    {
        private const string DevelopmentScreens = "FrontendSources/screen/src/development/screens";

        /// <summary>
        /// The <c>FrontendSources/screen/src/development/screens</c> folders of the sites around
        /// <paramref name="start"/>: in it or any folder above it (a solution in a site's
        /// App_Data/Projects), or a few levels below it (a repository with the site inside).
        /// </summary>
        public static IReadOnlyList<string> FindDevelopmentScreens(string start)
        {
            if (start is null)
            {
                throw new ArgumentNullException(nameof(start));
            }

            var found = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (DirectoryInfo? folder = new DirectoryInfo(start); folder != null; folder = folder.Parent)
            {
                Add(Path.Combine(folder.FullName, DevelopmentScreens), found, seen);
            }

            Below(start, 3, found, seen);
            return found;
        }

        /// <summary>
        /// The .html and .ts files under <paramref name="folder"/> that MuiLint checks, in a stable
        /// order, skipping node_modules and dot-folders.
        /// </summary>
        public static List<string> Files(string folder)
        {
            var files = new List<string>();
            AddFiles(folder, files);
            return files;
        }

        private static void AddFiles(string folder, List<string> files)
        {
            var found = new List<string>();
            foreach (string file in Directory.GetFiles(folder))
            {
                if (MuiLinter.CanScan(file))
                {
                    found.Add(file);
                }
            }

            found.Sort(StringComparer.Ordinal);
            files.AddRange(found);

            string[] subfolders = Directory.GetDirectories(folder);
            Array.Sort(subfolders, StringComparer.Ordinal);
            foreach (string subfolder in subfolders)
            {
                string name = Path.GetFileName(subfolder);
                if (name != "node_modules" && !name.StartsWith(".", StringComparison.Ordinal))
                {
                    AddFiles(subfolder, files);
                }
            }
        }

        private static void Below(string folder, int depth, List<string> found, HashSet<string> seen)
        {
            if (depth == 0)
            {
                return;
            }

            string[] subfolders;
            try
            {
                subfolders = Directory.GetDirectories(folder);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return;
            }

            Array.Sort(subfolders, StringComparer.Ordinal);
            foreach (string subfolder in subfolders)
            {
                string name = Path.GetFileName(subfolder);
                if (name == "node_modules" || name.StartsWith(".", StringComparison.Ordinal)
                    || string.Equals(name, "bin", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(name, "obj", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Found a site: don't wander into its thousands of folders looking for another.
                if (!Add(Path.Combine(subfolder, DevelopmentScreens), found, seen))
                {
                    Below(subfolder, depth - 1, found, seen);
                }
            }
        }

        private static bool Add(string candidate, List<string> found, HashSet<string> seen)
        {
            string full = Path.GetFullPath(candidate);
            if (!Directory.Exists(full))
            {
                return false;
            }

            if (seen.Add(full))
            {
                found.Add(full);
            }

            return true;
        }
    }
}
