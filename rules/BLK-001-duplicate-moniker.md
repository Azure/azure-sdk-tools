# BLK-001: Duplicate Moniker In Block

## Linter Source
`CodeownersLinter.cs:152-193`

## What It Checks
The same moniker type (PRLabel, ServiceLabel, ServiceOwners, AzureSdkOwners) must not appear
more than once in a single CODEOWNERS block.

## What Constitutes a Violation
Two or more lines with the same moniker type within one contiguous block.

## Auto-Fix
None in the current linter.

## ADO Audit Applicability
**No.** The ADO data model prevents this structurally—Label Owner work items are unique by
`(Repository, RepoPath, Section, LabelType, label set)`. Duplicate monikers cannot occur in
the generated output.
