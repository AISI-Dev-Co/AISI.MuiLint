# Changelog

All notable changes to this project are documented here. Versions follow [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added

- **AISI0011** (warning): a `view.bind`, field name, `state.bind` or `qp-panel` id that the screen's TypeScript
  doesn't declare. Reads the `.ts` beside the HTML and its imports, with extension interfaces merged in. No site
  needed.
- **AISI0012** (suggestion): a `qp-*` control without an `id`.
- **AISI0013** (error): a malformed `config.bind`.
- VSIX: completions for `view.bind`, `state.bind`, field names and `qp-panel` ids, taken from the screen's TypeScript.
- VSIX: go to definition (F12), from a selector into the stock screen HTML and from a binding into the `.ts`.
- A manual dry run for the release workflow, and a check of the packed VSIX's contents in CI and in releases.

## [0.2.0] - 2026-09-30

First public release.

### Added

- **AISI0006** malformed merge selector: an unclosed `[`, `(` or quote.
- **AISI0007** extension HTML with no `.ts` of the same name.
- **AISI0008** duplicate id, or the same field twice in one view (warning).
- **AISI0009** selector target not found in the stock screen HTML, following `qp-include`s (warning).
- **AISI0010** field added by an extension without the `Usr` prefix (suggestion).
- Severities: every finding is an error, warning or suggestion, and `dotnet_diagnostic.<id>.severity` in
  `.editorconfig` overrides it in the CLI and the VSIX, as it already did for the Roslyn analyser.
- `<!-- muilint-disable-next-line ID -->` and `<!-- muilint-disable ID -->` suppression comments.
- CLI `--format json|sarif|github`, `--help` and `--version`.
- GitHub Action (`uses: AISI-Dev-Co/AISI.MuiLint@v0.2.0`) with PR annotations and optional SARIF.
- VSIX lightbulb fixes: add the closing tag (AISI0001), remove an empty fieldset (AISI0005), suppress on the line
  or in the file.
- VSIX Error List severities and help links to `docs/rules/<id>.md`.
- VSIX `[name]` completions now read the real stock screen under `src/screens`; more `qp-*` tags and attributes.
- `examples/` with a stand-in stock screen, a clean extension and a broken one.

### Changed

- CLI output is now MSBuild style (`path(line,col): error AISI0001: message`) followed by a summary on stderr.
- CLI exits 1 only for errors; warnings and suggestions are reported but don't fail the run.
- CLI exits 2 if any input is missing or unreadable, even when other files had findings, and no longer crashes on a
  locked file.
- CLI skips `node_modules` and dot-folders when scanning a directory.
- Release workflow stamps the tag's version into the VSIX manifest and assemblies.

## [0.1.0]

- AISI0001 to AISI0005, the VS 2022 VSIX (squiggles, Error List, completions), the CLI and the Roslyn analyser.
