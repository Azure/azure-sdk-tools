# PATH-001: Path Must Exist In Repo

## Linter Source
`DirectoryUtils.cs:39-63, 208-218`

## What It Checks
Non-glob source paths must exist as directories or files in the repository.

## What Constitutes a Violation
A path expression that does not match any file or directory in the repo.

## Auto-Fix
None in the current linter.

## ADO Audit Applicability
**No (deferred).** Path validation requires repo context. The ADO data model stores `RepoPath`
on Label Owner work items and `PackageRepoPath` on Package work items. Validating these against
the actual repo is a separate concern that could be added in a later phase.
