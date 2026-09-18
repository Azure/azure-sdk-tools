# Suppression-Based Azure Guidelines Design

## Status

Proposed design. The architecture and runtime contract are implementation
ready; no runtime integration is implemented yet.

## Goal

Replace Azure Guidelines catalog ranking, web retrieval, and Agent judgment
with the `@azure-tools/typespec-suppressions` analyzer from
`Azure/azure-rest-api-specs`.

Keep the final assessment report presentation stable:

- retain the **Azure Guidelines** dimension and report card;
- retain pass, fail, and not-assessed status;
- retain finding cards, source links, expected behavior, and actual TypeSpec;
- replace expected code with the tool-provided rule description and official
  documentation link.

The new Azure Guidelines dimension is fully deterministic and requires no AI.

## Decision and scope

The Azure Guidelines dimension becomes a **checked suppression review**, not a
general conformance assessment. It answers:

> Did this comparison add or change a suppression for a rule selected by the
> repository's checked-rules policy?

It does not prove that changed TypeSpec conforms to every Azure guideline.
Accordingly:

- `passed` means no checked suppression requires review in the completely
  analyzed project scope;
- `failed` means the repository policy requires review of at least one checked
  suppression;
- `not-assessed` means that answer cannot be computed completely.

The report card keeps the **Azure Guidelines** title for output compatibility,
but its description and empty state must say **Checked TypeSpec suppressions**.
Do not describe `passed` as broad guideline compliance or a checked
suppression as a confirmed guideline violation.

This design deliberately does not run linter rules. It reviews suppression
policy changes already represented in TypeSpec source and `tspconfig.yaml`.

## Source tool

Use:

```text
eng/tools/typespec-suppressions
```

from the assessed `Azure/azure-rest-api-specs` repository.

The tool:

- parses inline `#suppress` directives with the TypeSpec compiler AST;
- reads `linter.disable` entries from `tspconfig.yaml`;
- compares base and head suppressions;
- classifies new, changed, removed, and unchanged suppressions;
- identifies suppressions by project, source kind, rule, and structural anchor;
- applies the repository's checked-rules file;
- provides rule descriptions, official documentation URLs, and optional
  guideline codes;
- provides source file, line, raw suppression, and justification;
- reports whether new or changed checked suppressions require approval.

Do not copy its parser or checked ruleset into this skill. Do not replace its
AST analysis with regex scanning.

The integration targets the source tool's current `SuppressionReport`
interface. The package is private and has no package version, so identify it
with the supplying repository commit, its `package.json` hash, and a hash of
its runtime source files. The current implementation enriches new and changed
suppressions with rule metadata; removed and unchanged records are not
enriched. `guidelineCodes` is optional in the interface but is not currently
populated by the metadata resolver.

## Removed workflow

Remove:

- Semantic-intent Guidelines query profiles;
- the 31-entry document catalog;
- catalog scoring and ranking;
- four-document selection;
- `web_fetch`;
- document excerpt extraction;
- general agentic search;
- per-intent Guidelines decisions;
- Guidelines content in `model-input.json`;
- `dimensions/compliance-search-requests.json`;
- `compliance-search-evidence.json`;
- Guidelines decisions in `assessment-judgment.json`;
- Guidelines schema reads from the Agent workspace.

`references/agentic-search.md` and
`references/reference-document-links.md` are no longer runtime dependencies.
They may be deleted after historical documentation no longer links to them.

No general resource, operation, versioning, LRO, paging, model, enum, or
decorator assessment remains unless the change introduces or modifies a
checked suppression for the corresponding rule.

REST compatibility, downstream SDK compatibility, Semantic intents, and
Documentation Completeness remain unchanged.

## New workflow

```text
Base and current TypeSpec projects
              |
              v
@azure-tools/typespec-suppressions
              |
              v
Normalize complete and checked suppression diffs
              |
              v
Join exact changed TypeSpec from source-index.json
              |
              v
Map checked new/changed suppressions to Guidelines findings
              |
              v
Existing Azure Guidelines report card
              |
              v
assessment.json + assessment.html
```

There is no Guidelines Agent phase and no network documentation retrieval.

## Invocation

### Pull request and explicit base/head modes

Run the tool with exact commits from `preparation-manifest.json`:

