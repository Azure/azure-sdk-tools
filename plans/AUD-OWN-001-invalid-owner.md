# AUD-OWN-001: Invalid Owner

## Summary
Validate that each individual (non-team) Owner work item represents a valid GitHub user who
is a public member of Azure and Microsoft orgs and has write permission.

## Criteria
1. Fetch all Owner work items where `IsGitHubTeam == false`.
2. For each, call the audit-specific overload of `CodeownersValidatorHelper` (wraps
   `ValidateCodeOwnerAsync` internally but throws transient exceptions instead of swallowing).
3. **Evaluate ALL owners first** — collect the full list of invalid owners before taking any
   fix action. Log each invalid owner so humans can investigate.
4. Results are either `Valid` or `Invalid`:
   - `Invalid`: GitHub user not found (404), not a public member of required orgs, or lacks
     write permission — **deterministic failure**.
   - `Valid`: Passed all checks.
   - Transient errors (rate limits, network, auth) are not retried — the audit fails
     immediately.

## Safety Threshold

If more than **5 invalid owners** are detected in a single pass, the rule throws an exception
and terminates the audit. This protects against a systemic error (e.g., GitHub API returning
unexpected results) causing mass relationship deletion.

The `--force` flag overrides this threshold and allows `--fix` to proceed regardless of how
many invalid owners are found.

**Without `--fix`**: The threshold does not apply — all violations are always reported so
humans can review the full list.

**With `--fix` and no `--force`**: If >5 invalid owners are detected, log all of them, then
throw: `"AUD-OWN-001: {count} invalid owners detected (threshold: 5). Use --force to
override."` No fixes are applied.

**With `--fix --force`**: All invalid owners are fixed regardless of count.

## Fix (`--fix`)
- For each `Invalid` owner:
  - Query all Label Owner and Package work items that have a `Related` link to this Owner.
  - Remove the `Related` relation from each linked work item (via idempotent wrapper).
  - Log each removal: `"Removed invalid owner '{alias}' from {workItemType} #{id}"`.
- The rule harness refreshes affected work items in `AuditContext` after all fixes are applied.
- Note: The Owner work item itself is NOT deleted—only its relations are severed.

## Affected Linter Rules
Consolidates: OWN-003 (public Azure member), OWN-005 (invalid user).

## Dependencies
- `ICodeownersValidatorHelper` (existing, with audit-specific overload that throws transient exceptions)
- Idempotent audit wrapper around `RemoveWorkItemRelationAsync` (treat already-absent as success)
- Rule harness handles `AuditContext` refresh after fixes

## Rate Limiting
Owner validation hits GitHub API. If rate limited, the audit fails immediately. Re-running
after the rate limit resets is safe — the audit is idempotent.
