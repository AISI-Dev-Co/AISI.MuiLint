# Security

MuiLint reads HTML, TypeScript and `.editorconfig` files from disk and writes findings. It doesn't run code from the
files it scans or talk to the network.

If you find something that could be abused, such as a crafted file that hangs the scanner, crashes Visual Studio or
makes the CLI read files it shouldn't, please report it privately through
[GitHub's private vulnerability reporting](https://github.com/AISI-Dev-Co/AISI.MuiLint/security/advisories/new)
rather than opening a public issue. Expect a reply within a week.

Only the latest release is supported.
