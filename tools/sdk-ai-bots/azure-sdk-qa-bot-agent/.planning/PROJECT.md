# Azure SDK QA Bot Agent

## What This Is

An AI-powered chatbot built on the Azure AI Foundry Agent Framework that answers Azure SDK questions across multiple service areas. Deployed as a Teams channel bot, it retrieves knowledge from indexed documentation via Azure AI Search, uses LLM reasoning to generate answers, and supports tool integrations (GitHub, Azure DevOps) for richer context. Currently live in limited Teams channels with real users.

## Core Value

Accurate, reliable answers across Azure SDK areas — users trust the bot's responses enough to act on them without double-checking.

## Requirements

### Validated

- ✓ Multi-tenant knowledge retrieval via Azure AI Search — existing
- ✓ FastAPI backend server with chat, feedback, and intention endpoints — existing
- ✓ Azure AI Foundry hosted agent with tool calling — existing
- ✓ Cosmos DB conversation mapping and persistence — existing
- ✓ Feedback collection (thumbs up/down) to Azure Blob Storage — existing
- ✓ Tenant-specific routing and skill-based context injection — existing
- ✓ Teams channel integration via Bot Framework — existing
- ✓ Offline evaluation pipeline for quality assessment — existing

### Active

- [ ] Improve answer quality across the full pipeline (indexing → retrieval → reasoning)
- [ ] Add observability to diagnose where answer quality breaks down (search, retrieval expansion, LLM generation)
- [ ] Get GitHub MCP tools working reliably (token refresh, Key Vault signing)
- [ ] Get Azure DevOps MCP tools working reliably
- [ ] Fix tech debt: empty exception handlers, hardcoded ports, credential timeouts
- [ ] Add test coverage for critical paths (chat service, conversation persistence, tenant routing)
- [ ] Implement GitHub issue creation for bad feedback reactions
- [ ] Add agent performance metrics (response time, tool usage, success rate)
- [ ] Production hardening: rate limiting, input validation, error handling

### Out of Scope

- Mobile app or standalone web UI — Teams is the primary interface
- Real-time chat (WebSocket) — Request/response model sufficient for Teams
- Multi-language support — English only for Azure SDK documentation
- Self-service tenant onboarding UI — Tenant config managed by team via code

## Context

- **Deployment:** Dual-deployment architecture — FastAPI backend on Azure App Service, AI agent as containerized service on Azure AI Foundry
- **Knowledge sources:** Azure AI Search indexes with OData filter-based tenant partitioning
- **Quality signals:** Real-time feedback from Teams users (thumbs up/down) + offline evaluation pipeline
- **Current state:** Draft/in-progress, deployed to limited Teams channels, answer quality is the primary concern
- **Key SDK dependencies:** azure-ai-projects (preview), agent-framework (RC), azure-ai-agentserver-agentframework (beta) — all pre-GA
- **Existing codebase:** ~3900 LOC across 31 Python files, 4 test files, minimal test coverage

## Constraints

- **Platform:** Azure AI Foundry Agent Framework — core architectural choice, not negotiable
- **Interface:** Microsoft Teams — primary user-facing channel
- **Infrastructure:** Azure managed services (AI Search, Cosmos DB, App Configuration, Key Vault, Blob Storage)
- **SDK stability:** Multiple beta/preview Azure SDK dependencies — breaking changes possible
- **Team:** Solo developer workflow

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Azure AI Foundry for agent hosting | Managed agent lifecycle, integrated tracing, Responses API | — Pending |
| Dual-deployment (backend + agent container) | Separation of API concerns from agent logic | — Pending |
| Azure AI Search for knowledge retrieval | Tight integration with Azure ecosystem, supports OData filters for tenant partitioning | — Pending |
| Excel-based feedback storage | Quick to implement, human-readable export | ⚠️ Revisit |
| Tenant config in code (Python) | Direct access, no external dependency | ⚠️ Revisit |

## Evolution

This document evolves at phase transitions and milestone boundaries.

**After each phase transition** (via `/gsd-transition`):
1. Requirements invalidated? → Move to Out of Scope with reason
2. Requirements validated? → Move to Validated with phase reference
3. New requirements emerged? → Add to Active
4. Decisions to log? → Add to Key Decisions
5. "What This Is" still accurate? → Update if drifted

**After each milestone** (via `/gsd-complete-milestone`):
1. Full review of all sections
2. Core Value check — still the right priority?
3. Audit Out of Scope — reasons still valid?
4. Update Context with current state

---
*Last updated: 2026-04-01 after initialization*
