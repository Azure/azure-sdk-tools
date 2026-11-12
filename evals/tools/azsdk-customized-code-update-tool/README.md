# azsdk_typespec_customized_code_update tool evals

Measure whether the customization tool produces correct, durable SDK customizations from four input types while respecting three edit scopes. Calling the right MCP tool is necessary but insufficient: the resulting API must satisfy the request, preserve service semantics and unrelated APIs, build when repair is expected, and survive regeneration.
The benchmark evaluates invocation correctness, repair correctness, edit-scope compliance, and actionable handling of requests that cannot be completed within the permitted scope.
Non-goals are deployment, package publishing, opening pull requests, arbitrary service implementation changes, and architectural redesign. Tool changes stay uncommitted inside disposable workspaces.

## Scope

The target coverage matrix spans three dimensions:

|Dimension|Coverage|
|---------|--------|
|Entry point|Customization request, feedback, build failure, and breaking changes|
|`editScope`|`SpecInputs`, `CustomCode`, and `All`|
|SDK language|Go, C#/.NET, Python, Java, and JavaScript/TypeScript|

The four entry points represent how a customization task starts: a direct request,
review feedback, build diagnostics, or a compatibility requirement. Each scenario
is evaluated across applicable edit scopes and target languages to verify both
the requested outcome and compliance with the permitted change boundary.

The full matrix has 60 entry-point/scope/language combinations (4 x 3 x 5), with
multiple scenarios possible within each combination. This is the target scope,
not a claim that every combination is implemented. The scenario tables below
track current coverage; language-specific stimuli capture differences in SDK
naming, types, signatures, and customization mechanisms.


## Eval Specs

### Eval naming convention

Evals for customization tool will cover four entry points:

|Entry point|	Code	|Eval-level input tag	|Content|
|---------------------|---------|----------------------|-----------------------------|
|Customization request|	001	|customizationRequest|	Direct request with target language and API symbol|
|Feedback|	002|	feedback|	API review or PR feedback with surrounding API context|
|Build failure|	003	|buildFailure|	Reproducible compiler, lint, or typing diagnostics|
|Breaking changes|004|breakingChange|Old/new public API evidence and compatibility requirement|

Each logical scenario has one [entry-point-code][scenario-id].eval.yaml file, with
separate stimuli for each covered `editScope` and target language. Full coverage
for one scenario can include up to 15 stimuli (3 edit scopes x 5 languages);
scenarios with limited applicability cover only the relevant combinations.

For example: **001001.eval.yaml**

- 001 represents the entry-point code.
- 001 represents the scenario ID within that entry point.

### tags

Each eval spec has two-level tags:

- eval-spec-level tag: **input**, it refer to the customization input, it can be `customizationRequest`, `feedback`, `buildFailure`, `breakingChange`
- simulate-level tag: **editScope**, it refer to the edit scope of the customization, it can be `SpecInputs`, `CustomCode`, `All`

### Grader


verify the typespec code and the API specification, and source code

- file-contains: 
    - typespec code match: validate typespec customization code
    - source code match: validate customized SDK source code
- typespec-compile:
    - compile typespec and regen SDK and verify the SDK codefor editscope contains customCode
- rubric: The request is resolved. e.g. for build failure inputs, the SDK can build successfully

## Scenarios

### Customization Request

TBD

### Feedback

TBD

### Build failure

TBD

### Breaking Changes

|Scenarios| editScope| Grader| Status|
|--------------|----------------|--------------------------------------------|-------|
|004001-model-renamed-breaking-change| SpecInputs| add @@clientName customization code in client.tsp|✅|
|| CustomCode| verify customization code snippet|➖|
|| All| add @@clientName customization code in client.tsp|➖|
|004002-property-renamed-breaking-change|SpecInputs| add @@clientName customization code in client.tsp|✅|
|| CustomCode| verify customization code snippet|➖|
|| All| add @@clientName customization code in client.tsp|➖|
|004003-operation-renamed-breaking-change|SpecInputs| add @@clientName customization code in client.tsp|✅|
|| CustomCode| verify customization code snippet|➖|
|| All| add @@clientName customization code in client.tsp|➖|
|004004-operation-param-order-breaking-change|SpecInputs| add @@override customization code in client.tsp|✅|
|| CustomCode| verify customization code snippet|➖|
|| All| add @@clientName customization code in client.tsp|➖|
|004005-client-renamed-breaking-change|SpecInputs| add @@clientName customization code in client.tsp|✅|
|| CustomCode| verify customization code snippet|➖|
|| All| add @@clientName customization code in client.tsp|➖|
|004006-operation-moved-to-another-client-breaking-change|SpecInputs| add @@clientLocation customization code in client.tsp|✅|
|| CustomCode| verify customization code snippet|➖|
|| All| add @@clientName customization code in client.tsp|➖|
|004007-property-type-change-breaking-change|SpecInputs| add @@alternateType customization code in client.tsp|✅|
|| CustomCode| verify customization code snippet|➖|
|| All| add @@clientName customization code in client.tsp|➖|
|004008-enum-value-renamed-breaking-change|SpecInputs| add @@clientName customization code in client.tsp|✅|
|| CustomCode| verify customization code snippet|➖|
|| All| add @@clientName customization code in client.tsp|➖|


## CI execution

All `*.eval.yaml` files in this directory and its subdirectories run in the
[nightly live eval pipeline](../../../eng/common/pipelines/live-eval.yml) against
`azsdk-mcp-live`. They are also included in the `scenarios-live` suite in
[the Vally configuration](../../.vally.yaml), not the hermetic mock pipeline.
The live pipeline runs daily at 09:00 UTC on `main` and can be queued manually.