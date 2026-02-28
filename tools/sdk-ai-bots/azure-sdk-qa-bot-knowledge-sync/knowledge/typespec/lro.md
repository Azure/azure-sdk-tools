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

Note: `x-ms-long-running-operation-options` is a Microsoft-specific OpenAPI extension and omitting it does not break SDK generation for TypeSpec-authored data plane services.
