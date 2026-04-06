---
name: audit-filters
description: "Analyze feedback and memories to suggest filter.yaml additions, then open a PR. Use for: audit filters, analyze feedback for filters, suggest filters, update filters, filter additions, feedback analysis, bad comments analysis, add filter rules, filter PR."
argument-hint: "Language and month (e.g. 'Java for March') or language and date range"
---

# Audit Filters

## When to Use
- Analyzing negative feedback (downvotes, deletions) on AI comments to find recurring patterns
- Suggesting new `filter.yaml` exception rules for a language based on feedback themes
- Opening a PR with proposed filter additions after user confirmation

## Overview

This is a multi-phase workflow:
1. **Collect** — Pull feedback and memories for the specified language and time period
2. **Analyze** — Categorize feedback by reason, theme, and `IsGeneric` status; identify recurring bad-comment patterns
3. **Recommend** — Propose new numbered `DO NOT ...` lines for `metadata/{lang}/filter.yaml`
4. **Confirm** — Present recommendations and ask the user whether to proceed
5. **PR** — Create a branch, apply changes, commit, push, and open a pull request

## Defaults

Unless the user says otherwise, always apply these defaults:
- **Environment**: `production`
- **Feedback types**: Focus on `bad` and `delete` feedback (exclude `good`)
- **Format**: JSON (redirect to file)

## Language Resolution

Map the user's language name to the metadata directory name:

| User says | `{lang}` directory | `--language` flag value |
|-----------|---------------------|------------------------|
| Java | `java` | `java` |
| C# / .NET / dotnet | `dotnet` | `dotnet` |
| Python | `python` | `python` |
| TypeScript / JavaScript | `typescript` | `typescript` |
| Go / Golang | `golang` | `golang` |
| Swift / iOS | `ios` | `ios` |
| Android | `android` | `android` |
| C / C++ / Clang | `clang` | `clang` |
| Rust | `rust` | `rust` |

## Date Resolution

The user will typically specify a calendar month by name (e.g. "March", "January 2025"). Resolve to the full month date range:

| User says | start_date | end_date |
|-----------|-----------|----------|
| "March" (current year) | `YYYY-03-01` | `YYYY-03-31` |
| "January 2025" | `2025-01-01` | `2025-01-31` |
| "March 1 to March 15" | `YYYY-03-01` | `YYYY-03-15` |

When only a month name is given without a year, use the current year. Be careful with month lengths (28/29/30/31 days).

---

## Phase 1: Collect Data

Run both commands sequentially in the **same foreground terminal**. Use a **120-second timeout** for each.

### Step 1a: Pull feedback

```powershell
New-Item -ItemType Directory -Path output -Force | Out-Null; if (Test-Path output/feedback_output.json) { Remove-Item output/feedback_output.json }; python cli.py report feedback -s <start_date> -e <end_date> -l <language> --exclude good | Out-File -Encoding UTF8 output/feedback_output.json
```

### Step 1b: Pull memories

```powershell
if (Test-Path output/memory_output.json) { Remove-Item output/memory_output.json }; python cli.py report memory -s <start_date> -e <end_date> -l <language> | Out-File -Encoding UTF8 output/memory_output.json
```

After both commands complete, **read both output files** with `read_file`.

---

## Phase 2: Analyze

Read the current filter file at `metadata/{lang}/filter.yaml` so you know what rules already exist.

Then analyze the collected feedback and memories. Produce a summary organized as follows:

### Analysis Structure

**By Feedback Reason** — Group comments by their `Feedback[].Reasons` values (e.g. `AcceptedRenderingChoice`, `FactuallyIncorrect`, `RenderingBug`, `NotRelevant`, `TooNitpicky`, `Other`). For each reason, count occurrences and list representative `CommentText` excerpts.

**By Theme** — Identify recurring themes across the bad comments. A theme is a pattern you can describe in one sentence (e.g. "commenting on interface method implementations", "suggesting consolidating overloads"). Include the count of comments matching each theme.

**By IsGeneric Status** — Report how many bad comments had `IsGeneric: true` vs `false`. Generic comments are not tied to a specific guideline and are more likely candidates for filter rules.

**By Submitter** — Note which users (`Feedback[].SubmittedBy`) provided the most feedback. The most significant contributor will be used as the PR assignee.

**Cross-reference with Memories** — Check if any memories (especially those with `is_exception: true`) suggest filter rules that are not yet in `filter.yaml`.

Present this analysis to the user in a clear summary table or grouped list.

---

## Phase 3: Recommend Filter Additions

Based on the analysis, propose specific new lines to add to `metadata/{lang}/filter.yaml`. Each recommendation must:

1. Follow the existing format: `  N. DO NOT <description>`
2. Be numbered sequentially after the last existing rule
3. **Not duplicate an existing rule** — Before proposing a rule, compare it against every existing rule in the current `filter.yaml`. If an existing rule already covers the same behavior (even with different wording), do NOT propose it again. Explain in the analysis that the theme was already covered and cite the existing rule number.
4. Be supported by at least 2 feedback items or 1 memory with `is_exception: true`
5. Be phrased as a clear, actionable instruction the LLM can follow

