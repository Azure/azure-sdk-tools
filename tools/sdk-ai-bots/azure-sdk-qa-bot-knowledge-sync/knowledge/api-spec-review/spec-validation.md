# Spec Validation

Common issues and solutions for API spec pipeline errors, CI failures, and linting issues.

## Swagger breaking change false positives in TypeSpec conversion PRs can be suppressed

TypeSpec conversion PRs may trigger false-positive swagger breaking change errors when the generated swagger uses common-types references (e.g., `TrackedResource/properties/tags`) that differ textually from the original hand-written swagger but are semantically equivalent.

These violations are suppressible. Follow the suppression docs for false positives at the PR suppression guide. Review each violation carefully — the conversion is not guaranteed to be 100% accurate, and real breaking changes should still go through the breaking change process.

## Avocado MISSING_APIS_IN_DEFAULT_TAG error after @renamedFrom on action routes

Avocado does not handle moves or renames of API paths. When using `@renamedFrom` on a custom action route, the old swagger path may not appear in the default tag, causing a `MISSING_APIS_IN_DEFAULT_TAG` error.

**Fix**: Add the `Approved-Avocado` label to the PR to suppress this Avocado failure. No changes to the TypeSpec or readme.md are needed.

## readme.md is required for SDK generation (Avocado failure)

Even for brand-new TypeSpec-based APIs, Avocado validates that a `readme.md` exists somewhere in the spec directory to configure SDK generation. TypeSpec compilation succeeds without it, but the SDK pipeline needs it to determine which API versions to generate and where the OpenAPI files are.

The location and factoring of `readme.md` can vary by service. You need at least one in your service directory structure.

## Swagger LintDiff Fails When Swagger Files Are Not Referenced by README

When a PR fails with a Swagger LintDiff error stating that **no affected swaggers were found**, it means the Swagger file reported in the error is **not reachable from the service `readme.md`**, either directly listed or indirectly referenced. LintDiff only analyzes Swagger files that are discoverable through the README; if a Swagger exists on disk but is not referenced, LintDiff treats it as orphaned and fails. To fix this, either **add the Swagger file to the appropriate `readme.md`** or **remove the unused Swagger file** so that all Swagger inputs are consistently tracked by the README-driven spec model.

## Swagger LintDiff CI failure is expected when fixing bugs already in main

If you are fixing a bug that is already present in the `main` branch (e.g., a malformed `readme.md` or a broken `input-file` path), the Swagger LintDiff check will still fail on your fix PR. This is by design: LintDiff runs on the spec in both the **before** state (the base branch) and the **after** state (your PR). If the base branch is already broken and causes LintDiff to crash or produce no input, the "before" run will fail — and LintDiff cannot compute a diff. This means the PR that fixes the bug will itself fail LintDiff. This is expected behavior, not a tooling bug. You can request a suppression (`Approved-LintDiff` label) for the PR if the failure is purely a consequence of fixing a pre-existing main-branch issue.

## TypeSpec conversion replaces existing swagger with generated swagger — Avocado validates equivalence

As part of a TypeSpec conversion PR, the existing hand-authored Swagger is replaced by a Swagger generated from TypeSpec. The generated Swagger is still used for some purposes (SDK generation, Avocado validation, breaking change detection), so Avocado protects the ability to process it.

The generated Swagger needs to be **equivalent** to the original, but not necessarily **identical**. Textually different representations (e.g., different `$ref` structures or property ordering) are acceptable as long as the API surface is the same.

Generally, any issue reflected in the generated Swagger will also show up in **breaking change checks**, which must be resolved or suppressed if they are false positives. Adding the `Approved-Avocado` label to bypass Avocado is appropriate only if the Avocado error is a known false positive (e.g., `MISSING_APIS_IN_DEFAULT_TAG` triggered by path renames handled by `@renamedFrom`). Real API differences must be fixed in the TypeSpec source first.

## Linter suppression in TypeSpec can only be provided inline — cannot be filtered by files in shared packages

TypeSpec linter suppressions can only be provided **inline** at the use site using `#suppress`. There is no mechanism to attach suppression rules to a shared package definition that would apply only when those types are consumed — suppressions do not follow type references.

Linter rules **can** be disabled globally in `tspconfig.yaml` using the `linter` options, but this disables the rule for the **entire project**, not just the files where the shared types are defined. There is currently no way to scope suppression to specific files within a shared/imported package.
