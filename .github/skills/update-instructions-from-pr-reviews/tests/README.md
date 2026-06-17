# Tests

Unit tests for the bundled scripts. **Not loaded by the skill** — kept here only for maintainers.

## Run

From the skill folder:

```bash
pnpm test                   # one-shot run
pnpm run test:watch         # interactive watch mode
pnpm run test:coverage      # text summary + coverage/lcov.info
```

No build step. Requires **Node ≥ 24**.

Vitest runs the TypeScript test files directly and is a better fit for the VS Code Vitest extension and editor coverage tooling.

## Run the skill from Copilot CLI

This folder includes a local plugin manifest (`plugin.json`) so you can run it as a real Copilot plugin without moving files.

From this skill folder:

```bash
copilot --plugin-dir . -i "Use the update-instructions-from-pr-reviews skill on this repository. Mine review feedback from the last 50 merged PRs and propose updates to .github/copilot-instructions.md with source PR citations."
```

To install from this repository subdirectory:

```bash
copilot plugin install richardpark-msft/ccrtools:skills/update-instructions-from-pr-reviews
```

## Adding tests

- Use `vitest` for `describe` / `it`. `node:assert/strict` is still fine for assertions.
- Import the script under test and call its exported functions directly.
- Prefer synthetic fixtures over real PR dumps so tests stay deterministic and small.
- Scripts that mostly shell out to `gh` (`resolve-repo`, `list-prs`, `fetch-pr`) aren't unit-tested — exercise them with a smoke run against a real repo instead.

## Excluding from distribution

If you ship the skill via `git archive` / tarball and want to strip tests, add to the repo root `.gitattributes`:

```
skills/update-instructions/tests/ export-ignore
```

Plain `git clone` will still include the folder — but it costs zero tokens, since nothing in `SKILL.md` links to it.
