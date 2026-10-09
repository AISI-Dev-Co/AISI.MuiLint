#nullable disable
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;

namespace AISI.MuiLint.Vsix
{
    /// <summary>
    /// VS 2022 package: binding path for <c>AISI.MuiLint.dll</c>, and the
    /// <see cref="AsyncPackage"/> joinable task factory used by the HTML tagger.
    /// <c>ThreadHelper.JoinableTaskFactory</c> is not in the IDE collection (VSSDK007).
    /// Auto-load on shell init so InitializeAsync typically runs before the first htmlx
    /// FileAndForget; MEF context is still the fallback until then. Never pragma VSSDK007.
    /// </summary>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("AISI MuiLint", "HTML merge linter for Acumatica Modern UI.", "0.2.0")]
    [ProvideAutoLoad(VSConstants.UICONTEXT.ShellInitialized_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideBindingPath]
    [Guid(MuiLintVsPackage.PackageGuidString)]
    public sealed class MuiLintVsPackage : AsyncPackage
    {
        /// <summary>Package GUID used in the generated pkgdef.</summary>
        public const string PackageGuidString = "e7c4b2a1-9d38-4f6c-8a15-3b9e0c7d2468";

        private static JoinableTaskFactory _packageJoinableTaskFactory;

        /// <summary>
        /// Factory captured in <see cref="InitializeAsync"/>. Null until the package loads.
        /// </summary>
        internal static JoinableTaskFactory PackageJoinableTaskFactory
        {
            get { return _packageJoinableTaskFactory; }
        }

        /// <inheritdoc />
        protected override async Task InitializeAsync(
            CancellationToken cancellationToken,
            IProgress<ServiceProgressData> progress)
        {
            _packageJoinableTaskFactory = this.JoinableTaskFactory;
            await base.InitializeAsync(cancellationToken, progress);
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            await MuiLintCommands.InitializeAsync(this);
        }
    }
}
