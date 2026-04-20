# OWN-001: Source Line Owners Required

## Linter Source
`Owners.cs:31-45`

## What It Checks
Every CODEOWNERS source path line must have at least one owner listed.

## What Constitutes a Violation
A path line with zero owners after parsing.

## Auto-Fix
None in the current linter.

## ADO Audit Applicability
**Yes.** Translates to: every Package work item must have at least one related Owner work item,
and each Label Owner with a non-empty `RepoPath` must have at least one related Owner.

The `check-package` validation strengthens this to require ≥2 unique individual owners.
