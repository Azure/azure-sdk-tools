# Spec Validation

Common issues and solutions for API spec pipeline errors, CI failures, and linting issues.

## Missing APIs in default tag error for TypeSpec conversion PR

Part of the TypeSpec conversion is replacing the existing hand-authored swagger with a generated swagger. The generated swagger is still used for some purposes (SDK generation, Avocado validation, breaking change detection), and Avocado protects the ability to process it. The generated swagger needs to be **equivalent** to the original, but not necessarily **identical** — textually different representations (e.g., different `$ref` structures, common-types references like `TrackedResource/properties/tags`, or property ordering) are acceptable as long as the API surface is the same.

Generally, issues reflected in the generated swagger will also show up in **breaking change checks**, which will have to be resolved (or suppressed if they are false positives). Review each violation carefully — the conversion is not guaranteed to be 100% accurate, and real breaking changes should still go through the breaking change process. Adding the `Approved-Avocado` label to bypass Avocado is appropriate only if the Avocado error is a known false positive.

## Avocado MISSING_APIS_IN_DEFAULT_TAG error after @renamedFrom on action routes

Avocado does not handle moves or renames of API paths. When using `@renamedFrom` on a custom action route, the old swagger path may not appear in the default tag, causing a `MISSING_APIS_IN_DEFAULT_TAG` error.

**Fix**: Add the `Approved-Avocado` label to the PR to suppress this Avocado failure. No changes to the TypeSpec or readme.md are needed.

## readme.md is required for SDK generation (Avocado failure)

Even for brand-new TypeSpec-based APIs, Avocado validates that a `readme.md` exists somewhere in the spec directory to configure SDK generation. TypeSpec compilation succeeds without it, but the SDK pipeline needs it to determine which API versions to generate and where the OpenAPI files are.

The location and factoring of `readme.md` can vary by service. You need at least one in your service directory structure.

## Swagger LintDiff Fails When Swagger Files Are Not Referenced by README

When a PR fails with a Swagger LintDiff error stating that **no affected swaggers were found**, it means the Swagger file reported in the error is **not reachable from the service `readme.md`**, either directly listed or indirectly referenced. LintDiff only analyzes Swagger files that are discoverable through the README; if a Swagger exists on disk but is not referenced, LintDiff treats it as orphaned and fails. To fix this, either **add the Swagger file to the appropriate `readme.md`** or **remove the unused Swagger file** so that all Swagger inputs are consistently tracked by the README-driven spec model.

## Swagger LintDiff CI failure is expected when fixing bugs already in main

If you are fixing a bug that is already present in the `main` branch (e.g., a malformed `readme.md` or a broken `input-file` path), the Swagger LintDiff check will still fail on your fix PR. This is by design: LintDiff runs on the spec in both the **before** state (the base branch) and the **after** state (your PR). If the base branch is already broken and causes LintDiff to crash or produce no input, the "before" run will fail — and LintDiff cannot compute a diff. This means the PR that fixes the bug will itself fail LintDiff. This is expected behavior, not a tooling bug.

A common symptom is LintDiff reporting `[Exception] No input files provided.` — this happens when the base branch `readme.md` is malformed or references nonexistent files, so AutoRest resolves an empty `input-file` set for the "before" state.

You can request a suppression (`Approved-LintDiff` label) for the PR if the failure is purely a consequence of fixing a pre-existing main-branch issue.

## ArmResourcePatchAsync with discriminated resource types

When using `ArmResourcePatchAsync` (or `ArmResourcePatchSync`) with a resource that has a discriminator, OAV may fail with `OBJECT_MISSING_REQUIRED_PROPERTY_DEFINITION`. This is because the discriminator is typically a required property on the resource, but PATCH requests should not require all properties.

The general guidance is that **PATCH requests over discriminated types should require the discriminator property**. This may result in some LintDiff violations (e.g., `PatchBodyParametersSchema`), but that is expected and those violations can be suppressed. This is especially true if, as most services, your PATCH operation would need the discriminator value on the wire to determine how to apply the PATCH request to the existing resource.

## Linter suppression in TypeSpec can only be provided inline — cannot be filtered by files in shared packages

TypeSpec linter suppressions can only be provided **inline** at the use site using `#suppress`. There is no mechanism to attach suppression rules to a shared package definition that would apply only when those types are consumed — suppressions do not follow type references.

Linter rules **can** be disabled globally in `tspconfig.yaml` using the `linter` options, but this disables the rule for the **entire project**, not just the files where the shared types are defined. There is currently no way to scope suppression to specific files within a shared/imported package.

# Missing APIs in default tag error for TypeSpec conversion PR

Part of the TypeSpec conversion is replacing the existing hand-authored swagger with a generated swagger. The generated swagger is still used for some purposes (SDK generation, Avocado validation, breaking change detection), and Avocado protects the ability to process it. The generated swagger needs to be **equivalent** to the original, but not necessarily **identical** — textually different representations (e.g., different `$ref` structures, common-types references like `TrackedResource/properties/tags`, or property ordering) are acceptable as long as the API surface is the same.

Generally, issues reflected in the generated swagger will also show up in **breaking change checks**, which will have to be resolved (or suppressed if they are false positives). Review each violation carefully — the conversion is not guaranteed to be 100% accurate, and real breaking changes should still go through the breaking change process. Adding the `Approved-Avocado` label to bypass Avocado is appropriate only if the Avocado error is a known false positive.

## Suppressing the Avocado failure

If the `MISSING_APIS_IN_DEFAULT_TAG` error is a known false positive (for example, from a TypeSpec conversion PR that does not change the API surface), you can request the `Approved-Avocado` label on your PR. This is a one-time, per-PR suppression. Permanent suppression is not available for Avocado.

For the newer `MULTIPLE_API_VERSION` rule (blocking error as of 7/1/2025), check the [Uniform Versioning Violation Guide](https://github.com/Azure/azure-rest-api-specs/wiki/Uniform-Versioning-Violation-Guide) or contact azversioning@service.microsoft.com for guidance.
