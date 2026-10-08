#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AISI.MuiLint;

namespace AISI.MuiLint.Vsix
{
    /// <summary>
    /// Fields of the C# DAC extensions under the solution folder, for field-name completions.
    /// Built in the background; until it's ready, completions simply don't include them.
    /// </summary>
    internal static class DacFieldIndex
    {
        private static readonly object Gate = new object();
        private static readonly string[] SkippedFolders = { "bin", "obj", "node_modules", "FrontendSources", "Pages" };
        private static string _root;
        private static IReadOnlyList<DacField> _fields = Array.Empty<DacField>();
        private static DateTime _builtAt;
        private static bool _building;

        /// <summary>What is known for <paramref name="root"/> now; rebuilds in the background when it's a minute old.</summary>
        public static IReadOnlyList<DacField> Get(string root)
        {
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            {
                return Array.Empty<DacField>();
            }

            lock (Gate)
            {
                bool stale = !string.Equals(root, _root, StringComparison.OrdinalIgnoreCase) || DateTime.UtcNow - _builtAt > TimeSpan.FromMinutes(1);
                if (stale && !_building)
                {
                    _building = true;
                    _ = Task.Run(() => Build(root));
                }

                return string.Equals(root, _root, StringComparison.OrdinalIgnoreCase) ? _fields : Array.Empty<DacField>();
            }
        }

        /// <summary>
        /// Where the C# for <paramref name="path"/> lives: walking up, the first folder with a .sln, or
        /// the site's App_Data/Projects, where Acumatica keeps extension libraries. Null if neither.
        /// </summary>
        public static string FindSourceFolder(string path)
        {
            try
            {
                for (string dir = Path.GetDirectoryName(path); !string.IsNullOrEmpty(dir); dir = Path.GetDirectoryName(dir))
                {
                    string projects = Path.Combine(dir, "App_Data", "Projects");
                    if (Directory.EnumerateFiles(dir, "*.sln").GetEnumerator().MoveNext())
                    {
                        return dir;
                    }

                    if (Directory.Exists(projects))
                    {
                        return projects;
                    }
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            return null;
        }

        private static void Build(string root)
        {
            var fields = new List<DacField>();
            try
            {
                var folders = new Stack<string>();
                folders.Push(root);
                while (folders.Count > 0)
                {
                    string folder = folders.Pop();
                    foreach (string sub in Directory.EnumerateDirectories(folder))
                    {
                        string name = Path.GetFileName(sub);
                        if (!name.StartsWith(".", StringComparison.Ordinal) && Array.IndexOf(SkippedFolders, name) < 0)
                        {
                            folders.Push(sub);
                        }
                    }

                    foreach (string file in Directory.EnumerateFiles(folder, "*.cs"))
                    {
                        if (new FileInfo(file).Length < 1024 * 1024)
                        {
                            fields.AddRange(DacFields.Read(File.ReadAllText(file)));
                        }
                    }
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            finally
            {
                lock (Gate)
                {
                    _root = root;
                    _fields = fields;
                    _builtAt = DateTime.UtcNow;
                    _building = false;
                }
            }
        }
    }
}
