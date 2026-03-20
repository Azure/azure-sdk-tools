# Guideline Ingestion & Knowledge Base Sync — Implementation Plan

## Goal

Automate the detection of guideline changes in the [azure-sdk](https://github.com/Azure/azure-sdk) repository and propagate those changes into the APIView Copilot knowledge base (Cosmos DB + Azure AI Search). This replaces the legacy manual process inherited from archagent-ai.

---

## What Has Been Done (branch: `avc/UpdateGuidelines`)

### 1. Data Model Updates (`src/_models.py`)

Added content-tracking fields to both `Guideline` and `Example` Pydantic models:

- `content_hash` — SHA-256 hash of normalized content for change detection
- `source_file_path` — Path to the source file in the azure-sdk repo (e.g., `docs/python/design.md`)
- `source_commit_sha` — Git commit SHA from which the record was extracted
- `last_synced_at` — Timestamp of the last successful sync

These fields enable efficient incremental syncing by comparing content hashes rather than full text.

### 2. Settings Manager Write Support (`src/_settings.py`)

Added a `set()` method to `SettingsManager` to write values back to Azure App Configuration. This is used to persist the `guidelines:last_synced_commit_sha` key, which tracks the last successfully synced commit.

### 3. Guideline Ingestor Module (`src/_guideline_ingestor.py`)

New ~900-line module implementing the core ingestion pipeline:

- **Git/GitHub Integration** — Fetches current HEAD SHA, compares commits via GitHub API, lists changed files in `docs/` across all supported languages.
- **Markdown Parsing** — BeautifulSoup + markdown-it pipeline (ported from the original archagent-ai `preprocess_guidelines.py`) that:
  - Renders markdown to HTML
  - Extracts guidelines from `<p>`, `<li>`, `<pre>` elements
  - Replaces Jekyll requirement tags (`{% include requirement/MUST id="..." %}`) with readable text
  - Generates stable IDs in format `{language}_{filename}=html={anchor}`
- **Content Hashing** — Normalizes content (whitespace, line endings) and computes SHA-256 for change detection.
- **Validation & Deduplication** — Checks ID format compatibility with Azure AI Search, filters duplicate IDs (exact copies vs. conflicting content).
- **Incremental Sync** (`_sync_incremental`) — Parses files at both base and target SHAs, compares content hashes, and applies only the deltas (create/update/delete).
- **Full Sync** (`_sync_full`) — Parses all files at a target SHA, compares against existing database records, handles deletions for guidelines that are no longer present in processed files.
- **Database Upsert** — Writes `Guideline` objects to Cosmos DB with content hash, source metadata, and preserved relationship fields. Runs the search indexer once at the end of sync.

### 4. CLI Command (`cli.py`)

Added `avc db ingest-guidelines` command with arguments:

| Flag | Description |
|------|-------------|
| `--dry-run` / `-d` | Preview changes without modifying the database |
| `--force` / `-f` | Ignore the last synced SHA; full resync |
| `--base-sha` / `-b` | Override the baseline commit SHA |
| `--target-sha` / `-t` | Override the target commit SHA |

### 5. Dependency Updates

Moved `beautifulsoup4` and `markdown-it-py` from dev-only to runtime dependencies (both `requirements.txt` and `dev_requirements.txt`), since the ingestor needs them at runtime.

---

## What Remains To Be Done

### Phase 1: Core Completion — ✅ DONE

#### 1.1 Example Extraction via LLM — ✅ DONE
LLM enrichment now always runs during sync. The `_parse_guidelines_with_llm()` method batches guidelines (10 per batch), sends them to gpt-5.4 via `prompts/other/parse_guidelines.prompty`, and returns enriched guideline dicts + extracted `ParsedExample` objects with good/bad classifications.

#### 1.2 Example Sync Logic — ✅ DONE
The `_sync_examples()` method creates/updates/deletes Example records in Cosmos DB, updates parent Guideline `related_examples` lists, and triggers the examples search indexer. Content hash comparison avoids unnecessary updates.

#### 1.3 Memory Reconciliation — ✅ DONE
The `_reconcile_memories()` method runs after example sync for all created/updated guidelines. For each guideline with `related_memories`, it sends the updated guideline content + related memories to the LLM (`prompts/other/reconcile_memories.prompty`) to determine which memories are now redundant. Absorbed memories are unlinked bidirectionally (guideline ↔ memory, example ↔ memory, sibling memory ↔ memory) and soft-deleted if orphaned. The `SyncResult` dataclass tracks `memories_absorbed` and `memories_retained`.

#### 1.4 Unit Tests
No tests exist for the guideline ingestor. Needed tests include:
- Markdown parsing (Jekyll tag replacement, ID extraction, BeautifulSoup parsing)
- Content hashing (normalization, stability)
- ID format validation and deduplication
- Incremental vs. full sync logic (with mocked GitHub API and database)
- CLI command integration tests
- Edge cases: files added/removed between commits, 404 on file fetch, malformed markdown

### Phase 2: Automation

#### 2.1 GitHub Webhook Endpoint
Add a FastAPI endpoint (e.g., `POST /webhooks/azure-sdk`) to receive push events from the azure-sdk repository. This should:
- Validate the webhook signature
- Extract the commit SHAs from the payload
- Trigger the ingestion pipeline asynchronously (background task)
- Return 202 Accepted immediately

#### 2.2 Scheduled/CI-Triggered Sync
As an alternative or complement to webhooks:
- Add a scheduled pipeline job (e.g., daily cron) in CI that runs `avc db ingest-guidelines`
- Or add an Azure Function / App Service WebJob for periodic sync

#### 2.3 Background Job Tracking
The sync operation can be long-running. Consider:
- Integrating with the existing review job tracking pattern (`/api-review/start` / `/api-review/{job_id}`)
- Adding a `POST /kb/sync` endpoint that returns a job ID and runs sync in the background

### Phase 3: Robustness & Observability

#### 3.1 Error Handling & Retry
- GitHub API rate limiting (currently uses token for higher limits but no retry-after handling)
- Cosmos DB transactional consistency (partial sync failure recovery)
- LLM prompt failures for example extraction (retry with backoff)

#### 3.2 Metrics & Logging
- Integrate with `_metrics.py` to report sync outcomes (counts, durations, errors)
- Add structured logging for production observability
- Track sync history (which SHAs were synced, when, results)

#### 3.3 Deletion Handling in Incremental Sync
The incremental sync detects deletions by comparing parsed guidelines between two SHAs. However, it only processes *changed files* — if a file is unchanged but a guideline was removed in a different commit range, it could be missed. Consider:
- Periodic full sync to catch drift
- Storing source file metadata in the database for cross-referencing

#### 3.4 Memory/Relationship Updates — ✅ DONE (via reconciliation)
Handled automatically by `_reconcile_memories()` during sync. When guidelines change, related memories are evaluated for absorption and unlinked/deleted as appropriate.

### Phase 4: Knowledge Base Management API

#### 4.1 REST Endpoints
Expose KB management operations via FastAPI for tooling and dashboards:
- `GET /kb/guidelines` — List/search guidelines
- `GET /kb/guidelines/{id}` — Get a specific guideline
- `POST /kb/sync` — Trigger sync
- `GET /kb/sync/status` — Get last sync status and history

#### 4.2 Admin Dashboard Integration
Surface sync status and KB health in existing tooling or a simple admin page.

---

## File Summary

| File | Status | Description |
|------|--------|-------------|
| `src/_models.py` | ✅ Done | Content tracking fields on `Guideline` and `Example` |
| `src/_settings.py` | ✅ Done | `set()` method for writing to App Configuration |
| `src/_guideline_ingestor.py` | ✅ Done | Full sync pipeline with LLM enrichment, example sync, memory reconciliation |
| `cli.py` | ✅ Done | `avc db ingest-guidelines` command with all flags |
| `requirements.txt` | ✅ Done | Added `beautifulsoup4`, `markdown-it-py` |
| `dev_requirements.txt` | ✅ Done | Moved deps to runtime section |
| `prompts/other/parse_guidelines.prompty` | ✅ Done | LLM prompt for guideline parsing + example extraction |
| `prompts/other/guideline_parsing_result_schema.json` | ✅ Done | JSON schema for parsing structured output |
| `prompts/other/reconcile_memories.prompty` | ✅ Done | LLM prompt for memory absorption detection |
| `prompts/other/reconcile_memories_schema.json` | ✅ Done | JSON schema for reconciliation structured output |
| `tests/*_ingestor_test.py` | ❌ Missing | No tests for ingestion |
| `app.py` | ❌ Missing | No webhook/sync endpoints |
| `ci.yml` | ❌ Missing | No scheduled sync job |
