# OWN-004: Malformed Team Entry

## Linter Source
`Owners.cs:80-87`

## What It Checks
Team entries must follow the `@Azure/<team-name>` format.

## What Constitutes a Violation
A team alias that contains `/` but does not start with `Azure/`.

## Auto-Fix
None in the current linter.

## ADO Audit Applicability
**Yes.** Owner work items where `IsGitHubTeam == true` should have aliases matching
`Azure/<team>` or `azure/<team>`. Malformed team aliases are flagged.
