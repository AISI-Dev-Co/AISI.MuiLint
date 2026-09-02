#nullable disable
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace AISI.MuiLint.Vsix
{
    /// <summary>
    /// Minimal VS 2022 package that writes a binding path into the VSIX pkgdef so
    /// <c>AISI.MuiLint.dll</c> loads from the same folder as this assembly.
    /// HTML squiggles and the Error List are MEF exports, not this package.
    /// </summary>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("AISI MuiLint", "HTML merge linter for Acumatica Modern UI.", "0.1.0")]
    [ProvideBindingPath]
    [Guid(MuiLintVsPackage.PackageGuidString)]
    public sealed class MuiLintVsPackage : AsyncPackage
    {
        /// <summary>Package GUID used in the generated pkgdef.</summary>
        public const string PackageGuidString = "e7c4b2a1-9d38-4f6c-8a15-3b9e0c7d2468";
    }
}
