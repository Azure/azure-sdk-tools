# Test Plan: Testing app.py Endpoint Logic Locally via `avc` CLI

This document maps each FastAPI endpoint in `app.py` to the `avc` CLI command that exercises the **same underlying logic** locally—without running or hitting the HTTP server. All commands below should be run **without** the `--remote` flag (unless noted otherwise).

> **Prerequisites:** Activate the virtualenv (`.venv\Scripts\activate`), install deps (`pip install -r dev_requirements.txt`), and ensure `AZURE_APP_CONFIG_ENDPOINT` and `ENVIRONMENT_NAME` environment variables are set.

---

## Endpoint → CLI Command Mapping

| # | Endpoint | Method | CLI Command | Local? |
|---|----------|--------|-------------|--------|
| 1 | `/api-review/start` | POST | `avc review generate` | ✅ Yes |
| 2 | `/api-review/{job_id}` | GET | `avc review get-job` | ✅ Yes |
| 3 | `/agent/chat` | POST | `avc agent chat` | ✅ Yes |
| 4 | `/api-review/mention` | POST | `avc agent mention` | ✅ Yes |
| 5 | `/api-review/resolve` | POST | `avc agent resolve-thread` | ✅ Yes |
| 6 | `/api-review/resolve-package` | POST | `avc apiview resolve-package` | ✅ Yes |
| 7 | `/api-review/summarize` | POST | `avc review summarize` | ✅ Yes |
| 8 | `/health-test` | GET | `avc app check` | ❌ Remote only |
| 9 | `/auth-test` | GET | `avc app check --include-auth` | ❌ Remote only |

---

## 1. Review Generation — `/api-review/start`

**What it tests:** The core review pipeline — sectioning, parallel LLM prompts (guideline, context, generic), filtering, deduplication, scoring, and comment grouping. This is the primary endpoint.

**Underlying code:** `ApiViewReview.run()` in `src/_apiview_reviewer.py`

```bash
# Minimal — review a single APIView file
avc review generate -l python -t scratch/apiviews/python/keyvault_secrets_4.11.0b1.txt

# With a base file for diff review
avc review generate -l python -t scratch/apiviews/python/keyvault_secrets_4.11.0b1.txt -b scratch/apiviews/python/keyvault_secrets_4.10.0b1.txt

# With existing comments (to test pre-existing comment filtering)
avc review generate -l python -t scratch/apiviews/python/keyvault_secrets_4.11.0b1.txt --existing-comments scratch/comments/python/keyvault_secrets_4.11.0b1.txt

# With debug logging (writes logs to scratch/logs/<LANG>/)
avc review generate -l python -t scratch/apiviews/python/keyvault_secrets_4.11.0b1.txt --debug-log
```

**Parameters:**

| Flag | Required | Description |
|------|----------|-------------|
| `-l / --language` | Yes | Language: `python`, `typescript`, `java`, `dotnet`, `golang`, `cpp`, `android`, `ios`, `rust`, `clang` |
| `-t / --target` | Yes | Path to the APIView text file to review |
| `-b / --base` | No | Path to base APIView text file (enables diff review) |
| `--existing-comments` | No | Path to JSON file containing existing comments |
| `--debug-log` | No | Enable debug logging to `scratch/logs/` |

**What this exercises from the endpoint:**
- Document sectioning (`SectionedDocument`)
- Guideline review (RAG search for language guidelines)
- Context review (semantic search)
- Generic review (custom rules from `metadata/<lang>/guidance.yaml`)
- Generic comment filtering against knowledge base
- Comment deduplication via LLM
- Hard filtering via `metadata/<lang>/filter.yaml`
- Pre-existing comment filtering (when `--existing-comments` provided)
- Judge scoring (confidence/severity)
- Correlation ID grouping

---

## 2. Job Status Retrieval — `/api-review/{job_id}`

**What it tests:** Retrieving the status and result of a previously submitted review job from the Cosmos DB `review-jobs` container. This is the same database lookup the endpoint performs.

**Underlying code:** `DatabaseManager.review_jobs.get()` in `src/_database_manager.py`

```bash
# Start a review job (submits to the remote API, returns a job ID)
avc review start-job -l python -t scratch/apiviews/python/keyvault_secrets_4.11.0b1.txt

# Query the database for the job by ID (use the job ID returned above)
avc review get-job --job-id <job-id>
```

**Parameters:**

| Flag | Required | Description |
|------|----------|-------------|
| `--job-id` | Yes | The job ID to look up |
| `--remote` | No | Query the remote API service instead of the local database |

**What this exercises from the endpoint:**
- Cosmos DB job retrieval from the `review-jobs` container
- Job status/result data model serialization

---

## 3. Agent Chat — `/agent/chat`

**What it tests:** Azure AI Agent Service integration — creates or continues a conversation thread with a read-only or read-write agent.

**Underlying code:** `invoke_agent()`, `get_readonly_agent()`, `get_readwrite_agent()` in `src/agent/`

