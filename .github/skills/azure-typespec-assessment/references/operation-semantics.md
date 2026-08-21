# Operation Semantics

Enumerate every operation directly or transitively affected by the TypeSpec
diff. Model, decorator, versioning, and client-only edits still require the
operations that consume them.

## REST signature

For each operation report:

- operation ID and API versions;
- HTTP method and complete path;
- every path, query, header, and cookie parameter with wire name, type,
  requiredness, and default;
- request content type and payload model, or explicitly `none`;
- every success and error response status, content type, payload model, and
  relevant headers;
- service behavior in domain terms, not just TypeSpec template names.

Resolve spread parameters and template arguments. Use emitted AutoRest as the
wire authority and TypeSpec source for intent and source links.

## Long-running operations

Always state whether the operation is an LRO.

For ARM, interpret async behavior using the
[ARM async API reference](https://github.com/cloud-and-ai-microsoft/resource-provider-contract/blob/master/v1.0/async-api-reference.md).
Report the initial status/body, `Azure-AsyncOperation`, `Location`,
`Operation-Location`, and `Retry-After` headers, `final-state-via`, polling
endpoint/status model, terminal states, final GET/result, and delete visibility
behavior when applicable.

For data plane, use the
[Azure REST LRO guidance](https://github.com/microsoft/api-guidelines/blob/vNext/azure/ConsiderationsForServiceDesign.md#long-running-operations).
Distinguish the target resource from the ephemeral status monitor. Report how
the operation starts, how clients poll, terminal states (`Succeeded`, `Failed`,
`Canceled`), error/result placement, and status-monitor retention behavior.

Do not infer SDK LRO behavior only from `x-ms-long-running-operation`. Confirm
that TypeSpec LRO metadata is visible to TCGC and identify mismatches between
OpenAPI and SDK interpretation.

## Paging

Always state whether the operation is paged. For paged operations report:

- page item model and collection property wire name;
- `nextLink` wire name and optionality;
- whether `nextLink` is an opaque absolute URL;
- client-controlled paging parameters such as `top`, `skip`, or `maxpagesize`;
- how the next request is issued and when paging terminates.

Use the
[Azure REST pagination guidance](https://github.com/microsoft/api-guidelines/blob/vNext/azure/ConsiderationsForServiceDesign.md#pagination).
Adding paging metadata to an existing response can be REST-compatible while
changing generated SDK return shapes and iteration behavior.
