# TypeSpec Paging

Common issues and solutions for implementing pagination patterns in TypeSpec.

## Pagination with ArmResourceActionSync and ProviderHub Controller Emitters

1. When defining a list API in TypeSpec, it is common to model the response as `Azure.Core.Page<T>` to express pagination semantics. This correctly signals to SDK generators that the operation is pageable. However, when using Microsoft.TypeSpec.Providerhub.Controller to generate controllers, pagination semantics are not automatically materialized into HTTP endpoints or query parameters.

2. The ProviderHub controller emitter generates only the initial action endpoint for ArmResourceActionSync. If that action is modeled as a POST (which is typical for resource actions), the emitter does not add pagination-related query parameters (such as continuation tokens), nor does it generate a follow-up endpoint for retrieving subsequent pages. This is by design: the emitter reflects the declared action, but does not infer or synthesize additional HTTP routes for paging. As a result, even though the response type is `Azure.Core.Page<T>`, the generated API surface is incomplete for end-to-end pagination support.

3. Azure SDKs rely on a GET-based next-page endpoint to enable automatic pagination. Without such an endpoint, SDK auto-paging does not work and would require custom client-side logic. To make pagination functional, you must manually add a GET endpoint that accepts a continuation token and returns the same paged response shape. ProviderHub-generated controllers are partial, so this additional endpoint can be implemented without modifying generated code.
