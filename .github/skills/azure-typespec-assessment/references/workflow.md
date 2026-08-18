# Assessment Workflow

## Prepare evidence

Run from the repository root:

```bash
node .github/skills/azure-typespec-assessment/scripts/prepare-assessment.mjs \
  --output .typespec-assessment
```

Optional flags:

| Flag              | Purpose                                                               |
| ----------------- | --------------------------------------------------------------------- |
| `--base <ref>`    | Override the auto-detected tracked/default remote baseline            |
| `--repo <path>`   | Assess another Git worktree                                           |
| `--output <path>` | Change the artifact directory                                         |
| `--skip-compile`  | Debug project/source discovery only; never use for a final assessment |

The script compares the baseline merge-base with committed, staged, unstaged, and untracked work. It discovers affected TypeSpec projects, compiles base/head with AutoRest and generic TCGC, and writes:

- `evidence.json`: changed files, source hunks, projects, compile results;
- `artifacts/<project>/<side>/<emitter>/`: generated YAML;
- `diffs/<project>/<emitter>/`: paired artifact diffs;
- `compile-logs/`: explicit failures and diagnostics.

## Assess

1. Read `evidence.json` and every changed TypeSpec hunk.
2. Correlate related decorators and declarations into one author intent.
3. Use AutoRest diffs for wire behavior and TCGC diffs for client shape.
4. Inspect source decorators directly for scoped client behavior missing from generic TCGC.
5. For every operation directly or transitively affected by the diff, read the
   emitted operation and record its signature, parameters, request, responses,
   LRO/paging metadata, and service behavior.
6. Write both reports and validate them.

Compilation failure blocks a complete assessment. Report the error; never interpret missing artifacts as “no break.”

Temporary worktrees are removed automatically. Generated assessment output is not committed unless the user requests it.
