#nullable disable
using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;

namespace AISI.MuiLint.Vsix
{
    internal static class VsDocuments
    {
        /// <summary>Opens <paramref name="path"/> in its editor and returns the view, or null if VS won't open it.</summary>
        public static IVsTextView Open(System.IServiceProvider services, string path)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                VsShellUtilities.OpenDocument(
                    services,
                    Path.GetFullPath(path),
                    VSConstants.LOGVIEWID.TextView_guid,
                    out IVsUIHierarchy _,
                    out uint _,
                    out IVsWindowFrame _,
                    out IVsTextView view);
                return view;
            }
            catch (COMException)
            {
                return null;
            }
        }
    }
}
