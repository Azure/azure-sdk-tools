# Architecture

**Analysis Date:** 2024-12-19

## Pattern Overview

**Overall:** Dual-deployment microservice architecture with Azure AI Foundry agent containerization

**Key Characteristics:**
- Separation between backend API server (FastAPI) and hosted AI agent (Agent Framework container)
- Backend orchestrates agent calls via Azure AI Foundry SDK, not direct invocation
- Heavy reliance on Azure managed services for state, search, and LLM capabilities
- Event-driven conversation management with Cosmos DB persistence
- Service layer coordinates cross-cutting Azure resource access

## Layers

**Entry Points:**
- Purpose: Application initialization and HTTP request handling
- Location: `server.py` (backend API), `agents/chat_agent/init.py` (hosted agent container)
- Contains: FastAPI app, lifespan management, middleware, agent server initialization
- Depends on: Services layer, config layer, utils layer
- Used by: External HTTP clients (Teams app, REST clients)

**Services:**
- Purpose: Business logic orchestration and workflow management
- Location: `services/`
- Contains: ChatService, ConversationService, FeedbackService, IntentionService
- Depends on: Utils (Azure clients), models, config, tools
- Used by: Entry points (server.py endpoints)

**Tools:**
- Purpose: AI agent function calling capabilities
- Location: `tools/`
- Contains: KnowledgeTools, MCP tools (GitHub, Azure DevOps), Skills provider
- Depends on: Utils (Azure clients), config, models
- Used by: Agent Framework (via `agents/chat_agent/init.py`)

**Utils:**
- Purpose: Azure SDK client singletons and resource access
- Location: `utils/`
- Contains: Azure AI Foundry, AI Search, Cosmos DB, Storage, credential clients
- Depends on: Config layer, models
- Used by: Services, tools

**Config:**
- Purpose: Centralized configuration from Azure App Configuration and tenant definitions
- Location: `config/`
- Contains: `app_config.py` (global settings), `tenant_config.py` (tenant-specific knowledge sources)
- Depends on: Utils (credential), models
- Used by: All layers

**Models:**
- Purpose: Pydantic data models and type definitions
- Location: `models/`
- Contains: ChatRequest/Response, Conversation, Feedback, Knowledge, Intention models
- Depends on: Nothing (pure data structures)
- Used by: All layers

**Prompts:**
- Purpose: Agent instructions and tenant-specific guidelines
- Location: `prompts/`
- Contains: `instruction.md` (agent persona), `tenants/` (per-tenant QA guidelines)
- Depends on: Nothing (static text files)
- Used by: Agent initialization, tenant routing logic

## Data Flow

**Chat Request Flow:**

1. **HTTP POST** → `server.py` `/agent/chat` endpoint receives `ChatRequest`
2. **ChatService.chat()** resolves or creates Azure AI Foundry conversation ID
3. **Conversation mapping** retrieved/stored via `ConversationService` → Cosmos DB
4. **Tenant system message** built from `tenant_config.py` and injected into conversation
5. **Agent invocation** via `openai_client.responses.create()` with conversation context
6. **Agent execution** (in separate container):
   - Loads skill via `SkillsProvider` based on tenant context
   - Calls `search_knowledge_base` tool → `KnowledgeTools` → `SearchClient` → Azure AI Search
   - Expands search results by header hierarchy (parallel queries to AI Search)
   - Calls GitHub/ADO MCP tools if URLs detected
   - Returns structured response with references
7. **Response postprocessing** in `ChatService`:
   - Extract tool results from agent output
   - Parse **References** section from markdown
   - Enrich references with source metadata
   - Detect tenant routing from `load_skill` calls
8. **HTTP Response** → `ChatResponse` with answer, references, optional full_context

**Feedback Flow:**

1. **HTTP POST** → `server.py` `/agent/feedback` endpoint receives `FeedbackRequest`
2. **FeedbackService.process()** appends feedback to monthly Excel file in Azure Blob Storage
3. If reaction is "bad", creates GitHub issue (TODO: not yet implemented)
4. Returns `FeedbackResponse` with saved status and optional issue URL

**Intention Classification Flow:**

1. **HTTP POST** → `server.py` `/message/intention` endpoint receives `IntentionRequest`
2. **IntentionService.classify()** uses LLM to determine if bot should auto-reply
3. Checks `ConversationService.has_expert_reply()` to avoid bot spam in threads with human responses
4. Returns `IntentionResponse` with classification result

**State Management:**
- Conversation mappings: Cosmos DB (`conversation-mappings` container, partitioned by conversation type)
- Conversation messages: Cosmos DB (`conversation-messages` container, partitioned by `{type}:{id}`)
- Agent conversation state: Azure AI Foundry Memory (managed by platform)
- Feedback records: Azure Blob Storage (monthly Excel files)
- Configuration: Azure App Configuration (loaded once at startup)

