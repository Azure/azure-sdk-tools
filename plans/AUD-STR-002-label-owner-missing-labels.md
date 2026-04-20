# AUD-STR-002: Label Owner Missing Labels

## Summary
Validate that every Label Owner work item has at least one related Label work item.

## Criteria
1. Fetch all Label Owner work items with hydrated relations.
2. For each, check that `Labels.Count > 0`.
3. Flag Label Owner entries with zero labels.

## Fix (`--fix`)
- **Report only** — Label Owner entries with zero labels require human investigation.
  This is a data integrity issue that may indicate missing label assignments, stale records,
  or configuration errors. No automated fix is applied.

## Affected Linter Rules
Maps to: LBL-001 (labels required), BLK-002 (AzureSdkOwners requires ServiceLabel),
BLK-003 (ServiceOwners requires ServiceLabel).

## Dependencies
- `IDevOpsService` for work item queries and deletion.
