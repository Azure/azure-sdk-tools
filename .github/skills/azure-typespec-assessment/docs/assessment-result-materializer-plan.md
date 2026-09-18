# Assessment Result Materializer Implementation

## Status

Implemented. The Agent now authors one compact versioned decision file, a
deterministic materializer expands it into the existing Agent artifact
contracts, and guarded finalization remains authoritative.

## Goal

Provide a production materializer that converts compact Agent decisions into
the complete
`compliance-search-evidence.json` and `assessment-judgment.json` artifacts.

The implementation reduces Agent serialization without moving semantic,
compatibility, or Azure Guidelines judgment into deterministic code.

## Design baseline and E2E placement

This plan is based on the implemented Agentic Search workflow in `design.md`
and `references/agentic-search.md`. It does not replace Agentic Search with a
suppression analyzer or another deterministic compliance source.

The Agent continues to:

- score the complete official document catalog from the bounded query profiles;
- break equal-score ties by canonical catalog order;
- fetch the four highest-ranked retrievable documents with `web_fetch`;
- record failed retrievals and select the next-ranked replacement;
- extract relevant guidance and compare it with each Semantic intent;
- author all REST, downstream, and Azure Guidelines decisions.

The materializer runs after that bounded Agent work and before guarded
finalization:

```text
deterministic preparation
  -> model-input.json and compact draft
  -> Agentic Search and compact Agent decisions
  -> deterministic materializer
  -> inference.json, compliance-search-evidence.json,
     assessment-judgment.json
  -> guarded finalization
  -> assessment.json and assessment.html
```

Preparation, candidate generation, Agentic Search, final assembly, validation,
and rendering retain their current ownership. Only the mechanical construction
of the three Agent artifacts moves into deterministic code.

## Addressed problem

Before this implementation, `build-agent-workspace.mjs` created
`assessment-judgment.draft.json`, but the draft only supplied IDs and
unresolved placeholders. The Agent still had to serialize:

- one title and summary per assessed Semantic intent;
- one decision per REST and downstream candidate;
- one Azure Guidelines decision per assessed intent;
- every canonical catalog ranking entry;
- four fetched-document records and their retrieval provenance;
- repeated source, hunk, declaration, URL, score, rank, hash, and accounting
  fields.

Most of these fields are canonical data or mechanically derived structure, not
Agent judgment. Manual construction also creates avoidable failure modes. A
fresh PR 44988 trial, for example, produced a baseline declaration ID where the
canonical request required the corresponding current declaration ID. Guarded
finalization correctly rejected the artifact, but only after the full artifact
had been written.

## Adopted decision

The materialized workflow is implemented. The Agent writes compact decisions;
the production script joins them with canonical inputs, validates all
references, and writes the complete artifacts atomically.

## Measurement baseline

Select at least three representative PRs:

- a small change with few intents and no inference;
- a medium change with REST or downstream candidates;
- a large change such as PR 44988 with multiple intents, downstream
  candidates, and Azure Guidelines findings.

For each PR, record:

- deterministic coordinator time;
- bounded-input bytes;
- Agent judgment wall time;
- Agent output bytes;
- file and shell tool calls during judgment;
- serialization and validation time;
- number and type of schema, coverage, and reference-linkage failures;
- final finding counts, selected Guidelines documents, blockers, and coverage.

Use the same commits, warm dependency state, and fetched document contents for
both approaches. Do not use historical runs containing host or compiler stalls
as performance baselines.

## Compact decision contract

The versioned compact schema contains only Agent-authored decisions:

- Semantic intent title and summary keyed by `reviewUnitId`;
- REST and downstream `approve` or `reject`, severity when required, and
  rationale keyed by `candidateId`;
- the four score signals and selection rationale for each catalog entry;
- selected document guidance sections, excerpts, query terms, and examples;
- retrieval results from the Agent's `web_fetch` calls, including failures and
  the provenance needed by the existing evidence contract;
- one Guidelines decision per assessed intent, including applicability,
  prefilled intent-owned qualified declaration names, expected behavior,
  actual behavior, rationale, finding title, and severity when required;
- overall confidence and explicit blockers;
- inference decisions only when the coordinator produced inference requests.

