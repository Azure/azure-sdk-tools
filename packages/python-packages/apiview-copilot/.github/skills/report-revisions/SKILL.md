---
name: report-revisions
description: "List APIView revisions created or actually opened/viewed in a date range, broken out by language and type. Use for: created revisions, opened revisions, viewed revisions, revision counts, how many revisions, revisions for March, revision breakdown, revision types, automatic vs manual revisions, PR revisions, pull request revisions, page views, actually viewed, who opened reviews, opened revision breakdown."
argument-hint: "Month name (e.g. 'March') or date range, optional --exclude languages"
---

# Report Revisions

Two commands for counting APIView revisions by language and type:

| Command | What it counts | Data source |
|---------|---------------|-------------|
| `list-created-revisions` | Revisions **created** in the date window | Cosmos DB |
| `list-opened-revisions` | Revisions actually **opened/viewed** by users | Application Insights + Cosmos DB |

Choose the right command based on the user's intent. If they ask about "created" or "how many revisions", use `list-created-revisions`. If they ask about "opened", "viewed", "actually looked at", or "page views", use `list-opened-revisions`.

By default, `list-opened-revisions` counts all revisions belonging to viewed reviews regardless of when those revisions were created. If the user wants to limit to revisions **created** within the window (e.g. to pair with `list-created-revisions`), add `--created-in-window`.

## Defaults

Unless the user says otherwise, always apply these defaults:
- **Environment**: `production`
- **Exclude**: Do NOT pass `--exclude` unless the user explicitly asks to omit languages

## Date Resolution

The user will typically specify a calendar month by name (e.g. "March", "January 2025"). Resolve to the full month date range:

| User says | start_date | end_date |
|-----------|-----------|----------|
| "March" (current year) | `YYYY-03-01` | `YYYY-03-31` |
| "January 2025" | `2025-01-01` | `2025-01-31` |
| "March 1 to March 15" | `YYYY-03-01` | `YYYY-03-15` |

When only a month name is given without a year, use the current year. Be careful with month lengths (28/29/30/31 days).

> **Note:** `list-opened-revisions` queries Application Insights, which has a default retention of 90 days. Queries beyond that window may return incomplete data.

## Running the Command

### Step 1: Run the Command

Show the resolved command and run it immediately in a **foreground terminal** with a **120-second timeout** (`timeout: 120000`). Always redirect output to a file so follow-up questions can be answered without re-querying.

#### Created revisions

```powershell
New-Item -ItemType Directory -Path output -Force | Out-Null; if (Test-Path output/created_revisions_output.txt) { Remove-Item output/created_revisions_output.txt }; python cli.py apiview list-created-revisions -s <start_date> -e <end_date> 2>&1 | Out-File -Encoding UTF8 output/created_revisions_output.txt
```

Output file: `output/created_revisions_output.txt`

#### Opened revisions

```powershell
New-Item -ItemType Directory -Path output -Force | Out-Null; if (Test-Path output/opened_revisions_output.txt) { Remove-Item output/opened_revisions_output.txt }; python cli.py apiview list-opened-revisions -s <start_date> -e <end_date> 2>&1 | Out-File -Encoding UTF8 output/opened_revisions_output.txt
```

Output file: `output/opened_revisions_output.txt`

#### With `--exclude`

Append `--exclude Java Go` (or whatever languages) to either command.

After the command completes, **read the output file** with `read_file` to get the results. Summarize the findings for the user.

### Step 2: Answer Follow-up Questions

For follow-up questions about the same data, **read the output file** instead of re-running the command.

### Examples

```powershell
# Created revisions for March 2026
python cli.py apiview list-created-revisions -s 2026-03-01 -e 2026-03-31

# Opened revisions for March 2026
python cli.py apiview list-opened-revisions -s 2026-03-01 -e 2026-03-31

# Exclude Java and Go
python cli.py apiview list-created-revisions -s 2026-03-01 -e 2026-03-31 --exclude Java Go
python cli.py apiview list-opened-revisions -s 2026-03-01 -e 2026-03-31 --exclude Java Go

# Staging environment
python cli.py apiview list-created-revisions -s 2026-03-01 -e 2026-03-31 --environment staging
```

## Available Flags (same for both commands)

| Flag | Type | Default | Description |
|------|------|---------|-------------|
| `--start-date` / `-s` | string | required | Start date (`YYYY-MM-DD`) |
| `--end-date` / `-e` | string | required | End date (`YYYY-MM-DD`) |
| `--environment` | string | `production` | `production` or `staging` |
| `--exclude` | string(s) | `None` | Languages to exclude (e.g., `--exclude Java Go`) |

## Output Format

Both commands print a table with one row per language and one column per revision type (Automatic, Manual, PullRequest). Each type cell shows the count and its percentage of that column's total. The Total column shows the language's count and its percentage of the grand total.
