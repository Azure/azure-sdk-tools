# OWN-003: User Must Be Public Azure Member

## Linter Source
`Owners.cs:70-79`

## What It Checks
Individual (non-team) owners must be public members of the Azure GitHub organization.

## What Constitutes a Violation
A user who is not found in the public Azure org membership list.

## Auto-Fix
None in the current linter.

## ADO Audit Applicability
**Yes.** This is the primary "invalid owner" detection rule. The audit should validate every
individual Owner work item's `GitHubAlias` against Azure org membership and write permission.
`--fix` removes relations from invalid owners.
