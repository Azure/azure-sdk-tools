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
avc review generate -l python -t scratch/apiviews/target.txt

# With a base file for diff review
avc review generate -l python -t scratch/apiviews/target.txt -b scratch/apiviews/base.txt

# With an outline file
avc review generate -l python -t scratch/apiviews/target.txt --outline scratch/apiviews/outline.txt

# With existing comments (to test pre-existing comment filtering)
avc review generate -l python -t scratch/apiviews/target.txt --existing-comments scratch/comments/existing.json

# With debug logging (writes logs to scratch/logs/<LANG>/)
avc review generate -l python -t scratch/apiviews/target.txt --debug-log
```

**Parameters:**

| Flag | Required | Description |
|------|----------|-------------|
| `-l / --language` | Yes | Language: `python`, `typescript`, `java`, `dotnet`, `golang`, `cpp`, `android`, `ios`, `rust`, `clang` |
| `-t / --target` | Yes | Path to the APIView text file to review |
| `-b / --base` | No | Path to base APIView text file (enables diff review) |
| `--outline` | No | Path to outline text file |
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
# Query the local database for a job by ID
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
# From a local JSON file
avc agent mention -c scratch/mention/sample_mention.json

# From a comment ID in the database
avc agent mention -i <comment-id>

# Dry run (print payload without executing)
avc agent mention -c scratch/mention/sample_mention.json --dry-run
```

**Parameters:**

| Flag | Required | Description |
|------|----------|-------------|
| `-c / --comments-path` | One of `-c` or `-i` | Path to JSON file with conversation data |
| `-i / --comment-id` | One of `-c` or `-i` | Comment ID to fetch from Cosmos DB |
| `--dry-run` | No | Print the request payload without executing |

**Expected JSON format** (for `--comments-path`):
```json
{
  "comments": ["Original comment text", "User reply mentioning @bot"],
  "language": "Python",
  "packageName": "azure-storage-blob",
  "code": "def some_api_method(self, param: str) -> None: ..."
}
```

> Note: `language` should be the **pretty name** (e.g., `"Python"`, not `"python"`).

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
# From a local JSON file
avc agent resolve-thread -c scratch/thread_resolution/sample_thread.json
```

**Parameters:**

| Flag | Required | Description |
|------|----------|-------------|
| `-c / --comments-path` | Yes | Path to JSON file with resolved thread data |

**Expected JSON format** (same structure as mention):
```json
{
  "comments": ["Original review comment", "Developer response", "Resolution note"],
  "language": "Python",
  "packageName": "azure-storage-blob",
  "code": "def some_api_method(self, param: str) -> None: ..."
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
avc review summarize -l python -t scratch/apiviews/target.txt

# Summarize the diff between two APIView files
avc review summarize -l python -t scratch/apiviews/target.txt -b scratch/apiviews/base.txt
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
avc apiview resolve-package -l python -p "azure-storage-blob"

# 2. Run a review on a small APIView file
avc review generate -l python -t scratch/apiviews/target.txt --debug-log

# 3. Test mention handling with a sample payload
avc agent mention -c scratch/mention/sample_mention.json --dry-run

# 4. Summarize an API locally
avc review summarize -l python -t scratch/apiviews/target.txt
```

### Full Coverage Test
```bash
# Review generation — all code paths
avc review generate -l python -t scratch/apiviews/target.txt
avc review generate -l python -t scratch/apiviews/target.txt -b scratch/apiviews/base.txt
avc review generate -l python -t scratch/apiviews/target.txt --existing-comments scratch/comments/existing.json

# Job status retrieval
avc review get-job --job-id <job-id>

# API summarization — single and diff
avc review summarize -l python -t scratch/apiviews/target.txt
avc review summarize -l python -t scratch/apiviews/target.txt -b scratch/apiviews/base.txt

# Agent chat — interactive
avc agent chat --readonly

# Mention processing
avc agent mention -c scratch/mention/sample_mention.json

# Thread resolution
avc agent resolve-thread -c scratch/thread_resolution/sample_thread.json

# Package resolution — multiple languages
avc apiview resolve-package -l python -p "azure-storage-blob"
avc apiview resolve-package -l typescript -p "azure/storage-blob"
avc apiview resolve-package -l java -p "azure-storage-blob"
```

### Unit Tests (No Azure Dependencies)
```bash
# Run the existing pytest suite
pytest tests
```
