# LBL-003: ServiceLabel Cannot Be Only ServiceAttention

## Linter Source
`Labels.cs:39-46`

## What It Checks
A `ServiceLabel` metadata comment where the only label is `Service Attention` is invalid.

## What Constitutes a Violation
A ServiceLabel with exactly one label and that label is `Service Attention`.

## Auto-Fix
None in the current linter.

## ADO Audit Applicability
**Partially.** In the ADO model, Label Owner work items of type `Service Owner` must have at
least one non-`Service Attention` label. The audit can validate this.
