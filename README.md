# AISI.MuiLint

MIT HTML merge linter for Acumatica Modern UI.

**Acuminator lints C#.** This catches HTML merge traps in Visual Studio (and from the
command line). It is not a second AcuMate: it does not convert Classic UI, does not emit
TypeScript, and does not copy GPL source. It reads raw HTML text and reports five merge
mistakes that the Acumatica frontend merge will silently mishandle.

The primary artifact is a Visual Studio 2022 VSIX. A packable VSIX project is in
`src/AISI.MuiLint.Vsix` (Publisher **AISI Dev Co**, https://www.aisidev.com/, installation
target `[17.0,18.0)`). Packing the VSIX requires Windows and the Visual Studio SDK; on Linux
the project still compiles as `net472`.

## Diagnostics

Independent raw-HTML scanner. No `view.bind` analysis, no MuiMerge order, no MuiPack, no
`PX.*`, no `IsActive`.

| Id | What it catches | Why it is a merge trap |
| --- | --- | --- |
| **AISI0001** | Self-closing `<field …/>` or `<qp-…/>` | Modern UI merge does not treat a self-closing tag as a start/end pair. Use `<field name="X"></field>` (and the same for `qp-*`). |
| **AISI0002** | `after` / `before` `[name='X']` where `X` is a `name=` in the **same file** | Merge sees only stock HTML. A selector cannot target a field you just introduced in this extension. Point `after`/`before` at a stock name. |
| **AISI0003** | Path is stock `src/screens` | Custom and customized screens belong in `development/screens` (or `customizationScreens`), not the stock tree. Publishing from `src/screens` collides with the product HTML. |
| **AISI0004** | File under `/extensions/` whose basename equals the parent screen folder (`SO301000.html`) | An extension is `SO301000_Custom.html`, not `SO301000/extensions/SO301000.html`. The latter looks like the screen itself. |
| **AISI0005** | Empty `qp-fieldset` (whitespace or comments only) | Merge produces an empty fieldset. Fieldsets that only `modify` / `remove` / `replace` a stock element are allowed to be empty. |

## Layout

```
AISI.MuiLint.sln
Directory.Build.props
LICENSE
README.md
src/AISI.MuiLint              netstandard2.0  scanner + Roslyn additional-file analyzer
src/AISI.MuiLint.Cli          net8            `muilint` command
src/AISI.MuiLint.Vsix         net472          VS 2022 VSIX (package stub calls Analyzer)
tests/AISI.MuiLint.Tests      net8            fail/pass fixtures for all five ids
.github/workflows/ci.yml      ubuntu test + roslynator; windows pack vsix
```

## Command line

```sh
export DOTNET_ROOT=/home/box/.dotnet
export PATH="$DOTNET_ROOT:$DOTNET_ROOT/tools:$PATH"

dotnet run --project src/AISI.MuiLint.Cli -- path/to/screens
```

Exit code `0` if clean, `1` if any finding, `2` on usage error. Each line is:

```
path(line,column): AISI0001: Self-closing <field> is not valid …
```

## Visual Studio

Install the VSIX (Windows, VS 2022). The analyzer runs on `.html` **additional files** of C#
projects today. The package stub (`MuiLintPackage.AnalyzeHtml`) is the hook a later iteration
will bind to the HTML editor so you do not have to list files as additional files.

Until then, `muilint` is the reliable way to scan a `development/screens` tree.

## Build and test

```sh
export DOTNET_ROOT=/home/box/.dotnet
export PATH="$DOTNET_ROOT:$DOTNET_ROOT/tools:$PATH"

dotnet test tests/AISI.MuiLint.Tests/AISI.MuiLint.Tests.csproj -c Release
roslynator analyze src/AISI.MuiLint/AISI.MuiLint.csproj src/AISI.MuiLint.Cli/AISI.MuiLint.Cli.csproj tests/AISI.MuiLint.Tests/AISI.MuiLint.Tests.csproj
```

`csharp-ls` 0.16.0 is the language server on this box; do not bump it.

Packing the VSIX (Windows):

```bat
dotnet build src/AISI.MuiLint.Vsix/AISI.MuiLint.Vsix.csproj -c Release
```

Linux will compile the VSIX project as `net472` (reference assemblies) and will **not** emit
a `.vsix` — that is expected. Windows CI packs it.

## License

MIT. Copyright (c) 2026 AISI Dev Co.
