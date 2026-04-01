# Codebase Structure

**Analysis Date:** 2024-12-19

## Directory Layout

```
azure-sdk-qa-bot-agent/
├── agents/                 # Hosted agent definitions (deployed to Foundry containers)
│   ├── chat_agent/         # Main QA bot agent
│   │   ├── agent.yaml      # Agent metadata (name)
│   │   ├── init.py         # Agent entrypoint (HTTP server on port 8088)
│   │   ├── instruction.md  # Agent persona and behavior instructions
│   │   └── Dockerfile      # Agent container build (separate from server)
│   └── feedback_agent/     # (Not yet implemented)
├── config/                 # Configuration loaders and tenant definitions
│   ├── app_config.py       # Azure App Configuration integration
│   └── tenant_config.py    # Tenant-specific knowledge sources and guidelines
├── models/                 # Pydantic data models
│   ├── chat.py             # ChatRequest, ChatResponse, Message, AdditionalInfo
│   ├── conversation.py     # ConversationMessage, ConversationMappingItem
│   ├── feedback.py         # FeedbackRequest, FeedbackResponse
│   ├── intention.py        # IntentionRequest, IntentionResponse
│   └── knowledge.py        # KnowledgeSource, KnowledgeChunk, Reference
├── pipelines/              # CI/CD pipeline definitions
│   ├── agent-cd.yml        # Agent container deployment pipeline
│   └── server-cd.yml       # Backend server deployment pipeline
├── prompts/                # Agent instructions and tenant-specific guidelines
│   ├── intention_classify.md  # Intention classification prompt
│   └── tenants/            # Per-tenant QA guidelines (e.g., typespec.md)
├── services/               # Business logic orchestration
│   ├── chat_service.py     # Agent invocation and response mapping
│   ├── conversation_service.py  # Conversation ID mapping (Cosmos DB)
│   ├── feedback_service.py # Feedback Excel file management (Blob Storage)
│   └── intention_service.py # Auto-reply classification (LLM-based)
├── tools/                  # Agent function calling tools
│   ├── __init__.py         # Tool registry with @tool decorator
│   ├── azsdk_mcp_tools.py  # Azure DevOps MCP tool (pipeline analysis)
│   ├── github_mcp_tools.py # GitHub MCP tool (PR, issues, files)
│   ├── knowledge_tools.py  # Knowledge base search tool
│   └── skills.py           # Tenant skill provider (progressive disclosure)
├── utils/                  # Azure SDK client singletons
│   ├── azure_ai_foundry.py # Agent client, project client, OpenAI client
│   ├── azure_ai_search.py  # Search client (agentic + vector search)
│   ├── azure_cosmosdb.py   # Cosmos DB containers (mappings, messages)
│   ├── azure_credential.py # Shared Azure credential singleton
│   ├── azure_memory_store.py  # (Not yet used, placeholder)
│   ├── azure_storage.py    # Blob storage upload/download
│   ├── llm.py              # LLM utility for intention classification
│   └── teams_image.py      # Teams image attachment download
├── tests/                  # Test files
│   ├── api_test.rest       # HTTP REST client tests
│   ├── azsdk_mcp_tools_test.py
│   ├── github_tools_test.py
│   ├── intention_service_test.py
│   └── knowledge_tools_test.py
├── server.py               # Backend FastAPI server entrypoint (port 8080)
├── requirements.txt        # Python dependencies
├── Dockerfile              # Backend server container build
└── README.md               # Setup and usage documentation
```

## Directory Purposes

**agents/:**
- Purpose: Self-contained agent definitions deployed to Azure AI Foundry as containers
- Contains: Agent entrypoint (`init.py`), instructions (`instruction.md`), metadata (`agent.yaml`)
- Key files: `agents/chat_agent/init.py` (agent HTTP server), `agents/chat_agent/instruction.md` (agent behavior)

**config/:**
- Purpose: Centralized configuration loading and tenant-specific settings
- Contains: Azure App Configuration integration, tenant knowledge source registry
- Key files: `config/app_config.py` (global settings), `config/tenant_config.py` (tenant definitions)

**models/:**
- Purpose: Pydantic data models for type-safe serialization and validation
- Contains: Request/response models, domain entities (Conversation, Knowledge)
- Key files: `models/chat.py` (main API shapes), `models/knowledge.py` (search results)

**services/:**
- Purpose: Business logic layer coordinating Azure resources and agent invocation
- Contains: Stateless service classes with async methods
- Key files: `services/chat_service.py` (agent orchestration), `services/conversation_service.py` (Cosmos DB persistence)

**tools/:**
- Purpose: Agent function calling tools (knowledge search, MCP tools for GitHub/ADO)
- Contains: `@tool`-decorated methods with JSON schema auto-generation
- Key files: `tools/knowledge_tools.py` (AI Search integration), `tools/__init__.py` (tool registry)

**utils/:**
- Purpose: Azure SDK client wrappers with singleton pattern and lifecycle management
- Contains: Client factories, credential management, resource-specific helpers
- Key files: `utils/azure_ai_foundry.py` (agent clients), `utils/azure_ai_search.py` (search clients), `utils/azure_cosmosdb.py` (Cosmos clients)

**prompts/:**
- Purpose: Natural language instructions for agent behavior and tenant-specific guidelines
- Contains: Markdown files loaded at runtime by agent or services
- Key files: `prompts/intention_classify.md`, `prompts/tenants/*.md` (per-tenant QA rules)

**pipelines/:**
- Purpose: Azure DevOps CI/CD pipeline definitions
- Contains: YAML pipeline files for agent and server deployments
- Key files: `pipelines/agent-cd.yml`, `pipelines/server-cd.yml`

