# Release History

## 0.2.1 (2026-10-30)

### Features Added

- Add read-only GitHub Actions job log retrieval with generic structured-diagnostic extraction and bounded verbatim excerpts.
- Load shared PR review guidance on demand through a dedicated skill.

### Bugs Fixed

- Preserve GitHub file evidence and pagination metadata while retaining output limits and trusted-author filtering.
- Expand raw static Q&A hits to the complete same-source, same-document question and answer, prioritize answers, and deduplicate repeated topic hits.
- Keep repository-access questions in the current tenant and include API Spec Review engineering guidance without design-title restrictions.
- Improve current-head PR readiness checks, workflow failure diagnosis, and evidence-grounded answer coverage.

## 0.2.0 (2026-10-13)

### Other Changes

- Remove unused Node.js and npm from the backend server container to reduce vulnerable dependencies.

## 0.1.0

### Features Added

- **server**: FastAPI backend with chat, feedback, conversation, and intention endpoints; health check (`/ping`) with version reporting.
- **services**: Chat service via Azure AI Foundry hosted agent; conversation mapping with Cosmos DB; thread memory with episode extraction; intention classification for multi-tenant routing; feedback workflow with GitHub issue creation.
- **tools**: GitHub MCP tools with Key Vault JWT auth; Azure DevOps MCP tools; Azure AI Search knowledge retrieval (hybrid semantic + vector); web search tools.
- **config**: Multi-tenant prompt support (Azure SDK, TypeSpec, API review, language channels).
- **pipelines**: Server CI pipeline with type checking and unit tests; server and hosted agent CD pipelines.
