---
name: update-guidelines
description: "Ingest guideline changes from the azure-sdk repo into the knowledge base. Use for: update guidelines, ingest guidelines, sync guidelines, guideline changes, kb update, update KB, guideline ingestion, sync KB, guideline PR, guideline diff."
argument-hint: "PR link, two SHAs, or two dates (e.g. 'PR #1234', 'abc123..def456', 'April 1 to April 30')"
---

# Update Guidelines

## When to Use
- Syncing guideline changes from the [Azure/azure-sdk](https://github.com/Azure/azure-sdk) repo into the Copilot knowledge base
- Ingesting a specific PR's guideline changes
- Syncing guideline changes between two dates or two commit SHAs

## Overview

The `avc db ingest-guidelines` command detects changes in the azure-sdk repo's guideline markdown files and syncs them to Cosmos DB. It compares a **base SHA** against a **target SHA** to find changed files, parses them, and upserts/deletes guidelines, examples, and memories accordingly.

This skill supports three input scenarios for resolving the base and target SHAs:

1. **PR link** — Extract SHAs from a GitHub pull request
2. **Explicit SHAs** — Use the SHAs directly
3. **Date range** — Find the closest commits on `main` to the given dates

**IMPORTANT:** Always run a **dry-run first** and confirm with the user before applying changes.

## Environment

The user MUST specify which environment to update: **staging** or **production**. If not specified, **ask the user** — do NOT assume. This determines which Cosmos DB and App Configuration instance is modified.

Always pass `--environment <env>` to the CLI commands.

## Language Filter

The user may optionally specify one or more languages to narrow the ingestion scope. If languages are specified, only guideline files for those languages (plus cross-language "general" guidelines) are processed.

Pass `--language <lang1> --language <lang2>` (or `-l <lang1> -l <lang2>`) to the CLI commands. Valid language names: `python`, `java`, `dotnet`, `typescript`, `golang`, `cpp`, `rust`, `ios`, `android`, `clang`. Case-insensitive aliases like `C#`, `Go`, `Swift` are also accepted.

If the user does not mention specific languages, omit the flag to process all languages.

---

## Scenario 1: PR Link

The user provides a GitHub PR link (e.g. `https://github.com/Azure/azure-sdk/pull/1234`).

### Step 1a: Extract the PR number

Parse the PR number from the URL.

### Step 1b: Fetch PR details from GitHub API

Run this command to get the base and merge commit SHAs:

```powershell
Invoke-RestMethod -Uri "https://api.github.com/repos/Azure/azure-sdk/pulls/<PR_NUMBER>" -Headers @{ "User-Agent" = "apiview-copilot" } | Select-Object -Property @{N='base_sha';E={$_.base.sha}}, @{N='merge_commit_sha';E={$_.merge_commit_sha}}, @{N='title';E={$_.title}}, @{N='state';E={$_.state}}, @{N='merged';E={$_.merged}} | Format-List
```

- Use `base.sha` as the **base SHA**
- Use `merge_commit_sha` as the **target SHA**
- If `merged` is `false`, warn the user that the PR is not merged and the merge commit SHA may change

### Step 1c: Confirm with user

Show the user:
- PR title and state
- Base SHA (first 8 chars)
- Target SHA (first 8 chars)
- Environment (staging or production)

Ask for confirmation before proceeding.

Then go to **Phase 2: Dry Run**.

---

## Scenario 2: Explicit SHAs

The user provides two commit SHAs directly.

Confirm with the user:
- Base SHA (first 8 chars)
- Target SHA (first 8 chars)
- Environment (staging or production)

Then go to **Phase 2: Dry Run**.

---

## Scenario 3: Date Range

The user provides two dates (e.g. "April 1 to April 30", "March 15 and March 20").

### Step 3a: Resolve dates to SHAs

For each date, find the closest commit on `main` of the Azure/azure-sdk repo.

Use the GitHub Commits API with the `until` parameter to find the most recent commit on or before the given date:

**For the base date:**
```powershell
(Invoke-RestMethod -Uri "https://api.github.com/repos/Azure/azure-sdk/commits?sha=main&until=<BASE_DATE>T23:59:59Z&per_page=1" -Headers @{ "User-Agent" = "apiview-copilot" })[0] | Select-Object -Property @{N='sha';E={$_.sha}}, @{N='date';E={$_.commit.committer.date}}, @{N='message';E={$_.commit.message.Split("`n")[0]}} | Format-List
```

**For the target date:**
```powershell
(Invoke-RestMethod -Uri "https://api.github.com/repos/Azure/azure-sdk/commits?sha=main&until=<TARGET_DATE>T23:59:59Z&per_page=1" -Headers @{ "User-Agent" = "apiview-copilot" })[0] | Select-Object -Property @{N='sha';E={$_.sha}}, @{N='date';E={$_.commit.committer.date}}, @{N='message';E={$_.commit.message.Split("`n")[0]}} | Format-List
```

Replace `<BASE_DATE>` and `<TARGET_DATE>` with ISO dates (e.g. `2025-04-15`).

### Step 3b: Confirm with user

The resolved commit dates may not exactly match the user's requested dates. **Always show the user what was resolved and ask for confirmation**, especially if the commit date differs from the requested date by more than a day.

Show:
- Requested base date → Resolved commit date and SHA (first 8 chars) and commit message
- Requested target date → Resolved commit date and SHA (first 8 chars) and commit message
- Environment (staging or production)

Ask: "These are the closest commits to your requested dates. Proceed with dry run?"

Then go to **Phase 2: Dry Run**.

---

## Phase 2: Dry Run

**Always run a dry-run first.** Use a **300-second timeout** (the command can be slow due to LLM enrichment).

```powershell
python cli.py db ingest-guidelines --environment <ENV> --base-sha <BASE_SHA> --target-sha <TARGET_SHA> --dry-run --details [--language <LANG1> --language <LANG2>]
```

Include `--language` flags only if the user specified languages to filter.

### Interpreting results

Read the terminal output. The command prints:
- **Guidelines**: N to create, N to update, N to delete, N unchanged
- **Examples**: N to create, N to update, N to delete, N unchanged
- **Memories**: N to absorb, N to retain
- **Errors**: any errors encountered

With `--details`, the JSON output includes before/after content for each change.

### Present to user

Summarize what the dry run found:
- How many guidelines will be created/updated/deleted
- How many examples will be created/updated/deleted
- How many memories will be absorbed
- Any errors
- Remind them which environment this targets

Ask: "Ready to apply these changes to **{environment}**?"

---

## Phase 3: Apply

Only after the user confirms the dry-run results, run the actual ingestion:

```powershell
python cli.py db ingest-guidelines --environment <ENV> --base-sha <BASE_SHA> --target-sha <TARGET_SHA> [--language <LANG1> --language <LANG2>]
```

Use a **300-second timeout**. Include the same `--language` flags used in the dry run.

After completion, report the final counts to the user.

---

## Gotchas

- **Always dry-run first.** Never skip the dry run. The ingestion modifies Cosmos DB and App Configuration.
- **Environment matters.** Staging and production have separate Cosmos DB instances. Double-check with the user.
- **Large diffs are slow.** If many files changed, the LLM enrichment step can take several minutes. Use `--details` only on dry runs to inspect changes; omit it on the real run to save time.
- **Language filter.** Use `--language` to scope to specific languages when debugging or testing. The filter also includes cross-language ("general") guidelines automatically.
- **Merged PRs only.** If a PR is not yet merged, the merge commit SHA is provisional and may change. Warn the user.
