# Spec Validation

Common issues and solutions for API spec pipeline errors, CI failures, and linting issues.

## Swagger breaking change false positives in TypeSpec conversion PRs can be suppressed

TypeSpec conversion PRs may trigger false-positive swagger breaking change errors when the generated swagger uses common-types references (e.g., `TrackedResource/properties/tags`) that differ textually from the original hand-written swagger but are semantically equivalent.

These violations are suppressible. Follow the suppression docs for false positives at the PR suppression guide. Review each violation carefully — the conversion is not guaranteed to be 100% accurate, and real breaking changes should still go through the breaking change process.

## Avocado MISSING_APIS_IN_DEFAULT_TAG error after @renamedFrom on action routes

Avocado does not handle moves or renames of API paths. When using `@renamedFrom` on a custom action route, the old swagger path may not appear in the default tag, causing a `MISSING_APIS_IN_DEFAULT_TAG` error.

**Fix**: Add the `Approved-Avocado` label to the PR to suppress this Avocado failure. No changes to the TypeSpec or readme.md are needed.