```bash
# Interactive chat session (auto-detects read/write from token roles)
avc agent chat

# Continue an existing thread
avc agent chat -t <thread-id>

# Force readonly mode
avc agent chat --readonly

# Quiet mode (suppress errors, agent retries automatically)
avc agent chat --quiet
```

**Parameters:**

| Flag | Required | Description |
|------|----------|-------------|
| `-t / --thread-id` | No | Thread ID to continue a previous conversation |
| `--readonly / -r` | No | Force readonly agent (bypasses token role detection) |
| `--quiet / -q` | No | Suppress error messages |

**What this exercises from the endpoint:**
- Agent selection (read-only vs read-write based on Azure token claims)
- Thread creation and continuation
- Multi-turn conversation handling
- Message history management

---

## 4. Mention Processing — `/api-review/mention`

**What it tests:** @mention feedback handling — processes a conversation thread where a user mentioned the bot and generates a response.

**Underlying code:** `handle_mention_request()` in `src/_mention.py`

```bash
# From a local JSON file (user agrees with the bot's comment)
avc agent mention -c scratch/mention/no_update.txt

# From a local JSON file (user disagrees, requests guideline update)
avc agent mention -c scratch/mention/update_with_guideline.txt

# From a comment ID in the database
avc agent mention -i <comment-id>

# Dry run (print payload without executing)
avc agent mention -c scratch/mention/update_with_guideline.txt --dry-run
```

**Parameters:**

| Flag | Required | Description |
|------|----------|-------------|
| `-c / --comments-path` | One of `-c` or `-i` | Path to JSON file with conversation data |
| `-i / --comment-id` | One of `-c` or `-i` | Comment ID to fetch from Cosmos DB |
| `--dry-run` | No | Print the request payload without executing |

**Expected JSON format** (for `--comments-path`) — see `scratch/mention/*.txt` for real examples:
```json
{
  "language": "python",
  "package_name": "azure.widgets",
  "code": "async def list_widgets(): AsyncIterator[Widget]",
  "comments": [
    {
      "lineNo": 50,
      "createdBy": "azure-sdk",
      "createdOn": "2025-07-03T09:15:00-07:00",
      "commentText": "Async operations should use the async keyword."
    },
    {
      "lineNo": 50,
      "createdBy": "annatisch",
      "createdOn": "2025-07-03T09:20:00-07:00",
      "commentText": "@azure-sdk no for async list operations..."
    }
  ]
}
```

**What this exercises from the endpoint:**
- Mention request parsing
- Conversation context assembly
- LLM prompt execution for mention response
- Knowledge base search for relevant guidelines

---

## 5. Thread Resolution — `/api-review/resolve`

**What it tests:** Thread resolution logic — when a review comment thread is resolved, this determines whether the resolution was appropriate and records learnings.

**Underlying code:** `handle_thread_resolution_request()` in `src/_thread_resolution.py`

```bash
# Thread resolved with no action needed
avc agent resolve-thread -c scratch/thread_resolution/no_action.txt

# Thread resolved — record an exception to a guideline
avc agent resolve-thread -c scratch/thread_resolution/record_exception.txt

# Thread resolved — record a review result
avc agent resolve-thread -c scratch/thread_resolution/record_result.txt

# Thread resolved — unclear outcome
avc agent resolve-thread -c scratch/thread_resolution/unclear.txt
```

**Parameters:**

| Flag | Required | Description |
|------|----------|-------------|
| `-c / --comments-path` | Yes | Path to JSON file with resolved thread data |

**Expected JSON format** (same structure as mention) — see `scratch/thread_resolution/*.txt` for real examples:
```json
{
  "language": "python",
  "package_name": "azure.widgets",
  "code": "class WidgetObject:",
  "comments": [
    {
      "CreatedBy": "azure-sdk",
      "CommentText": "This name is unnecessarily verbose.",
      "CreatedOn": "2025-03-17T17:48:25.920445-04:00"
    },
    {
      "CreatedBy": "noodle",
      "CommentText": "We discussed it internally and want to keep it as is.",
      "CreatedOn": "2025-03-18T13:15:19.1494832-04:00"
    }
  ]
}
```

**What this exercises from the endpoint:**
- Thread resolution analysis
- LLM prompt execution for resolution judgment
- Memory/learning extraction from resolved threads

---

## 6. Package Resolution — `/api-review/resolve-package`

**What it tests:** Package lookup logic — resolves a package name or description to its APIView review metadata (review ID, revision, version).

**Underlying code:** `resolve_package()` in `src/_apiview.py`

```bash
# Basic lookup
avc apiview resolve-package -l python -p "azure-storage-blob"

# With a specific version
avc apiview resolve-package -l python -p "azure-storage-blob" -v "12.14.0"

# Against staging environment
avc apiview resolve-package -l python -p "azure-storage-blob" --environment staging
```

**Parameters:**

