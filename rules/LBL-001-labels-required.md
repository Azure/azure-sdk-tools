# LBL-001: Labels Required

## Linter Source
`Labels.cs:25-28`

## What It Checks
`PRLabel` and `ServiceLabel` metadata comments must have at least one label value.

## What Constitutes a Violation
A `PRLabel:` or `ServiceLabel:` comment with an empty label list.

## Auto-Fix
None in the current linter.

## ADO Audit Applicability
**Yes.** Translates to: Label Owner work items must have at least one related Label work item.
Packages must have at least one related Label for PR labeling. `--fix` could remove orphaned
Label Owner entries that have zero labels.
