# BLK-003: ServiceOwners Requires ServiceLabel

## Linter Source
`CodeownersLinter.cs:226-229`

## What It Checks
A `ServiceOwners` moniker must be accompanied by a `ServiceLabel` in the same block.

## What Constitutes a Violation
A `ServiceOwners` comment with no corresponding `ServiceLabel` in the block.

## Auto-Fix
None in the current linter.

## ADO Audit Applicability
**Yes.** In the ADO model, a Label Owner with `LabelType = "Service Owner"` must have at least
one related Label. Audit flags Label Owner entries of this type with zero labels.
