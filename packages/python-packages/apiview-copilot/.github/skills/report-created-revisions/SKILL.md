---
name: report-created-revisions
description: "List APIView revisions created in a date range, broken out by language and type. Use for: created revisions, revision counts, how many revisions, revisions for March, revision breakdown, revision types, automatic vs manual revisions, PR revisions, pull request revisions."
argument-hint: "Month name (e.g. 'March') or date range, optional --exclude languages"
---

# Report Created Revisions

## When to Use
- Counting how many APIView revisions were created in a time period
- Breaking down revision counts by language and type (Automatic, Manual, PullRequest)
- Comparing revision volume across languages
- Filtering out specific languages from the report

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

## Running the Command

### Step 1: Run the Command

Show the resolved command and run it immediately in a **foreground terminal** with a **120-second timeout** (`timeout: 120000`). Always redirect output to a file so follow-up questions can be answered without re-querying.

**Full terminal command** (cleanup + run):
```powershell
New-Item -ItemType Directory -Path output -Force | Out-Null; if (Test-Path output/created_revisions_output.txt) { Remove-Item output/created_revisions_output.txt }; python cli.py apiview list-created-revisions -s <start_date> -e <end_date> 2>&1 | Out-File -Encoding UTF8 output/created_revisions_output.txt
```

With `--exclude`:
```powershell
New-Item -ItemType Directory -Path output -Force | Out-Null; if (Test-Path output/created_revisions_output.txt) { Remove-Item output/created_revisions_output.txt }; python cli.py apiview list-created-revisions -s <start_date> -e <end_date> --exclude Java Go 2>&1 | Out-File -Encoding UTF8 output/created_revisions_output.txt
```

After the command completes, **read the output file** with `read_file` to get the results. Summarize the findings for the user.

### Step 2: Answer Follow-up Questions

For follow-up questions about the same data, **read the output file** at `output/created_revisions_output.txt` instead of re-running the command.

### Examples

```powershell
# All languages for March 2026
python cli.py apiview list-created-revisions -s 2026-03-01 -e 2026-03-31

# Exclude Java and Go
python cli.py apiview list-created-revisions -s 2026-03-01 -e 2026-03-31 --exclude Java Go

# Staging environment
python cli.py apiview list-created-revisions -s 2026-03-01 -e 2026-03-31 --environment staging
```

## Available Flags

| Flag | Type | Default | Description |
|------|------|---------|-------------|
| `--start-date` / `-s` | string | required | Start date (`YYYY-MM-DD`) |
| `--end-date` / `-e` | string | required | End date (`YYYY-MM-DD`) |
| `--environment` | string | `production` | `production` or `staging` |
| `--exclude` | string(s) | `None` | Languages to exclude (e.g., `--exclude Java Go`) |

## Output Format

Prints a table with one row per language and one column per revision type (Automatic, Manual, PullRequest). Each type cell shows the count and its percentage of that column's total. The Total column shows the language's count and its percentage of the grand total.

```
Language    Automatic      Manual    PullRequest   Total
--------------------------------------------------------
C#          3292 (36%)     4 (8%)    201 (4%)      3497 (25%)
Java        3451 (38%)     26 (53%)  587 (12%)     4064 (29%)
Python      601 (7%)       4 (8%)    635 (13%)     1240 (9%)
...
--------------------------------------------------------
TOTAL       9102 (64%)     49 (0%)   5066 (36%)    14217
```

## Gotchas

- **Use `python cli.py` not `.\avc`**: The `avc.bat` script may resolve to system Python.
- **Month end dates**: February has 28/29 days, April/June/Sept/Nov have 30 days.
- **Language names for `--exclude`**: Use pretty names as shown in output (e.g., `Java`, `C#`, `Python`, `Go`, `JavaScript`, `C++`, `Rust`, `Swift`).
