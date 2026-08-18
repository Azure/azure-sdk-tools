# Output Contract

Produce `assessment.json` and `assessment.md` from the same findings.

## JSON shape

```json
{
  "schemaVersion": 1,
  "overallConfidence": "high",
  "baseline": { "ref": "origin/main", "commit": "..." },
  "head": { "commit": "...", "hasWorkingTreeChanges": true },
  "projects": ["specification/widget/Widget"],
  "dimensions": {
    "semanticUnderstanding": { "items": [] },
    "restBreakingChanges": { "findings": [] },
    "restCompatibleDownstreamBreakingChanges": { "findings": [] },
    "azureCompliance": {
      "status": "not-assessed",
      "reason": "Deferred from MVP."
    }
  },
  "errors": []
}
```

`overallConfidence` is required and must be `high`, `medium`, or `low`. It
reflects confidence in the complete report, not an individual finding. Lower it
when compilation failures or missing evidence block a complete assessment.

Semantic items require `id`, `intent`, `transformationChain`,
`restRepresentation`, `confidence`, and `sourceReferences`.
Breaking findings require `id`, `title`, `severity`, `confidence`, `summary`,
`evidence`, and `sourceReferences`.

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
[Markdown assessment template](assessment-markdown-template.md).
