---
name: report-memory
description: "Retrieve knowledge base memories created in a date range. Use for: memory report, show memories, new memories, memories for March, what memories were created, knowledge base additions, KB entries."
argument-hint: "Month name (e.g. 'March') or language + month (e.g. 'Python for March')"
---

# Report Memory

## When to Use
- Reviewing what knowledge base memories were created during a period
- Auditing KB growth for a specific language
- Checking what the system has learned from resolved threads

## Defaults

Unless the user says otherwise, always apply these defaults:
- **Environment**: `production`
- **Language**: All languages (do not pass `--language` unless user specifies one)
- **Format**: JSON (do not pass `--format`)

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

Show the resolved command and run it immediately in a **foreground terminal** with a **60-second timeout** (`timeout: 60000`). Redirect to a file since output can be large.

**Full terminal command** (cleanup + run):
```powershell
New-Item -ItemType Directory -Path output -Force | Out-Null; if (Test-Path output/memory_output.json) { Remove-Item output/memory_output.json }; python cli.py report memory -s <start_date> -e <end_date> | Out-File -Encoding UTF8 output/memory_output.json
```

After the command completes, **read the output file** with `read_file` to get the JSON results. Summarize the findings for the user (total count, breakdown by language, etc.).

### Step 2: Answer Follow-up Questions

For follow-up questions about the same data (filtering, counting, searching), **read the output file** with `read_file` instead of re-running the command. The file is at `output/memory_output.json`.

### Examples

```powershell
# All memories for March 2025
python cli.py report memory -s 2025-03-01 -e 2025-03-31

# Python memories only
python cli.py report memory -s 2025-03-01 -e 2025-03-31 -l python

# YAML output
python cli.py report memory -s 2025-03-01 -e 2025-03-31 --format yaml

# Staging environment
python cli.py report memory -s 2025-03-01 -e 2025-03-31 --environment staging
```

## Available Flags

| Flag | Type | Default | Description |
|------|------|---------|-------------|
| `--start-date` / `-s` | string | required | Start date (`YYYY-MM-DD`) |
| `--end-date` / `-e` | string | required | End date (`YYYY-MM-DD`) |
| `--language` / `-l` | string | all | Language to filter by (e.g., `python`, `csharp`, `C#`) |
| `--environment` | string | `production` | `production` or `staging` |
| `--format` / `-f` | string | `json` | Output format: `json` or `yaml` |

## Output

Each memory object includes:
- `id` — Cosmos DB document ID
- `language` — Language the memory applies to
- `created_at` — Human-readable timestamp (converted from Cosmos `_ts`)
- Memory content fields (rule, context, etc.)

## Gotchas

- **Date filtering uses Cosmos `_ts`**: Filters by when the document was created/modified in Cosmos DB, not by any explicit date field in the memory content.
- **Output can be large**: Redirect to file and use `read_file` rather than relying on terminal output.
- **Use `python cli.py` not `.\avc`**: The `avc.bat` script may resolve to system Python.
- **Do NOT use `2>&1`**: Merges stderr into stdout, corrupting JSON. Only redirect stdout.
- **Do NOT use `>`**: Produces UTF-16 in PowerShell 5.1. Use `| Out-File -Encoding UTF8`.
- **Month end dates**: February has 28/29 days, April/June/Sept/Nov have 30 days.
