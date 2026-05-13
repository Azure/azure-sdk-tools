# Agent Framework and Memory Design

## 1 Overview

We are building a new agent service (`azure-sdk-qa-bot-agent`) based on the Azure AI Foundry Agent framework to replace the existing Go backend (`azure-sdk-qa-bot-backend`) built on a custom RAG framework. The motivations for this migration are:

- **Rigid workflow** — The Go backend uses a hard-coded workflow that is difficult to extend. Adding new capabilities requires modifying core code rather than configuration.
- **No tool abstraction** — Search, analysis, prompt building, and LLM calls are all inline with no separation of concerns.
- **Ecosystem mismatch** — Go is misaligned with the Python-first AI/LLM ecosystem (Azure AI Agents SDK, prompt frameworks, evaluation tooling).

### 1.1 Agent Framework — GitHub Copilot SDK vs Azure AI Foundry Agent SDK

> **References:**
> - [GitHub Copilot SDK](https://github.com/github/copilot-sdk)
> - [Azure AI Foundry Agents](https://learn.microsoft.com/en-us/azure/foundry/agents/overview)

| | GitHub Copilot SDK | Azure AI Foundry Agent SDK |
|---|---|---|
| **Description** | Embeds the same agentic core that powers GitHub Copilot CLI into any application, removing the need to build custom agent infrastructure. | A full platform for building, deploying, and governing general-purpose enterprise AI agents across any business domain. |
| **Key Capabilities** | Agent execution loop (production-tested multi-turn engine), multi-model support, custom agents & skills via Skill files, tool integration (file system, Git, web requests), streaming responses. | Conversation visibility (user-to-agent and agent-to-agent), multi-agent coordination, server-side tool orchestration with retries, trust & safety guardrails (XPIA protection), enterprise integration (BYOS, VNet, AI Search), observability (Application Insights), identity & policy control (Entra ID, RBAC, audit logs). |
| **Scenario** | Developer tools, Copilot Extensions, and developer-facing software. | Business-wide automation — customer support agents, document processing, HR/finance/IT workflows, RAG-based knowledge management. |
| **Best For** | TypeSpec authoring, code review, and other developer workflows. | Teams chatbots, customer support. |

**Decision:** We chose Azure AI Foundry Agent Service (`azure-ai-agents`) because it provides:

1. **Complete Agent Lifecycle Management** — Server-side persistent agents with create/list/update/delete; no need to rebuild per request.
2. **Rich Tool Definition Framework** — `FunctionTool`, `CodeInterpreterTool`, `FileSearchTool`, `AzureAISearchTool`, `BingGroundingTool`, `OpenApiTool`, `AzureFunctionTool`, `LogicAppTool`, with MCP Server compatibility.
3. **Server-side Orchestration** — Tool call execution, retries, and state management handled server-side; no manual polling required on the client.
4. **Enterprise-grade Authentication** — Microsoft Entra ID / Managed Identity / RBAC, with seamless integration into Azure resources.
5. **Observability** — Native OpenTelemetry tracing + Application Insights integration to trace every agent decision and tool invocation.
6. **Security & Compliance** — Built-in content safety guardrails, network isolation (VNet), XPIA protection, and data encryption.

![Agent overview](images/agent.png)

#### AI Foundry Agent SDK — Key Concepts

> **Reference:** [Azure AI Projects SDK (Python)](https://github.com/Azure/azure-sdk-for-python/blob/main/sdk/ai/azure-ai-projects/README.md)

| Concept | Description |
| --- | --- |
| **Agent** | A persistent, versioned entity that binds a model, instructions, and tools. Created via `project_client.agents.create_version(...)` and retrieved by name for reuse via `project_client.agents.get(...)`. |
| **Conversation** | An isolated session container for multi-turn interactions between a user and an agent. Created via `openai_client.conversations.create(...)`. |
| **Conversation Item** | A single message within a conversation. Appended via `openai_client.conversations.items.create(...)`. |
| **Response** | Triggers the agent to reason over conversation context and generate a reply. Created via `openai_client.responses.create(...)`. Supports streaming. |
| **Tool** | Capabilities attached to an agent definition. Built-in tools include `CodeInterpreterTool`, `AzureAISearchTool`, `BingGroundingTool`, `OpenApiTool`. Custom logic uses `FunctionTool`. |
| **Function Call** | When the agent invokes a custom tool, the response output contains a `function_call` item. Results are submitted back via `responses.create(...)` with `function_call_output`. |

#### AI Foundry Agent SDK — Workflow

![Agent lifecycle](images/agent_lifecycle.png)

### 1.2 Memory — Hybrid Approach (AI Foundry Memory + Cosmos DB Episodes)

> **References:**
> - [AI Foundry Memory](https://learn.microsoft.com/en-us/azure/foundry/agents/concepts/what-is-memory?tabs=conversational-agent)
> - [AI Agent Memory Concepts](https://www.geeksforgeeks.org/artificial-intelligence/ai-agent-memory/)

AI Agent Memory is the ability of an agent to store, recall, and use information from past interactions to make better decisions. Without memory, an agent treats every interaction as if it were the first. With memory, an agent can maintain context, adapt to users, and improve over time — gaining continuity, context-awareness, and learning abilities.

| Requirement | AI Foundry Memory | Self-hosted (Cosmos DB / AI Search) |
| --- | --- | --- |
| Manage memory items | Search returns items, but no update/patch per item | Full CRUD per item |
| Compression / retention policy | Not supported — LLM-driven consolidation, opaque and uncontrollable | Fully customizable (keep N most recent, TTL, age-based purge) |
| Structured schema | Unstructured text only | Fully customizable structure |
| Filtered queries (by repo, language, service) | Semantic search only, no field-level filters | Structured filters + semantic search |
| Stability | Public preview — API and behavior may change | Production GA services |

**Decision:** We adopted a **hybrid memory architecture** that combines both approaches:

1. **AI Foundry Memory Store** — used for **user-scoped memory** (personal preferences, SDK/language context, working patterns). Foundry handles automatic user-profile extraction and chat-summary consolidation, providing low-cost per-user personalisation.
2. **Self-hosted Cosmos DB** — used for **expert experience episodes** (structured problem-solution pairs with reasoning chains). Episodes require a custom schema, deterministic IDs for upsert, vector embeddings for similarity search, and tenant-scoped partitioning — capabilities that Foundry Memory does not support.

This gives us the simplicity of Foundry for per-user context while retaining full control over the structured knowledge base that drives expert-level answer quality.

## 2 Design

### 2.1 Overview

![Architecture diagram](images/architecture_diagram.png)

#### 2.1.1 Q&A Workflow

**Before:**

![Q&A workflow — before](images/qa_workflow_before.png)

**After:**

![Q&A workflow — after](images/qa_workflow_after.png)

#### 2.1.2 Feedback Workflow

**Before:**

![Feedback workflow — before](images/feedback_workflow_before.png)

**After:**

![Feedback workflow — after](images/feedback_workflow_after.png)

### 2.2 Interaction Design

#### 2.2.1 Q&A

![Q&A interaction diagram](images/qa_interact_diagram.png)

#### 2.2.2 Feedback

![Feedback interaction diagram](images/feedback_interact_diagram.png)

### 2.3 Memory Design

#### 2.3.1 Memory Types

AI Foundry Memory Store provides two built-in types of long-term memory (see [Foundry Memory — Memory Types](https://learn.microsoft.com/en-us/azure/foundry/agents/concepts/what-is-memory?tabs=conversational-agent#memory-types)):

- **User profile memory** — Information and preferences *about the user* (e.g., preferred name, SDK language, working patterns). These are considered "static" with respect to a conversation because they don't depend on the current chat context. They are retrieved **once at the start of each session**.
- **Chat summary memory** — A distilled summary of each topic covered in prior chat sessions. These allow users to continue conversations or reference earlier sessions without repeating context. They are retrieved **every turn** using the current input messages as search items.

In addition to the two Foundry-managed types, we maintain a third type — **expert episodes** — in a self-hosted Cosmos DB store for structured knowledge that requires custom schema and vector search.

The two memory categories differ in their **data source**:

- **User memory** (profile + chat summary) is derived from **the user's messages and the agent's responses**. Every time a user interacts with the bot, the exchange is used to build and update that user's memory. It captures what this specific user cares about and what they've discussed with the bot.
- **Expert episodes** are derived from **the full conversation thread including all participants**. They are only extracted when a human expert (someone other than the original poster or the bot) replies in the thread. The value comes from expert reasoning, so the entire multi-party thread is sent for episode extraction.

| Memory Type | Store | Scope | Description |
| --- | --- | --- | --- |
| **User profile** (static) | AI Foundry Memory Store | Per user (`user_{user_id}`) | Personal preferences, SDK/language, project context. Fetched once per session via `search_memories` with no query items. |
| **User contextual** (chat summary) | AI Foundry Memory Store | Per user (`user_{user_id}`) | Conversation-relevant memories retrieved every turn using input messages as search items. Incremental via `previous_search_id`. |
| **Expert episodes** (tenant) | Cosmos DB `experience-episodes` | Per tenant (`tenant_id` partition key) | Structured problem-solution pairs extracted from expert-resolved threads. Retrieved via cosine vector similarity search against the user's current question. |

#### 2.3.2 Key Components

| Component | Purpose |
| --- | --- |
| `utils/memory_context_provider.py` | `MemoryContextProvider` — retrieves and injects memories before each agent turn, updates user store after. |
| `utils/azure_memory_store.py` | Foundry Memory Store helpers — store creation, config accessors, scope sanitization. |
| `services/thread_memory_service.py` | `ThreadMemoryService` — extracts episodes from expert-resolved threads and stores in Cosmos DB. |
| `prompts/episode_extraction.md` | LLM prompt for structured episode extraction. |

#### 2.3.3 Memory Lifecycle

##### Write Path — User Memory

After each agent response, `MemoryContextProvider.after_run` collects user + assistant messages and submits them to the Foundry Memory Store via `begin_update_memories`. Foundry asynchronously extracts and consolidates user-profile facts. A configurable `update_delay` (default 300 s) controls how soon updates are processed.

##### Write Path — Expert Episodes

When a conversation message is saved via `/conversation/save`, `ThreadMemoryService.process_thread_update` runs as a background task:

1. **Quality gate** — Only triggers when the latest message is from an expert (not the original poster or the bot).
2. **LLM extraction** — Sends the full thread transcript to `chat.completions` with a structured episode-extraction prompt (`prompts/episode_extraction.md`). The LLM returns a JSON `Episode` or `null` if the thread is unresolved or low-value.
3. **Embedding** — Generates a vector embedding of `trigger + symptoms` using the configured embedding model (default `text-embedding-3-small`).
4. **Upsert** — Stores the `EpisodeDocument` in the `experience-episodes` Cosmos DB container with a deterministic ID (`episode-{tenant_id}-{source_thread_id}`), so re-extractions as the thread grows replace previous versions.

##### Read Path — Before Each Agent Turn

`MemoryContextProvider.before_run` assembles memory context before model invocation:

1. **Resolve scopes** — Extracts `user_scope` from `[memory_scope] value=…` marker and `tenant_scope` from `[tenant_context] original_tenant_id=…` marker in input messages.
2. **Fetch static user memories** — On first turn only (per session), queries the user store with no items to retrieve user-profile memories.
3. **Search contextual user memories** — Every turn, searches the user store using input messages as items (incremental via `previous_search_id`).
4. **Search expert episodes** — Generates an embedding of the latest user message and performs a Cosmos DB `VectorDistance` query within the tenant partition. Results are filtered by a similarity threshold (default 0.80, top-k default 2).
5. **Inject context** — Formats all memories into a system message with `## User memories` and `## Expert experience` sections and injects it into the agent context.

#### 2.3.4 Episode Schema

Episodes stored in Cosmos DB follow a structured schema (`models/episode.py`):

| Field | Description |
| --- | --- |
| `trigger` | The symptom or question that started the thread. |
| `symptoms` | Observable signs — error messages, unexpected behavior. |
| `reasoning_chain` | Step-by-step diagnostic process the expert followed (min 2 steps). |
| `resolution` | What ultimately fixed the problem or answered the question. |
| `key_insight` | Generalizable takeaway that applies beyond this specific case. |
| `confidence` | Extraction confidence (0–1). Episodes below 0.5 are discarded. |

The `EpisodeDocument` extends this with storage fields: `id`, `tenant_id`, `source_thread_id`, `message_count`, `embedding`, and timestamps.

### 2.4 API Design

See the [TypeSpec definitions](../azure-sdk-qa-bot-agent/tsp).

## 3 Project Structure

See the [Project directory](../azure-sdk-qa-bot-agent).

## 4 Evaluation

The backend API response retains the same structure (`answer` + `references`), so the current accuracy evaluation approach continues to work. Additionally, we plan to introduce a new **usefulness evaluator** that analyzes full conversation threads to measure the bot's overall helpfulness.