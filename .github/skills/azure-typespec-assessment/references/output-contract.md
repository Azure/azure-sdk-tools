# Output Contract

Produce `assessment.json` and `assessment.html` from the same validated
structured assessment. Do not generate `assessment.md`.

Before the final assessment, deterministic preparation produces the bounded
`model-input.json` and a pretty diagnostic copy in `assessment-draft.json`.
Their operation groups, compact contract summaries, source-impact links,
semantic review units, compatibility candidates, compliance review items,
documentation routes, and timing are evidence, not final conclusions. The
model supplies intent wording, applicability, final findings, and confidence
while preserving the deterministic coverage boundaries.

The draft is intentionally compact. It contains a method/path/version manifest
for added and removed operations, detailed before/after contracts for modified
operations, compatibility candidates, changed TypeSpec hunks and source index,
and matched documentation excerpts. Complete operation contracts and raw TCGC
leaf diffs remain in `analysis.json` and must not be loaded into model context
unless a candidate requires deeper inspection.

Every judgment semantic intent references exactly one deterministic review unit
and copies that unit's operation/source evidence IDs exactly. Every review unit
must be referenced once. Every compliance review item must also receive exactly
one decision. A failed decision maps to one unique finding whose document URL
and single source-change ID match the review item.

When a source-only model or metadata change has no directly affected REST
operation, the assembled semantic item sets `sourceOnly: true`, keeps
`restRepresentation.operations`, `operationIds`, and `apiVersions` empty, and
renders the exact changed TypeSpec declarations. Do not invent an operation
contract to satisfy the normal operation inventory.

Render the standalone, responsive report with
`scripts/render-assessment-html.mjs`; do not hand-author or patch individual
sections. The renderer derives the assessment decision and all summary counts
from the structured assessment.

## JSON shape

```json
{
  "schemaVersion": 2,
  "overallConfidence": "high",
  "baseline": { "ref": "origin/main", "commit": "..." },
  "head": { "commit": "...", "hasWorkingTreeChanges": true },
  "assessmentDuration": {
    "totalMs": 40000,
    "note": "Dimension timings and their quality are recorded below.",
    "breakdown": {
      "semanticUnderstandingMs": 10000,
      "semanticUnderstandingQuality": "estimated",
      "restBreakingMs": 5000,
      "restBreakingQuality": "estimated",
      "downstreamBreakingMs": 5000,
      "downstreamBreakingQuality": "estimated",
      "complianceMs": 15000,
      "complianceQuality": "measured",
      "overheadMs": 5000,
      "overheadQuality": "derived",
      "totalMs": 40000,
      "totalQuality": "measured",
      "searchRoute": "catalog only"
    }
  },
  "projects": ["specification/widget/Widget"],
  "dimensions": {
    "semanticUnderstanding": { "items": [] },
    "restBreakingChanges": { "findings": [] },
    "restCompatibleDownstreamBreakingChanges": { "findings": [] },
    "azureCompliance": {
      "status": "failed",
      "summary": {
        "patternsAssessed": 1,
        "findingCount": 1
      },
      "documents": [
        {
          "title": "ARM resource operations",
          "url": "https://azure.github.io/typespec-azure/docs/howtos/arm/resource-operations/",
          "section": "CreateOrUpdate, Update, Delete, and List",
          "guidanceExcerpt": "CreateOrUpdate (PUT), Update (Patch), Delete, and Action (POST) operations over a resource may be either synchronous or asynchronous.",
          "applicableGuidance": "Resource operations use the standard ARM templates.",
          "evidence": "The changed interface uses a legacy routed action helper instead of the documented resource operation template.",
          "expectedCodeStatus": "available",
          "expectedCodeSnippets": [
            {
              "language": "tsp",
              "caption": "Documented resource read template",
              "url": "https://azure.github.io/typespec-azure/docs/howtos/arm/resource-operations/",
              "section": "CreateOrUpdate, Update, Delete, and List",
              "lines": ["get is ArmResourceRead<Resource>;"]
            }
          ],
          "sourceReferences": [
            {
              "path": "specification/widget/main.tsp",
              "revision": "head",
              "startLine": 20,
              "endLine": 28,
              "link": "specification/widget/main.tsp#L20-L28"
            }
          ]
        }
      ],
      "findings": [
        {
          "id": "compliance-standard-resource-operation",
          "title": "Operation does not use the documented ARM template",
          "severity": "medium",
          "summary": "The changed operation duplicates a standard ARM resource operation instead of using its documented template.",
          "documentationUrl": "https://azure.github.io/typespec-azure/docs/howtos/arm/resource-operations/",
          "evidence": [
            "Changed operation signature",
            "Fetched ARM operation guidance"
          ],
          "codeSnippets": [
            {
              "path": "specification/widget/main.tsp",
              "startLine": 20,
              "endLine": 21,
              "lines": [
                "interface Widgets {",
                "  get is Legacy.RoutedOperations.ActionSync<Widget>;"
              ]
            }
          ],
          "sourceReferences": [
            {
              "path": "specification/widget/main.tsp",
              "revision": "head",
              "startLine": 20,
              "endLine": 28,
              "link": "specification/widget/main.tsp#L20-L28"
            }
          ]
        }
      ]
    }
  },
  "errors": []
}
```

