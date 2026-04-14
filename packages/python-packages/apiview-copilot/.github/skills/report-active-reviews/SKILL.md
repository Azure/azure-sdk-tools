---
name: report-active-reviews
description: "Query active APIView reviews for a language and date range. Use for: active reviews, list reviews, show reviews, reviews for March, what reviews happened, which packages were reviewed, active reviews for Python."
argument-hint: "Language and month name (e.g. 'Python for March') or date range"
---

# Report Active Reviews

## When to Use
- Listing which API reviews were active during a time period
- Checking which packages were reviewed for a specific language
- Verifying which revisions were approved vs unapproved
- Investigating review activity for a language

## Defaults

Unless the user says otherwise, always apply these defaults:
- **Environment**: `production`
- **Summary**: Include `--summary` for a quick overview (skip if user asks for full details or JSON)
- **Approved only**: Do NOT pass `--approved-only` unless the user explicitly asks

## Date Resolution

The user will typically specify a calendar month by name (e.g. "March", "January 2025"). Resolve to the full month date range:

| User says | start_date | end_date |
|-----------|-----------|----------|
| "March" (current year) | `YYYY-03-01` | `YYYY-03-31` |
| "January 2025" | `2025-01-01` | `2025-01-31` |
| "March 1 to March 15" | `YYYY-03-01` | `YYYY-03-15` |

When only a month name is given without a year, use the current year. Be careful with month lengths (28/29/30/31 days).

## Language

The `--language` flag is **optional**. If omitted, the command returns results for all languages. When a language is provided, results are filtered to that language only.

## Running the Command

### Step 1: Run the Command

Show the resolved command and run it immediately in a **foreground terminal** with a **120-second timeout** (`timeout: 120000`). Always redirect output to a file so follow-up questions can be answered without re-querying.

**Full terminal command** (cleanup + run):
```powershell
New-Item -ItemType Directory -Path output -Force | Out-Null; if (Test-Path output/active_reviews_output.txt) { Remove-Item output/active_reviews_output.txt }; python cli.py report active-reviews -s <start_date> -e <end_date> -l <language> --summary 2>&1 | Out-File -Encoding UTF8 output/active_reviews_output.txt
```

After the command completes, **read the output file** with `read_file` to get the results. Summarize the findings for the user.

For **JSON mode** (no `--summary`), use `.json` extension and do NOT use `2>&1`:
```powershell
New-Item -ItemType Directory -Path output -Force | Out-Null; if (Test-Path output/active_reviews_output.json) { Remove-Item output/active_reviews_output.json }; python cli.py report active-reviews -s <start_date> -e <end_date> -l <language> | Out-File -Encoding UTF8 output/active_reviews_output.json
```

### Step 2: Answer Follow-up Questions

For follow-up questions about the same data (filtering, counting, searching), **read the output file** with `read_file` instead of re-running the command. The file is at `output/active_reviews_output.txt` (summary) or `output/active_reviews_output.json` (JSON).

### Examples

```powershell
# Python reviews for March 2025 (summary)
python cli.py report active-reviews -s 2025-03-01 -e 2025-03-31 -l python --summary

# C# reviews, approved only
python cli.py report active-reviews -s 2025-03-01 -e 2025-03-31 -l dotnet --approved-only --summary

# Full JSON output for Java
python cli.py report active-reviews -s 2025-03-01 -e 2025-03-31 -l java

# Staging environment
python cli.py report active-reviews -s 2025-03-01 -e 2025-03-31 -l python --summary --environment staging
```

## Available Flags

| Flag | Type | Default | Description |
|------|------|---------|-------------|
| `--start-date` / `-s` | string | required | Start date (`YYYY-MM-DD`) |
| `--end-date` / `-e` | string | required | End date (`YYYY-MM-DD`) |
| `--language` / `-l` | string | `None` | Language to filter by (e.g., `python`, `Go`, `C#`). If omitted, returns all languages. |
| `--environment` | string | `production` | `production` or `staging` |
| `--summary` | flag | off | Table format instead of JSON (package, version, status, copilot) |
| `--approved-only` | flag | off | Show only approved revisions |

## Output Formats

- **Summary mode** (`--summary`): Formatted table with PACKAGE, VERSION, STATUS (APPROVED/unapproved), COPILOT (YES/no), TYPE, APPROVED timestamp.
- **JSON mode** (default): Array of objects with `review_id`, `name`, and `revisions` list.

## Gotchas

- **Use `python cli.py` not `.\avc`**: The `avc.bat` script may resolve to system Python.
- **Month end dates**: February has 28/29 days, April/June/Sept/Nov have 30 days.
