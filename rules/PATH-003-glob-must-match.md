# PATH-003: Glob Must Match Repo Files

## Linter Source
`DirectoryUtils.cs:43-52, 193-200`

## What It Checks
Valid glob expressions must match at least one file in the repository.

## What Constitutes a Violation
A glob that resolves to zero matches.

## Auto-Fix
None in the current linter.

## ADO Audit Applicability
**No (deferred).** Same reasoning as PATH-001 and PATH-002. Requires repo context.