`overallConfidence` is required and must be `high`, `medium`, or `low`. It
reflects confidence in the complete report, not an individual finding. Lower it
when compilation failures or missing evidence block a complete assessment.
The HTML report displays confidence in its hero metadata and a derived overall
code-safety indicator: Low for assessment errors or high-severity findings,
Medium for other findings or unassessed compliance, and High only when no
errors/findings exist and compliance passed. Code safety is derived by the
renderer rather than stored in the structured assessment.
`assessmentDuration` records the end-to-end total. When per-dimension timing is
available, `breakdown` records semantic understanding, REST breaking,
downstream breaking, compliance, overhead, total, timing-quality labels, and
the documentation search route. HTML reports render this breakdown directly.
When historical evidence was assessed as one shared batch,
`documentationReviewMs` may be `null`; include a
`note` explaining why it cannot be attributed per PR and do not estimate it.
Timing-quality labels distinguish measured, estimated, derived, and mixed
estimated/measured values. Assessment timing excludes worktree creation,
dependency installation, and other environment setup.

Semantic items require `id`, `intent`, `transformationChain`,
`changes`, `restRepresentation`, `confidence`, and `sourceReferences`.
Their audience is a TypeSpec author: TypeSpec language and Azure library
concepts are expected, but semantic change rows explain operation-level REST
behavior. Do not mention TCGC, cross-language definition IDs, or other emitter
internals there. Keep generated-client shape and source-compatibility analysis
in `restCompatibleDownstreamBreakingChanges`. Represent one REST behavior
change with one before/after aspect instead of duplicating it as a REST row and
an SDK row.
Downstream findings describe public generated-client behavior—such as a method
moving clients, becoming pageable, or changing from a fixed enum to an
extensible string-backed shape—without exposing TCGC or its internal metadata
field names in user-visible titles, summaries, or evidence.
Breaking findings require `id`, `title`, `severity`, `confidence`, `summary`,
`evidence`, and `sourceReferences`.

PR metadata is optional. `baseline` and `head` describe the actual comparison
for both PR and local-worktree assessments. When the head includes local
changes, set `head.hasWorkingTreeChanges` and record the included
`staged`, `unstaged`, and `untracked` scopes so the report and follow-up
prompts identify the comparison precisely.

