# Release History

## 0.2.0 (2026-10-13)

### Other Changes

- Remove unused Node.js and npm from the backend server container to reduce vulnerable dependencies. Node.js remains available in the separate hosted-agent containers for MCP tools, and knowledge sync bundles its own Node.js runtime.

## 0.1.0

### Features Added

- **server**: FastAPI backend with chat, feedback, conversation, and intention endpoints; health check (`/ping`) with version reporting.
- **services**: Chat service via Azure AI Foundry hosted agent; conversation mapping with Cosmos DB; thread memory with episode extraction; intention classification for multi-tenant routing; feedback workflow with GitHub issue creation.
- **tools**: GitHub MCP tools with Key Vault JWT auth; Azure DevOps MCP tools; Azure AI Search knowledge retrieval (hybrid semantic + vector); web search tools.
- **config**: Multi-tenant prompt support (Azure SDK, TypeSpec, API review, language channels).
- **pipelines**: Server CI pipeline with type checking and unit tests; server and hosted agent CD pipelines.
