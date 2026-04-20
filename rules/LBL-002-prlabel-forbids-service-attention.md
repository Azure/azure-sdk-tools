# LBL-002: PRLabel Forbids ServiceAttention

## Linter Source
`Labels.cs:30-38`

## What It Checks
A `PRLabel` metadata comment must not include the `Service Attention` label.

## What Constitutes a Violation
`Service Attention` appears in a PRLabel label list.

## Auto-Fix
None in the current linter.

## ADO Audit Applicability
**Partially.** In the ADO model, a Package work item's related Labels represent PR labels.
`Service Attention` should not be a label directly linked to a Package. The audit can check
for this. However, `Service Attention` is handled by triage automation and is not typically
added via the management commands.
