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
- **Format**: JSON output (UTF-8 encoded)

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

### Step 1: Run the Command

Show the resolved command and run it immediately in a **single foreground terminal invocation** with a **180-second timeout** (`timeout: 180000`). Before running, clean up stale output so you never accidentally present old results.

**Important**: Do NOT use `2>&1` — that merges stderr log messages (e.g. "Saved: output\charts\adoption.png") into the JSON output file, producing invalid JSON. Pipe stdout through `Out-File -Encoding UTF8` so the output file is always valid UTF-8 JSON.

**Full terminal command** (cleanup + run):
```powershell
New-Item -ItemType Directory -Path output -Force | Out-Null; if (Test-Path output/metrics_output.json) { Remove-Item output/metrics_output.json }; if (Test-Path output/charts) { Remove-Item output/charts/* -Force }; python cli.py report metrics -s <start_date> -e <end_date> --charts | Out-File -Encoding UTF8 output/metrics_output.json
```

After the command completes, **read the output file** with `read_file` to get the JSON results — do NOT use a separate terminal command. Then use `view_image` (not a terminal command) to display the chart PNGs. This means the entire workflow requires only **one** terminal invocation.

### Examples

```powershell
# March 2025, production, all languages, with charts (typical request)
python cli.py report metrics -s 2025-03-01 -e 2025-03-31 --charts

# Staging environment
python cli.py report metrics -s 2025-03-01 -e 2025-03-31 --charts --environment staging

# Exclude specific languages
python cli.py report metrics -s 2025-03-01 -e 2025-03-31 --charts --exclude Java Golang

# Custom date range
python cli.py report metrics -s 2025-03-10 -e 2025-03-20 --charts

# Save to database (ONLY when user explicitly requests)
python cli.py report metrics -s 2025-03-01 -e 2025-03-31 --charts --save
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
| `--charts` | flag | off | Generate PNG charts to `output/charts/` |
| `--save` | flag | off | Persist metrics to Cosmos DB (**never use unless user explicitly requests**) |

## Chart Outputs

When `--charts` is enabled, 4 PNGs are saved to `output/charts/`:
1. **adoption.png** — Copilot vs non-Copilot reviews by language
2. **comment_quality.png** — Comment quality breakdown (upvoted, implicit good/bad, downvoted, deleted)
3. **human_copilot_split.png** — Human vs AI comment split
4. **human_comments_comparison.png** — Human comments with vs without Copilot

## Gotchas

- **Redirect stdout to a file**: Always pipe through `| Out-File -Encoding UTF8 output/metrics_output.json` and read the file afterward. Do NOT use `>` which produces UTF-16 in PowerShell 5.1.
- **Clean up before running**: Delete the previous `output/metrics_output.json` and `output/charts/*` before running. This prevents presenting stale results if the command fails silently.
- **Do NOT use `2>&1`**: This merges stderr log lines (e.g. "Saved: ...") into the JSON output file, producing invalid JSON. Only redirect stdout.
- **Use foreground with timeout**: Run with `isBackground=false` and `timeout: 180000` (3 min). Do NOT use `isBackground=true` and poll — that causes repeated user approval prompts.
- **Use `New-Item -ItemType Directory` not `mkdir`**: `mkdir` is aliased differently across shells. Use `New-Item -ItemType Directory -Path output -Force | Out-Null` for reliable directory creation.
- **Read results with `read_file` and `view_image`**: After the command finishes, use `read_file` for the JSON and `view_image` for charts. Do NOT launch additional terminal commands to read the file.
- **Use `python cli.py` not `.\avc`**: The `avc.bat` script may resolve to system Python. Use `python cli.py report metrics ...` to ensure the correct environment.
- **Month end dates**: February has 28/29 days, April/June/Sept/Nov have 30 days. Get it right.
- **Built-in exclusions**: `c`, `c++`, `typespec`, `swagger`, `xml` are always excluded automatically — no need to add them.
