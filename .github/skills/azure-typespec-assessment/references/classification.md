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

Follow [documentation-grounded compliance](compliance.md). Select relevant
documents from the shared catalog and fetch them; do not encode their
requirements in this classifier.

- `passed`: every applicable fetched pattern is followed by the changed
  TypeSpec.
- `failed`: changed source contradicts at least one applicable documented
  pattern. Cite that document and the TypeSpec lines.
- `not-assessed`: no relevant authoritative document exists or a selected page
  could not be retrieved.

Compiler and emitter success are supporting evidence, not compliance proof.

## Overall code safety

Derive the report's code-safety indicator from the complete assessment:

1. `Low` when assessment errors exist or any high-severity finding exists.
2. `Medium` when any lower-severity finding exists or compliance is
   `not-assessed`.
3. `High` when there are no errors or findings and compliance passed.

This indicator summarizes assessed risk; it is not a release approval.
