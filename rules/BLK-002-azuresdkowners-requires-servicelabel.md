# BLK-002: AzureSdkOwners Requires ServiceLabel

## Linter Source
`CodeownersLinter.cs:219-224`

## What It Checks
An `AzureSdkOwners` moniker must be accompanied by a `ServiceLabel` in the same block.

## What Constitutes a Violation
An `AzureSdkOwners` comment with no corresponding `ServiceLabel` comment in the block.

## Auto-Fix
None in the current linter.

## ADO Audit Applicability
**Yes.** In the ADO model, a Label Owner with `LabelType = "Azure SDK Owner"` must have at
least one related Label work item (which becomes the ServiceLabel in rendered output). The
audit should flag Label Owner entries of this type with zero labels.