**tests/:**
- Purpose: Unit and integration tests
- Contains: pytest test files, REST client files for manual testing
- Key files: `tests/api_test.rest` (HTTP endpoint tests), `tests/*_test.py` (unit tests)

## Key File Locations

**Entry Points:**
- `server.py`: Backend FastAPI server (port 8080) - invoked via `uvicorn server:app`
- `agents/chat_agent/init.py`: Hosted agent container (port 8088) - invoked via `python -m agents.chat_agent.init` or `agentdev run`

**Configuration:**
- `config/app_config.py`: Azure App Configuration loader (requires `AZURE_APPCONFIG_ENDPOINT` env var)
- `config/tenant_config.py`: Tenant knowledge source definitions and routing logic
- `.env` (not committed): Local environment variables (`AZURE_APPCONFIG_ENDPOINT`, `GITHUB_TOKEN`)

**Core Logic:**
- `services/chat_service.py`: Agent invocation, response parsing, reference extraction
- `tools/knowledge_tools.py`: Knowledge base search with hierarchy expansion
- `utils/azure_ai_search.py`: Agentic + vector search implementation

**Testing:**
- `tests/api_test.rest`: REST Client file for manual API testing
- `tests/*_test.py`: pytest test files

**Deployment:**
- `Dockerfile`: Backend server container (copies all `config/`, `models/`, `services/`, `utils/`, `server.py`)
- `agents/chat_agent/Dockerfile`: Agent container (copies all agent code)

## Naming Conventions

**Files:**
- Services: `{domain}_service.py` (e.g., `chat_service.py`, `conversation_service.py`)
- Utils: `azure_{service}.py` (e.g., `azure_ai_search.py`, `azure_cosmosdb.py`)
- Models: `{domain}.py` (e.g., `chat.py`, `knowledge.py`)
- Tools: `{source}_mcp_tools.py` for MCP tools, `{domain}_tools.py` for others

**Directories:**
- All lowercase with underscores: `chat_agent`, `conversation_service.py`
- No hyphens in Python module paths (use underscores)

**Classes:**
- PascalCase: `ChatService`, `ConversationService`, `KnowledgeTools`
- Suffix pattern: `*Service` for services, `*Tools` for tool classes

**Functions:**
- snake_case: `search_knowledge_base()`, `get_agent_client()`
- Private functions: `_unwrap_json()`, `_build_hierarchy_filter()`

**Constants:**
- SCREAMING_SNAKE_CASE: `TOOL_REGISTRY`, `KNOWLEDGE_SOURCE_REGISTRY`, `_DEFAULT_DATABASE_NAME`

## Where to Add New Code

**New Feature (backend endpoint):**
- Primary code: `services/{feature}_service.py` (business logic)
- Models: `models/{feature}.py` (request/response models)
- Endpoint: Add to `server.py` (`@app.post("/agent/{feature}")`)
- Tests: `tests/{feature}_service_test.py`

**New Agent Tool:**
- Implementation: `tools/{tool_name}_tools.py` with `@tool` decorator
- Model: `models/{domain}.py` if tool returns structured data
- Registration: Automatic via `tools/__init__.py` auto-import
- Agent config: Add tool instance to `tools` list in `agents/chat_agent/init.py`

**New Azure Integration:**
- Client wrapper: `utils/azure_{service}.py` with singleton pattern
- Client closure: Add to `close_clients()` in `utils/azure_{service}.py` and `server.py` lifespan
- Config: Add `{SERVICE}_ENDPOINT` or `{SERVICE}_*` keys to Azure App Configuration

**New Tenant:**
- Tenant ID: Add to `TenantID` enum in `config/tenant_config.py`
- Knowledge sources: Register sources in `KNOWLEDGE_SOURCE_REGISTRY` (if new)
- Tenant config: Add `TenantConfig` entry in `_TENANT_CONFIGS` dict
- QA guideline: Create `prompts/tenants/{tenant_name}.md`

**Utilities:**
- Shared helpers: `utils/{domain}.py` (e.g., `utils/llm.py` for LLM utilities)
- Keep utils stateless or singleton-based; no business logic

## Special Directories

**.venv/:**
- Purpose: Python virtual environment (created by `python -m venv .venv`)
- Generated: Yes (by developer)
- Committed: No (in `.gitignore`)

**__pycache__/:**
- Purpose: Python bytecode cache
- Generated: Yes (by Python interpreter)
- Committed: No (in `.gitignore`)

**.planning/:**
- Purpose: GSD planner documents and codebase analysis
- Generated: By GSD commands
- Committed: Yes (for team visibility)

**.foundry/:**
- Purpose: Azure AI Foundry local state (if present)
- Generated: By Foundry CLI tools
- Committed: No

**.vscode/:**
- Purpose: VS Code debug configurations
- Generated: By developer or Copilot
- Committed: Optional (typically yes for shared debug configs)

**images/:**
- Purpose: Documentation images (screenshots for README)
- Generated: By documentation authors
- Committed: Yes

**tsp/:**
- Purpose: TypeSpec definitions (possibly shared with backend)
- Generated: By service authors
- Committed: Yes

## Component Isolation

**Backend Server vs Agent Container:**
- Backend (`server.py`): Does NOT import `agents/` code, only calls agent via Azure AI Foundry SDK
- Agent (`agents/chat_agent/init.py`): Self-contained, imports `tools/`, `config/`, `utils/`, `models/`
- Shared code: `config/`, `models/`, `utils/` used by both

**Services vs Utils:**
- Services depend on utils, NOT vice versa
- Utils are pure infrastructure wrappers, no business logic
- Services orchestrate multiple utils and tools

**Tools vs Services:**
- Tools are called by the Agent Framework, return structured responses
- Services are called by HTTP endpoints, orchestrate agent and storage
- Tools can use utils directly but should not import services

---

*Structure analysis: 2024-12-19*
