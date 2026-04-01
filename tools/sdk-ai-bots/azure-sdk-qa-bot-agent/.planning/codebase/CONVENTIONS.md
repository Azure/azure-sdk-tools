# Coding Conventions

**Analysis Date:** 2025-01-04

## Naming Patterns

**Files:**
- Python modules use lowercase with underscores: `chat_service.py`, `intention_service.py`, `azure_cosmosdb.py`
- Test files use `_test.py` suffix: `intention_service_test.py`, `knowledge_tools_test.py`
- Configuration files use descriptive names: `app_config.py`, `tenant_config.py`
- Tool modules use `_tools.py` suffix: `knowledge_tools.py`, `azsdk_mcp_tools.py`, `github_mcp_tools.py`

**Functions:**
- Use snake_case for all functions: `get_credential()`, `search_knowledge_base()`, `load_tenant_qa_guideline()`
- Private functions prefix with underscore: `_load_classify_prompt()`, `_build_mapping_key()`, `_unwrap_json()`
- Async functions use `async def` prefix: `async def init()`, `async def classify()`, `async def chat()`
- Internal helper functions in modules: `_register()`, `_trim_file_format()`, `_get_endpoint()`

**Variables:**
- snake_case for regular variables: `agent_name`, `conversation_id`, `project_client`
- Private module-level constants use underscore prefix: `_PROMPTS_DIR`, `_DEFAULT_DATABASE_NAME`, `_HEADERS`
- Module singletons use leading underscore: `_credential`, `_client`, `_settings`, `_agent_client`
- Context variables for request tracking: `_request_id_ctx_var`

**Classes:**
- PascalCase for class names: `ChatService`, `IntentionService`, `KnowledgeTools`, `ConversationService`
- Pydantic models use PascalCase: `ChatRequest`, `ChatResponse`, `IntentionRequest`, `Message`
- Enum classes inherit from `str, Enum`: `class TenantID(str, Enum)`, `class Role(str, Enum)`
- Service classes end with `Service`: `ChatService`, `FeedbackService`, `ConversationService`, `IntentionService`
- Tool classes end with `Tools`: `KnowledgeTools`

**Types:**
- Enum values use snake_case: `teams_channel`, `typespec_channel_qa_bot`, `management_plane`
- Constants use SCREAMING_SNAKE_CASE for source names: `SRC_TYPESPEC_DOCS`, `SRC_AZURE_API_GUIDELINES`

## Code Style

**Formatting:**
- No explicit formatter configuration detected (no `.black`, `.prettierrc`, `pyproject.toml` with formatting)
- Line length appears to be ~88-100 characters based on observed code
- String quotes: Double quotes preferred for docstrings, both single and double used for regular strings
- Indentation: 4 spaces (standard Python)
- Blank lines: Two blank lines between top-level definitions, one within classes

**Linting:**
- No linting configuration files detected (no `.flake8`, `.pylintrc`, `setup.cfg`)
- Code follows PEP 8 conventions by inspection

## Import Organization

**Order:**
1. Future imports: `from __future__ import annotations`
2. Standard library: `import json`, `import logging`, `from pathlib import Path`
3. Third-party packages: `from azure.ai.projects.aio import AIProjectClient`, `from pydantic import BaseModel`
4. Local application imports: `from config.app_config import get as cfg`, `from models.chat import Message`

**Patterns:**
```python
from __future__ import annotations

import asyncio
import logging
from pathlib import Path

from azure.cosmos.aio import CosmosClient
from pydantic import BaseModel

from config.app_config import get as cfg
from utils.azure_credential import get_credential
```

**Path Aliases:**
- No path aliases detected
- All imports use relative package names: `from models.chat import`, `from services.intention_service import`
- Project root added to sys.path in test files for imports to work

## Error Handling

**Patterns:**
- Try-except for Azure SDK calls with status code checking:
```python
try:
    raw = await container.read_item(item=id, partition_key=key)
except Exception as exc:
    if getattr(exc, "status_code", None) == 404:
        return None
    raise
```

- Graceful fallback on LLM errors:
```python
try:
    response = await openai_client.chat.completions.create(...)
    return IntentionResponse.model_validate_json(raw)
except Exception:
    logger.exception("LLM intention classification failed, defaulting to respond")
    return IntentionResponse(should_respond=True, reason="llm_error_default_respond")
```

- RuntimeError for configuration errors:
```python
if not endpoint:
    raise RuntimeError("AZURE_APPCONFIG_ENDPOINT environment variable is required.")
```

- Custom exceptions defined inline:
```python
class LLMError(Exception):
    """Raised when an LLM prompt execution fails."""
```

- Exception tolerance in parallel operations:
```python
results = await asyncio.gather(*tasks, return_exceptions=True)
for result in results:
    if isinstance(result, BaseException):
        logger.warning("Search failed: %s", result)
    else:
        knowledges.extend(result)
```

## Logging

**Framework:** Python standard library `logging` module

**Patterns:**
- Module-level logger initialization:
```python
logger = logging.getLogger(__name__)
```

- Logging levels used:
  - `logger.info()` for operational events: "Created new AI Foundry conversation", "Loaded N settings"
  - `logger.warning()` for degraded operations: "Failed to fetch Teams image", "Search failed"
  - `logger.exception()` for caught exceptions: "LLM intention classification failed"
  - `logger.error()` for serious errors that don't crash: Error-level failures

- Parameterized logging (not f-strings in log calls):
```python
logger.info("Using agent: name=%s", agent.name)
logger.warning("Failed to decode tool output for %s: %s", tool_name, e)
```

