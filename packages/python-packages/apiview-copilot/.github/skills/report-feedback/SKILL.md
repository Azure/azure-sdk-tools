---
name: report-feedback
description: "Retrieve negative feedback on AI-generated APIView comments. Use for: feedback report, comment feedback, show feedback, feedback for March, what feedback, downvotes, deleted comments, bad comments."
argument-hint: "Month name (e.g. 'March') or language + month (e.g. 'Python for March')"
---

# Report Feedback

## When to Use
- Reviewing negative feedback (downvotes, deletions) on AI-generated comments
- Investigating comment quality issues for a specific language
- Understanding why AI comments were marked bad or deleted during a period

## Understanding Feedback

Feedback describes **why a bad AI comment is bad**. Each feedback entry includes a reason (e.g., `FactuallyIncorrect`, `AcceptedSDKPattern`, `RenderingBug`) and an optional free-text comment. There are no upvotes in the feedback data — upvotes on comments exist in APIView but are not surfaced through this report. All feedback returned by this command is negative.

**Do NOT** state that feedback is "100% bad" or highlight that all feedback is negative in your summary. This is always the case by design — the report only surfaces negative feedback. Treat it as a given and focus the summary on the count, reasons, themes, and actionable insights.

## Defaults

Unless the user says otherwise, always apply these defaults:
- **Environment**: `production`
- **Language**: All languages (do not pass `--language` unless user specifies one)
- **Exclude**: Do not pass `--exclude` unless user asks to filter out certain feedback types
- **Include implicit**: Always pass `--include-implicit` by default. Only omit it if the user explicitly asks to exclude implicit bad comments.
- **Format**: JSON (do not pass `--format`)

## Implicit Bad Comments

The `--include-implicit` flag also returns **implicit bad** comments: AI comments on approved revisions that were never upvoted, downvoted, or resolved. The inference is that the reviewer ignored them and approved anyway, suggesting they were unhelpful.

This skill always passes `--include-implicit` (the CLI flag defaults to off, but the skill includes it for completeness). It has a weaker signal than explicit feedback because there is no reason or confirmation — just silence. Only omit `--include-implicit` if the user explicitly asks to exclude them (e.g., "only explicit feedback", "exclude implicit bad").

The output will contain items with `"FeedbackTypes": ["implicit_bad"]`.

### Summarizing Implicit Bad

When presenting results, **break out implicit bad themes separately** from explicit feedback:

1. **Explicit feedback** — Summarize count, breakdown by reason, and themes for items that have explicit feedback types (e.g., `bad`, `delete`).
2. **Implicit bad** — Summarize separately: count, common comment topics/patterns, and any notable themes. Note that these lack a reason — group them by the comment content or guideline referenced instead.

This separation helps the user understand the strength of signal behind each theme.

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

Show the resolved command and run it immediately in a **foreground terminal** with a **120-second timeout** (`timeout: 120000`). Redirect to a file since feedback output can be very large.

**Full terminal command** (cleanup + run):
```powershell
New-Item -ItemType Directory -Path output -Force | Out-Null; if (Test-Path output/feedback_output.json) { Remove-Item output/feedback_output.json }; python cli.py report feedback -s <start_date> -e <end_date> --include-implicit | Out-File -Encoding UTF8 output/feedback_output.json
```

After the command completes, **read the output file** with `read_file` to get the JSON results. Summarize the findings for the user (total count, breakdown by feedback reason, common themes, etc.).

### Step 2: Answer Follow-up Questions

For follow-up questions about the same data (filtering, counting, searching), **read the output file** with `read_file` instead of re-running the command. The file is at `output/feedback_output.json`.

### Examples

```powershell
# All feedback for March 2025 (implicit bad included by default)
python cli.py report feedback -s 2025-03-01 -e 2025-03-31 --include-implicit

# Python feedback only
python cli.py report feedback -s 2025-03-01 -e 2025-03-31 -l python --include-implicit

# Exclude implicit bad (only explicit feedback)
python cli.py report feedback -s 2025-03-01 -e 2025-03-31

# Exclude good feedback (show only bad and deleted)
python cli.py report feedback -s 2025-03-01 -e 2025-03-31 --include-implicit --exclude good

# YAML output
python cli.py report feedback -s 2025-03-01 -e 2025-03-31 --include-implicit --format yaml

# Staging environment
python cli.py report feedback -s 2025-03-01 -e 2025-03-31 --include-implicit --environment staging
```

## Available Flags

| Flag | Type | Default | Description |
|------|------|---------|-------------|
| `--start-date` / `-s` | string | required | Start date (`YYYY-MM-DD`) |
| `--end-date` / `-e` | string | required | End date (`YYYY-MM-DD`) |
| `--language` / `-l` | string | all | Language to filter by (e.g., `python`, `Go`, `C#`) |
| `--environment` | string | `production` | `production` or `staging` |
| `--exclude` | list | none | Feedback types to exclude: `good`, `bad`, `delete`, `implicit_bad` |
| `--include-implicit` | flag | off | Include implicit bad comments (unresolved, unvoted on approved revisions) |
| `--format` / `-f` | string | `json` | Output format: `json` or `yaml` |

## Gotchas

- **Output can be large**: Redirect to file and use `read_file` rather than relying on terminal output.
- **Date range filters by feedback submission time**: Not by when the comment was created. A comment created in January but downvoted in March will appear in March's feedback report.
- **Use `python cli.py` not `.\avc`**: The `avc.bat` script may resolve to system Python.
- **Do NOT use `2>&1`**: Merges stderr into stdout, corrupting JSON. Only redirect stdout.
- **Do NOT use `>`**: Produces UTF-16 in PowerShell 5.1. Use `| Out-File -Encoding UTF8`.
- **Month end dates**: February has 28/29 days, April/June/Sept/Nov have 30 days.
