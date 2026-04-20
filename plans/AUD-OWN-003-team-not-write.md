# AUD-OWN-003: Team Not Under azure-sdk-write

## Summary
Validate that Owner work items representing teams descend from the `azure-sdk-write` team.

## Criteria
1. Fetch all Owner work items where `IsGitHubTeam == true`.
2. **Skip** any team whose alias does not match `Azure/<team>` format (these are flagged
   separately by AUD-OWN-002 — OWN-003 must not crash on them).
3. For well-formed teams, check team ancestry: first check `ITeamUserCache` for a cached
   result, and if the team is not found in cache, fall back to `IGitHubService` parent-chain
   validation (reuse `CodeownersManagementHelper`'s `ThrowIfInvalidTeamAlias`-equivalent logic).
4. Flag teams that don't descend from `azure-sdk-write`.

## Fix (`--fix`)
- For each invalid team Owner work item:
  - Remove `Related` relations from all linked Label Owner and Package work items.
  - Log each removal.

## Affected Linter Rules
Maps to: OWN-002 (team must be write team).

## Dependencies
- `ITeamUserCache` for team hierarchy lookup (existing).
- `IGitHubService` for team ancestry verification (existing).
