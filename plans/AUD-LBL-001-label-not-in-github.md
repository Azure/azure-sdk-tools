# AUD-LBL-001: Label Not In GitHub

## Summary
Validate that Label work items correspond to labels that exist in the GitHub repos where
they are used.

## Criteria
1. Fetch all Label work items.
2. For each label, determine the repos where it is used by looking at the related Label Owner
   and Package work items that reference it:
   - Label Owners have `Custom.Repository` — use that repo directly.
   - Packages have `Custom.Language` — map to the corresponding SDK repo.
3. For each (label, repo) pair, check if the label exists in that GitHub repo using
   `IGitHubService.GetRepoLabels`.
4. Flag labels that don't exist in any repo where they are referenced.

## Fix (`--fix`)
**Report only.** Missing labels may be intentional (new services, pending creation) or may
indicate stale data. Human review is needed. The existing `github-label create` command
can be used to create missing labels.

## Affected Linter Rules
Maps to: LBL-004 (repo label must exist).

## Dependencies
- `IGitHubService` for repo label lookup — **must add a `GetRepoLabels` method** to
  `IGitHubService` that returns the set of labels for a given repo. This method does not
  exist today and must be implemented as part of this rule.
- `github-label sync-ado` already handles label synchronization—this rule detects drift.
