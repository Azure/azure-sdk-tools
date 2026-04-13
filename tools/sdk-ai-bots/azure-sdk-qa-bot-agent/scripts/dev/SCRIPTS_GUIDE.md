# Scripts Guide

Personal-use scripts for debugging, testing, and populating the Azure SDK QA Bot memory system.

> **Not intended for commit** — these are local development/diagnostic tools.

## Prerequisites

All scripts run from the project root with the virtual environment activated:

```bash
cd tools/sdk-ai-bots/azure-sdk-qa-bot-agent
.venv\Scripts\activate        # Windows
# source .venv/bin/activate   # Linux/Mac
```

---

## 1. debug_memory.py

**CLI tool to inspect, query, and manage memory stores.**

### Usage

```bash
# Query user-scoped memories
python scripts/dev/debug_memory.py --user-id "mock-user-001" --query "What SDK do you use?"

# Send a test message to extract memories from
python scripts/dev/debug_memory.py --user-id "mock-user-001" --update "I work on the Python SDK"

# Delete and recreate the user store
python scripts/dev/debug_memory.py --recreate

# Launch the web dashboard
python scripts/dev/debug_memory.py --web
```

### Flags

| Flag | Default | Description |
|------|---------|-------------|
| `--user-id` | — | Teams user ID; auto-formatted as `user_{id}` (required for most operations) |
| `--query` | — | Search query for contextual memory retrieval |
| `--update` | — | Test message to send for memory extraction |
| `--store` | from config | Override memory store name |
| `--recreate` | `False` | Delete and recreate the memory store |
| `--web` | `False` | Launch the web dashboard |
| `--port` | `8501` | Port for the web dashboard |

### Notes

- Scopes are sanitized to `[A-Za-z0-9_-]{1,256}`

---

## 2. mock_teams_chat.py

**Interactive CLI chat client that simulates Teams messages against the server or local agent.**

### Usage

```bash
# Chat via the backend server (default)
python scripts/dev/mock_teams_chat.py --user-id "alice_123" --user-name "Alice"

# Chat via the local agent directly
python scripts/dev/mock_teams_chat.py --local --tenant azure_sdk_onboarding

# Specify a custom server URL
python scripts/dev/mock_teams_chat.py --server http://my-server:8080
```

### Flags

| Flag | Default | Description |
|------|---------|-------------|
| `--local` | `False` | Send to local agent (port 8088) instead of server |
| `--server` | `http://localhost:8080` | Server URL (`8088` if `--local`) |
| `--user-id` | `mock-user-001` | Simulated Teams user ID |
| `--user-name` | `MockUser` | Display name |
| `--tenant` | `azure_sdk_qa_bot` | Tenant ID (from predefined list) |
| `--conversation-id` | auto-generated | Override conversation ID |
| `--conversation-type` | `teams_channel` | Conversation type |
| `--full-context` | `False` | Request full context in response (server mode) |

### Interactive Commands

| Command | Effect |
|---------|--------|
| `/new` | Start a new conversation (fresh ID) |
| `/tenant <id>` | Switch tenant |
| `/user <id> [name]` | Switch user ID and optionally display name |
| `/save <content>` | POST a `ConversationMessage` to `/conversation/save` (triggers episode extraction) |
| `/status` | Show current settings |
| `/tenants` | List available tenant IDs |
| `quit` / `exit` | Exit |

### Notes

- **Server mode** sends `ChatRequest` JSON to `/agent/chat`
- **Local mode** sends Responses protocol messages to `/responses`, injecting `[tenant_context]` and `[memory_scope]` system messages
- Timeout is 120 seconds per request

---

## 3. episode_dashboard.html

**Web UI for browsing expert experience episodes from Cosmos DB.** Served by `debug_memory.py --web`.

### Usage

```bash
# Start the dashboard server
python scripts/dev/debug_memory.py --web --port 8501

# Open in browser
# → http://localhost:8501
```

### Features

- Filter episodes by **tenant** (dropdown)
- Search by **conversation ID** (substring match)
- Expandable episode cards showing trigger, symptoms, reasoning chain, resolution, and key insight
- Confidence badge (green ≥70%, yellow <70%)

### API Endpoints

| Endpoint | Parameters | Description |
|----------|-----------|-------------|
| `/api/config` | — | Returns tenant list |
| `/api/episodes` | `tenant_id`, `thread_id` (both optional) | Query episodes from Cosmos DB |

