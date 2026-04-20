# OWN-005: Invalid User

## Linter Source
`Owners.cs:87-97`

## What It Checks
Non-team owners must be in the known write-user set (users with write access to the repo).

## What Constitutes a Violation
An individual username not found in the write-users set.

## Auto-Fix
None in the current linter.

## ADO Audit Applicability
**Yes.** Overlaps with OWN-003. In the ADO audit, this is consolidated into the owner
validation rule: check public org membership and write permissions via
`CodeownersValidatorHelper`.
