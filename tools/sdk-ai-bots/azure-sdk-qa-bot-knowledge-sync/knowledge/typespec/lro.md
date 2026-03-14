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

Legacy Swagger specs sometimes mark GET endpoints with `x-ms-long-running-operation: true`, but this pattern violates ARM semantics. In ARM, LROs represent asynchronous state changes initiated by non-GET methods (POST, PUT, PATCH, DELETE). A GET is inherently idempotent and cannot be an LRO. TypeSpec intentionally rejects modeling a GET as an LRO using `ArmAcceptedLroResponse`.

Model non-resource asynchronous work as POST-based actions (`ArmResourceActionAsync` or `ArmProviderActionAsync`) and reserve GET for simple retrieval. Treat legacy Swagger GET-LRO patterns as modeling mistakes during migration and refactor them to match ARM semantics.

## LRO delete status monitor must not be a child of the deleted resource

When implementing an asynchronous delete, the status monitor endpoint must NOT be a child of the resource being deleted. If the status monitor is nested under the resource URL (e.g., `/resources/{id}/operations/{opId}`), it becomes inaccessible once the resource is removed.

**Fix**: Define a separate `@resource("operations")` status model and use `@sharedRoute` for the status read operation.

```typespec
@resource("operations")
model MyDeleteOperationStatus
  is Foundations.OperationStatus<never>;

interface MyResources {
  @sharedRoute
  getDeleteStatus is Operations.ResourceRead<MyDeleteOperationStatus>;

  @pollingOperation(MyResources.getDeleteStatus)
  delete is Operations.LongRunningResourceDelete<MyResource>;
}
```

Note: Any LRO that might change or remove the resource ID should use a status monitor endpoint that is not nested under the resource itself.

## LongRunningResourceCreateWithServiceProvidedName requires ResponseHeadersTrait for Operation-Location header

`LongRunningResourceCreateWithServiceProvidedName` emits a `Location` header by default but not the `Operation-Location` header required by `@pollingOperation`. This triggers the `polling-operation-no-ref-or-link` warning.

**Fix**: Add `ResponseHeadersTrait` to include the `Operation-Location` header:

```typespec
createOrReplace is StandardOperations.LongRunningResourceCreateWithServiceProvidedName<
  MyResource,
  Traits = ResponseHeadersTrait<{
    ...Foundations.LongRunningStatusLocation;
  }>
>;
```