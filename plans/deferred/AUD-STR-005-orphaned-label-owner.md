# AUD-STR-005: Orphaned Label Owner After Fix

## Summary
After invalid owner removal, detect Label Owner entries that have been left with insufficient
ownership to pass the codeowners linter.

## Criteria
1. Run after AUD-OWN-001/003 fixes — the rule harness has already refreshed context with
   post-fix state from ADO.
2. For each Label Owner with a non-empty `RepoPath`:
   - Expand remaining team owners and count unique individual owners.
   - Flag if fewer than 2 unique individual owners remain (matches `check-package` threshold).
3. For each Label Owner with type `Service Owner`:
   - Same: flag if fewer than 2 unique individual owners remain.

## Fix (`--fix`)
- If a Label Owner has zero owners remaining: delete it (fully orphaned after fix) via
  idempotent delete wrapper.
- If a Label Owner has 1 owner remaining: report it as under-minimum (requires human action).
- The rule harness refreshes context after deletes.

## State Management
This rule naturally sees post-fix state because the rule harness refreshes `AuditContext`
after each preceding rule's fixes. No special cascade tracking needed.

## Dependencies
- Must run after AUD-OWN-001 and AUD-OWN-003 fixes.
- `ITeamUserCache` for member expansion.
- `IDevOpsService.DeleteWorkItemAsync` (generic, needs to be added, with idempotent wrapper).
