# OWN-002: Team Must Be Write Team

## Linter Source
`Owners.cs:49-60`

## What It Checks
Any `@Azure/...` team alias listed as an owner must have write permission (i.e., descend from
`azure-sdk-write`).

## What Constitutes a Violation
A team alias that is not in the known write-teams set.

## Auto-Fix
None in the current linter.

## ADO Audit Applicability
**Yes.** Owner work items with `IsGitHubTeam == true` must be validated against the
`azure-sdk-write` team hierarchy. Invalid team owners should be flagged (and optionally
removed with `--fix`).
