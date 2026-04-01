# Testing Patterns

**Analysis Date:** 2025-01-04

## Test Framework

**Runner:**
- pytest
- No explicit config file (`pytest.ini` or `pyproject.toml` not present)
- Configured via command-line or defaults

**Assertion Library:**
- pytest's built-in assertions: `assert resp.should_respond is False`

**Async Support:**
- pytest-asyncio for async test execution
- Tests decorated with `@pytest.mark.asyncio`

**Run Commands:**
```bash
pytest                          # Run all tests
pytest tests/                   # Run tests in directory
pytest tests/intention_service_test.py  # Run specific file
pytest -v                       # Verbose mode
pytest --tb=short              # Shorter traceback
```

## Test File Organization

**Location:**
- Tests in dedicated `tests/` directory at project root
- Not co-located with source files

**Naming:**
- Test files use `_test.py` suffix: `intention_service_test.py`, `knowledge_tools_test.py`
- Test functions use `test_` prefix: `test_expert_reply_skips()`, `test_llm_classifies_technical_question()`

**Structure:**
```
tests/
├── intention_service_test.py
├── knowledge_tools_test.py
├── azsdk_mcp_tools_test.py
├── github_tools_test.py
└── api_test.rest
```

## Test Structure

**Suite Organization:**
```python
"""Unit tests for the intention classification service."""

from __future__ import annotations

import sys
from pathlib import Path
from unittest.mock import AsyncMock, MagicMock, patch

import pytest

# Add project root to sys.path
_PROJECT_ROOT = str(Path(__file__).resolve().parent.parent)
if _PROJECT_ROOT not in sys.path:
    sys.path.insert(0, _PROJECT_ROOT)

from models.intention import IntentionRequest, IntentionResponse
from services.intention_service import IntentionService


@pytest.fixture
def service() -> IntentionService:
    return IntentionService()


@pytest.mark.asyncio
async def test_expert_reply_skips(service: IntentionService) -> None:
    req = IntentionRequest(...)
    # Arrange
    mock_conversation_service = AsyncMock()
    mock_conversation_service.has_expert_reply = AsyncMock(return_value=True)
    service._conversation_service = mock_conversation_service
    
    # Act
    resp = await service.classify(req)
    
    # Assert
    assert resp.should_respond is False
    assert resp.reason == "expert_already_replied"
```

**Patterns:**
- Docstring at module level explaining test scope
- `from __future__ import annotations` for consistency with source
- sys.path manipulation at top to import project modules
- Fixtures for service instances: `@pytest.fixture def service() -> IntentionService`
- Async tests with `@pytest.mark.asyncio`
- Arrange-Act-Assert pattern (implicit, no comments)

## Mocking

**Framework:** `unittest.mock` from Python standard library

**Patterns:**

**AsyncMock for async methods:**
```python
mock_conversation_service = AsyncMock()
mock_conversation_service.has_expert_reply = AsyncMock(return_value=True)
service._conversation_service = mock_conversation_service
```

**MagicMock for synchronous objects:**
```python
mock_choice = MagicMock()
mock_choice.message.content = '{"should_respond": true, "reason": "..."}'
mock_response = MagicMock()
mock_response.choices = [mock_choice]
```

**patch() for module-level functions:**
```python
with patch(
    "services.intention_service.get_project_client",
    return_value=mock_project_client,
):
    resp = await service.classify(req)
```

**What to Mock:**
- External API clients: `get_project_client()`, `openai_client`
- Azure SDK clients: `AIProjectClient`, `AzureAppConfigurationClient`
- Dependencies injected into services: `_conversation_service`
- Network calls and I/O operations

**What NOT to Mock:**
- Pydantic models: Use real instances
- Data structures: Use real dicts/lists
- Pure functions without side effects
- The system under test itself

## Fixtures and Factories

**Test Data:**
```python
req = IntentionRequest(
    message=Message(
        role="user", 
        content="How do I fix this CI error?", 
        user_id="user-1"
    ),
    conversation_id="conv-123",
    conversation_type="teams_channel",
)
```

**Fixture Pattern:**
```python
@pytest.fixture
def service() -> IntentionService:
    return IntentionService()
```

**Module-scoped fixtures for expensive setup:**
```python
@pytest_asyncio.fixture(scope="module")
async def ai_client():
    """Initialise App Configuration and return an AzureAIClient."""
    await app_config.init()
    return AzureAIClient(
        project_endpoint=cfg("AI_FOUNDRY_PROJECT_ENDPOINT"),
        model_deployment_name=cfg("AI_FOUNDRY_AGENT_COMPLETION_MODEL"),
        credential=DefaultAzureCredential(),
    )
```

**Location:**
- Fixtures defined inline in test files
- No central `conftest.py` detected

## Coverage

**Requirements:** Not enforced

**Current State:** Partial coverage
- 4 test files covering critical services and tools
- Unit tests: `intention_service_test.py`
- Integration tests: `knowledge_tools_test.py`, `azsdk_mcp_tools_test.py`, `github_tools_test.py`
- 31 Python source files, 4 test files (~13% file coverage)

