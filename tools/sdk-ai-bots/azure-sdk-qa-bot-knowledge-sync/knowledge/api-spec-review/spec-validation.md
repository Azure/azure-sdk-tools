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
