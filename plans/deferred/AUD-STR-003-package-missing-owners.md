# AUD-STR-003: Package Missing Minimum Owners

## Summary
Validate that every Package work item has at least 2 unique individual owners.

## Criteria
1. Fetch all Package work items with hydrated Owner relations.
2. Expand team owners to individual members using `ITeamUserCache`.
3. Count unique individual owners (exclude unresolved team aliases).
4. Flag packages with fewer than 2 unique individual owners.

## Fix (`--fix`)
**Report only.** Adding owners requires human decision-making—the audit cannot determine
who should own a package.

## Affected Linter Rules
Maps to: `check-package` validation rule 3 (≥2 unique individual source owners).

> **Note**: The CODEOWNERS linter itself only requires ≥1 owner per source path (OWN-001).
> The ≥2 threshold is enforced by `check-package`, which is used for PR merge and release
> gating. A path with 1 owner passes the linter but fails `check-package`.

## Dependencies
- `ITeamUserCache` for team expansion.
- Package hydration logic (existing).