Azure compliance status must be `passed`, `failed`, or `not-assessed`.
`documents` records every fetched authoritative page used in the assessment,
its matching section, a short verbatim `guidanceExcerpt`, applicable guidance,
observed evidence, and TypeSpec source references. For documents supporting a
finding, `expectedCodeStatus` is `available` or `not-present`. `available`
requires one or two `expectedCodeSnippets`, each with `language: "tsp"`,
`caption`, `url`, `section`, and at most 12 exact documented `lines`; the URL
and section must match the parent document. `not-present` requires
`expectedCodeReason` and no snippets. Never generate or infer Expected code.
The section and excerpt are
outputs of the shared author-skill agentic search procedure and prove that the
page content—not the reference-catalog description—was used.
Compliance findings require `id`, `title`, `severity`, `summary`,
`documentationUrl`, `evidence`, TypeSpec `sourceReferences`, and one or two
focused `codeSnippets` of no more than 12 lines each. Each finding resolves its
matching fetched document by `documentationUrl`; the renderers present the
finding `summary` as the visible **Gap**, then place **Expected** and **Actual**
in separate default-collapsed sections. Expected contains the document's
`applicableGuidance`, authoritative example when available, and guidance link.
Actual contains document `evidence` and finding `codeSnippets`. Actual snippets
contain only the exact
declaration, decorator, base type, or operation template demonstrating the
mismatch and must be covered by a finding source reference. `passed`
requires at least one fetched document and no findings; `failed` requires at
least one document and finding. Use `not-assessed` with `reason` when no
relevant authoritative document exists, no review items can be constructed, or
retrieval fails.

Before assigning compliance status, use Agent search to fetch the applicable
authoritative reference document, retain its matching section, and compare the
documented pattern directly with the changed TypeSpec source.

Document and finding source references must identify the exact declaration
compared with the fetched pattern. Do not attach every changed source range to
every document or use a broad intent range when the relevant template is
declared elsewhere.

`restRepresentation` requires a `summary` and an `operations` array. Every
affected operation must be enumerated in that array:

For LRO and paging changes, the rendered operation detail combines metadata
evidence with the most useful service behavior. Show changed behavior first and
only enough unchanged context to explain the result. Do not use a fixed field
checklist, and do not treat an unchanged REST signature as proof that TypeSpec
LRO or paging metadata is also unchanged.

```json
{
  "summary": "Creates or replaces a widget.",
  "operations": [
    {
      "operationId": "Widgets_Create",
      "apiVersions": ["2026-01-01"],
      "method": "PUT",
      "path": "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Example/widgets/{widgetName}",
      "signature": "PUT /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Example/widgets/{widgetName}",
      "parameters": [
        "path widgetName: string, required",
        "query api-version: string, required"
      ],
      "requestPayload": "application/json payload: Widget",
      "responsePayloads": [
        "200 application/json payload: Widget",
        "201 application/json payload: Widget; Azure-AsyncOperation and Retry-After headers",
        "default application/json payload: ErrorResponse"
      ],
      "serviceBehavior": "Creates or replaces the widget and provisions it asynchronously.",
      "lro": {
        "isLongRunning": true,
        "pattern": "arm",
        "finalStateVia": "azure-async-operation",
        "polling": "Poll Azure-AsyncOperation after Retry-After until Succeeded, Failed, or Canceled.",
        "finalResult": "GET the resource after terminal success."
      },
      "paging": { "isPaged": false },
      "sourceReferences": []
    }
  ]
}
```

For non-LRO and non-paged operations, retain the objects with
`isLongRunning: false` and `isPaged: false`. For paging, state the item type,
`nextLink` wire name, whether the link is opaque/absolute, and how the next
request is made. Never omit operations because their wire signature is
unchanged or because the diff is client-only.

Each semantic item also requires one or more explicit `changes`. These are the
only source for the human-facing semantic diff; the renderer must not infer
before/after behavior from intent prose:

```json
{
  "kind": "modified",
  "summary": "Expose the existing result as pageable.",
  "operationIds": ["OutboundRules_Post"],
  "apiVersions": ["2024-10-01"],
  "aspects": [
    {
      "field": "paging",
      "before": "LRO result is exposed as a non-pageable response.",
      "after": "LRO result is pageable with items `value` and continuation link `nextLink`."
    }
  ],
  "effect": "The wire response is unchanged; generated clients expose paging.",
  "typeSpecCause": "Add @list, @pageItems, and @nextLink metadata.",
  "typeSpecDiffs": [
    {
      "path": "specification/widget/main.tsp",
      "oldStart": 20,
      "oldCount": 6,
      "newStart": 20,
      "newCount": 7,
      "context": "interface Widgets",
      "lines": [
        "   @action(\"refresh\")",
        "+  @list",
        "   refresh is ArmResourceActionAsync<Widget>;"
      ]
    }
  ],
  "linkedFindingIds": ["sdk-pageable-result"],
  "sourceReferences": []
}
```