Present the recommendations in a numbered list, each with:
- The proposed rule text
- The evidence (feedback count, representative comment texts, memory references)
- Whether the pattern was `IsGeneric` or guideline-linked

Example recommendation format:

> **Proposed rule 8**: `DO NOT comment on explicit interface implementations for serialization (IJsonModel, IPersistableModel)`
> - Evidence: 4 bad comments with reason `FactuallyIncorrect`, all `IsGeneric: true`
> - Example: *"Interface method implementation for AzureAISearchIndex is unexpected here"*

---

## Phase 4: Confirm

Use the `vscode_askQuestions` tool to present the user with a selection:

- **header**: `"Confirm filter PR"`
- **question**: `"Here are the proposed filter additions for {lang}. Should I create a PR with these changes?"`
- **options**:
  - `"Yes, all of them"` (recommended)
  - `"Yes, but only specific ones (let me pick)"`
  - `"No, skip the PR"`

If the user selects specific ones, note which rule numbers to include.

If the user says no, stop here.

---

## Phase 5: Create PR

### Step 5a: Determine the current user's GitHub handle

```powershell
gh api user --jq .login
```

Store this as `{current_user}`.

### Step 5b: Determine the PR reviewer

The reviewer should be the feedback submitter (`Feedback[].SubmittedBy`) who appears most frequently in the bad/deleted comments that led to the filter additions. If there is a tie, pick the one whose feedback is most relevant to the proposed rules.

Store this as `{top_submitter}`.

### Step 5c: Create a branch

Generate a branch name: `avc/update-{lang}-filter-{YYYYMMDD}` (using today's date).

The branch MUST be based on `origin/main` so the PR contains **only** the filter.yaml change. Do NOT branch from the current working branch — it may contain unrelated changes.

```powershell
git fetch origin main; git checkout -b avc/update-{lang}-filter-{YYYYMMDD} origin/main
```

If `origin/main` fails (e.g. `main` is in another worktree), use `FETCH_HEAD`:

```powershell
git fetch origin main; git checkout -b avc/update-{lang}-filter-{YYYYMMDD} FETCH_HEAD
```

### Step 5d: Apply filter changes

Edit `metadata/{lang}/filter.yaml` to append the confirmed rules. Use sequential numbering continuing from the last existing rule. Maintain the existing indentation (2-space indent under the YAML block scalar `exceptions: |`).

### Step 5e: Commit and push

Stage **only** the filter file — never use `git add .` or `git add -A`:

```powershell
git add metadata/{lang}/filter.yaml; git commit -m "[AVC] Update {lang} filter based on {month} {year} feedback"
```

Before pushing, **verify** the commit contains exactly 1 file:

```powershell
git diff --stat origin/main..HEAD
```

If more than 1 file appears, STOP and fix the branch before pushing. Only after confirming 1 file changed:

```powershell
git push origin avc/update-{lang}-filter-{YYYYMMDD}
```

### Step 5f: Open the PR

```powershell
gh pr create --repo Azure/azure-sdk-tools --title "[AVC] Update {lang} filter" --body "Filter additions based on a review of feedback collected during {timespan}." --label "APIView Copilot" --assignee {current_user} --reviewer {top_submitter} --base main
```

Where:
- `{lang}` — The language name (e.g. `java`, `dotnet`, `python`)
- `{timespan}` — The human-readable date range (e.g. "March 2026", "January 1 – January 15, 2025")
- `{current_user}` — The GitHub handle of the person running the skill (PR **assignee**)
- `{top_submitter}` — The GitHub handle of the most significant feedback contributor (PR **reviewer**)

After the PR is created, report the PR URL to the user.

---

## Gotchas

- **Use `python cli.py` not `.\avc`**: The `avc.bat` script may resolve to system Python.
- **Do NOT use `2>&1`**: Merges stderr into stdout, corrupting JSON. Only redirect stdout.
- **Do NOT use `>`**: Produces UTF-16 in PowerShell 5.1. Use `| Out-File -Encoding UTF8`.
- **Month end dates**: February has 28/29 days, April/June/Sept/Nov have 30 days.
- **Output can be large**: Redirect to file and use `read_file` rather than relying on terminal output.
- **Existing rules**: Always read the current `filter.yaml` before proposing additions to avoid duplicates.
- **Branch conflicts**: If the branch already exists, append a short suffix (e.g. `-2`).
- **Branch base**: ALWAYS branch from `origin/main`, never from the current working branch. The working branch may contain dozens of unrelated changes that will pollute the PR. Verify with `git diff --stat origin/main..HEAD` before pushing.
- **Label must exist**: The `APIView Copilot` label must already exist in the repo. If `gh pr create` fails on the label, omit `--label` and add the label manually after creation.
- **Assignee validation**: GitHub usernames from APIView feedback may not exactly match GitHub handles. If `gh pr create` fails on an assignee, omit that assignee and note it in the PR body instead.
