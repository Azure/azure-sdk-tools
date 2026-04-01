# External Integrations

**Analysis Date:** 2024-12-19

## APIs & External Services

**Azure AI Foundry:**
- Azure AI Foundry Project - Agent hosting, LLM inference, memory store
  - SDK/Client: `azure-ai-projects>=2.0.0b4`, `agent-framework>=1.0.0rc3`
  - Auth: Managed Identity via `UMI_BACKEND_CLIENT_ID` or Azure CLI credential
  - Endpoint: `AI_FOUNDRY_PROJECT_ENDPOINT` (from App Config)
  - Models: `gpt-5.4` (chat completion), `text-embedding-ada-002` (memory embeddings)
  - Memory: Foundry Memory Store for conversation context (`utils/azure_memory_store.py`)
  - Files: `utils/azure_ai_foundry.py`, `agents/chat_agent/init.py`

**Azure AI Search:**
- Azure AI Search - Knowledge base retrieval with hybrid semantic + vector search
  - SDK/Client: `azure-search-documents>=11.7.0b2`
  - Auth: Shared credential from `utils.azure_credential`
  - Configuration: `AI_SEARCH_BASE_URL`, `AI_SEARCH_INDEX`, `AI_SEARCH_KNOWLEDGE_BASE`, `AI_SEARCH_KNOWLEDGE_SOURCE`
  - Search strategies: Agentic search (KnowledgeBaseRetrievalClient) + vector search (hybrid semantic)
  - Files: `utils/azure_ai_search.py`, `tools/knowledge_tools.py`

**GitHub API:**
- GitHub REST API & GitHub Copilot MCP Server - Repository, issue, PR, and pipeline analysis
  - SDK/Client: `httpx` for REST API, `agent-framework.MCPStdioTool` for MCP
  - Auth (production): GitHub App JWT signed via Azure Key Vault, exchanged for installation token
  - Auth (local dev): Personal access token via `GITHUB_TOKEN` env var
  - MCP server: `https://api.githubcopilot.com/mcp/`
  - Toolsets: repos, issues, actions, pull_requests (read-only)
  - Token refresh: 5-minute buffer before expiry (automatic background refresh)
  - Files: `tools/github_mcp_tools.py`

**Azure SDK MCP Server:**
- Azure SDK MCP Server - Pipeline analysis tools via stdio MCP protocol
  - SDK/Client: `agent-framework.MCPStdioTool`
  - Command: `azsdk mcp`
  - Auth: `AZURE_CLIENT_ID` from `UMI_BACKEND_CLIENT_ID`
  - Tools: `azsdk_analyze_pipeline`, `azsdk_get_pipeline_status`, `azsdk_get_pipeline_llm_artifacts`
  - Files: `tools/azsdk_mcp_tools.py`

**Microsoft Teams (Bot Framework):**
- Teams Image API - Inline image retrieval from Teams messages
  - Endpoint: `smba.trafficmanager.net` (HTTPS only, SSRF-protected)
  - Auth: Bot Framework bearer token via `BOT_CLIENT_ID` Managed Identity
  - Scope: `https://api.botframework.com/.default`
  - Token tenant: `BOT_TENANT_ID` env var
  - Files: `utils/teams_image.py`

## Data Storage

**Databases:**
- Azure Cosmos DB (NoSQL)
  - Connection: `AZURE_COSMOSDB_ENDPOINT` (from App Config)
  - Client: `azure-cosmos>=4.9.1` async SDK
  - Database: `azure-sdk-qa-bot` (default)
  - Containers: `conversation-mappings`, `conversation-messages`
  - Auth: Managed Identity credential
  - Files: `utils/azure_cosmosdb.py`, `services/conversation_service.py`

**File Storage:**
- Azure Blob Storage
  - Connection: `STORAGE_BASE_URL` (from App Config)
  - Client: `azure-storage-blob>=12.20.0` async SDK
  - Usage: Feedback records stored as monthly Excel files (`.xlsx`)
  - Container: Configured per tenant
  - Auth: Managed Identity credential
  - Files: `utils/azure_storage.py`, `services/feedback_service.py`

**Caching:**
- None - No explicit caching layer. Foundry Memory Store provides conversation context persistence.

## Authentication & Identity

