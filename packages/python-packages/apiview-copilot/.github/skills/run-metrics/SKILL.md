---
name: run-metrics
description: "Run metrics reports for APIView Copilot. Use for: run metrics, metrics report, generate metrics, monthly metrics, metrics for March, metrics for January, adoption metrics, comment quality, save metrics, metrics charts."
argument-hint: "Month name (e.g. 'March') or date range (e.g. '2025-03-01 to 2025-03-31')"
---

# Run Metrics

## When to Use
- Generating monthly or custom-range metrics reports
- Reviewing adoption rates and comment quality across languages
- Producing charts for metrics visualization
- Saving finalized metrics to the database (only when user explicitly requests)

## Defaults

Unless the user says otherwise, always apply these defaults:
- **Environment**: `production`
- **Charts**: Always include `--charts`
- **Languages**: All languages (do not pass `--exclude`)
- **Save**: Do **NOT** pass `--save` unless the user explicitly asks to save (e.g. "save it", "persist", "write to DB")
- **Format**: JSON output (do not pass `--markdown` unless requested)

## Date Resolution

The user will typically specify a calendar month by name (e.g. "March", "January 2025"). Resolve to the full month date range:

| User says | start_date | end_date |
|-----------|-----------|----------|
| "March" (current year) | `YYYY-03-01` | `YYYY-03-31` |
| "January 2025" | `2025-01-01` | `2025-01-31` |
| "March 1 to March 15" | `YYYY-03-01` | `YYYY-03-15` |
| "2025-06-01 to 2025-06-30" | `2025-06-01` | `2025-06-30` |

When only a month name is given without a year, use the current year. Be careful with month lengths (28/29/30/31 days).

## Running Metrics

Activate the Python environment first, then run via CLI.

### Output File

This command can be long-running (1-3 minutes). **Always redirect output to a file** so results are retrievable after the command completes. Use `scratch/metrics_output.json` as the output file (or `scratch/metrics_output.md` with `--markdown`).

After the command finishes, read the output file to present results to the user.

### Basic monthly report with charts
```bash
python cli.py report metrics -s <start_date> -e <end_date> --charts > scratch/metrics_output.json 2>&1
```

### Examples

```bash
# March 2025, production, all languages, with charts (typical request)
python cli.py report metrics -s 2025-03-01 -e 2025-03-31 --charts > scratch/metrics_output.json 2>&1

# Staging environment
python cli.py report metrics -s 2025-03-01 -e 2025-03-31 --charts --environment staging > scratch/metrics_output.json 2>&1

# Exclude specific languages
python cli.py report metrics -s 2025-03-01 -e 2025-03-31 --charts --exclude Java Golang > scratch/metrics_output.json 2>&1

# Custom date range
python cli.py report metrics -s 2025-03-10 -e 2025-03-20 --charts > scratch/metrics_output.json 2>&1

# Save to database (ONLY when user explicitly requests)
python cli.py report metrics -s 2025-03-01 -e 2025-03-31 --charts --save > scratch/metrics_output.json 2>&1

# Markdown summary
python cli.py report metrics -s 2025-03-01 -e 2025-03-31 --charts --markdown > scratch/metrics_output.md 2>&1
```

## Follow-up: "Save it"

If the user asks to save after a metrics run (e.g. "okay save it", "persist that", "write to DB"), re-run the **same command** with `--save` appended. Keep all other flags identical to the previous run.

## Available Flags

| Flag | Type | Default | Description |
|------|------|---------|-------------|
| `--start-date` / `-s` | string | required | Start date (`YYYY-MM-DD`) |
| `--end-date` / `-e` | string | required | End date (`YYYY-MM-DD`) |
| `--environment` | string | `production` | `production` or `staging` |
| `--exclude` | list | none | Space-separated language names to exclude |
| `--charts` | flag | off | Generate PNG charts to `scratch/charts/` |
| `--markdown` | flag | off | Render output as Markdown via LLM summary |
| `--save` | flag | off | Persist metrics to Cosmos DB (**never use unless user explicitly requests**) |

## Chart Outputs

When `--charts` is enabled, 4 PNGs are saved to `scratch/charts/`:
1. **adoption.png** — Copilot vs non-Copilot reviews by language
2. **comment_quality.png** — Comment quality breakdown (upvoted, implicit good/bad, downvoted, deleted)
3. **human_copilot_split.png** — Human vs AI comment split
4. **human_comments_comparison.png** — Human comments with vs without Copilot

## Gotchas

- **Always redirect output to a file**: The command can take 1-3 minutes. Terminal output may be lost if the command runs too long. Always use `> scratch/metrics_output.json 2>&1` and read the file afterward.
- **Use `python cli.py` not `.\avc`**: The `avc.bat` script may resolve to system Python. Use `python cli.py report metrics ...` to ensure the correct environment.
- **Month end dates**: February has 28/29 days, April/June/Sept/Nov have 30 days. Get it right.
- **Built-in exclusions**: `c`, `c++`, `typespec`, `swagger`, `xml` are always excluded automatically — no need to add them.
