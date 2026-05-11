---
name: report-apiview-metrics
description: "Run APIView platform metrics (versioned revisions and cross-language compliance). Use for: apiview metrics, platform metrics, version coverage, versioned revisions, cross-language compliance, compliance metrics, PackageVersion coverage, CrossLanguagePackageId, apiview-metrics, parser compliance."
argument-hint: "Optional: --months N, --end-date YYYY-MM-DD, --languages Python Java, --chart, --summary"
---

# Report APIView Metrics

## When to Use
- Monitoring progress toward 100% versioned revisions across languages
- Checking cross-language metadata compliance (CrossLanguagePackageId)
- Generating trend charts for APIView platform health
- Reviewing parser compliance over time

## What It Produces

A combined report with two metric buckets:

| Bucket | What it measures |
|--------|-----------------|
| **versions** | % of revisions with a valid `PackageVersion`, broken out by language and revision type (Automatic, Manual, PullRequest) |
| **compliance** | % of reviews whose latest revision includes `CrossLanguagePackageId` (from `CrossLanguageMetadata`) |

Output is JSON with top-level `"versions"` and `"compliance"` keys. With `--summary`, human-readable tables are printed to stderr.

## Defaults

Unless the user says otherwise, always apply these defaults:
- **Environment**: `production`
- **Months**: `6`
- **Languages**: Python, C#, Java, JavaScript, Go (all included by default — do not pass `--languages` unless user restricts)
- **Chart**: Include `--chart` unless user says no charts

## Running the Command

### Step 1: Run the Command

Show the resolved command and run it immediately in a **foreground terminal** with a **120-second timeout** (`timeout: 120000`). Clean up stale output first.

```powershell
New-Item -ItemType Directory -Path output -Force | Out-Null; New-Item -ItemType Directory -Path output/charts -Force | Out-Null; if (Test-Path output/apiview_metrics_output.json) { Remove-Item output/apiview_metrics_output.json }; python cli.py report apiview-metrics --chart --summary 2>$null | Out-File -Encoding UTF8 output/apiview_metrics_output.json
```

After the command completes, **read the output file** with `read_file` to get the JSON results. Then use `view_image` to display charts at:
- `output/charts/apiview_version_trends.png`
- `output/charts/cross_language_compliance.png`

### Examples

```powershell
# Default: 6 months, all languages, production, with charts
python cli.py report apiview-metrics --chart

# With human-readable summary tables on stderr
python cli.py report apiview-metrics --chart --summary

# Custom lookback
python cli.py report apiview-metrics --months 3 --chart

# Specific end date
python cli.py report apiview-metrics --months 6 --end-date 2026-04-30 --chart

# Specific languages only
python cli.py report apiview-metrics --languages Python Java --chart

# Staging environment
python cli.py report apiview-metrics --chart --environment staging
```

### Step 2: Present Results

After reading the output file:
1. Summarize the version-coverage trends (highlight languages below 100%)
2. Summarize the compliance trends (highlight languages below 100%)
3. Show the chart images with `view_image`

### Step 3: Answer Follow-up Questions

For follow-up questions about the same data, **read the output file** instead of re-running the command.

## Available Flags

| Flag | Type | Default | Description |
|------|------|---------|-------------|
| `--months` | int | `6` | Number of calendar months to look back from end date |
| `--end-date` / `-e` | string | today | Inclusive query end date (`YYYY-MM-DD`) |
| `--languages` | list | all 5 | Space-separated language names to include |
| `--chart` | flag | off | Generate PNG trend charts to `output/charts/` |
| `--summary` | flag | off | Print human-readable summary tables to stderr |
| `--environment` | string | `production` | `production` or `staging` |

## Gotchas

- **Redirect stdout to a file**: Always pipe through `| Out-File -Encoding UTF8 output/apiview_metrics_output.json`. Do NOT use `>` which produces UTF-16 in PowerShell 5.1.
- **Do NOT use `2>&1`**: This merges stderr log lines into the JSON output, producing invalid JSON.
- **Clean up before running**: Delete previous output files before running to avoid presenting stale results.
- **Use `python cli.py` not `.\avc`**: Ensures the correct Python environment is used.
- **Use `read_file` and `view_image`**: Do NOT use additional terminal commands to read output files or display charts.
- **Built-in exclusions**: `c`, `c++`, `typespec`, `swagger`, `xml` are always excluded automatically.