**Auth Provider:**
- Azure Identity (Managed Identity + Azure CLI)
  - Implementation: Chained credential pattern
  - Production: `ManagedIdentityCredential(client_id=UMI_BACKEND_CLIENT_ID)` → `AzureCliCredential()`
  - Local dev: `AzureCliCredential()` only (skip MI to avoid IMDS timeout)
  - Multiple identities:
    - Backend: `UMI_BACKEND_CLIENT_ID` - Main app services
    - Frontend: `UMI_FRONTEND_CLIENT_ID` - GitHub operations
    - Bot: `BOT_CLIENT_ID` - Teams image retrieval
  - Files: `utils/azure_credential.py`

**Required Roles:**
- App Configuration Data Reader - On Azure App Configuration resource
- Storage Blob Data Contributor - On Azure Storage account
- Azure AI User - On Foundry Project
- Cosmos DB Built-in Data Contributor - On Cosmos DB account

## Monitoring & Observability

**Error Tracking:**
- OpenTelemetry tracing with custom span processor
  - Injects `microsoft.foundry.project.id` span attribute for Foundry Traces tab
  - Implementation: `_FoundryProjectIdProcessor` in `agents/chat_agent/init.py`

**Logs:**
- Structured logging via Python `logging` module
  - Format: `%(asctime)s %(levelname)s [RequestID: %(request_id)s] %(name)s: %(message)s`
  - Request ID injected via FastAPI middleware and context variables
  - Output: stdout (captured by Azure App Service / Foundry)
  - Configuration: `server.py:_configure_logging()`

## CI/CD & Deployment

**Hosting:**
- Azure App Service (Linux container) - Backend server (`server.py` on port 8080)
- Azure AI Foundry - Hosted agent (`agents/chat_agent/` on port 8088)
- Azure Container Registry - Image storage

**CI Pipeline:**
- Azure Pipelines (YAML)
  - Agent deployment: `pipelines/agent-cd.yml`
  - Server deployment: `pipelines/server-cd.yml`
  - Environments: dev, preview, prod
  - Build: Docker multi-stage build via `Dockerfile`
  - Deploy: ACR push + App Service update (server) or Foundry agent deploy (agent)
  - Health check: `/ping` endpoint validation post-deployment

**Deployment Scripts:**
- `scripts/deploy_hosted_agent.py` - Automated agent deployment to Foundry

## Environment Configuration

**Required env vars (loaded from Azure App Configuration at runtime):**
- `AI_FOUNDRY_PROJECT_ENDPOINT` - Foundry project endpoint
- `AI_FOUNDRY_AGENT_COMPLETION_MODEL` - Chat model name (default: `gpt-5.4`)
- `AI_SEARCH_BASE_URL` - AI Search endpoint
- `AI_SEARCH_INDEX` - Search index name
- `AI_SEARCH_KNOWLEDGE_BASE` - Knowledge base name
- `AI_SEARCH_KNOWLEDGE_SOURCE` - Knowledge source name
- `AI_SEARCH_TOPK` - Number of results to retrieve
- `AZURE_COSMOSDB_ENDPOINT` - Cosmos DB endpoint
- `STORAGE_BASE_URL` - Blob storage account URL
- `MEMORY_STORE_NAME` - Foundry memory store name (default: `azure-sdk-qa-bot-memory-store`)
- `MEMORY_UPDATE_DELAY` - Memory update delay in seconds (default: 300)
- `MEMORY_STORE_EMBEDDING_MODEL` - Embedding model for memory (default: `text-embedding-ada-002`)
- `MEMORY_STORE_SCOPE` - Memory scope identifier (default: `azure-sdk-qa-bot`)
- `AOAI_CHAT_REASONING_MODEL` - Reasoning model for memory (default: `gpt-5.4`)

**Secrets location:**
- Azure App Configuration - Non-sensitive configuration
- Azure Key Vault - Sensitive keys (GitHub App private key for JWT signing)
- Environment variables - Identity client IDs, local dev tokens

## Webhooks & Callbacks

**Incoming:**
- `/agent/chat` (POST) - Chat message processing
- `/agent/feedback` (POST) - User feedback submission
- `/message/intention` (POST) - Message intention classification
- `/conversation/save` (POST) - Conversation message persistence
- `/ping` (GET) - Health check endpoint
- Legacy: `/completion` (POST), `/feedback` (POST) - Backwards compatibility

**Outgoing:**
- GitHub API - Issue creation for negative feedback (planned/implemented in feedback workflow)
- Azure AI Foundry - Agent invocation via Responses protocol
- Azure AI Search - Knowledge retrieval queries
- Teams Bot Framework - Image download from Teams messages

---

*Integration audit: 2024-12-19*
