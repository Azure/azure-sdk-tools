# BLK-005: ServiceLabel Block Completeness

## Linter Source
`CodeownersLinter.cs:237-253`

## What It Checks
A `ServiceLabel` block must be accompanied by one of:
- `AzureSdkOwners`, or
- `ServiceOwners` with `/<NotInRepo>/`, or
- A source path/owner line

Also rejects having too many owner sources (e.g., both AzureSdkOwners and ServiceOwners with
a source path).

## What Constitutes a Violation
A `ServiceLabel` that has no associated owner source, or has too many owner sources.

## Auto-Fix
None in the current linter.

## ADO Audit Applicability
**Yes.** For every unique service label set in ADO, there should be a corresponding Label Owner
entry with owners. The audit checks that Label Owner work items linked to Labels have at least
one Owner relation, and that the ownership structure is valid (not missing owners entirely, not
having conflicting owner types).
