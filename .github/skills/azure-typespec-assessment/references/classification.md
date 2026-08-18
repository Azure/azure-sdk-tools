# Assessment Classification

## Semantic understanding

Infer the user's intended API change, not a list of decorators. Correlate edits such as remove + rename + re-add + default into one statement: “add a default value to the existing property while preserving its wire name and version history.”

For each intent, provide:

- intent and confidence;
- supporting transformation chain;
- affected operations, models, and API versions;
- one operation entry per affected operation, including REST signature, HTTP
  method/path, parameters, request, responses, and service behavior;
- explicit LRO and paging behavior, including polling/final-result and
  continuation semantics when applicable;
- TypeSpec source links.

`restRepresentation` must contain the operation inventory; it is not a prose
summary alone. Do not summarize several operations into a generic statement.
Follow the [operation semantics rules](operation-semantics.md).

## REST breaking changes

Use AutoRest base/head diffs to detect incompatible wire changes, including:

- removed paths, verbs, parameters, responses, properties, or enum values;
- optional-to-required changes;
- incompatible type, format, serialization, discriminator, or parameter-location changes;
- versioning changes that alter an existing API version.

State the affected request/response contract and source lines.

## REST-compatible downstream breaking changes

Use this category only when REST remains compatible but generated SDKs or other clients can break. Common patterns:

| Pattern                                     | Downstream risk                                            |
| ------------------------------------------- | ---------------------------------------------------------- |
| `@@clientLocation` / operation-group change | Method moves between clients                               |
| override or parameter reorder               | Positional signature changes                               |
| flattening/nesting change                   | Construction and member access change                      |
| hierarchy change                            | Assignability, inheritance, discriminator behavior changes |
| usage/access/reachability change            | Public model appears, disappears, or changes visibility    |
| naming/alternate type change                | Public names or language types change                      |

Explain why the wire contract is unchanged, which client surface changes, and how existing code can fail. If REST also breaks, classify under REST breaking changes instead.

## Azure compliance

MVP output is always:

```json
{ "status": "not-assessed", "reason": "Deferred from MVP." }
```

Do not infer compliance from compilation or linter success.