### Notes

- The HTML file is self-contained (inline CSS/JS, no dependencies)
- Queries Cosmos DB `experience-episodes` container directly
- Episodes are returned without embeddings for efficiency

---

## 4. extract_historical_messages.py

**Parses Teams channel markdown exports into structured JSON files (one per tenant).**

### Usage

```bash
# Parse markdown and write JSON
python scripts/dev/extract_historical_messages.py

# Preview without writing files
python scripts/dev/extract_historical_messages.py --dry-run
```

### Flags

| Flag | Default | Description |
|------|---------|-------------|
| `--dry-run` | `False` | Parse and report stats without writing output |

### Input / Output

- **Input**: `historical_messages/*.md` — markdown files with Teams channel posts
- **Output**: `historical_messages/json/{tenant_id}.json` — one JSON file per tenant

### Expected Markdown Format

```markdown
# {Channel Name} Teams Channel Posts
tenant_id: {default_tenant_id}
---
## Thread Title
**From:** Poster Name　　**Time:** 2024-01-15 14:30　　[View post](teams_url)

Question body here...

### Replies
> **Replier Name** · 2024-01-15 15:00
> Reply content here...

---
```

### Channel → Tenant Mapping

| Channel | Tenant ID |
|---------|-----------|
| Azure SDK Onboarding | `azure_sdk_onboarding` |
| API Spec Review | `api_spec_review_bot` |
| TypeSpec Discussion | `azure_sdk_qa_bot` |
| General | `general_qa_bot` |
| Language – JS ＆ TS 🥷 | `javascript_channel_qa_bot` |
| Language - Java | `java_channel_qa_bot` |
| Language - Go | `golang_channel_qa_bot` |
| Language - Python | `python_channel_qa_bot` |
| Language - .NET | `dotnet_channel_qa_bot` |

### Redirect Detection

If a bot reply contains `💬 Not resolved? Please re-post in the 👉 [{Channel Name}](...)`, the entire thread is reassigned to the target channel's tenant.

### Notes

- Bot identified by sender name `"Azure SDK Q&A Bot"` → `sender_role: "system"`
- All messages in a thread are sorted chronologically
- Message IDs are deterministic (SHA-256 based)

---

## 5. batch_extract_episodes.py

**Extracts structured episodes from historical Teams threads and stores them in Cosmos DB.**

### Usage

```bash
# Preview what would be extracted (no API calls)
python scripts/dev/batch_extract_episodes.py --dry-run

# Extract from a single file
python scripts/dev/batch_extract_episodes.py --file historical_messages/TypeSpec_Discussion.md

# Extract all with a limit
python scripts/dev/batch_extract_episodes.py --limit 10

# Full extraction
python scripts/dev/batch_extract_episodes.py
```

### Flags

| Flag | Default | Description |
|------|---------|-------------|
| `--dry-run` | `False` | Parse and report stats without calling the LLM |
| `--file` | all | Process only this MD file |
| `--limit` | all | Max threads to process |
| `--delay` | `5` | Seconds to wait between LLM calls |
| `--output` | — | Write a report to this file |
| `--verbose` | `False` | Print extracted episode details |

### Input

Reads MD files from `historical_messages/*.md` (Teams channel exports).

### Behavior

1. Parses threads from MD files using `scripts/dev/md_thread_parser.py`
2. Filters to qualifying threads (≥4 messages with expert reply)
3. For each thread, calls `ThreadMemoryService._extract_episode()`
4. Episode is embedded and stored in the `experience-episodes` Cosmos DB container

### Notes

- Replaces `populate_tenant_memories.py` (tenant memory store has been removed)
- Uses the shared MD parser in `scripts/dev/md_thread_parser.py`
- Progress logged per thread; summary at the end

---

## Typical Workflow

```bash
# 1. Extract historical Teams posts to JSON (if not already done)
python scripts/dev/extract_historical_messages.py

# 2. Preview episode extraction
python scripts/dev/batch_extract_episodes.py --dry-run

# 3. Extract episodes from historical threads
python scripts/dev/batch_extract_episodes.py

# 4. Inspect user memories via dashboard
python scripts/dev/debug_memory.py --web
# → Open http://localhost:8501

# 5. Test with a simulated chat
python scripts/dev/mock_teams_chat.py --tenant azure_sdk_onboarding --user-id "test-user"
```
