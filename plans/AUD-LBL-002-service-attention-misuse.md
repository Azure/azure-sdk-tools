# AUD-LBL-002: Service Attention Misuse

## Summary
Validate that `Service Attention` is not used as a primary/only label on Label Owner or
Package entries.

## Criteria
1. Fetch all Label Owner work items.
2. For each, hydrate related Labels.
3. Flag if:
   - A Label Owner of type `PR Label` has `Service Attention` in its labels.
   - A Label Owner of type `Service Owner` has ONLY `Service Attention` as its label.
4. Fetch all Package work items.
5. For each, hydrate related Labels (PR labels).
6. Flag if a Package has `Service Attention` as a PR label.

## Fix (`--fix`)
- Report only — requires human review to determine the
  correct service label replacement.

## Affected Linter Rules
Maps to: LBL-002 (PRLabel forbids ServiceAttention), LBL-003 (ServiceLabel cannot be only
ServiceAttention).

## Dependencies
- Label hydration logic in `CodeownersManagementHelper` (existing).
