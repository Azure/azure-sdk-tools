# AUD-STR-004: Package Missing Labels

## Summary
Validate that every Package work item has at least one related Label (used as PR label).

## Criteria
1. Fetch all Package work items with hydrated Label relations.
2. Flag packages with zero labels.

## Fix (`--fix`)
**Report only.** Label assignment requires human knowledge of which service label applies.

## Affected Linter Rules
Maps to: `check-package` validation rule 4 (≥1 PR label), BLK-004 (PRLabel block ending).

## Dependencies
- Package hydration logic (existing).
