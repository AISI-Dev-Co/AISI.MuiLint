using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace AISI.MuiLint.Site
{
    /// <summary>
    /// Reads an Acumatica site's Bin folder into <see cref="SiteMetadata"/> from metadata alone:
    /// no assembly is loaded and no method body is decoded.
    /// </summary>
    public static class SiteMetadataReader
    {
        /// <summary>
        /// Reads the graphs, views, actions, DAC fields and features of the assemblies in
        /// <paramref name="binFolder"/>, skipping files that aren't readable .NET assemblies.
        /// </summary>
        /// <returns>The metadata, or null when the folder doesn't exist or holds no readable .NET assembly.</returns>
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

                return readers.Count == 0 ? null : new SiteTypes(readers).Build();
            }
            finally
            {
                foreach (PEReader pe in open)
                {
                    pe.Dispose();
                }
            }
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
