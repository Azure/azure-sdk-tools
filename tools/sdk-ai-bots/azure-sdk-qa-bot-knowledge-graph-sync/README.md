# Azure SDK QA Bot — Knowledge Graph Sync

Python-based knowledge graph sync pipeline for the Azure SDK QA Bot. This project:

1. **Syncs documentation** from multiple repositories into Azure Blob Storage
2. **Builds a knowledge graph** using Microsoft's [GraphRAG](https://github.com/microsoft/graphrag) library with Azure AI Search as the native vector store
3. **Incremental indexing** — uses GraphRAG's built-in `update` command to only re-process documents that changed

## Architecture

```
┌─────────────────┐     ┌──────────────┐
│  Git Repos      │────▶│  Blob Store  │
│  (TypeSpec,     │     │  (markdown)  │
│   Guidelines)   │     └──────┬───────┘
└─────────────────┘            │
                               ▼
                    ┌──────────────────────┐
                    │  GraphRAG Pipeline   │
                    │  • Entity extraction │
                    │  • Community detect  │
                    │  • Embedding gen     │
                    └──────────┬───────────┘
                               │
                    ┌──────────▼───────────┐
                    │  Azure AI Search     │
                    │  (vector store)      │
                    │  • text_unit_text    │
                    │  • entity_description│
                    │  • community_content │
                    └──────────────────────┘
```

**Key insight**: GraphRAG natively supports Azure AI Search as a vector store. Instead of maintaining a separate search indexing pipeline, GraphRAG handles all vector embedding, chunking, and index writes automatically during its `index`/`update` commands.

## Incremental Indexing

Uses GraphRAG's native `update` command for incremental processing:

1. **Doc sync** identifies which blob paths changed and which were deleted
2. **Deleted files** are removed from `graphrag_config/input/`
3. **Changed files** are downloaded (additive) to `graphrag_config/input/`
4. **`graphrag update`** processes only new/modified documents and merges results into existing graph
5. **Update output** is merged back into the main output directory for subsequent runs

If no prior index exists, the system automatically falls back to a full `graphrag index`.

Use `--full-graphrag` to force a complete rebuild when needed (e.g., after changing extraction prompts).

## Query Modes

GraphRAG provides four built-in query modes (used by the bot agent at runtime):

| Mode | Best For |
|------|----------|
| **Local Search** | Specific entity questions (fans out to neighbors + text chunks) |
| **Global Search** | Holistic questions requiring cross-document reasoning |
| **DRIFT Search** | Multi-hop reasoning with community context |
| **Basic Search** | Standard vector RAG (top-k text unit similarity) |

## Prerequisites

- Python 3.11+
- Azure credentials (DefaultAzureCredential / Managed Identity)
- Access to Azure Blob Storage, Azure AI Search, and Azure OpenAI

## Setup

```bash
# Install with dev dependencies
pip install -e ".[dev]"

# Or production only
pip install -e .
```

## Usage

```bash
# Normal daily run: sync docs + incremental graph update
sync-knowledge-graph

# Sync docs only (skip graph indexing)
sync-knowledge-graph --skip-graphrag

# GraphRAG only (skip doc sync, use existing blobs)
sync-knowledge-graph --graphrag-only

# Force full graph rebuild (re-indexes all sources)
sync-knowledge-graph --full-graphrag

# Specific sources for full re-index
sync-knowledge-graph --full-graphrag --sources typespec_docs,azure_api_guidelines
```

## Environment Variables

| Variable | Description |
|----------|-------------|
| `AZURE_APP_CONFIG_ENDPOINT` | Azure App Configuration endpoint |
| `STORAGE_ACCOUNT_NAME` | Azure Storage account name |
| `STORAGE_KNOWLEDGE_CONTAINER` | Blob container for processed docs |
| `AI_SEARCH_ENDPOINT` | Azure AI Search endpoint URL |
| `AI_SEARCH_INDEX_TEXT_UNITS` | AI Search index for text unit embeddings |
| `AI_SEARCH_INDEX_ENTITIES` | AI Search index for entity embeddings |
| `AI_SEARCH_INDEX_COMMUNITIES` | AI Search index for community embeddings |
| `AZURE_OPENAI_ENDPOINT` | Azure OpenAI endpoint (for LLM + embeddings) |
| `SSH_PRIVATE_KEY` | Base64-encoded SSH private key (for private repos) |
| `STORAGE_GRAPHRAG_OUTPUT_CONTAINER` | Destination container for parquet snapshots (e.g. `graphrag-output`). When unset, the post-indexing publish step degrades to a logged no-op. |
| `BOT_AGENT_RELOAD_URL` | Bot agent reload endpoint (e.g. `https://<bot>/admin/graphrag/reload`) |
| `BOT_AGENT_ADMIN_TOKEN` | Shared secret sent as `X-Admin-Token` to the bot reload endpoint |

## Testing

```bash
python -m pytest tests/ -v
```

## Project Structure

```
src/
├── main.py                     # CLI entry point
├── daily_sync.py               # Main sync orchestrator (returns SyncResult)
├── services/
│   ├── app_config.py           # Azure App Configuration
│   ├── app_secret.py           # Key Vault secrets
│   ├── configuration_loader.py # Config parser
│   ├── metadata_resolver.py    # Glob-based metadata
│   ├── storage_service.py      # Blob Storage CRUD + download helpers
│   ├── spector_processor.py    # TypeSpec scenarios (OpenAI)
│   └── typespec_processor.py   # TypeSpec AST → markdown
└── graphrag/
    └── run_indexing.py          # GraphRAG pipeline orchestration

graphrag_config/
├── settings.yaml               # GraphRAG config (AI Search vector store)
└── prompts/                    # Custom extraction prompts

config/
├── knowledge-config.json       # Repository and documentation sources
└── knowledge-config.schema.json

tests/
├── test_daily_sync.py          # Core function tests
└── test_configuration_loader.py # Config loader tests
```

## Key Design Decisions

- **GraphRAG as single indexing engine**: No custom search indexing or Cosmos upload code. GraphRAG handles entity extraction, embedding generation, and vector store writes natively via its `azure_ai_search` vector store backend.
- **Native incremental update**: Uses `graphrag update` instead of custom change-tracking logic for the graph. The doc sync still detects file-level changes to minimize unnecessary downloads.
- **Blob Storage as source of truth**: Raw processed markdown is stored in blobs. GraphRAG reads from a local `input/` directory populated from these blobs.
- **Managed Identity auth**: Uses Azure Managed Identity for both Azure OpenAI and AI Search (no API keys in config).
- **12 entity types**: Decorator, Pattern, Tool, Service, API, ErrorCode, Guideline, Library, Operation, Model, Configuration, Protocol

## Relationship to Other Projects

- **[azure-sdk-qa-bot-agent](../azure-sdk-qa-bot-agent/)** — The QA bot that queries the knowledge base at runtime using GraphRAG's query API (local/global/drift/basic search).
- **[azure-sdk-qa-bot-knowledge-sync](../azure-sdk-qa-bot-knowledge-sync/)** — The original TypeScript implementation (doc sync only, no graph). This Python project is a full port + GraphRAG replacement.