**View Coverage:**
```bash
pytest --cov=. --cov-report=html    # Generate HTML report
pytest --cov=. --cov-report=term    # Terminal report
```

## Test Types

**Unit Tests:**
- Scope: Single service or module
- Example: `tests/intention_service_test.py`
- Approach: Mock all dependencies, test logic in isolation
```python
async def test_llm_error_defaults_to_respond(service: IntentionService) -> None:
    mock_openai_client.chat.completions = AsyncMock(
        side_effect=RuntimeError("connection failed")
    )
    # Test that service handles error gracefully
```

**Integration Tests:**
- Scope: Real external services (Azure AI Search, Azure SDK MCP)
- Example: `tests/knowledge_tools_test.py`, `tests/azsdk_mcp_tools_test.py`
- Approach: Use real credentials, hit real APIs
- Requirements documented in docstrings:
```python
"""Integration tests for Azure DevOps MCP tools.

Requirements:
  - ``AZURE_APPCONFIG_ENDPOINT`` env var set
  - Azure credentials available (``DefaultAzureCredential``)
  - ``azsdk`` available on PATH
  - Network access to Azure SDK MCP server
"""
```

**Integration test pattern:**
```python
@pytest.mark.asyncio
async def test_search_knowledge_tool() -> None:
    query = "how to solve tsv failure"
    sources = [SRC_AZURE_REST_API_SPECS_WIKI]
    
    result = await KnowledgeTools().search_knowledge_base(
        query=query, sources=sources, tenant_id=TenantID.TYPESPEC_CHANNEL_QA_BOT
    )
    
    assert len(result.results) > 0
```

**E2E Tests:**
- Not present in current test suite
- API test file exists: `tests/api_test.rest` (manual testing)

## Common Patterns

**Async Testing:**
```python
@pytest.mark.asyncio
async def test_something(service: IntentionService) -> None:
    result = await service.classify(req)
    assert result.should_respond is True
```

**Error Testing:**
```python
@pytest.mark.asyncio
async def test_llm_error_defaults_to_respond(service: IntentionService) -> None:
    mock_client = MagicMock()
    mock_client.chat.completions = AsyncMock(
        side_effect=RuntimeError("connection failed")
    )
    
    with patch("services.intention_service.get_project_client", 
               return_value=mock_client):
        resp = await service.classify(req)
    
    assert resp.should_respond is True
    assert resp.reason == "llm_error_default_respond"
```

**Mocking Chain Pattern:**
```python
# Mock nested object structure
mock_completions = AsyncMock()
mock_completions.create = AsyncMock(return_value=mock_response)

mock_openai_client = MagicMock()
mock_openai_client.chat.completions = mock_completions

mock_project_client = MagicMock()
mock_project_client.get_openai_client.return_value = mock_openai_client
```

**Environment Setup for Integration Tests:**
```python
from dotenv import load_dotenv

load_dotenv()

# Tests then use environment variables loaded from .env
```

**sys.path Manipulation:**
```python
# Every test file includes this boilerplate
_PROJECT_ROOT = str(Path(__file__).resolve().parent.parent)
if _PROJECT_ROOT not in sys.path:
    sys.path.insert(0, _PROJECT_ROOT)
```

## Test Isolation

**Setup:**
- Each test creates fresh service instances via fixtures
- Mock objects reset between tests automatically

**Teardown:**
- No explicit teardown in current tests
- pytest handles cleanup of fixtures

**State Management:**
- Tests don't share state
- Each test mocks dependencies independently
- Integration tests use real resources but query read-only endpoints

## Test Organization Best Practices

**File Structure:**
- One test file per service/module: `intention_service_test.py` tests `intention_service.py`
- Integration tests grouped by external system: `azsdk_mcp_tools_test.py`, `github_tools_test.py`

**Test Naming:**
- Descriptive names indicating scenario: `test_expert_reply_skips`, `test_llm_classifies_technical_question`
- Negative cases explicit: `test_llm_error_defaults_to_respond`

**Assertions:**
- Test both value and type: `assert resp.should_respond is False`
- Test specific error reasons: `assert resp.reason == "expert_already_replied"`
- Use exact equality checks, not truthy/falsy

## Missing Test Coverage

**Areas Without Tests:**
- `services/chat_service.py` — Core chat logic (complex, high priority)
- `services/conversation_service.py` — Conversation storage
- `services/feedback_service.py` — Feedback workflow
- `utils/azure_ai_search.py` — Search client wrapper
- `utils/azure_cosmosdb.py` — Cosmos DB client
- `utils/azure_storage.py` — Blob storage operations
- `config/tenant_config.py` — Tenant configuration (large, important)
- `tools/skills.py` — Skill loading and registration
- `server.py` — FastAPI endpoint handlers

**Test Types Needed:**
- More unit tests for services
- FastAPI endpoint tests (using TestClient)
- Error handling edge cases
- Configuration validation tests

---

*Testing analysis: 2025-01-04*
