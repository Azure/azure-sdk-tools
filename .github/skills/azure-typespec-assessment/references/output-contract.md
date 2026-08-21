# Output Contract

Produce `assessment.json` and `assessment.md` from the same findings.

Render Markdown with `scripts/render-assessment.mjs`; do not hand-author or
patch individual sections. The renderer derives the assessment decision and all
summary counts from JSON.

## JSON shape

```json
{
  "schemaVersion": 1,
  "overallConfidence": "high",
  "baseline": { "ref": "origin/main", "commit": "..." },
  "head": { "commit": "...", "hasWorkingTreeChanges": true },
  "assessmentDuration": {
    "toolchainSetupMs": 12000,
    "preparationMs": 22000,
    "documentationReviewMs": 18000,
    "totalMs": 40000
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
Markdown displays it as `🟢 high`, `🟡 medium`, or `🔴 low`.
Markdown also displays a derived overall code-safety indicator: `🔴 Low` for
assessment errors or high-severity findings, `🟡 Medium` for other findings or
unassessed compliance, and `🟢 High` only when no errors/findings exist and
compliance passed. This value is derived by the renderer rather than stored in
JSON.
`assessmentDuration` records measured wall-clock preparation and
toolchain-setup, preparation, and documentation-assessment time. `totalMs` is
their sum. When historical evidence was assessed as one shared batch,
`documentationReviewMs` may be `null`; include a
`note` explaining why it cannot be attributed per PR and do not estimate it.

Semantic items require `id`, `intent`, `transformationChain`,
`restRepresentation`, `confidence`, and `sourceReferences`.
Breaking findings require `id`, `title`, `severity`, `confidence`, `summary`,
`evidence`, and `sourceReferences`.

Azure compliance status must be `passed`, `failed`, or `not-assessed`.
`documents` records every fetched authoritative page used in the assessment,
its matching section, a short verbatim `guidanceExcerpt`, applicable guidance,
observed evidence, and TypeSpec source references. The section and excerpt are
outputs of the shared author-skill agentic search procedure and prove that the
page content—not the reference-catalog description—was used.
Compliance findings require `id`, `title`, `severity`, `summary`,
`documentationUrl`, `evidence`, and TypeSpec `sourceReferences`. `passed`
requires at least one fetched document and no findings; `failed` requires at
least one document and finding. Use `not-assessed` with `reason` when no
relevant authoritative document exists or retrieval fails.

Before assigning compliance status, read
`evidence.json#projects[].validation`. A `failed` or `unavailable` repository
validation must be represented in `errors` and blocks a complete assessment.
A `succeeded` result is supporting evidence only; it does not prove that
patterns outside the validator's implemented checks are compliant.

Document and finding source references must identify the exact declaration
compared with the fetched pattern. Do not attach every changed source range to
every document or use a broad intent range when the relevant template is
declared elsewhere.

`restRepresentation` requires a `summary` and an `operations` array. Every
affected operation must be enumerated in that array:

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

Use baseline revision links for deleted source. Every item/finding must have at least one TypeSpec source reference.

## Markdown output

Render `assessment.md` using the
[assessment-first Markdown template](assessment-markdown-template.md). Every
semantic intent appears first in the Semantic Understanding Change Overview and
then in its detailed operation group. Every operation appears once with its
complete contract in Semantic Understanding. The internal transformation chain
remains in JSON and is not rendered. Supporting execution and guidance evidence
is grouped in the Appendix rather than interleaved with the assessment
conclusions.
