# Security

MuiLint reads files and reports findings. It reads HTML, TypeScript, `.editorconfig` and `.cs` files, the site's
`client-controls` package, and the assemblies in the site's `Bin` as metadata, without loading them. It doesn't run
code from the files it scans or talk to the network.

If you find something that could be abused, such as a crafted file that hangs the scanner, crashes Visual Studio or
makes the CLI read files it shouldn't, report it privately through
[GitHub's private vulnerability reporting](https://github.com/AISI-Dev-Co/AISI.MuiLint/security/advisories/new)
rather than opening a public issue. Expect a reply within a week.

Only the latest release is supported.
