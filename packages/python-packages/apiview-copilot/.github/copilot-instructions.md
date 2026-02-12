# Copilot Instructions for APIView Copilot

## Overview

APIView Copilot is an AI-powered automated reviewer for Azure SDK API surface reviews. It ingests APIView text representations of SDK public APIs, sections them, runs multi-stage LLM prompts (guideline, context, and generic reviews), filters and deduplicates results, and produces structured review comments. It is deployed as a **FastAPI** web service on Azure App Service and also provides a CLI (`avc`) for local development.

## Project Structure

- `app.py` — FastAPI application entry point (endpoints for review jobs, agent chat, mentions, thread resolution, summarization, package resolution)
- `cli.py` — CLI entry point using [Knack](https://github.com/microsoft/knack). Invoked via the `avc` batch script.
- `src/` — Core source code:
  - `_apiview_reviewer.py` — Main review orchestration (`ApiViewReview` class). Sections documents, submits parallel LLM prompt tasks, filters, deduplicates, scores, and groups comments.
  - `_sectioned_document.py` — Splits large API text into manageable sections based on indentation and line numbers.
  - `_models.py` — Pydantic models (`Comment`, `ExistingComment`, `ReviewResult`, `Guideline`, `Example`, `Memory`, etc.).
  - `_search_manager.py` — Azure AI Search integration for RAG. Searches guidelines, examples, and memories.
  - `_database_manager.py` — Azure Cosmos DB integration. Singleton `DatabaseManager` with typed container clients.
  - `_settings.py` — Singleton `SettingsManager` reading from Azure App Configuration with Key Vault secret resolution.
  - `_prompt_runner.py` — Runs `.prompty` files with retry logic.
  - `_comment_grouper.py` — Groups similar comments with correlation IDs.
  - `_diff.py` — Generates numbered diffs between base and target API views.
  - `_mention.py` — Handles @mention feedback processing.
  - `_thread_resolution.py` — Handles thread resolution requests.
  - `_auth.py` — FastAPI authentication dependencies with role-based access control.
  - `_credential.py` — Azure credential helpers (`DefaultAzureCredential`).
  - `_metrics.py` — Metrics reporting and aggregation.
  - `_retry.py` — Generic retry-with-backoff utility.
  - `_utils.py` — Shared utilities including `run_prompty` and language name mapping.
  - `agent/` — Azure AI Agent Service integration (read-only and read-write agents).
- `prompts/` — All `.prompty` prompt template files organized by feature:
  - `api_review/` — Core review prompts (guideline, context, generic review; filtering; merging; scoring).
  - `mention/` — Mention/feedback processing prompts.
  - `summarize/` — API and diff summarization prompts.
  - `thread_resolution/` — Thread resolution prompts.
  - `evals/` — Evaluation judge prompts.
  - `other/` — Miscellaneous prompts (comment theme analysis, package resolution, metrics summarization).
- `metadata/` — Per-language YAML configuration files:
  - `filter.yaml` — Language-specific filtering exceptions.
  - `guidance.yaml` — Language-specific custom rules for generic review.
- `evals/` — Evaluation framework for testing prompt quality. See `evals/README.md`.
- `tests/` — Unit tests using pytest.
- `scripts/` — Deployment and permissions scripts.
- `scratch/` — Local scratch directory for debug output, API views, and charts (gitignored).

## Architecture & Key Patterns

### Review Pipeline

The review pipeline in `ApiViewReview.run()` follows these stages:
1. **Sectioning** — `SectionedDocument` splits the API text into chunks (default 500 lines, 450 for Java/Android).
2. **Parallel prompt evaluation** — For each section, three prompts run in parallel: guideline review (RAG with language guidelines), context review (RAG with semantic search), and generic review (custom rules).
3. **Generic comment filtering** — Generic comments are validated against the knowledge base.
4. **Deduplication** — Comments on the same line are merged via LLM.
5. **Hard filtering** — Comments are checked against language-specific filter exceptions and the API outline.
6. **Pre-existing comment filtering** — New comments are compared against existing human comments on the same lines.
7. **Judge scoring** — Each comment is scored for confidence and severity.
8. **Correlation ID assignment** — Similar comments are grouped for batched display.

### Singleton Patterns

- `SettingsManager` — Thread-safe singleton for Azure App Configuration. Uses `_instance` + `threading.Lock`.
- `DatabaseManager` — Singleton via `get_instance()` class method.

### Prompt Execution

- Prompts are defined as `.prompty` files in the `prompts/` directory.
- Executed via `run_prompty()` in `_utils.py` (uses the `prompty` library with `prompty.azure`).
- Retry logic is in `_prompt_runner.py` using `retry_with_backoff` from `_retry.py`.
- In CI, an API key from settings is used; locally, `DefaultAzureCredential` is used.

### Data Models

- All models use **Pydantic v2** with `BaseModel`.
- Cosmos DB models use field aliases that match their respective sources (for example, `ExistingComment` uses camelCase aliases with `populate_by_name = True`, while `APIViewComment` uses PascalCase aliases like `ReviewId` and `APIRevisionId` to mirror APIView payload fields).
- The knowledge base consists of three entity types: `Guideline`, `Example`, and `Memory`, stored in separate Cosmos DB containers and indexed in Azure AI Search.

## Coding Conventions

### Python Style

- **Formatting**: Use [Black](https://black.readthedocs.io/) for all code formatting. Configuration is in `pyproject.toml`. Run `python scripts/format.py` to format, or `python scripts/format.py --check` to verify.
- **Linting**: Use Pylint. Run `pylint src scripts tests`.
- **Type hints**: Use throughout. Use `Optional[T]` for nullable fields. Use `List`, `Dict`, `Set` from `typing` for compatibility.
- **Docstrings**: Use triple-double-quote docstrings for all public classes and methods.
- **Imports**: Group as stdlib → third-party → local, separated by blank lines. Local imports use absolute paths (e.g., `from src._models import Comment`). `__init__.py` files and subpackage sibling imports may use relative paths (e.g., `from ._base import MentionWorkflow`). Be consistent within a module.
- **File headers**: Every source file should have the Microsoft copyright header. Run `python scripts/check_copyright_headers.py` to verify, or `--fix` to add missing headers automatically.
- **Private modules**: All source modules in `src/` use underscore prefix (e.g., `_models.py`).
- **Logging**: Use Python's `logging` module. Module-level logger via `logger = logging.getLogger(__name__)`. The `ApiViewReview` class uses a `JobLogger` wrapper that prepends the job ID to all log messages.

### Error Handling

- Handle `429 (Rate limit)` responses with retry-after logic.
- All prompt executions use retry with exponential backoff.
- Log errors with full context (exception type, message, and traceback where appropriate).
- Use `json.JSONDecodeError` handling when parsing LLM responses.

### Testing

- Tests live in `tests/` and use **pytest**.
- Run tests: `pytest tests`
- Test files are named `*_test.py` (e.g., `apiview_test.py`, `metrics_test.py`). Some use `test_*.py` convention.
- Fixtures are in `conftest.py`.
- Evaluation tests (prompt quality) are in `evals/` and run separately via `avc eval run` or `python evals/run.py`.

### Evaluation Tests

- Eval test cases are YAML files in `evals/tests/<workflow_name>/`.
- Each workflow has a `test-config.yaml` defining name and kind.
- Target functions are registered in `evals/_custom.py`.
- Evals use the `prompty` library directly to execute prompts and compare results.
- Use `--use-recording` to cache LLM responses for faster iteration.

## Azure Resource Dependencies

- **Azure App Configuration** — Central configuration store (`AZURE_APP_CONFIG_ENDPOINT` env var, `ENVIRONMENT_NAME` label).
- **Azure Key Vault** — Secret storage, referenced from App Configuration.
- **Azure Cosmos DB** — Data store for guidelines, examples, memories, review jobs, metrics, and evals.
- **Azure AI Search** — Semantic search index over guidelines, examples, and memories for RAG.
- **Azure OpenAI** — LLM backend for prompt execution.
- **Azure AI Agent Service** — Agent-based chat functionality.
- **Azure App Service** — Hosting for the FastAPI application.

## CLI Reference

The CLI is invoked via `avc` (or `python cli.py`). Key command groups:

- `avc review generate` — Generate a review locally or remotely.
- `avc review start-job` / `avc review get-job` — Async review job management.
- `avc agent chat` — Interactive agent chat session.
- `avc agent mention` — Process @mention feedback.
- `avc eval run` — Run evaluation tests.
- `avc search kb` — Search the knowledge base.
- `avc search reindex` — Trigger search index refresh.
- `avc db get` / `avc db delete` / `avc db purge` — Database operations.
- `avc metrics report` — Generate metrics reports.
- `avc permissions grant` / `avc permissions revoke` — Manage Azure RBAC permissions for local development.
- `avc app deploy` — Deploy to Azure App Service.
- `avc app check` — Health check the deployed service.

## Environment Setup

A Python virtual environment is used for development. **Always activate the virtualenv before running any commands** (tests, linting, formatting, scripts, etc.):

```bash
# Windows
.venv\Scripts\activate

# Linux/macOS
source .venv/bin/activate
```

Install dependencies after activation:
```bash
pip install -r dev_requirements.txt
```

Required environment variables (typically in `.env`):
- `AZURE_APP_CONFIG_ENDPOINT` — Azure App Configuration endpoint URL.
- `ENVIRONMENT_NAME` — Configuration label (e.g., `production`, `staging`).

All other settings (Cosmos DB endpoint, Search endpoint, OpenAI keys, etc.) are resolved from App Configuration at runtime.

## Important Guidelines for Changes

- **Prompts**: When modifying `.prompty` files, always run the relevant eval tests to verify prompt quality hasn't regressed. Use `avc eval run --test-paths evals/tests/<workflow>`.
- **Models**: When modifying Pydantic models, ensure JSON serialization aliases remain consistent with API contracts and Cosmos DB schemas.
- **Search/RAG**: Changes to `SearchManager` or the knowledge base schema may require reindexing (`avc search reindex`).
- **Settings**: New configuration keys must be added to Azure App Configuration for both `production` and `staging` labels.
- **Endpoints**: The FastAPI app uses role-based auth. Test changes with `avc app check --include-auth`.
- **CI**: The CI pipeline (`ci.yml`) runs packaging, unit tests (`pytest tests`), linting (`pylint src scripts tests`), and copyright header checks (`python scripts/check_copyright_headers.py`).
- **Dependencies**: Runtime deps go in `requirements.txt`; dev/test deps go in `dev_requirements.txt`.

## Supported Languages

APIView Copilot supports reviews for: `android`, `clang`, `cpp`, `dotnet`, `golang`, `ios`, `java`, `python`, `rust`, `typescript`.