- Request ID context tracking in server.py using contextvars:
```python
_request_id_ctx_var: ContextVar[str] = ContextVar("request_id", default="system")
```

- Structured format with request ID:
```python
"%(asctime)s %(levelname)s [RequestID: %(request_id)s] %(name)s: %(message)s"
```

## Comments

**When to Comment:**
- Module docstrings at file top explaining purpose: `"""Intention classification service."""`
- Class docstrings explaining responsibility: `"""Coordinates conversation state, hosted-agent invocation."""`
- Method docstrings with Args/Returns for public APIs:
```python
async def classify(self, req: IntentionRequest) -> IntentionResponse:
    """Classify the intention of a message.

    Applies rule-based pre-filters first, then falls back to LLM
    classification for ambiguous cases.
    """
```

- Inline comments for non-obvious logic:
```python
# Build a lookup from the tool results for enrichment
# Cap queries to avoid excessive parallel searches
# TODO: implement with GitHub API
```

**Docstring Style:**
- Triple double-quotes for docstrings: `"""..."""`
- One-line docstrings for simple cases: `"""Return a config value, falling back to *default*."""`
- Multi-line docstrings with summary line, blank line, details for complex functions
- reStructuredText style for parameters in some cases:
```python
"""Execute a prompt template against the LLM.

Parameters
----------
template:
    The :class:`PromptTemplate` describing what to send.
"""
```

**JSDoc/TSDoc:**
- Not applicable (Python codebase)

## Function Design

**Size:**
- Services methods 20-100 lines typically
- Helper functions kept small: 5-20 lines
- Complex methods like `_postprocess()` reach 80+ lines when handling multiple concerns

**Parameters:**
- Use keyword-only arguments for clarity in tool definitions: `async def search_knowledge_base(self, *, queries: ..., sources: ...)`
- Type hints on all function signatures: `async def get(key: str, default: str | None = None) -> str | None`
- Use Pydantic models for structured inputs: `async def chat(self, req: ChatRequest) -> ChatResponse`
- Annotated types for tool parameters to provide LLM guidance:
```python
queries: Annotated[
    list[str],
    "One or more search queries to run against the knowledge base. ..."
]
```

**Return Values:**
- Explicit return types in all signatures: `-> IntentionResponse`, `-> str | None`, `-> tuple[str, bool]`
- Use Pydantic models for structured responses: `ChatResponse`, `FeedbackResponse`, `SearchKnowledgeBaseResult`
- Return None for not-found cases: `return None`
- Return tuples for multiple related values: `return (agent_conversation_id, is_new)`

## Module Design

**Exports:**
- No explicit `__all__` declarations
- Public functions/classes are module-level, private ones use underscore prefix
- Services expose class with public async methods
- Utilities expose functions directly: `get_credential()`, `get_project_client()`

**Barrel Files:**
- `tools/__init__.py` auto-imports all `*_tools.py` modules to trigger decorator registration:
```python
for _info in pkgutil.iter_modules([_package_dir]):
    if _info.name.endswith("_tools"):
        importlib.import_module(f"tools.{_info.name}")
```

## Async Patterns

**Async/Await:**
- All I/O operations use async: `async def init()`, `await container.read_item()`
- Client methods are async: `async def classify()`, `async def chat()`
- Use `asyncio.gather()` for parallel operations:
```python
tasks = [search_client.agentic_search(...), search_client.vector_search(...)]
results = await asyncio.gather(*tasks, return_exceptions=True)
```

**Context Managers:**
- Async context managers for lifecycle: `async with Agent(...) as agent:`
- Manual client lifecycle management with explicit close: `await client.close()`
- Lifespan context manager for FastAPI:
```python
@asynccontextmanager
async def lifespan(application: FastAPI):
    await app_config.init()
    yield
    await close_clients()
```

## Singleton Pattern

**Resource Singletons:**
- Module-level singletons for expensive clients: `_credential`, `_client`, `_agent_client`, `_settings`
- Lazy initialization on first access:
```python
_client: CosmosClient | None = None

async def _get_client() -> CosmosClient:
    global _client
    if _client is not None:
        return _client
    async with _client_lock:
        if _client is None:
            _client = CosmosClient(...)
```

- Thread-safe with asyncio.Lock: `_client_lock = asyncio.Lock()`
- Explicit cleanup functions: `async def close_credential()`, `async def close_clients()`

## Type Annotations

**Usage:**
- `from __future__ import annotations` in all files for forward references
- All function signatures have return types: `-> str`, `-> dict[str, str]`, `-> None`
- Union types using pipe syntax: `str | None`, `list[str] | None`
- Generic types: `dict[str, str]`, `list[Reference]`, `ContextVar[str]`
- Pydantic Field with validation_alias for API mapping:
```python
content: str = Field(default="", validation_alias="chunk")
source: str = Field(default="", validation_alias="context_id")
```

## Configuration Management

**Pattern:**
- Centralized config loading from Azure App Configuration in `config/app_config.py`
- Environment variable only for bootstrap: `AZURE_APPCONFIG_ENDPOINT`
- All other config via `get()` function:
```python
from config.app_config import get as cfg

endpoint = cfg("AI_FOUNDRY_PROJECT_ENDPOINT")
model = cfg("AI_FOUNDRY_AGENT_COMPLETION_MODEL", "gpt-4o-mini")
```

- Settings loaded once at startup: `await app_config.init()`
- Raises RuntimeError if accessed before init

---

*Convention analysis: 2025-01-04*
