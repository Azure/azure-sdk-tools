# AUD-STR-001: Label Owner Missing Owners

## Summary
Validate that every Label Owner work item has at least one related Owner work item.

## Criteria
1. Fetch all Label Owner work items with hydrated relations.
2. For each, check that `Owners.Count > 0`.
3. Flag Label Owner entries with zero owners.

## Fix (`--fix`)
- If a Label Owner has zero owners, delete the work item.

## Affected Linter Rules
Maps to: OWN-001 (owners required), BLK-005 (ServiceLabel completeness).

## Context
This violation is particularly important after `--fix` runs AUD-OWN-001 (invalid owner
removal). Removing an invalid owner's relations may leave Label Owner entries without any
owners. This rule catches that cascade.

## Dependencies
- `IDevOpsService` for work item deletion and relation queries.
