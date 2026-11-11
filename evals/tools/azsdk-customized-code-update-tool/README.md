# azsdk_typespec_customized_code_update tool evals

Measure whether the customization tool produces correct, durable SDK customizations from four input types while respecting three edit scopes. Calling the right MCP tool is necessary but insufficient: the resulting API must satisfy the request, preserve service semantics and unrelated APIs, build when repair is expected, and survive regeneration.
The benchmark evaluates invocation correctness, repair correctness, edit-scope compliance, and actionable handling of requests that cannot be completed within the permitted scope.
Non-goals are deployment, package publishing, opening pull requests, arbitrary service implementation changes, and architectural redesign. Tool changes stay uncommitted inside disposable workspaces.

## Requirements and contract reconciliation


Evals for customization tool will cover four entry points:

|Entry point|	Code	|Eval-level input tag	|Content|
|---------------------|---------|----------------------|-----------------------------|
|Customization request|	001	|customizationRequest|	Direct request with target language and API symbol|
|Feedback|	002|	feedback|	API review or PR feedback with surrounding API context|
|Build failure|	003	|buildFailure|	Reproducible compiler, lint, or typing diagnostics|
|Breaking changes|004|breakingChange|Old/new public API evidence and compatibility requirement|

Each logical scenario has one [entry-point-code][scenario-id].eval.yaml file and three stimuli, one per editScope: SpecInputs, CustomCode, and All.

## Scope and outcome model

`editScope` is an authorization boundary, not just an editing preference.


|Scope |	Permitted authored changes|	Forbidden authored changes|
|----------|--------------------------------------|---------------------------------|	
|SpecInputs|	Scenario-approved client.tsp, tspconfig.yaml, and pinned-spec reference changes|	Handwritten SDK customizations and unrelated service specification files|	
|CustomCode|	Supported non-generated customization files	|Spec inputs and changes to the pinned spec commit|	
|All|	Both approved source categories|	Unrelated files, service/wire-contract changes, direct generated-source edits|

## Grader


verify the typespec code and the API specification, and source code

- typespec code match: validate typespec customization code
- compile typespec and regen SDK and verify the SDK codefor editscope contains customCode
- The request is resolved. e.g. for build failure inputs, the SDK can build successfully

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