`kind` is `added`, `modified`, or `removed`. Added aspects use `before: null`;
metadata and other changes to existing operations use `modified`; deleted
operations or contract surface use `removed` with `after: null`. Every affected
operation ID must be covered by exactly one change in its semantic item. A
change records only fields that differ, while `restRepresentation.operations`
retains the complete REST contract. A mixed intent may use multiple change
records. Pure-addition operation families may use one grouped aspect rather
than repeating every REST signature; the exact operation IDs remain in
``operationIds`.

`typeSpecDiffs` contains the real Git source hunks that caused the semantic
change. Every hunk records its TypeSpec path, old/new ranges, optional hunk
context, and source lines with their original `" "`, `"+"`, or `"-"` prefix.
The renderer shows at most two relevant hunks per semantic change and may
excerpt an unusually large hunk, but every complete hunk remains in JSON.
The report renders every affected operation as an individually
collapsed disclosure. Expanding an operation shows its corresponding TypeSpec
decorator/declaration excerpts and source links without expanding unrelated
operations. Added and removed operations show their REST API signature;
modified operations show concrete signature diff lines containing the
method/path and only structurally different parameters, request, responses, LRO,
or paging fields. When no wire field changes, the report says `REST wire
signature unchanged` rather than converting a semantic summary into a fake REST
diff. All affected operations are directly available in these disclosures.
TypeSpec diff blocks appear only inside those operation disclosures. Each
semantic change table includes an `Impact` column linking directly to its REST,
downstream, or compliance findings.
Azure compliance findings render as collapsed disclosures by default. Each
finding should carry focused `codeSnippets` that directly demonstrate the
stated mismatch; do not infer its displayed code from a broadly related
semantic intent. Every snippet must be covered by one of the finding's exact
TypeSpec source references.
Its summary cards, navigation, and body use the same four categories: semantic
intents, REST breaking changes, downstream breaking changes, and Azure
compliance. A separate overall-code-safety status card precedes those four
category cards but does not add a fifth body category. Each summary card has a
result icon: code safety uses pass, review, or fail according to its risk level;
semantic intents use an informational icon; and REST, downstream, and
compliance use pass or fail based on their findings. Icons and card titles are
visually prominent, while secondary counts remain smaller. The compliance card
uses its finding count as the primary value. Overall confidence appears in the
hero metadata rather than adding another summary card.
REST and downstream findings are not mutually exclusive. Every REST breaking
finding must have a corresponding downstream finding that explains the
generated-client or consumer impact.
Semantic intents are individually collapsed and sorted with linked-impact
intents first; impacted intent titles display a warning icon. Up to ten intent
summaries are visible initially. Only intents beyond the first ten are hidden
behind a collapsed `Show N more semantic intents` disclosure.
The report ends with a collapsed Appendix containing assessment errors,
code-to-guidance evidence, tooling used, artifact evidence, and the detailed
execution-time breakdown. Execution timing is not a top-level section or
navigation item.
`linkedFindingIds` references REST breaking, downstream breaking, or compliance
findings caused by the change. Every breaking or compliance finding must be
linked from at least one semantic change.

Each source reference requires:

```json
{
  "path": "specification/widget/main.tsp",
  "revision": "head",
  "startLine": 20,
  "endLine": 28,
  "link": "specification/widget/main.tsp#L20-L28"
}
```

Use baseline revision links for deleted source. Every item, change, and finding
must have at least one TypeSpec source reference.

## Report output

Write the complete structured assessment to `assessment.json`, then render
`assessment.html`. Semantic Understanding displays change kind, aspect, and
plain-language Before/After values, with focused TypeSpec source diffs inside
the affected operation disclosures. Supporting execution and guidance evidence
remains in the collapsed Appendix.
