# LBL-004: Repo Label Must Exist

## Linter Source
`Labels.cs:48-55`

## What It Checks
Every label referenced in CODEOWNERS must exist as an actual label in the repository.

## What Constitutes a Violation
A label name that does not match any label in the repository's label set.

## Auto-Fix
None in the current linter.

## ADO Audit Applicability
**Yes.** In the ADO model, Label work items should correspond to actual GitHub repo labels.
The `github-label sync-ado` command already handles syncing. The audit can verify Label work
items reference valid labels.
