# Release History

## 0.3.0 (2026-11-18)

### Features Added

- Integrate the Microsoft Agent Framework harness for hosted agents (`create_harness_agent`) with `history_source="agent"`, token-aware context compaction, and OpenTelemetry instrumentation.
- Offload large external tool results (>20,000 characters) to Foundry Files (with JSON pretty-printing) for inspection inside Foundry's per-conversation `container_auto` shell sandbox.

### Breaking / Behavioral Changes

- Remove custom `truncating_mcp_parser` callbacks in favor of raw MCP outputs protected by `ToolOutputSecurityMiddleware` and `ToolOutputStorageMiddleware`.
- Update hosted agent deployment to Responses protocol `2.0.0`.

## 0.2.2 (2026-11-18)

### Features Added

- Add catalog-driven Teams channel handoff via `config/support-channel.yaml`, `models/support_channel.py`, and the on-demand `support-channels` skill (`{{channel:<key>}}`), returning `route_channel_name` and `route_channel_link` while soft-deprecating `route_tenant`.
- Pass `channel_name` from `ChatRequest` into `[tenant_context]` and suppress self-routing back to the current channel.

### Bugs Fixed

- Stop inferring `route_tenant` from `load_skill` calls and decouple `load_skill` from Teams channel handoffs.
- Clarify `api-stewardship-board` as data-plane only and direct ARM/management-plane questions to `typespec`.
- Keep TypeSpec PR design-review requests in `typespec` instead of switching to `api-spec-review` or merge-blocker-only triage.

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
