# TypeSpec Long Running Operations (LRO)

Common issues and solutions for implementing Long Running Operations in TypeSpec.

## emit-lro-options: none does not affect data plane SDK generation

`emit-lro-options: "none"` in `@azure-tools/typespec-autorest` is a pure OpenAPI emission setting — it controls whether `x-ms-long-running-operation-options` is written into the emitted Swagger. Setting it to `"none"` omits that extension while still emitting `x-ms-long-running-operation: true`.

This option has **no effect on data plane SDK generation behavior** because data plane clients are generated directly from TypeSpec, not from the emitted OpenAPI. The LRO semantics are fully encoded in the TypeSpec operation definition itself.

**Fix**: To verify LRO encoding is correct, use `emit-lro-options: "all"` during development to visually confirm the extension is emitted as expected. This is not required for check-in; the extension has little documentary value for customers.

```yaml
# tspconfig.yaml — use "all" for local verification, "none" is acceptable for check-in
options:
  "@azure-tools/typespec-autorest":
    emit-lro-options: "all"  # verifies lro encoding; switch to "none" when confirmed
```

Note: `x-ms-long-running-operation-options` (including `final-state-schema`) is a Microsoft-specific OpenAPI extension. The original expert clarified that `final-state-schema` "is just for debugging purposes and is not necessary in the actual swagger." Omitting it does not break SDK generation for TypeSpec-authored services, as SDK emitters read LRO metadata directly from the TypeSpec model.

## Non‑resource long‑running GET operations are not supported in ARM TypeSpec modeling

This **scenario** typically appears when migrating legacy Swagger specifications into TypeSpec, where a GET endpoint is marked with x-ms-long-running-operation: true and returns an operation result at a subscription or provider scope rather than representing a real ARM resource. Although such patterns existed historically in Swagger, they do not align with ARM’s resource and operation semantics and therefore cannot be modeled as long‑running operations in TypeSpec.

In ARM, long‑running operations represent asynchronous actions that change state and are initiated by non‑GET methods, most commonly POST, PUT, PATCH, or DELETE. A GET operation is inherently a read and must be idempotent; treating it as long‑running conflates polling with execution. As a result, TypeSpec intentionally rejects modeling a GET operation as an LRO using ArmAcceptedLroResponse. This is a design rule, not a tooling limitation, and reflects ARM API guidelines rather than emitter constraints.

When an endpoint returns the status or outcome of a previously started operation, it should be modeled as a regular GET without long‑running semantics, or the asynchronous behavior should be moved to a provider‑level or resource‑level action initiated via POST. For example, an async “move” or “start” action is modeled as an ArmResourceActionAsync or ArmProviderActionAsync, while subsequent GETs simply retrieve state or result data. This separation produces clearer, evolvable specifications and avoids invalid LRO constructs.

The **recommended approach** is therefore to model non‑resource asynchronous work as POST‑based actions and reserve GET for simple retrieval. Treat legacy Swagger GET‑LRO patterns as modeling mistakes during migration and refactor them to match ARM semantics. This results in correct validation, consistent SDK generation, and long‑term maintainability.