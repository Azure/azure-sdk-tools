# Release History

## 0.1.0 (Unreleased)

### Features Added

- **server**: FastAPI backend with chat, feedback, conversation, and intention endpoints; health check (`/ping`) with version reporting.
- **services**: Chat service via Azure AI Foundry hosted agent; conversation mapping with Cosmos DB; thread memory with episode extraction; intention classification for multi-tenant routing; feedback workflow with GitHub issue creation.
- **tools**: GitHub MCP tools with Key Vault JWT auth; Azure DevOps MCP tools; Azure AI Search knowledge retrieval (hybrid semantic + vector); web search tools.
- **config**: Multi-tenant prompt support (Azure SDK, TypeSpec, API review, language channels).
- **skills**: Remote Agent Skill loading (`RemoteAgentSkillsSource`) that integrates the shared `azure-api-review` skill from `azure-rest-api-specs`, with reference files exposed as lazily-fetched skill resources.
- **pipelines**: Server CI pipeline with type checking and unit tests; server and hosted agent CD pipelines.
