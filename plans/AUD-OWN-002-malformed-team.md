# AUD-OWN-002: Malformed Team Entry

## Summary
Validate that Owner work items representing teams follow the `Azure/<team-name>` format.

## Criteria
1. Fetch all Owner work items where `IsGitHubTeam == true`.
2. Check that `GitHubAlias` matches pattern `Azure/<team>` (case-insensitive).
3. Flag if the alias contains `/` but doesn't match the expected format.

## Fix (`--fix`)
**Report only.** Malformed team names require human correction—there is no reliable way
to auto-correct an invalid team alias.

## Affected Linter Rules
Maps to: OWN-004 (malformed team entry).

## Dependencies
- No external API calls needed; pure string validation.