| Flag | Required | Description |
|------|----------|-------------|
| `-l / --language` | Yes | Language (canonical form) |
| `-p / --package` | Yes | Package name or description query |
| `-v / --version` | No | Version filter (omit for latest) |
| `--environment` | No | `production` or `staging` (default: `production`) |

**What this exercises from the endpoint:**
- Package name resolution logic
- APIView metadata lookup
- Version filtering

---

## 7. API Summarization — `/api-review/summarize`

**What it tests:** Summarization of an API surface or a diff between two API versions using LLM prompts. Locally, this reads the file(s), builds the prompt inputs, and calls `run_prompt` directly — the same code path the endpoint uses.

**Underlying code:** `run_prompt()` with `summarize_api.prompty` or `summarize_diff.prompty` in `prompts/summarize/`

```bash
# Summarize a single APIView file
avc review summarize -l python -t scratch/apiviews/python/keyvault_secrets_4.11.0b1.txt

# Summarize the diff between two APIView files
avc review summarize -l python -t scratch/apiviews/python/keyvault_secrets_4.11.0b1.txt -b scratch/apiviews/python/keyvault_secrets_4.10.0b1.txt
```

**Parameters:**

| Flag | Required | Description |
|------|----------|-------------|
| `-l / --language` | Yes | Language (canonical form) |
| `-t / --target` | Yes | Path to the APIView text file to summarize |
| `-b / --base` | No | Path to the base APIView file (enables diff summarization) |
| `--remote` | No | Use the remote API service instead of local processing |

**What this exercises from the endpoint:**
- File reading and content preparation
- Diff generation via `create_diff_with_line_numbers()` (when `--base` provided)
- Language name resolution (canonical → pretty)
- LLM prompt execution (`summarize_api.prompty` or `summarize_diff.prompty`)

---

## Endpoints Without Local CLI Equivalents

| Endpoint | Reason | Alternative |
|----------|--------|-------------|
| `/health-test` (GET) | Only meaningful against a running server | N/A — trivial health check |
| `/auth-test` (GET) | Only meaningful against a running server with auth | N/A — tests role-based auth middleware |

---

## Suggested Test Workflow

### Quick Smoke Test
```bash
# 1. Verify package resolution works
avc apiview resolve-package -l python -p "azure-keyvault-secrets"

# 2. Run a review on a small APIView file
avc review generate -l python -t scratch/apiviews/python/keyvault_secrets_4.11.0b1.txt --debug-log

# 3. Test mention handling with a sample payload
avc agent mention -c scratch/mention/update_with_guideline.txt --dry-run

# 4. Summarize an API locally
avc review summarize -l python -t scratch/apiviews/python/keyvault_secrets_4.11.0b1.txt
```

### Full Coverage Test
```bash
# Review generation — all code paths
avc review generate -l python -t scratch/apiviews/python/keyvault_secrets_4.11.0b1.txt
avc review generate -l python -t scratch/apiviews/python/keyvault_secrets_4.11.0b1.txt -b scratch/apiviews/python/keyvault_secrets_4.10.0b1.txt
avc review generate -l python -t scratch/apiviews/python/keyvault_secrets_4.11.0b1.txt --existing-comments scratch/comments/python/keyvault_secrets_4.11.0b1.txt

# Review generation — other languages
avc review generate -l typescript -t scratch/apiviews/typescript/search_documents.txt
avc review generate -l java -t scratch/apiviews/java/search_documents.txt
avc review generate -l rust -t scratch/apiviews/rust/storage_blob.txt
avc review generate -l golang -t scratch/apiviews/golang/cosmos.txt

# Job status retrieval
avc review get-job --job-id <job-id>

# API summarization — single and diff
avc review summarize -l python -t scratch/apiviews/python/keyvault_secrets_4.11.0b1.txt
avc review summarize -l python -t scratch/apiviews/python/keyvault_secrets_4.11.0b1.txt -b scratch/apiviews/python/keyvault_secrets_4.10.0b1.txt

# Agent chat — interactive
avc agent chat --readonly

# Mention processing — multiple scenarios
avc agent mention -c scratch/mention/no_update.txt
avc agent mention -c scratch/mention/update_with_guideline.txt
avc agent mention -c scratch/mention/update_with_no_guideline.txt
avc agent mention -c scratch/mention/open_gh_parser_issue.txt

# Thread resolution — multiple scenarios
avc agent resolve-thread -c scratch/thread_resolution/no_action.txt
avc agent resolve-thread -c scratch/thread_resolution/record_exception.txt
avc agent resolve-thread -c scratch/thread_resolution/record_result.txt
avc agent resolve-thread -c scratch/thread_resolution/unclear.txt

# Package resolution — multiple languages
avc apiview resolve-package -l python -p "azure-keyvault-secrets"
avc apiview resolve-package -l typescript -p "azure/search-documents"
avc apiview resolve-package -l java -p "azure-search-documents"
```

### Unit Tests (No Azure Dependencies)
```bash
# Run the existing pytest suite
pytest tests
```
