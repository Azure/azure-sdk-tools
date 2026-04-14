# TypeSpec Migration

Common issues and solutions for converting OpenAPI/Swagger specifications to TypeSpec.

## Record<unknown> produces additionalProperties in swagger and that is correct

When converting Swagger to TypeSpec, `Record<unknown>` maps to `{ "type": "object", "additionalProperties": {} }` in the generated Swagger. This is semantically equivalent to the original `{ "type": "object" }` even though the text differs. Do not try to make the generated swagger match the original exactly — the two representations are equivalent.

If the original property was `"type": "object"` without `additionalProperties`, use either `Record<unknown>` or `unknown` in TypeSpec depending on the intent.

## TypeSpec specs have two copies of examples — update the TypeSpec copy first

When working with TypeSpec-based specs, examples exist in two locations:

1. **TypeSpec copy**: `specification/{service}/{Service}.Management/examples/{version}/`
2. **Swagger copy**: `specification/{service}/resource-manager/{RP}/{version-status}/{version}/examples/`

Always update examples in the TypeSpec copy, then run `tsp compile` to propagate them to the swagger folder. TypeSpec Validation (TSV) will fail if the two copies are out of sync.

If `oav generate-examples` fails, it may be an oav bug — file an issue at the oav repository.

## LintDiff false positives during TypeSpec migration require explicit suppression in readme.md

During TypeSpec migration, the AutoRest emitter normalizes swagger output, producing thousands of functionally equivalent but textually different diffs. The LintDiff auto-baselining algorithm cannot recognize unchanged areas in large diffs, so pre-existing violations get flagged as new errors.

**Fix**: Explicitly suppress all LintDiff errors in your `readme.md` file regardless of whether they are new or pre-existing. Do not rely on the auto-baselining mechanism for TSP migration PRs.

## Restoring x-ms-client-flatten behavior during TypeSpec migration using Legacy.flattenProperty

When migrating a Swagger ARM spec to TypeSpec, `x-ms-client-flatten: true` on a properties bag is not automatically preserved. If the original Swagger had this flag to flatten the `properties` envelope into the SDK client model, you need to explicitly apply it in TypeSpec.

**Fix**: Use the augment decorator `@@Azure.ClientGenerator.Core.Legacy.flattenProperty` targeting the property you want flattened:

```typespec
@@Azure.ClientGenerator.Core.Legacy.flattenProperty(ArcSetting.properties);
```

By convention, place this decorator in a `back-compatible.tsp` file (e.g., `back-compat.tsp`) in your spec folder. This keeps backward-compatibility concerns separate from the main TypeSpec definition.

This is the correct way to preserve SDK client flatten behavior from Swagger in a TypeSpec-based spec.

## The service-under-conversion label does not block existing or concurrent PRs

The `service-under-conversion` label is an informational/coordination label indicating that the service is undergoing TypeSpec conversion. It does **not** block or delay merging of other PRs (including stable API PRs) for the same service.

If there is a later API version than the conversion PR, the TypeSpec conversion would normally be based on that later API version once it is checked in. The label is applied to track conversion status, not to create a merge dependency.