The compact contract must not require the Agent to repeat canonical titles,
URLs, catalog order, calculated rank or score total, unchanged query profiles,
source IDs, hunk IDs, or complete output object scaffolding. Retrieval
provenance is recorded once with the corresponding Agent fetch result; the
materializer must preserve it and must not invent or refetch evidence.

## Materializer responsibilities

The production script:

1. Read `agent-index.json`, `model-input.json`, canonical dimension inputs, the
   catalog, and the compact Agent decision file.
2. Verify exact coverage before producing any final artifact.
3. Resolve every qualified declaration name to exactly one canonical ID only
   within the owning request; reject unknown, duplicate, ambiguous, and
   cross-intent names.
4. Copy canonical query profiles, source IDs, hunk IDs, declaration IDs,
   catalog titles, URLs, and catalog order.
5. Calculate score totals, stable ranking, and derived input accounting.
   Preserve and validate the retrieval provenance supplied by Agentic Search;
   derive excerpt applicability only from validated judgments citing the same
   catalog entry and section, and drop uncited excerpts.
6. Generate `inference.json` when required,
   `compliance-search-evidence.json`, and `assessment-judgment.json`.
7. Validate generated artifacts against their existing schemas and semantic
   invariants before atomically replacing any file.
8. Leave `finalize-assessment.mjs` responsible for assembly, final validation,
   HTML rendering, workflow state, and result hashes.

The materializer must fail explicitly for unknown, duplicate, missing, or
cross-intent IDs. It must not silently omit a decision or convert incomplete
work into a pass, rejection, or `no-applicable-guidance`.

## Judgment boundary

Deterministic code may copy, join, rank, count, hash, validate, and serialize.
It must not:

- decide whether a REST or downstream candidate is a finding;
- choose a severity;
- choose catalog score signals or their rationale;
- decide whether guidance applies;
- decide Guidelines pass or fail;
- invent expected behavior, actual behavior, rationale, or source evidence;
- select a fallback result when Agent output is missing or invalid.
- replace Agentic Search, fetch documents independently, or substitute
  suppression diagnostics for fetched guidance.

For the 31-entry catalog, the Agent continues to supply the four score signals
and selection rationale. The materializer reads canonical metadata, calculates
the total, applies stable sorting, and emits the full ranking. Only the first
four retrievable documents are retained as shared evidence.

## Validation and tests

Focused tests cover:

- compact schema acceptance and rejection;
- exact Semantic, candidate, inference, and Guidelines coverage;
- canonical catalog metadata, score totals, ordering, ties, and replacement
  after retrieval failure;
- valid declaration ownership and rejection of cross-intent declarations;
- canonical source and hunk linkage;
- empty Guidelines requests and informational-only assessments;
- inference-present and inference-absent workflows;
- duplicate, missing, unknown, and unsupported IDs;
- applicable pass/fail evidence requirements;
- `no-applicable-guidance` and `not-assessed` invariants;
- atomic output behavior when materialization fails;
- end-to-end equivalence through guarded finalization.

Existing final artifact schemas remain authoritative. The materializer is an
authoring aid, not a relaxation of validation.

## A/B quality and performance gates

Adopt the materializer only when all of these conditions hold:

- final findings, statuses, coverage, selected documents, and blockers are
  equivalent for all benchmark cases;
- Agent output bytes decrease by at least 50%;
- Agent serialization and tool time decrease by at least 50%;
- materialization completes in under two seconds for the large case;
- no benchmark requires manual schema or canonical-linkage repair;
- no new success-shaped fallback or judgment in deterministic code is found;
- targeted tests, the complete assessment test suite, skill lint, and
  `git diff --check` pass, apart from separately documented pre-existing
  failures.

If the output reduction is small or reasoning still dominates the measured
wall time, implement enhanced drafts instead of a new compact contract.

## Implemented surfaces

- `scripts/agent-decisions.schema.json`;
- `scripts/materialize-assessment-results.mjs` and focused tests;
- compact draft, output metadata, and command in `agent-index.json`;
- unchanged final schemas and guarded finalizer authority;
- materialization duration and output-byte telemetry in
  `workflow-state.json`.

## Exit criteria

Implementation is complete when focused and full script tests, skill lint, and
`git diff --check` pass and fresh runs expose materialization timing and byte
overhead for comparison.
