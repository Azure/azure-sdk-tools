# Assessment Workflow

## Prepare evidence

Run the complete deterministic preparation from the repository root:

```bash
node .github/skills/azure-typespec-assessment/scripts/run-assessment-analysis.mjs \
  --output .typespec-assessment
```

Optional flags:

| Flag                      | Purpose                                                               |
| ------------------------- | --------------------------------------------------------------------- |
| `--base <ref>`            | Override the auto-detected tracked/default remote baseline            |
| `--repo <path>`           | Assess another Git worktree                                           |
| `--output <path>`         | Change the artifact directory                                         |
| `--skip-compile`          | Debug project/source discovery only; never use for a final assessment |
| `--document-cache <path>` | Override the persistent fetched-document cache                        |
| `--artifact-cache <path>` | Override the persistent input-keyed emitter artifact cache            |
| `--checkout-cache <path>` | Override the persistent base/head sparse-worktree cache               |
| `--raw-artifact-diffs`    | Also generate expensive textual artifact diffs for diagnostics        |

To measure the complete local pre-PR workflow with and without warm caches,
including compilation and documentation search, run:

```bash
node .github/skills/azure-typespec-assessment/scripts/benchmark-assessment.mjs \
  --repo . \
  --base origin/main \
  --output .typespec-assessment-benchmark
```

The command performs one cold run and one input-identical warm run, then writes
`benchmark.json`. Use this result rather than `--skip-compile` timing when
evaluating assessment performance.

The primary scenario is a local pre-PR worktree. The script compares the
baseline merge-base with the current `HEAD` plus committed, staged, unstaged,
and untracked work. Historical PRs are regression evidence only; the workflow
does not require PR metadata or a remote branch. It discovers affected TypeSpec
projects where `tspconfig.yaml`/`tspconfig.yml` and `main.tsp` coexist in either
the baseline or head, so deleting an entrypoint still produces removal
evidence. Before compilation, it verifies that the active Node version satisfies
`package.json#engines.node` and that the installed TypeSpec compiler, OpenAPI,
AutoRest, ARM, and client-generator-core libraries exactly match
`package-lock.json`.

The baseline and head use isolated sparse worktrees containing only the parent
roots of affected projects. The head worktree overlays and commits the relevant
staged, unstaged, and untracked source so compilation cannot format or
regenerate files in the user's worktree.

Base/head and their isolated AutoRest and generic TCGC emitter runs execute
concurrently and remain individually timed. Package CLI JavaScript entrypoints
are launched with the active `process.execPath`, so a runtime selected through
commands such as `npx node@24` remains active. Progress and elapsed time are
written to stderr for baseline resolution, sparse checkout, emitter
compilation, artifact diffs, and overall completion.

AutoRest and TCGC artifacts are reused on warm local reruns only when the
synthetic project tree, which includes staged, unstaged, and untracked content,
the project emitter configuration, lockfile, Node version, and emitter identity
all match. Base and synthetic-head sparse worktrees are also retained and reset
to their input commits on later runs instead of being recreated. Structured
`analysis.json` is the default artifact comparison; textual `git diff
--no-index` files are generated only with `--raw-artifact-diffs`.

Generated emitter configs retain the project's existing options. AutoRest keeps options such as `output-splitting: legacy-feature-files` while using deterministic assessment output directories and `{version-status}/{version}/{feature}.json` files. Generic TCGC emits all API versions.

The script writes:

- `evidence.json`: changed files, source hunks, projects, compile results, total
  and per-stage `durationMs`, and precise failure summaries;
- `analysis.json`: canonical baseline/head REST operations, field-level changes,
  REST breaking candidates, and normalized TCGC downstream candidates;
- `compliance-evidence.json`: changed TypeSpec symbols, routed authoritative
  documents, content hashes, cache status, and matching excerpts;
