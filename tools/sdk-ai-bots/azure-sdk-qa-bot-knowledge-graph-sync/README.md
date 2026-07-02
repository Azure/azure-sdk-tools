# Azure SDK QA Bot — Knowledge Graph Sync

Python pipeline that builds a [GraphRAG](https://github.com/microsoft/graphrag) knowledge graph over the Azure SDK documentation corpus and publishes the resulting index artefacts for the QA bot's graph-retrieval tool.

This project does **one** thing: a **full GraphRAG build**. It reads the markdown the [`azure-sdk-qa-bot-knowledge-sync`](../azure-sdk-qa-bot-knowledge-sync/) project already maintains in the knowledge blob container, runs GraphRAG over it, and writes the parquet artefacts + AI Search vector index the bot reads at query time. Document collection / normalisation is **not** done here.

## Architecture

```
┌──────────────────────────┐
│  Knowledge Blob Container │   ← maintained by azure-sdk-qa-bot-knowledge-sync
│  (markdown docs)          │
└────────────┬─────────────┘
             │  (GraphRAG azure_blob input storage)
             ▼
  ┌──────────────────────┐
  │  GraphRAG full build │
  │  • Entity extraction │
  │  • Community detect   │
  │  • Embedding gen     │
  └──────────┬───────────┘
             │
   ┌─────────┴──────────┐
   ▼                    ▼
┌──────────────┐  ┌──────────────────────┐
│ Blob snapshot│  │  Azure AI Search     │
│ (parquets +  │  │  (vector store)      │
│  latest.json)│  │  • text_unit_text    │
└──────────────┘  │  • entity_description│
                  │  • community_content │
                  └──────────────────────┘
```

Each run writes parquets to a fresh timestamped sub-prefix
(`snapshots/<ts>/`) so old snapshots stay intact until `latest.json` is
flipped — the bot keeps serving the previous snapshot until the new
manifest is published, then hot-swaps it in on its next daily poll.

> **Full rebuild each run.** Every scheduled run rebuilds the whole
> graph from the current knowledge container, keeping entities and
> communities globally consistent.

## Query modes (used by the bot at runtime)

| Mode | Best For |
|------|----------|
| **Local Search** | Specific entity questions (fans out to neighbors + text chunks) |
| **Global Search** | Holistic questions requiring cross-document reasoning |
| **DRIFT Search** | Multi-hop reasoning with community context |
| **Basic Search** | Standard vector RAG (top-k text unit similarity) |

## Prerequisites

- Python 3.11+
- Azure credentials (DefaultAzureCredential / Managed Identity)
- Access to the knowledge blob container, Azure AI Search, and Azure OpenAI

## Setup

```bash
# Install with dev dependencies
pip install -e ".[dev]"

# Or production only
pip install -e .
```

## Usage

```bash
# Run a full GraphRAG build and publish the snapshot
sync-knowledge-graph
```

## Environment Variables

The pipeline reads its bootstrap endpoint from an environment variable;
everything else is pulled from Azure App Configuration and Azure Key Vault
at startup (see `azure_sdk_qa_bot_knowledge_graph_sync/services/app_config.py` and `azure_sdk_qa_bot_knowledge_graph_sync/services/app_secret.py`).

| Variable | Source | Description |
|----------|--------|-------------|
| `AZURE_APPCONFIG_ENDPOINT` | env | Azure App Configuration endpoint. All other config keys are loaded from here. |
| `KEYVAULT_ENDPOINT` | App Config | Azure Key Vault endpoint. Loaded from App Config, then secrets are exported to env. |
| `STORAGE_ACCOUNT_NAME` | App Config | Azure Storage account name. |
| `STORAGE_KNOWLEDGE_CONTAINER` | App Config | Blob container holding the markdown docs GraphRAG indexes. |
| `STORAGE_GRAPHRAG_OUTPUT_CONTAINER` | App Config | Destination container for parquet snapshots (e.g. `graphrag-output`). When unset, the post-indexing publish step degrades to a logged no-op. |
| `AI_SEARCH_BASE_URL` | App Config | Azure AI Search endpoint URL — referenced as `${AI_SEARCH_BASE_URL}` by `graphrag_config/settings.yaml`. |
| `AI_SEARCH_INDEX_TEXT_UNITS` | App Config | AI Search index for text unit embeddings. |
| `AI_SEARCH_INDEX_ENTITIES` | App Config | AI Search index for entity embeddings. |
| `AI_SEARCH_INDEX_COMMUNITIES` | App Config | AI Search index for community embeddings. |
| `AI_SEARCH_API_KEY` | Key Vault (`AI-SEARCH-APIKEY`) | AI Search admin key. |
| `AOAI_CHAT_COMPLETIONS_ENDPOINT` | App Config | Azure OpenAI endpoint (used by GraphRAG). |

The bot discovers new snapshots on its own by polling `latest.json` daily (configurable via `GRAPH_RELOAD_POLL_SECONDS` on the bot) — this pipeline does not call the bot.

## Testing

```bash
python -m pytest tests/ -v
```

## Project Structure

```
azure_sdk_qa_bot_knowledge_graph_sync/
├── main.py                       # CLI entry point (full GraphRAG build + publish)
├── services/
│   ├── app_config.py             # Azure App Configuration
│   ├── app_secret.py             # Key Vault secrets
│   └── storage_service.py        # Minimal Blob writer (latest.json manifest)
└── graphrag/
    ├── run_indexing.py           # GraphRAG full-build orchestration
    ├── publish_output.py         # latest.json manifest publish
    └── source_aware_reader.py    # input reader preserving source folder + path title

graphrag_config/
├── settings.yaml                 # GraphRAG config (AI Search vector store)
└── prompts/                      # Custom extraction prompts

tests/
└── test_source_aware_reader.py   # input-reader title/raw_data contract tests
```

## Key Design Decisions

- **GraphRAG as single indexing engine**: GraphRAG handles entity extraction, embedding generation, and vector store writes natively via its `azure_ai_search` vector store backend.
- **Blob-direct input**: GraphRAG reads markdown straight from the knowledge container via its native `azure_blob` input storage — no local download step.
- **Immutable snapshots**: Each build lands in its own timestamped prefix; `latest.json` is flipped last so the bot never reads a half-built snapshot and old snapshots remain for rollback.
- **Pull-based reload**: This pipeline only writes `latest.json`. The bot polls it on a daily schedule and hot-swaps the index when the `build_id` changes.
- **Managed Identity auth**: Uses Azure Managed Identity for Azure OpenAI and AI Search.

## Pipelines

| File | Purpose |
|------|---------|
| `ci.yml` | Build + tests on every PR and on `main` (path-scoped to this project). |
| `sync_knowledge_graph.yml` | Daily scheduled run (03:00 UTC) on an internal 1ES agent — installs the project, runs `sync-knowledge-graph`, and publishes the new parquet snapshot + `latest.json` to blob storage. The bot picks up the new snapshot on its own daily `latest.json` poll. |

## Relationship to Other Projects

- **[azure-sdk-qa-bot-agent](../azure-sdk-qa-bot-agent/)** — The QA bot that queries the knowledge graph at runtime using GraphRAG's query API (local/global/drift/basic search).
- **[azure-sdk-qa-bot-knowledge-sync](../azure-sdk-qa-bot-knowledge-sync/)** — Maintains the markdown docs in the knowledge blob container that this project indexes. Document collection lives there, not here.
