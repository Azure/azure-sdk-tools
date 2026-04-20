# AVC CLI Reference

The `avc` CLI is the primary tool for working with APIView Copilot locally. It is implemented in `cli.py` using [Knack](https://github.com/microsoft/knack) and invoked via the `avc` shell script (or `avc.bat` on Windows CMD.exe).

## Setup

Activate your virtual environment before running any commands:

```bash
# Windows (PowerShell / CMD)
.venv\Scripts\activate

# Linux / macOS
source .venv/bin/activate
```

Install dependencies:

```bash
pip install -r dev_requirements.txt
```

Create a `.env` file with:

```
ENVIRONMENT_NAME="staging"   # or "production"
```

Log in to Azure (required for all commands that access Azure resources):

```bash
az login
```

---

## Command Groups

| Group | Purpose |
|-------|---------|
| `avc review` | Generate and manage API reviews |
| `avc agent` | Interact with the AI agent |
| `avc kb` | Manage the knowledge base |
| `avc db` | Direct database operations |
| `avc report` | Analytics, auditing, and reporting |
| `avc apiview` | Query APIView data |
| `avc ops` | Deployment and infrastructure |
| `avc test` | Development and testing utilities |

---

## `avc review` — API Reviews

### `avc review generate`

Generate a review synchronously. By default, runs locally; use `--remote` to send to the deployed service.

```bash
avc review generate -l <LANG> -t <TARGET_FILE> [-b <BASE_FILE>] [--outline <OUTLINE_FILE>] [--existing-comments <FILE>] [--debug-log] [--remote]
```

| Option | Description |
|--------|-------------|
| `-l/--language` | Language (e.g., `python`, `dotnet`, `Go`) |
| `-t/--target` | Path to the target APIView text file |
| `-b/--base` | Path to base APIView text file (diff mode) |
| `--outline` | Path to a plain-text file containing the package outline |
| `--existing-comments` | Path to JSON file with existing review comments |
| `--debug-log` | Write intermediate filter/judge results to `scratch/output/<job_id>/` |
| `--remote` | Use the deployed service instead of running locally |

**Examples:**
```bash
# Full review (local)
avc review generate -l python -t scratch/apiviews/python/myapi.txt

# Diff review with debug output
avc review generate -l dotnet -t scratch/apiviews/dotnet/new.txt -b scratch/apiviews/dotnet/old.txt --debug-log

# Remote review
avc review generate -l java -t scratch/apiviews/java/myapi.txt --remote
```

---

### `avc review start-job`

Start an asynchronous review job on the deployed service. Returns a `jobId` to poll with `get-job`.

```bash
avc review start-job -l <LANG> -t <TARGET_FILE> [-b <BASE_FILE>] [--outline <FILE>] [--existing-comments <FILE>]
```

---

### `avc review get-job`

Get the status or result of a previously started review job.

```bash
avc review get-job --job-id <JOB_ID> [--remote]
```

| Option | Description |
|--------|-------------|
| `--job-id` | The job ID returned by `start-job` |
| `--remote` | Query the remote service instead of local database |

---

### `avc review summarize`

Summarize an API or a diff of two APIs.

```bash
avc review summarize -l <LANG> -t <TARGET_FILE> [-b <BASE_FILE>] [--remote]
```

---

### `avc review group-comments`

Group similar comments in an existing comments JSON file by assigning correlation IDs.

```bash
avc review group-comments -c <COMMENTS_JSON_FILE>
```

---

## `avc agent` — AI Agent

### `avc agent chat`

Start or resume an interactive chat session with the AI agent.

```bash
avc agent chat [-t <THREAD_ID>] [--remote] [--quiet] [--readonly]
```

| Option | Description |
|--------|-------------|
| `-t/--thread-id` | Resume a previous conversation thread |
| `--remote` | Use the remote agent endpoint instead of a local agent |
| `--quiet` | Suppress error messages during tool execution (agent retries automatically) |
| `--readonly` | Force read-only mode even if you have write permissions |

Type `exit` or `quit` to end the session. Save the printed thread ID to resume later.

The agent selects read-only or read-write mode based on your Azure RBAC roles. Read-write mode allows the agent to create and modify knowledge base entries.

---

### `avc agent mention`

Process @mention feedback from an APIView comment thread. Creates memories in the KB based on reviewer feedback.

```bash
# From a JSON file
avc agent mention -c <COMMENTS_JSON_FILE> [--source-comment-id <ID>] [--remote] [--dry-run]

# Fetch directly from the database
avc agent mention --fetch-comment-id <COMMENT_ID> [--remote] [--dry-run]
```

| Option | Description |
|--------|-------------|
| `-c/--comments-path` | Path to a JSON file with the comment thread |
| `--fetch-comment-id` | Fetch a comment from the DB and manufacture a feedback conversation |
| `--source-comment-id` | The original APIView comment ID (for audit traceability) |
| `--dry-run` | Print the payload without executing the request |
| `--remote` | Send to the deployed service instead of running locally |

---

### `avc agent resolve-thread`

Update the knowledge base when an APIView conversation thread is resolved. Creates a memory reflecting the reviewer's decision.

```bash
avc agent resolve-thread -c <COMMENTS_JSON_FILE> [--remote]
```

---

## `avc kb` — Knowledge Base

### `avc kb search`

Search the knowledge base and display what the LLM would receive as context.

```bash
# Search by text query
avc kb search --text "error handling" -l python

# Search from a file
avc kb search --path myquery.txt -l dotnet

# Search by known item IDs
avc kb search --ids <ID1> <ID2>

# Output as Markdown (what the LLM sees)
avc kb search --text "naming conventions" -l python --markdown > context.md
```

| Option | Description |
|--------|-------------|
| `-l/--language` | Language to filter results (required unless using `--ids`) |
| `--text` | Text query string |
| `--path` | Path to a file containing the query |
| `--ids` | One or more specific item IDs to retrieve |
| `--markdown` | Render output as Markdown instead of JSON |

---

### `avc kb reindex`

Trigger the Azure AI Search indexers to sync updated KB data.

```bash
# Reindex all containers
avc kb reindex

# Reindex specific containers
avc kb reindex -c guidelines examples
```

When no `-c` flag is provided, the CLI runs indexers for all KB containers: `guidelines`, `examples`, and `memories`.

---

### `avc kb all-guidelines`

Retrieve all guidelines for a language.

```bash
avc kb all-guidelines -l python [--markdown]
```

---

### `avc kb check-links`

Audit bidirectional links between guidelines, memories, and examples. Reports dangling references (target doesn't exist) and one-way links (A→B but B does not reference A back).

```bash
# Report issues for all languages
avc kb check-links

# Report issues for a single language
avc kb check-links -l python

# Repair all issues
avc kb check-links --fix all

# Repair only broken (dangling) references
avc kb check-links --fix broken

# Repair only one-way links (add missing back-refs)
avc kb check-links --fix oneway
```

---

### `avc kb consolidate-memories`

Find and merge duplicate memories linked to the specified items. Runs in dry-run mode by default.

```bash
# Dry-run: show what would be merged
avc kb consolidate-memories --kind guideline --ids <ID1> <ID2>

# Execute the consolidation
avc kb consolidate-memories --kind guideline --ids <ID1> --apply
```

`--kind` accepts `guideline`, `example`, or `memory`. `--ids` takes one or more IDs of the specified kind.

---

## `avc db` — Database Operations

### `avc db get`

Retrieve a single item from a Cosmos DB container.

```bash
avc db get -c <CONTAINER> -i <ITEM_ID>
```

Available containers: `guidelines`, `examples`, `memories`, `review-jobs`, `metrics`, `evals`

---

### `avc db delete`

Soft-delete an item from the database. The item is marked with `isDeleted: true` (not removed from Cosmos DB) and excluded from search results.

For KB containers (`guidelines`, `memories`, `examples`), deletion automatically **cascades**:
- Back-links are removed from all related items.
- Orphaned examples (no remaining `memory_ids` or `guideline_ids`) are soft-deleted.
- Orphaned memories and guidelines are always retained.

```bash
avc db delete -c <CONTAINER> -i <ITEM_ID>
```

---

### `avc db purge`

Permanently remove all soft-deleted items from one or more containers.

```bash
# Purge all data containers
avc db purge

# Purge specific containers, running the indexer first
avc db purge -c guidelines memories --run-indexer
```

Use `--verbose` (Knack built-in) to print each item as it is hard-deleted or skipped.

---

### `avc db link`

Link two KB items by adding each to the other's related collection. Provide exactly two of `--guideline`, `--memory`, `--example`.

```bash
avc db link -g <GUIDELINE_ID> -m <MEMORY_ID> [--reindex]
avc db link -g <GUIDELINE_ID> -e <EXAMPLE_ID> [--reindex]
avc db link -m <MEMORY_ID> -e <EXAMPLE_ID> [--reindex]
```

If the second update fails, the first is rolled back to keep items consistent. Use `--reindex` to trigger a full search reindex after linking.

---

### `avc db unlink`

Remove the link between two KB items.

```bash
avc db unlink -g <GUIDELINE_ID> -m <MEMORY_ID> [--reindex]
```

Same flags and rollback behavior as `db link`.

---

## `avc report` — Reporting and Analytics

### `avc report metrics`

Generate a metrics report for a date range.

```bash
avc report metrics -s 2026-01-01 -e 2026-01-31 [--environment production|staging] [--save] [--charts] [--exclude Java Go]
```

| Option | Description |
|--------|-------------|
| `-s/--start-date` | Start date (YYYY-MM-DD) |
| `-e/--end-date` | End date (YYYY-MM-DD) |
| `--environment` | `production` (default) or `staging` |
| `--save` | Persist metrics segments to Cosmos DB (for Power BI) |
| `--charts` | Generate PNG charts in `output/charts/` |
| `--exclude` | Languages to exclude (e.g., `--exclude Java Go`) |

See [metrics.md](./metrics.md) for details on what is measured and how.

---

### `avc report quality-trends`

Generate the multi-language comment bucket trend chart for a calendar-month lookback ending on a specified date. The chart is saved under output/charts, matching the metrics command behavior.

```bash
avc report quality-trends [--end-date 2026-04-17] [--months 6] [--languages Python Java] [--exclude-human] [--neutral] [--environment production|staging]
```

| Option | Description |
|--------|-------------|
| `-e/--end-date` | Inclusive query end date; defaults to today, and its month counts as one of the requested months |
| `--months` | Number of calendar months to look back from the end date |
| `--languages` | Languages to include; defaults to Python, C#, Java, and JavaScript |
| `--exclude-human` | Exclude human comments from the chart |
| `--neutral` | Include neutral AI comments as a separate bucket |
| `--environment` | `production` (default) or `staging` |

---

### `avc report active-reviews`

Query active APIView reviews in a date range.

```bash
avc report active-reviews -s 2026-01-01 -e 2026-01-31 [-l python] [--summary] [--approved-only] [--environment staging]
```

| Option | Description |
|--------|-------------|
| `-l/--language` | Filter by language (optional; omit for all languages) |
| `--summary` | Output a compact table instead of detailed JSON |
| `--approved-only` | Show only approved revisions |
| `--environment` | `production` (default) or `staging` |

---

### `avc report feedback`

Audit AI comment feedback (votes, resolution) in a date range.

```bash
avc report feedback -s 2026-01-01 -e 2026-01-31 [-l python] [--exclude good bad delete] [--format json|yaml]
```

---

### `avc report memory`

List memories created in a date range.

```bash
avc report memory -s 2026-01-01 -e 2026-01-31 [-l python] [--format json|yaml]
```

---

### `avc report analyze-comments`

Analyze human reviewer comment themes for a language and date range using an LLM.

```bash
avc report analyze-comments -l python -s 2026-01-01 -e 2026-01-31 [--environment staging]
```

---

## `avc apiview` — APIView Data

### `avc apiview get-comments`

Retrieve all comments for a specific APIView revision, grouped by line number.

```bash
avc apiview get-comments -r <REVISION_ID> [--environment production|staging]
```

---

### `avc apiview resolve-package`

Resolve package information from a package name or description. Uses exact match first, then LLM-powered fallback.

```bash
avc apiview resolve-package -p azure-storage-blob -l python [--version 12.19.0] [--environment staging]

# Natural language description (LLM fallback)
avc apiview resolve-package -p "storage blobs" -l python
```

Returns: package name, review ID, language, version.

---

## `avc ops` — Deployment and Infrastructure

### `avc ops deploy`

Deploy the FastAPI app to Azure App Service.

```bash
avc ops deploy
```

---

### `avc ops check`

Check that the deployed service is healthy and accessible.

```bash
avc ops check [--include-auth]
```

`--include-auth` tests the authenticated endpoint in addition to the health check.

---

### `avc ops grant`

Grant Azure RBAC permissions needed to run AVC locally. Grants access to App Configuration, Azure AI Search, Cosmos DB, Key Vault, Azure OpenAI, and Azure AI Foundry.

```bash
avc ops grant [--assignee-id <OBJECT_ID>]
```

If `--assignee-id` is omitted, uses the currently logged-in user. After granting, run `az logout && az login` to refresh your token.

---

### `avc ops revoke`

Revoke previously granted Azure RBAC permissions.

```bash
avc ops revoke [--assignee-id <OBJECT_ID>]
```

---

## `avc test` — Development and Testing

### `avc test eval`

Run evaluation tests to assess prompt quality.

```bash
# Run all eval workflows
avc test eval

# Run specific workflow(s)
avc test eval -p evals/tests/api_review_guidelines

# Run with recordings (no LLM calls)
avc test eval --use-recording

# Run 3 times and save results to DB
avc test eval -n 3 --save

# Verbose output (show all test cases, not just failures)
avc test eval --style verbose
```

| Option | Description |
|--------|-------------|
| `-p/--test-paths` | Paths to eval workflow folders (omit for all) |
| `-n/--num-runs` | Number of runs per test case (default: 1) |
| `--use-recording` | Use cached LLM responses instead of live calls |
| `--style` | `compact` (failures only) or `verbose` (all cases) |
| `--save` | Save eval results to Cosmos DB |

---

### `avc test prompt`

Test a single `.prompty` file, or smoke-test all prompts.

```bash
# Smoke-test all prompts (checks they execute without errors)
avc test prompt

# Test a specific prompt
avc test prompt -p prompts/api_review/guidelines_review.prompty

# Control parallelism for smoke test
avc test prompt -w 8
```

---

### `avc test pytest`

Run unit tests with pytest.

```bash
avc test pytest

# Pass extra pytest arguments
avc test pytest --args "-k test_sectioned_document -v"
```

---

### `avc test extract-section`

Extract a single section from an APIView file, for testing the sectioning logic.

```bash
avc test extract-section <FILE_PATH> <SECTION_SIZE> [--index <N>]
```

| Option | Description |
|--------|-------------|
| `size` | Max number of lines per section |
| `--index/-i` | Section index to extract (1-based, default: 1) |

---

## Global Options

| Option | Description |
|--------|-------------|
| `--environment` | Select `production` or `staging` (default: `production`). Sets `ENVIRONMENT_NAME` for the process. |
| `-l/--language` | Language identifier. Accepts canonical names (`dotnet`), pretty names (`C#`), and common aliases (`csharp`). Case-insensitive. |

## Notes

- On Windows CMD.exe, use `avc.bat` in place of `avc`.
- Most commands that access Azure resources require `az login` and appropriate RBAC permissions. Use `avc ops grant` to grant those permissions.
- The `--remote` flag (available on `review generate`, `agent chat`, `agent mention`, `agent resolve-thread`) routes calls to the deployed App Service rather than running locally.