- `model-input.json`: the minified, size-gated deterministic input for model
  review, including operation groups, compact before/after summaries,
  source-impact links, semantic review units, declaration-document compliance
  review items, documentation evidence, and timing;
- `assessment-draft.json`: a pretty-printed diagnostic copy of the same bounded
  input;
- `artifacts/<project>/<side>/<emitter>/`: generated OpenAPI and TCGC artifacts;
- `diffs/<project>/<emitter>/`: paired artifact diffs;
- `compile-logs/`: explicit failures and diagnostics.

The final report records measured preparation and total assessment time per PR.

## Fast impact-only assessment

Use fast mode when the user needs only actionable REST, SDK/downstream, and
compliance impacts:

```bash
node .github/skills/azure-typespec-assessment/scripts/run-assessment-analysis.mjs \
  --fast --output .typespec-fast-assessment
```

This writes `fast-model-input.json` and
`fast-assessment-draft.json`. Review every REST and downstream candidate,
compare compliance evidence with changed source, and write
`fast-assessment-judgment.json` following
`scripts/fast-assessment-judgment.schema.json`. Approved findings must include
actual and expected behavior, severity, confidence, evidence, affected
operations, and source-change IDs or paths. Rejected candidates require a
rationale. Decide every compliance review item exactly once; each failed
decision requires its own finding with the review item's document and exact
source-change ID.

Render the standalone report with:

```bash
node .github/skills/azure-typespec-assessment/scripts/assemble-fast-assessment.mjs \
  .typespec-fast-assessment/fast-model-input.json \
  fast-assessment-judgment.json \
  assessment.html
```

Fast mode omits semantic intents and non-actionable change explanations. It
still performs deterministic compilation, artifact comparison, and
documentation retrieval so an empty impact report remains evidence-based.

## Assess

1. Read `model-input.json`. Consult raw `evidence.json` or artifacts by stable
   evidence ID only when a candidate is marked ambiguous or unsupported.
2. Write exactly one author intent for every `semanticReviewUnit`. Preserve its
   resource-family plus observable-behavior boundary and copy its evidence IDs
   exactly. Only a version-propagation unit may group unchanged contracts.
3. Use AutoRest diffs for wire behavior and TCGC diffs for client shape.
4. Inspect source decorators directly for scoped client behavior missing from generic TCGC.
5. Follow [documentation-grounded compliance](compliance.md) by running the
   author skill's shared agentic search procedure. Search the fetched page
   content for the intent-derived query and iterate until the applicable
   guidance is resolved.
6. Record each fetched document's matching section and short verbatim excerpt,
   then decide every `complianceEvidence.reviewItems` comparison. Report one
   source-linked finding for each `applicable-fail` decision; do not combine
   declarations.
7. For every operation directly or transitively affected by the diff, compare
   baseline and head artifacts field by field. Classify it as added, modified,
   or removed; record only changed aspects as structured before/after values
   and link the exact TypeSpec cause.
8. Attach the corresponding real TypeSpec Git hunks to each semantic change.
   Keep behavior before/after text separate from source diff lines. Select
   excerpts around the declarations and decorators named by the TypeSpec change
   summary. Link semantic changes to the breaking or compliance findings they
   cause.
9. Preserve the complete signature, parameters, request, responses,
   LRO/paging metadata, and service behavior in
   `restRepresentation.operations`. These details remain in JSON and are
   retrieved through the report's generated copyable prompts.
10. Validate and write `assessment.json`, then render `assessment.html` with
    `scripts/render-assessment-html.mjs`. Do not generate Markdown.

Compilation failure blocks a complete assessment. Report the first compiler
diagnostic, error count, and referenced compile log; never interpret missing
artifacts as “no break.”

Temporary worktrees are removed automatically. Generated assessment output is not committed unless the user requests it.

For a curated evidence set, run
`evals/scripts/finalize-rerun-assessments.mjs`. The finalizer validates
each structured assessment and retains `assessment.json` and
`assessment.html`.
