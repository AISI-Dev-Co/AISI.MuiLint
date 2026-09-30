# AISI MuiLint

Squiggles for the Acumatica Modern UI HTML mistakes that the frontend merge otherwise swallows without a word.

Acuminator lints your C#. MuiLint lints the `.html` side of your customisation: the extension files under `development/screens` that get merged into the stock screen at build time. When the merge can't place something, it doesn't tell you. The field just isn't there. MuiLint tells you while you're still typing.

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

Each id in the Error List links to a page explaining the rule, with a before and after.

## And also

- **Lightbulb fixes** (Ctrl+.): close a self-closing tag, remove an empty fieldset, or suppress a finding on the line or in the file.
- **Completions** for Modern UI tags, merge attributes, and `[name='…']` values read from the stock screen, so you anchor on a field that actually exists.
- **Severities from `.editorconfig`**, using the same `dotnet_diagnostic.AISI0008.severity = error` keys as Roslyn.
- A **command-line tool and a GitHub Action** that run the same rules in CI, with SARIF output for code scanning.

Free and MIT licensed. Source, issues and the CLI: [github.com/AISI-Dev-Co/AISI.MuiLint](https://github.com/AISI-Dev-Co/AISI.MuiLint).
