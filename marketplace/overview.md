# AISI MuiLint

Squiggles for the Acumatica Modern UI HTML and TypeScript mistakes that the frontend merge otherwise swallows without a word.

Acuminator lints your C#. MuiLint lints the front end of your customisation: the HTML extension files under `development/screens` that get merged into the stock screen at build time, and the TypeScript behind them. When the merge can't place something, it doesn't tell you. The field just isn't there. MuiLint tells you while you're still typing.

## What it catches

| Id | Catches |
| --- | --- |
| AISI0001 | Self-closing `<field/>` or `<qp-*/>` tags |
| AISI0002 | `after`/`before` pointing at a field this same file adds |
| AISI0003 | Customisations saved under the stock `src/screens` tree |
| AISI0004 | `SO301000/extensions/SO301000.html` (an extension named like the screen) |
| AISI0005 | Empty `qp-fieldset` |
| AISI0006 | Malformed selectors: unclosed `[`, `(` or quotes |
| AISI0007 | Extension HTML with no `.ts` of the same name, so it never loads |
| AISI0008 | The same id, or the same field in the same view, twice |
| AISI0009 | Selectors naming a `[name]` or `#id` the stock screen doesn't have |
| AISI0010 | Added fields without the `Usr` prefix (a hint, not an error) |
| AISI0011 | `view.bind`, fields, `state.bind` or a `qp-panel` id that the screen's `.ts` doesn't declare |
| AISI0012 | `qp-*` controls without an `id` (a hint) |
| AISI0013 | A `config.bind` with an unclosed brace, bracket or quote |
| AISI0014 | Half a TypeScript extension: the interface without its class, or the other way round |
| AISI0015 | `primaryView` or `@handleEvent` naming a view the screen doesn't have |
| AISI0016 | A view created from a class that isn't a `PXView` |

Each id in the Error List links to a page explaining the rule, with a before and after.

## And also

- **Lightbulb fixes** (Ctrl+.): close a self-closing tag, remove an empty fieldset, declare a missing field in your TypeScript extension (writing the extension if there isn't one), create a missing extension `.ts`, or suppress a finding on the line or in the file.
- **Completions** for Modern UI tags and attributes; the stock screen's `#ids` and `[name='…']` values in every merge selector, scoped to the container you've named; the views, fields and actions your TypeScript declares; and the fields of the C# DAC extensions in your solution.
- **Go to definition** (F12) and **hover** from a selector into the stock screen HTML, and from a view, action or field into its TypeScript declaration.
- **Severities from `.editorconfig`**, using the same `dotnet_diagnostic.AISI0008.severity = error` keys as Roslyn.
- A **command-line tool and a GitHub Action** that run the same rules in CI, with SARIF output for code scanning.

Free and MIT licensed. Source, issues and the CLI: [github.com/AISI-Dev-Co/AISI.MuiLint](https://github.com/AISI-Dev-Co/AISI.MuiLint).