```powershell
node <isolated-current-snapshot>\eng\tools\typespec-suppressions\cmd\typespec-suppressions.js `
  --base <merge-base-commit> `
  --head <head-commit> `
  --json-output <work>\raw-typespec-suppressions.json `
  --check-rules-file <isolated-current-snapshot>\eng\tools\typespec-suppressions\check-rules.json `
  <affected-TypeSpec-project>...
```

Run with the isolated assessed repository root as the current directory. Use
commit SHAs rather than temporary coordinator refs, `npx`, or a registry
package. Capture stdout, stderr, exit code, executable path, and Node.js version
in preparation provenance.

The analyzer code and checked-rules file come from the coordinator's isolated
current snapshot. Record their content hashes and the repository commit that
supplied them. Do not execute either from the user's mutable checkout.

The executable snapshot must include `cmd/typespec-suppressions.js`,
`src/**/*.ts`, `package.json`, the repository package-manager declaration, and
the lockfile needed to resolve runtime dependencies. The suppression tool
currently requires Node.js `>=24.14.1` and the repository currently declares
`pnpm@11.8.0`; read both requirements from the isolated snapshot rather than
hard-coding them in the adapter. Reuse or install dependencies only from that
snapshot's frozen lockfile. Never resolve the private package from a registry.

### Local complete mode

Use the exported `analyzeTypeSpecSuppressionsFromDirectories` API against the
isolated base and current worktrees. The current worktree must contain staged,
unstaged, and relevant untracked overlays.

Never run against the user's mutable checkout.

Directory mode accepts one common relative project path for both roots and
does not implement the Git mode's project-rename mapping. When preparation
detects a renamed project, create analyzer-only snapshots that place the base
and current project contents under the canonical current project path. Record
the original base path and canonical current path in coverage. If that
lossless mapping cannot be constructed, block the project instead of treating
the old project as empty.

The directory API reports directory names in `baseRevision`, `headRevision`,
and `repoRoot`. These values are execution details, not comparison identity.
The adapter must take commits, working-tree flags, and snapshot hashes from the
preparation manifest and must not copy directory names into the canonical
comparison.

### Project scope

Pass the TypeSpec project directories discovered during preparation. Each path
must contain `tspconfig.yaml` in the base or current revision.

Deduplicate and sort canonical current project paths. Record requested,
assessed, renamed, and blocked projects explicitly by path, not only as counts.
Exact coverage means every requested project appears once in either assessed
or blocked projects and no unrequested project appears.

Discover projects from the union of the isolated base and current snapshots,
not only from the current snapshot. Use the current path as canonical when a
project was renamed and retain both paths in `renamedProjects`. A deleted
project keeps its base path as the canonical path. Preflight-invalid projects
are recorded as blocked and omitted from the analyzer invocation; all
remaining projects are analyzed together. If that invocation fails, mark every
attempted project blocked and do not retain a partial analyzer result.

## Runtime capability

The analyzer is a private repository workspace package and may require a newer
Node.js runtime than the coordinator.

Verify:

- analyzer source or executable availability;
- compatible Node.js runtime;
- repository package manager, frozen lockfile, and runtime dependencies;
- checked-rules file;
- base and head objects;
- valid TypeSpec project paths.

Do not install an unpinned registry substitute. Do not turn tool failure into
an empty successful report.

If the analyzer cannot run, Azure Guidelines is `not-assessed`.

The analyzer's `loadCheckRulesFile` intentionally warns and returns an empty
array for a missing, malformed, or invalid file. The coordinator must therefore
parse the file independently before invocation and require an object with a
`rules` array containing only nonempty strings. Normalize the independently
parsed list exactly as the tool does: trim, remove empty values, deduplicate,
and sort with the tool's `localeCompare` ordering. After invocation, require:

- a `checkedSuppressions` block;
- exact equality between its normalized `checkRules`, the top-level
  `checkRules`, and the independently parsed rules;
- the recorded checked-rules SHA-256;
- internally consistent counts and aggregate/spec-level records.

A valid empty checked-rules array is allowed, but it must be distinguishable
from load failure. Analyzer warnings are retained as provenance and warnings
about the checked-rules file are blockers.

The raw analyzer report has no schema version. Treat its documented
`SuppressionReport` interface as adapter version 1: validate every consumed
field, reject unknown enum values and duplicate identities, and retain the raw
report hash. Also validate the per-spec arrays, aggregate arrays, and their
object content against one another; do not trust only top-level counts.
Changes to that interface require an adapter-version and fixture update.

## Canonical input

Normalize the tool report into:

```json
{
  "schemaVersion": 1,
  "comparison": {
    "baseCommit": "<sha>",
    "headCommit": "<sha>",
    "workingTree": {
      "staged": false,
      "unstaged": false,
      "untracked": false
    },
    "baseSnapshotSha256": "sha256:...",
    "currentSnapshotSha256": "sha256:..."
  },
  "tool": {
    "name": "@azure-tools/typespec-suppressions",
    "repositoryCommit": "<sha>",
    "packageJsonSha256": "sha256:...",
    "runtimeSourceSha256": "sha256:...",
    "nodeVersion": "<node version>",
    "rawReportSha256": "sha256:...",
    "checkRulesFile": "eng/tools/typespec-suppressions/check-rules.json",
    "checkRulesSha256": "sha256:...",
    "checkRules": ["@azure-tools/typespec-azure-resource-manager/arm-no-record"]
  },
  "coverage": {
    "requestedProjects": ["specification/..."],
    "assessedProjects": ["specification/..."],
    "renamedProjects": [],
    "blockedProjects": []
  },
  "counts": {
    "specs": 2,
    "base": 2407,
    "head": 2490,
    "new": 288,
    "changed": 6,
    "removed": 205,
    "unchanged": 2199
  },
  "checked": {
    "requiresApproval": true,
    "new": 16,
    "changed": 2,
    "removed": 0
  },
  "items": [
    {
      "id": "suppression-...",
      "change": "new",
      "checked": true,
      "identity": {
        "specPath": "specification/...",
        "sourceKind": "inline",
        "ruleName": "@azure-tools/typespec-azure-resource-manager/arm-no-record",
        "anchorPath": "namespace:Microsoft.Network/model:Selector/property:matchLabels"
      },
      "ruleMetadata": {
        "description": "Don't use Record types for ARM resources.",
        "documentationUrl": "https://...",
        "guidelineCodes": []
      },
      "before": null,
      "after": {
        "sourceFile": "specification/.../models.tsp",
        "line": 12984,
        "column": 3,
        "justification": "matchLabels is a pass-through Kubernetes label map.",
        "rawText": "#suppress ...",
        "sourceMapping": "exact-declaration",
        "sourceChangeIds": ["source-..."],
        "hunkIds": ["hunk-..."],
        "declarationIds": ["declaration-..."],
        "semanticIntentIds": ["semantic-..."],
        "actualCode": "#suppress ...\nmodel Selector { ... }",
        "actualCodeTruncated": false
      }
    }
  ],
  "blockers": []
}
```

Each blocked project is an object with nonempty `path`, `code`, and `message`
fields. Each renamed project records `basePath` and `currentPath`; the current
path is the canonical requested and assessed path.

`before` is null for a new suppression, `after` is null for a removed
suppression, and both are present for a changed suppression. Each side
preserves its own source location, raw text, justification, source mapping, and
bounded TypeSpec. Do not flatten changed records in the canonical artifact.
Report assembly may project convenience fields from the appropriate side.

`ruleMetadata` is optional for unchecked and removed items. It is required for
checked new and changed items, and its `description` must be nonempty.
`documentationUrl` and `guidelineCodes` remain optional.

Compute each snapshot hash from a canonical manifest of every `.tsp` and
`tspconfig.yaml` file read by the analyzer: normalized project-relative path,
file byte length, and SHA-256, sorted by path and encoded with `canonicalJson`.
This proves the exact directory-mode input without hashing unrelated sparse
worktree content.

Compute `runtimeSourceSha256` the same way over
`cmd/typespec-suppressions.js` and every `src/**/*.ts` file. Hash file bytes,
not Git object IDs or timestamps.

The PR 44988 trial produced:

| Scope | New | Changed | Removed |
| --- | ---: | ---: | ---: |
| Complete suppression diff | 288 | 6 | 205 |
| Repository checked rules | 16 | 2 | 0 |

Use the checked subset for findings. Preserve complete counts as provenance.

These trial counts are provisional. Regenerate the PR 44988 report before
using it as a golden fixture and determine whether the previously recorded
head count of `2492` was a transcription error or duplicate identity tuples
collapsed by the analyzer's internal maps. For every spec and for the
aggregate, require:

```text
base = unchanged + changed + removed
head = unchanged + changed + new
```

The adapter must reject duplicate identity tuples and unexplained count
non-conservation rather than normalize them away.

`items` contains every new, changed, and removed suppression, including
unchecked items. Do not copy unchanged suppression records into the normalized
artifact; preserve only their count. Sort items by project path, source kind,
rule name, anchor, and change. Set `checked` by exact membership in the
validated checked-rules list, not by trusting only the analyzer's filtered
arrays. Cross-check the reconstructed checked subset against
`checkedSuppressions`.

Stable IDs derive from:

- project path;
- source kind;
- rule name;
- structural anchor.

Line number and justification are evidence, not identity.

For changed suppressions, preserve the complete before and after analyzer
records. Use metadata from the enriched `after` record and require any metadata
copied onto `before` to be identical. Removed suppressions may lack rule
metadata because the analyzer enriches only new and changed records.

## Actual TypeSpec

`before.actualCode` and `after.actualCode` are deterministic.

For inline suppressions:

1. Resolve each analyzer record against the matching base or current isolated
   snapshot.
2. Verify `rawText` at the reported source file, line, and column.
3. Resolve the unique declaration in `source-index.json` for the same revision
   and file whose compiler span and kind/name agree with the analyzer anchor.
4. Use the existing bounded `sourceSnippet` contract: include attached
   documentation, decorators, the suppression directive, and up to 40 lines of
   the declaration; preserve the existing `truncated` state as
   `actualCodeTruncated`.

For `tspconfig.yaml`, parse the matching revision with the existing `yaml`
dependency, locate the mapping pair at the analyzer's line and column, verify
that its key and semantic value equal the analyzer record, and slice the
pair's exact source range. This preserves quoting and multiline
justifications without adding another suppression detector.

Record one source-mapping state:

- `exact-declaration` for an inline suppression mapped to a declaration;
- `exact-config-entry` for a `tspconfig.yaml` suppression;
- `exact-lines` when the analyzer location and revision source are verified but
  no declaration exists in `source-index.json`;
- `unresolved` when the revision source or location cannot be verified.

`exact-lines` retains the analyzer's `rawText` and bounded nearby source and is
assessed without Semantic intent links. `unresolved` is a blocker for a checked
new or changed suppression on either required side. A removed suppression maps
only against base and does not block the dimension when unresolved because it
cannot create a finding. Declaration and Semantic intent links are optional for
config entries and exact-line mappings; never fabricate them.

## Expected guidance

Do not generate `expectedCode`.

Use the analyzer's rule metadata directly:

```json
{
  "expected": "Don't use Record types for ARM resources.",
  "guidance": [
    {
      "canonicalDocumentUrl": "https://azure.github.io/...",
      "guidanceSection": "@azure-tools/typespec-azure-resource-manager/arm-no-record"
    }
  ]
}
```

The report labels this area **Expected guidance**, not **Expected TypeSpec**.

The documentation URL is a link only. The assessment does not fetch, quote, or
summarize the page. The rule description is preserved exactly from analyzer
metadata.

When no documentation URL is available:

- retain the exact rule name and description;
- render the rule name without a link;
- do not invent a URL;
- do not block the result when the description is available;
- block a checked new or changed suppression when its description is missing,
  regardless of whether a URL is present.

Descriptions are required because they supply `expected`. Documentation URLs
and guideline codes are optional metadata. When no documentation URL exists,
emit an empty `guidance` array; the rule name and description remain visible
without a link.

## Finding mapping

Each checked new or changed suppression becomes one Azure Guidelines finding:

```json
{
  "id": "compliance-...",
  "title": "New TypeSpec suppression: arm-no-record",
  "severity": "medium",
  "expected": "Don't use Record types for ARM resources.",
  "actual": "A checked arm-no-record suppression was added.",
  "rationale": "The repository suppression policy requires review of this new checked suppression.",
  "guidance": [
    {
      "canonicalDocumentUrl": "https://...",
      "guidanceSection": "@azure-tools/typespec-azure-resource-manager/arm-no-record"
    }
  ],
  "actualCode": "#suppress ...\nmodel Selector { ... }",
  "actualCodeTruncated": false,
  "sourceChangeIds": ["source-..."],
  "hunkIds": ["hunk-..."],
  "declarationIds": ["declaration-..."],
  "semanticIntentIds": ["semantic-..."]
}
```

All fields are deterministic:

- title derives from change type and rule short name;
- severity is always `medium`, retained only for schema and machine-consumer
  compatibility and never presented as a safety severity;
- expected and guidance derive from rule metadata;
- actual derives from change type, rule, source, and current justification;
- rationale states the checked-rules approval policy;
- source links and actual code derive from the normalized `after` side;
- changed findings also retain the normalized `before` justification and source
  location for presentation.

Do not evaluate whether the justification is acceptable. That decision belongs
to the repository's suppression approval workflow, not this assessment.
The finding means **review required**, not **guideline violation confirmed**.

Finding IDs use `stableId("compliance", { suppressionId })` so the existing
compliance anchor namespace remains stable while identity follows the
suppression rather than a Semantic intent.

## Dimension status

- `failed`: `checked.requiresApproval` is true because one or more checked new
  or changed suppressions exist.
- `passed`: checked-rules analysis completes and no checked new or changed
  suppressions exist.
- `not-assessed`: analyzer execution, project coverage, normalization, or
  required metadata or checked-item source verification is incomplete.

Removed suppressions are informational and do not fail the dimension.

If any project is blocked, the dimension is `not-assessed` even when findings
from successfully assessed projects are retained. Never downgrade a known
checked finding to `passed` because another project failed. Compute status from
the normalized records and blockers, then assert that it agrees with
`checkedSuppressions.requiresApproval` when coverage is complete.

Azure Guidelines remains outside REST/downstream scoped code safety.

## Final dimension contract

Write the new result under the existing `dimensions.compliance` key to preserve
report consumers and section anchors:

```json
{
  "assessmentVersion": 2,
  "kind": "checked-typespec-suppressions",
  "status": "failed",
  "summary": "18 checked TypeSpec suppressions require review.",
  "coverage": {
    "requestedProjects": ["specification/..."],
    "assessedProjects": ["specification/..."],
    "renamedProjects": [],
    "blockedProjects": []
  },
  "counts": {
    "specs": 2,
    "base": 2407,
    "head": 2490,
    "new": 288,
    "changed": 6,
    "removed": 205,
    "unchanged": 2199
  },
  "checked": {
    "requiresApproval": true,
    "new": 16,
    "changed": 2,
    "removed": 0
  },
  "findings": [],
  "removedSuppressions": [],
  "blockers": []
}
```

`assessmentVersion: 2` distinguishes suppression-based results from historical
unversioned search-based results. The schema accepts both shapes, but new
assembly emits only version 2. Version 2 removes `intentAssessments`,
`sharedSearch`, and `retrievalFailures`. `removedSuppressions` contains checked
removed suppressions only, projected from their normalized `before` sides; all
unchecked suppression details remain in the normalized artifact and only
aggregate counts reach the report appendix.

The abbreviated example omits the 18 finding objects implied by
`checked.new + checked.changed`. A production result requires exact equality
between that sum and `findings.length`, and between `checked.removed` and
`removedSuppressions.length`.

## Final report

Keep the existing Azure Guidelines card, section order, anchors, finding count,
and collapsed-card behavior.

Each checked new or changed suppression card shows:

- new or changed classification;
- linked rule name;
- exact rule description under **Expected guidance**;
- source file and line;
- current justification;
- previous justification for changed suppressions;
- related Semantic intent links;
- collapsed **Actual TypeSpec**;
- official rule documentation link.

Checked removed suppressions appear in a collapsed informational group.
Unchecked suppressions appear only in appendix counts.

Use **Review required** in finding-card status text. The dimension may retain
the machine-readable `failed` status for compatibility, but the UI must not
label these findings as confirmed violations.

Do not show:

- catalog ranking;
- selected documents;
- network retrieval state;
- fetched excerpts;
- search queries;
- expected TypeSpec code;
- Agent confidence or rationale;
- GitHub label state.

## Artifact changes

Remove:

- `dimensions/compliance-search-requests.json`;
- `compliance-search-evidence.json`;
- `scripts/compliance-search-evidence.schema.json`;
- Guidelines decisions from `assessment-judgment.json`;
- Guidelines content and accounting from `model-input.json`;
- Guidelines schemas and checklist items from `agent-index.json`.

Add:

- `raw-typespec-suppressions.json`;
- `dimensions/typespec-suppressions-input.json`;
- `scripts/typespec-suppressions-input.schema.json`.

`raw-typespec-suppressions.json` is immutable analyzer output and is not read
by the Agent. `dimensions/typespec-suppressions-input.json` is the canonical
normalized artifact consumed by assembly, validation, and rendering. Register
both hashes in `preparation-manifest.json`; expose only the normalized artifact
through final provenance.

Both files are produced before `model-input.json` and their hashes are verified
again during finalization. They are not listed in Agent-readable
`artifactReferences`. Version the affected Agent contracts rather than silently
changing version 1:

- `model-input.json` schema version 2 removes `complianceSearchRequests` and
  its artifact reference and accounting;
- `assessment-judgment.json` schema version 2 removes
  `complianceDecisions`;
- `agent-workspace/agent-index.json` schema version 2 removes Guidelines
  counts, outputs, schemas, and checklist items;
- global `assessment.json.schemaVersion` remains 1, while
  `dimensions.compliance.assessmentVersion` selects the historical or
  suppression-based shape.

Historical reports using old Guidelines artifacts remain readable.

## Validation

Validate:

- exact comparison identity;
- complete project coverage;
- complete and checked counts, including both conservation equations;
- checked-rules file identity;
- raw report, analyzer source, and input snapshot hashes;
- exact checked-rules equality after independent parsing;
- aggregate/spec-level analyzer consistency;
- stable suppression IDs;
- new, changed, removed, and unchanged classification;
- rule name, description, URL, and guideline code preservation;
- before and after justification;
- source and declaration mapping;
- exact actual TypeSpec;
- one finding per checked new or changed suppression;
- no finding for removed or unchecked suppressions;
- version 2 model-input, judgment, and Agent-index contracts contain no
  Guidelines work;
- `failed`, `passed`, and `not-assessed` derivation;
- unchanged Azure Guidelines report placement and presentation;
- historical report compatibility.

Reject:

- unknown or duplicate suppression IDs;
- duplicate analyzer identity tuples;
- missing checked suppressions;
- documentation URLs that differ from analyzer metadata or are invented;
- source outside the assessed projects;
- changed canonical analyzer output;
- success-shaped empty output after analyzer failure.

## Failure handling

Record explicit blockers for:

- missing analyzer;
- incompatible Node.js runtime;
- missing repository dependencies;
- missing checked-rules file;
- invalid project or missing `tspconfig.yaml`;
- missing base or head object;
- malformed analyzer output;
- checked-rules load warnings or post-run mismatch;
- unresolved checked-item source;
- missing checked-item rule description;
- partial project coverage.

Do not fall back to regex, model knowledge, web search, or the old Guidelines
catalog.

## Performance expectations

The replacement eliminates:

- Guidelines model input;
- Guidelines schema reads;
- 31-entry ranking;
- four web requests;
- document inspection;
- per-intent Guidelines decisions;
- Guidelines evidence serialization.

Record timings for:

- analyzer startup;
- base/head extraction and diff;
- rule metadata enrichment;
- normalization and source linkage;
- final assembly and HTML rendering.

PR 44988 is the large benchmark: two projects and 18 checked new or changed
suppressions, with no Guidelines Agent or network work.

## Implementation sequence

1. Pin raw-report fixtures to a supplying repository commit. Add the normalized
   schema, strict adapter validation, count conservation, and deterministic
   adapter tests before wiring preparation.
2. Add analyzer capability preflight, independent checked-rules validation,
   immutable invocation, provenance hashes, and local snapshot/rename handling.
3. Link normalized suppression records to exact source, declarations, hunks,
   and Semantic intents; test config and degraded exact-line mappings.
4. Map checked new/changed suppressions to deterministic Guidelines findings
   and derive status from coverage, blockers, and checked records.
5. Update validation and HTML rendering, including review-required wording,
   removed-suppression information, and historical report compatibility.
6. Remove catalog, fetch, search, Guidelines Agent inputs, schemas, accounting,
   and finalization timestamp dependencies.
7. Update `SKILL.md`, workflow, output contract, main design, and performance
   findings only after the runtime contract is green.
8. Run targeted tests, the complete skill test suite, and fixture compatibility
   tests for historical reports.
9. Run a fresh PR 44988 E2E assessment and verify exact comparison, project,
   rule-file, raw-report, and snapshot identities.
10. Compare normalized checked records and rendered cards with the analyzer's
    native JSON and Markdown. Counts, rules, source locations, descriptions,
    URLs, and justifications must match exactly; presentation grouping may
    differ.