## Key Abstractions

**Agent Framework Agent:**
- Purpose: Represents the AI agent with tools, instructions, and context providers
- Examples: `agents/chat_agent/init.py`
- Pattern: Builder pattern with `Agent(agent_client, name, id, instructions, tools, context_providers)`

**Service Singletons:**
- Purpose: Stateless service instances created once at server startup
- Examples: `_chat_service`, `_conversation_service`, `_feedback_service`, `_intention_service` in `server.py`
- Pattern: Module-level singleton instances, no shared mutable state

**Azure Client Singletons:**
- Purpose: Lazily-initialized, globally-shared Azure SDK clients
- Examples: `utils/azure_ai_foundry.py`, `utils/azure_cosmosdb.py`, `utils/azure_ai_search.py`
- Pattern: Global variable with lock-protected initialization, `get_*()` accessor functions

**Tool Registry:**
- Purpose: Auto-register agent tools by decorator and infer response models from return type hints
- Examples: `tools/__init__.py`, `@tool` decorator on `KnowledgeTools.search_knowledge_base`
- Pattern: Decorator-based registration with `TOOL_REGISTRY` dict mapping tool name → response model

**Tenant Configuration:**
- Purpose: Encapsulate tenant-specific knowledge sources, guidelines, and routing rules
- Examples: `config/tenant_config.py` with `TenantConfig` dataclass per tenant
- Pattern: Enum-based tenant IDs, global registry of knowledge sources, per-tenant config lookup

**Knowledge Source:**
- Purpose: Represents a searchable knowledge base with name, description, and OData filter
- Examples: `models/knowledge.py` `KnowledgeSource` dataclass
- Pattern: Immutable dataclass with `get_link()` method to resolve document URLs

## Entry Points

**Backend Server (server.py):**
- Location: `server.py`
- Triggers: HTTP requests from Teams app or REST clients
- Responsibilities:
  - FastAPI app with CORS and request ID middleware
  - Lifespan management: load config on startup, close clients on shutdown
  - Endpoint routing: `/agent/chat`, `/agent/feedback`, `/message/intention`, `/conversation/save`
  - Delegates business logic to service layer

**Hosted Agent Container (agents/chat_agent/init.py):**
- Location: `agents/chat_agent/init.py`
- Triggers: Deployed to Azure AI Foundry as containerized agent, invoked by backend via Responses API
- Responsibilities:
  - Load agent instructions from `instruction.md`
  - Initialize Agent Framework with tools (KnowledgeTools, MCP tools, web search)
  - Register `SkillsProvider` for tenant-specific context injection
  - Start HTTP server on port 8088 using Responses protocol
  - Configure OpenTelemetry tracing with Foundry project ID injection

## Error Handling

**Strategy:** Defensive with fallback to partial success

**Patterns:**
- **Try-except with logging:** Azure client calls wrapped in try-except, log warnings, continue processing
- **Graceful degradation:** Search failures don't block response; collected via `asyncio.gather(..., return_exceptions=True)`
- **404 detection:** Cosmos DB 404 status code → return None instead of raising exception
- **Tool result validation:** Missing or malformed tool outputs logged as warnings, not fatal errors
- **HTTP middleware:** Request ID context variable for traceable logs across async boundaries

## Cross-Cutting Concerns

**Logging:** 
- Structured logging with `logging` module
- Request ID injected via `ContextVar` and middleware (`server.py`)
- All Azure client operations logged at INFO level
- Tool failures logged at WARNING level

**Validation:** 
- Pydantic models for all request/response shapes
- FastAPI automatic validation on endpoint entry
- Azure client credentials validated at first use (fail-fast on missing config)

**Authentication:** 
- Azure DefaultAzureCredential chain (managed identity → CLI → environment)
- Shared credential instance from `utils/azure_credential.py`
- GitHub token: environment variable (`GITHUB_TOKEN`) or Key Vault in production

**Configuration:**
- Centralized in `config/app_config.py`
- All settings loaded from Azure App Configuration at startup
- `get(key, default)` accessor prevents direct `os.getenv` calls
- Tenant-specific overrides in `config/tenant_config.py`

**Tracing:**
- OpenTelemetry with Application Insights exporter (agent container only)
- Custom `SpanProcessor` injects `microsoft.foundry.project.id` attribute for Foundry Traces UI
- Agent version appended to agent ID for per-version trace filtering

**Resource Cleanup:**
- FastAPI lifespan context manager closes all Azure clients on shutdown
- Async context managers (`__aenter__`/`__aexit__`) for Cosmos client
- Explicit `close()` methods for all singleton clients

---

*Architecture analysis: 2024-12-19*
