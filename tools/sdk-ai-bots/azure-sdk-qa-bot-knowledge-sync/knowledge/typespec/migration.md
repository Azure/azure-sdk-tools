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
