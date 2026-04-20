# PATH-002: Glob Syntax Valid

## Linter Source
`DirectoryUtils.cs:93-166`

## What It Checks
Glob path expressions must use valid syntax (no `\#`, `!`, `[ ]`, `?`, invalid `/**/` usage,
etc.).

## What Constitutes a Violation
A path expression containing forbidden glob characters or patterns.

## Auto-Fix
None in the current linter.

## ADO Audit Applicability
**Yes.** Label Owner `RepoPath` values can contain glob patterns and must use valid syntax.
This is included in the audit as AUD-PATH-001. The same validation is also applied as input
validation on `add-label-owner --path` to prevent invalid paths from entering the data model.
