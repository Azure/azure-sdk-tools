# Assessment Classification

## Semantic understanding

Infer the user's intended API change, not a list of decorators. Correlate edits such as remove + rename + re-add + default into one statement: “add a default value to the existing property while preserving its wire name and version history.”

Write for TypeSpec authors. Use TypeSpec concepts such as models, unions,
decorators, LROs, and paging, but explain each intent through the affected
operations and their REST behavior. Do not expose emitter implementation terms
such as TCGC or cross-language definition IDs in semantic understanding.
Generated-client consequences belong in downstream findings, phrased as
user-visible SDK changes. Report them separately even when the same change also
breaks the REST contract.

For each intent, provide:

- intent and confidence;
- supporting transformation chain;
- affected operations, models, and API versions;
- one operation entry per affected operation, including REST signature, HTTP
  method/path, parameters, request, responses, and service behavior;
- explicit LRO and paging behavior, including polling/final-result and
  continuation semantics when applicable;
- TypeSpec source links.

`model-input.json` defines deterministic semantic review units. Author exactly
one intent for each unit and reference that unit's ID. A unit represents one
resource family plus one caller-observable REST or metadata behavior. Do not
merge unrelated families or behaviors into one intent, and do not split one
unit into multiple intents. Pure publication of unchanged contracts in a new
API version may use one version-lineage unit; a material parameter, response,
LRO, paging, route, or operation-family change remains separate.

`restRepresentation` must contain the operation inventory; it is not a prose
summary alone. Do not summarize several operations into a generic statement.
Follow the [operation semantics rules](operation-semantics.md).

Also classify every affected operation exactly once as `added`, `modified`, or
`removed`. Use `added` when the operation becomes available, `modified` for a
change to an existing operation including metadata changes, and `removed` when
an operation or contract surface is deleted. Compare baseline and head
artifacts field by field and record only changed aspects as explicit
before/after values. Merge multiple observations of the same REST behavior into
one aspect; do not add a second semantic row for the corresponding generated
SDK shape. Additions have no before value and removals have no after value.
Connect each change to the exact TypeSpec declaration that caused it. Do not
derive these behavior differences from intent prose, and do not include
unchanged REST fields. Attach the real TypeSpec Git hunks that caused each
change separately; fenced diff blocks are reserved for source code. Link a
change to REST or downstream findings it causes, and omit a generic effect when
no such finding exists.

## REST breaking changes

Use AutoRest base/head diffs to detect incompatible wire changes, including:

- removed paths, verbs, parameters, responses, properties, or enum values;
- optional-to-required changes;
- incompatible type, format, serialization, discriminator, or parameter-location changes;
- versioning changes that alter an existing API version.

State the affected request/response contract and source lines.

## Downstream breaking changes

Use this category whenever generated SDKs or other clients can break. A REST
breaking change normally also requires a downstream finding describing the
public client surface or runtime behavior that breaks. Common patterns:

| Pattern                                     | Downstream risk                                            |
| ------------------------------------------- | ---------------------------------------------------------- |
| `@@clientLocation` / operation-group change | Method moves between clients                               |
| override or parameter reorder               | Positional signature changes                               |
| flattening/nesting change                   | Construction and member access change                      |
| hierarchy change                            | Assignability, inheritance, discriminator behavior changes |
| usage/access/reachability change            | Public model appears, disappears, or changes visibility    |
| naming/alternate type change                | Public names or language types change                      |

Explain which client surface changes and how existing code can fail. When the
wire contract is unchanged, state that explicitly. When REST also breaks, keep
the REST finding and add a separate downstream finding rather than treating the
categories as mutually exclusive.
Phrase the finding in generated-client behavior that a TypeSpec author can
act on. Do not require the reader to understand TCGC or its internal metadata
field names; those artifacts may support the conclusion without appearing in
the user-facing explanation.

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

Decide every compliance review item exactly once. Each item compares one exact
changed TypeSpec hunk with one fetched guidance document. Use
`applicable-pass`, `applicable-fail`, `not-applicable`, or `not-assessed`, and
give the evidence-based rationale. Every `applicable-fail` decision requires
its own finding; a finding cannot combine multiple changed declarations.

Compiler and emitter success are supporting evidence, not compliance proof.

## Overall code safety

Derive the report's code-safety indicator from the complete assessment:

1. `Low` when assessment errors exist or any high-severity finding exists.
2. `Medium` when any lower-severity finding exists or compliance is
   `not-assessed`.
3. `High` when there are no errors or findings and compliance passed.

This indicator summarizes assessed risk; it is not a release approval.
