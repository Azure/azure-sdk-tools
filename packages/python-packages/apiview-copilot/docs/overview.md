# APIView Copilot (AVC) — Architecture Overview

APIView Copilot (AVC) is an AI-powered automated reviewer for Azure SDK API surface reviews. It ingests plain-text representations of SDK public APIs from [APIView](https://apiview.dev), runs a multi-stage LLM pipeline over them, and produces structured review comments that are surfaced directly in the APIView UI.

## High-Level Architecture

```
  ┌──────────────────────────────────────────────────────────────┐
  │  APIView (Web App)                                           │
  │  • Triggers review jobs via POST /api-review/start           │
  │  • Polls for results via GET /api-review/{job_id}            │
  └─────────────────────────────┬────────────────────────────────┘
                                │  HTTPS
                                ▼
  ┌──────────────────────────────────────────────────────────────┐
  │  AVC — FastAPI App (Azure App Service)                       │
  │  app.py — endpoints, job dispatch, auth, agent chat          │
  │                                                              │
  │  ┌────────────────────────────────────────────────────────┐  │
  │  │  ApiViewReview  (src/_apiview_reviewer.py)             │  │
  │  │  Multi-stage LLM review pipeline                       │  │
  │  └────────────────────────────────────────────────────────┘  │
  │                                                              │
  │  ┌────────────────┐  ┌────────────────┐  ┌───────────────┐   │
  │  │ SearchManager  │  │DatabaseManager │  │SettingsManager│   │
  │  │ (RAG/AI Search)│  │(Cosmos DB)     │  │(App Config)   │   │
  │  └────────────────┘  └────────────────┘  └───────────────┘   │
  └──────────────────────────────────────────────────────────────┘
```

## Core Components

| Component | File | Purpose |
|-----------|------|---------|
| FastAPI app | `app.py` | HTTP endpoints, background job dispatch, agent chat, @mention handling |
| CLI | `cli.py` | `avc` command-line tool for local development |
| Review pipeline | `src/_apiview_reviewer.py` | Sections API text, runs parallel prompts, filters, deduplicates, scores comments |
| Sectioning | `src/_sectioned_document.py` | Splits large API text into manageable chunks |
| Search / RAG | `src/_search_manager.py` | Azure AI Search integration for guideline/context retrieval |
| Database | `src/_database_manager.py` | Cosmos DB access for guidelines, memories, examples, jobs, metrics |
| Memory utilities | `src/_memory_utils.py` | Write-time deduplication and batch memory consolidation |
| Settings | `src/_settings.py` | Azure App Configuration + Key Vault resolution |
| Prompts | `prompts/` | `.prompty` template files organized by feature |
| Metadata | `metadata/<lang>/` | Per-language `filter.yaml` and `guidance.yaml` configuration |
| Agent | `src/agent/` | Azure AI Agent Service integration (read-only and read-write agents) |

## Azure Resource Dependencies

| Resource | Purpose |
|----------|---------|
| **Azure App Configuration** | Central config store; endpoint resolved from `ENVIRONMENT_NAME` |
| **Azure Key Vault** | Secrets referenced from App Configuration |
| **Azure Cosmos DB** | Stores guidelines, examples, memories, review jobs, metrics, evals |
| **Azure AI Search** | Semantic search index for RAG over the knowledge base |
| **Azure AI Foundry** | LLM backend (inference endpoint) for prompt execution |
| **Azure AI Agent Service** | Agent-based interactive chat |
| **Azure App Service** | Hosts the FastAPI application |
| **Azure Application Insights** | Telemetry and distributed tracing |

## Environments

Two environments are supported: `production` and `staging`. The environment is selected by setting the `ENVIRONMENT_NAME` environment variable. App Configuration endpoints are resolved automatically:

| Environment | App Config Endpoint |
|-------------|---------------------|
| `production` | `https://avc-appconfig.azconfig.io` |
| `staging` | `https://avc-appconfig-staging.azconfig.io` |

## Supported Languages

`android`, `clang`, `cpp`, `dotnet`, `golang`, `ios`, `java`, `python`, `rust`, `typescript`

## Related Documents

- [API Review Algorithm](./api-review.md) — Detailed description of the review pipeline stages
- [Knowledge Base](./kb.md) — How the knowledge base is structured and used
- [Metrics](./metrics.md) — Metrics collected, why, and how to access them
- [CLI Reference](./cli.md) — All `avc` CLI commands for local development
