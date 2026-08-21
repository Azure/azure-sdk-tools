# Assessment Workflow

## Prepare evidence

Run from the repository root:

```bash
node .github/skills/azure-typespec-assessment/scripts/prepare-assessment.mjs \
  --output .typespec-assessment
```

Optional flags:

| Flag                | Purpose                                                                       |
| ------------------- | ----------------------------------------------------------------------------- |
| `--base <ref>`      | Override the auto-detected tracked/default remote baseline                    |
| `--repo <path>`     | Assess another Git worktree                                                   |
| `--output <path>`   | Change the artifact directory                                                 |
| `--skip-compile`    | Debug project/source discovery only; never use for a final assessment         |
| `--skip-validation` | Experimental timing comparison only; records repository validation as skipped |

The script compares the baseline merge-base with committed, staged, unstaged, and untracked work. It discovers affected TypeSpec projects where `tspconfig.yaml`/`tspconfig.yml` and `main.tsp` coexist in either the baseline or head, so deleting an entrypoint still produces removal evidence. Before compilation, it verifies that the active Node version satisfies `package.json#engines.node` and that the installed TypeSpec compiler, OpenAPI, AutoRest, ARM, and client-generator-core libraries exactly match `package-lock.json`.

The baseline and head use isolated sparse worktrees containing only the parent roots of affected projects. The head worktree overlays and commits the relevant staged, unstaged, and untracked source so validation cannot format or regenerate files in the user's worktree.

For every affected head project, the script also runs the repository-native
`eng/tools/typespec-validation/cmd/tsv.js` CLI. This is the same underlying
tool used by the `TypeSpec Validation` pipeline and includes ruleset selection,
`tsp compile --warn-as-error`, generated-output drift, formatting, and
repository configuration checks. A failed or unavailable validator blocks a
complete compliance assessment. A successful run is evidence only for checks
the tool implements; documentation assessment remains required for patterns the
linter does not detect.

Base and head then compile concurrently while AutoRest and generic TCGC run as individually timed stages. Package CLI JavaScript entrypoints are launched with the active `process.execPath`, so a runtime selected through commands such as `npx node@24` remains active. Progress and elapsed time are written to stderr for baseline resolution, sparse checkout, repository validation, emitter compilation, artifact diffs, and overall completion.

Generated emitter configs retain the project's existing options. AutoRest keeps options such as `output-splitting: legacy-feature-files` while using deterministic assessment output directories and `{version-status}/{version}/{feature}.json` files. Generic TCGC emits all API versions.

The script writes:

- `evidence.json`: changed files, source hunks, projects, repository-validation and compile results, total and per-stage `durationMs`, and precise failure summaries;
- `artifacts/<project>/<side>/<emitter>/`: generated OpenAPI and TCGC artifacts;
- `diffs/<project>/<emitter>/`: paired artifact diffs;
- `compile-logs/`: explicit failures and diagnostics.

The final report records measured preparation and total assessment time per PR.

## Assess

1. Read `evidence.json` and every changed TypeSpec hunk.
2. Correlate related decorators and declarations into one author intent.
3. Use AutoRest diffs for wire behavior and TCGC diffs for client shape.
4. Inspect source decorators directly for scoped client behavior missing from generic TCGC.
5. Follow [documentation-grounded compliance](compliance.md) by running the
   author skill's shared agentic search procedure. Search the fetched page
   content for the intent-derived query and iterate until the applicable
   guidance is resolved.
6. Record each fetched document's matching section and short verbatim excerpt,
   then compare it with the exact changed declaration. Report a source-linked
   finding only for a documented mismatch.
7. For every operation directly or transitively affected by the diff, read the
   emitted operation and record its signature, parameters, request, responses,
   LRO/paging metadata, and service behavior.
8. Write `assessment.json`, render the assessment-first Markdown with
   `scripts/render-assessment.mjs`, and validate both reports.

Repository-validation or compilation failure blocks a complete assessment. Report the validator's `failureSummary`, or the first compiler diagnostic, error count, and referenced compile log; never interpret missing artifacts as “no break.”

Temporary worktrees are removed automatically. Generated assessment output is not committed unless the user requests it.
