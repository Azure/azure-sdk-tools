# BLK-004: PRLabel Block Must End With Source Path

## Linter Source
`CodeownersLinter.cs:231-235`

## What It Checks
A block starting with `PRLabel` must end with a source path/owner line.

## What Constitutes a Violation
A `PRLabel` comment that is not followed by a path line before the next block.

## Auto-Fix
None in the current linter.

## ADO Audit Applicability
**Partially.** In the ADO model, PR labels are linked to Packages and Label 
Owners can be of type PR Label. In the case of a Label Owner of type PR Label 
ensure that the repo path is not empty. 
