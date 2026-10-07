# Changelog

All notable changes to this project are documented here. Versions follow [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added

- **AISI0014** (warning): half a TypeScript extension, an `interface X extends Y` without its `class X` or the other
  way round.
- **AISI0015** (warning): `primaryView` in `@graphInfo`, or `view` in `@handleEvent`, naming a view the screen doesn't
  have.
- **AISI0016** (error): `createSingle`/`createCollection` given a class that isn't a `PXView`.
- **AISI0017** (error): an extension `.html` or `.ts` under `development/screens` or `customizationScreens` that isn't in
  an `extensions` folder, so the build never merges it.
- The CLI, the Action and the VSIX now check `.ts` files under `screens` and `customizationScreens` folders, with
  `// muilint-disable` comments.
- VSIX lightbulb: declare a field the HTML uses but the TypeScript doesn't (AISI0011), writing the extension class and
  its imports when needed. Another creates the missing extension `.ts` (AISI0007).
- VSIX completions:
  - `#ids` as well as `[name='…']` in every merge selector, not just `after`/`before`, with names scoped to the
    container the selector already names;
  - attributes the stock screen uses on a tag, and the values it gives them;
  - fields from the C# DAC extensions in your solution.
- VSIX hover on selectors and bindings.
- VSIX TypeScript snippets: `muifield`, `muifieldcc`, `muiaction`, `muicollection`, `muisingle`, `muiviewclass`,
  `muiext`, `muiscreenext`, `muiimports` and `muiimportscreen`.

### Changed

- AISI0009, AISI0011 and the TypeScript rules now see every extension of the screen: the stock screen's own
  `extensions` folder, `development/screens`, and every project under `customizationScreens`. A field or view another
  extension declares no longer gets reported as missing.

- Selector completions offer the fields and ids this file adds above the caret, then the stock screen's. Names from
  the `.ts` are no longer offered: a field the HTML doesn't place isn't an anchor.

### Removed

- **AISI0002** (after/before anchored on a field the same file adds). It was wrong: the merge applies elements in
  order, so a later element can anchor on an earlier one, and Acumatica's own training does exactly that. The id
  won't be reused.

## [0.3.0] - 2026-10-05

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

- AISI0009, AISI0011 and the TypeScript rules now see every extension of the screen: the stock screen's own
  `extensions` folder, `development/screens`, and every project under `customizationScreens`. A field or view another
  extension declares no longer gets reported as missing.

- CLI output is now MSBuild style (`path(line,col): error AISI0001: message`) followed by a summary on stderr.
- CLI exits 1 only for errors; warnings and suggestions are reported but don't fail the run.
- CLI exits 2 if any input is missing or unreadable, even when other files had findings, and no longer crashes on a
  locked file.
- CLI skips `node_modules` and dot-folders when scanning a directory.
- Release workflow stamps the tag's version into the VSIX manifest and assemblies.

## [0.1.0]

- AISI0001 to AISI0005, the VS 2022 VSIX (squiggles, Error List, completions), the CLI and the Roslyn analyser.
