using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;

namespace AISI.MuiLint.Site
{
    /// <summary>Reads an Acumatica site's Bin folder into <see cref="SiteMetadata"/> from metadata alone, loading no assembly.</summary>
    public static class SiteMetadataReader
    {
        private static readonly Regex Identifier = new Regex("[A-Za-z_][A-Za-z0-9_]*", RegexOptions.CultureInvariant);

        /// <summary>Reads the assemblies in <paramref name="binFolder"/>, or returns null when it holds no readable .NET assembly.</summary>
        public static SiteMetadata? Read(string binFolder)
        {
            if (string.IsNullOrEmpty(binFolder) || !Directory.Exists(binFolder))
            {
                return null;
            }

            string[] files;
            try
            {
                files = Directory.GetFiles(binFolder, "*.dll");
            }
            catch (Exception ex) when (IsReadError(ex))
            {
                return null;
            }

            // Sorted so that, when two assemblies define the same type, the same one wins every time.
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            var open = new List<PEReader>();
            try
            {
                var readers = new List<MetadataReader>();
                foreach (string file in files)
                {
                    MetadataReader? reader = Open(file, open);
                    if (reader != null)
                    {
                        readers.Add(reader);
                    }
                }

                return readers.Count == 0 ? null : new SiteTypes(readers).Build(SourceNames(binFolder));
            }
            finally
            {
                foreach (PEReader pe in open)
                {
                    pe.Dispose();
                }
            }
        }

        // Customisation source in App_RuntimeCode is compiled elsewhere, so any identifier in it may be declared there.
        private static HashSet<string> SourceNames(string binFolder)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string? site = Path.GetDirectoryName(Path.GetFullPath(binFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            string folder = Path.Combine(site ?? binFolder, "App_RuntimeCode");
            try
            {
                if (Directory.Exists(folder))
                {
                    foreach (string file in Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories))
                    {
                        foreach (Match name in Identifier.Matches(File.ReadAllText(file)))
                        {
                            names.Add(name.Value);
                        }
                    }
                }
            }
            catch (Exception ex) when (IsReadError(ex))
            {
                // Half a list only makes the rules quieter, which is the safe way to be wrong.
            }

            return names;
        }

        private static MetadataReader? Open(string file, List<PEReader> open)
        {
            FileStream? stream = null;
            try
            {
                stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                var pe = new PEReader(stream);
                stream = null;
                open.Add(pe);
                return pe.HasMetadata ? pe.GetMetadataReader() : null;
            }
            catch (Exception ex) when (IsReadError(ex))
            {
                return null;
            }
            finally
            {
                stream?.Dispose();
            }
        }

        private static bool IsReadError(Exception ex)
        {
            return ex is BadImageFormatException
                || ex is IOException
                || ex is UnauthorizedAccessException
                || ex is InvalidOperationException
                || ex is ArgumentException
                || ex is NotSupportedException;
        }
    }
}
