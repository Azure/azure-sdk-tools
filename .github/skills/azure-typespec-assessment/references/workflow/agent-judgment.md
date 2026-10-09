# Bounded Agent Judgment

When preparation prints `awaiting-agent-judgment`, continue immediately without
a progress stop or another user turn.

Read `<work-directory>\agent-workspace\agent-index.json` once. It identifies
the single bounded model input, required outputs, drafts, exact coverage IDs,
and completion checklist. Read `model-input.json` exactly once unless guarded
finalization requests the one permitted correction turn.

`api-version-publication` and `api-version-wide-change` are excluded from Agent
coverage. Guarded finalization restores them as deterministic informational
Semantic intents with canonical affected operations and no finding
relationships. Version-wide classification uses `Versions` declaration and
version-transition/governance evidence, never an operation-count threshold.

Also read only:

- [classification guidance](../classification.md), including
  [downstream cases](../downstream-breaking-cases.md) and
  [candidate rules](../downstream-candidate-rules.md);
- the [agentic search procedure](../agentic-search.md);
- the [official document catalog](../reference-document-links.md);
- the [documentation checks](../document-quality.md).

For SDK naming with known targets, or when explicitly requested, invoke
`azsdk-common-typespec-naming` in review mode and read its selected references.
Invoke it before classifying missing target context or unsupported profiles;
direct reference reads and delegated subtasks do not replace the handoff. Reuse
the supplied changed declarations, language and release evidence; do not start
another repository scan, generation or customization. Missing evidence or a
missing naming profile never means passed: missing required evidence is
`not-assessed`, while a confirmed target with no applicable profile is
`not-covered`. Write the result to `sdkNamingReview` in
`agent-decisions.json`: a summary, one coverage entry per supplied target,
findings, and blockers. Findings record the declaration, supplied current SDK
name, recommendation when permitted, language scope, rule, rationale,
compatibility evidence, verification state, optional source location, and
optional owning `reviewUnitId`. Do not insert local naming rules into fetched
Azure Guidelines provenance.
Keep this exact compact shape for bounded planning responses too:

- `summary: string`;
- `coverage: Array<{ language, serviceType, profile?, status, rationale }>`;
- `findings: Array<{ reviewUnitId?, declaration, currentSdkName, recommendedSdkName?, languageScope, decision, rule, rationale, compatibilityEvidence, verification, sourceLocation? }>`;
- `blockers: string[]`.

Use the schema enums exactly. In particular, use lowercase `arm`,
`not-covered` for a confirmed target with no applicable profile,
`not-assessed` for missing target context or required evidence, and
`supplied-generated` when the current emitted or generated SDK name was
supplied. `not-covered` takes precedence once the target is confirmed and no
profile exists; record missing generated-name or compatibility evidence as
blockers instead of changing that coverage status. Do not rename properties,
add properties such as `summary` to coverage entries, or pass through auxiliary
handoff metadata.

Do not recursively list the work directory, broadly search report artifacts,
inspect raw compiler output, or repeatedly read schemas and canonical inputs.
Use `agent-workspace\agent-decisions.draft.json` only as the structural
template. Its compliance judgments prefill eligible intent-scoped qualified
`declarationNames`; retain only names supporting each judgment. Never copy its
intentionally invalid unresolved placeholders into the completed file.

If `inferenceRequests` is empty, omit `inferenceResults`. Otherwise, analyze
only supplied unknown hunks and write one exact compact result per request to
`agent-workspace\agent-decisions.json`. Each is `candidates`, `no-impact`, or
`blocked`. Inferred candidates may use only the request's source, hunk,
operations, facts, and allowed dimensions. Never modify `model-input.json`.

The compact decision file must contain one concise result per supplied Semantic
review unit and exact deterministic and inferred REST/downstream candidate
coverage. Write one Azure Guidelines decision per supplied compliance search
request. When there are no compliance search requests, leave catalog scores,
retrieval results, search blockers, and compliance judgments empty. Do not read
raw AutoRest/TCGC output, compiler logs, unrelated unchanged source, prior
answers, or use catalog descriptions as guidance. Candidate and review-unit
evidence omitted from the bounded input is available only through declared
canonical artifact references; do not scan unrelated artifact entries.
The naming review is read-only and evidence-bound: use `reviewed` only for an
applicable profile with sufficient supplied context, `not-covered` when no
profile applies, and `not-assessed` when language, service type, compatibility,
or generated-name evidence is missing. Distinguish proposed names from supplied
generated names and already-applied changes.

Documentation Completeness is assembled deterministically from
`dimensions/document-quality-input.json`. The Agent neither reads that artifact
nor authors documentation decisions.

Continue with the
[Azure Guidelines search and materialization](compliance-and-materialization.md).
