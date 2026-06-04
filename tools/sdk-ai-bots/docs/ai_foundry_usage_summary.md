# Using Azure AI Foundry in the Azure SDK QA Bot

A concise summary of how the Azure SDK QA Bot (`azure-sdk-qa-bot-agent`) leverages
Azure AI Foundry — covering the hosted agent, the agent library, memory, and
evaluation. For the full design see
[agent_framework_and_memory_design.md](agent_framework_and_memory_design.md).

## 1 Why Azure AI Foundry

We migrated from a custom Go RAG backend to Azure AI Foundry to gain:

- **Server-side agent lifecycle** — persistent, versioned agents created once and
  reused across requests; no per-request rebuild.
- **Built-in tool orchestration** — Foundry handles tool-call execution, retries,
  and state management on the server.
- **Enterprise integration** — Entra ID / Managed Identity / RBAC, VNet, XPIA
  protection, and Application Insights tracing out of the box.
- **Python-first ecosystem** alignment with Azure AI Agents SDK and the broader
  LLM tooling landscape (prompt frameworks, evaluators).

## 2 Hosted Agent

The Chat Agent (`azure-sdk-qa-bot-agent/agents/chat_agent/`) is deployed as a
**Foundry hosted container agent** and exposed via the Responses protocol. The
FastAPI backend (`server.py`) calls it through the Azure AI Foundry SDK to
handle chat, conversations, and feedback.

A single `Agent` instance binds:

| Piece | Purpose |
|---|---|
| Model + instructions | Role, behavior, and response format. |
| Tools | Knowledge search, web fetch, pipeline analysis, Bing grounding, ADO/GitHub MCP. |
| Skills | Per-tenant (Teams channel) QA guidelines; the agent self-routes by skill description. |
| Context providers | `SkillsProvider`, `MemoryContextProvider`, `CompactionProvider`. |

### 2.1 Tools

| Tool | Type | Description |
|---|---|---|
| `search_knowledge_base` | `FunctionTool` | Hybrid vector + keyword search over Azure AI Search with semantic reranking and header-hierarchy expansion. Supports `quick` (vector only) and `deep` (agentic + vector) modes. |
| `web_fetch` | `FunctionTool` | Fetches and extracts content from a URL. |
| `azsdk_analyze_pipeline` | `FunctionTool` | Diagnoses Azure SDK CI pipeline failures. |
| `web_search` | Built-in | Bing grounding for real-time information. |
| ADO / GitHub | MCP servers | Work item, issue, and PR lookups. |

### 2.2 Skills (per-tenant routing)

Each Teams channel maps to a `Skill` bundling a short description (used by the
agent to route requests), a tenant-specific QA guideline prompt, and the set of
knowledge sources to search. Tenants include TypeSpec Discussion, Azure SDK
Onboarding, Azure TypeSpec Authoring, API Spec Review, Python SDK, and .NET SDK.

## 3 Agent Libraries

We use a two-layer stack: Microsoft's **Agent Framework** for authoring and
runtime composition of the agent, and the **Azure AI Foundry SDK** for
provisioning, hosting, and invoking the agent on Foundry.

### 3.1 Microsoft Agent Framework (`agent_framework`)

The open-source Microsoft Agent Framework is our primary authoring library —
the entire Chat Agent and Feedback Agent are built on it. It provides the
high-level abstractions we need to compose a tool-using, memory-aware agent
without writing orchestration glue.

| Import | Where it's used | Purpose |
|---|---|---|
| `Agent` | [`agents/chat_agent/init.py`](../azure-sdk-qa-bot-agent/agents/chat_agent/init.py), [`agents/feedback_agent/init.py`](../azure-sdk-qa-bot-agent/agents/feedback_agent/init.py) | The top-level agent object that binds a chat client, instructions, tools, skills, and context providers. |
| `FoundryChatClient` (`agent_framework.foundry`) | [`utils/azure_ai_foundry.py`](../azure-sdk-qa-bot-agent/utils/azure_ai_foundry.py) | Chat client backed by an Azure AI Foundry deployment; supplies the model the agent reasons with. |
| `Skill` + `SkillsProvider` | [`skills/tenant_skills.py`](../azure-sdk-qa-bot-agent/skills/tenant_skills.py), `chat_agent/init.py` | Defines a per-tenant capability (description + QA guideline) and routes incoming requests to the matching skill. |
| `MCPStdioTool` | [`tools/ado_mcp_tools.py`](../azure-sdk-qa-bot-agent/tools/ado_mcp_tools.py), [`tools/azsdk_mcp_tools.py`](../azure-sdk-qa-bot-agent/tools/azsdk_mcp_tools.py) | Adapts external MCP servers (Azure DevOps, GitHub, azsdk) as agent tools over stdio. |
| `CompactionProvider` + `ToolResultCompactionStrategy` | `chat_agent/init.py`, `feedback_agent/init.py` | Compacts long tool outputs (e.g. search hits) to keep the working context within model limits. |
| `TruncationStrategy` | `utils/azure_ai_foundry.py` | Truncates oversize history before sending to the model. |
| `agent_framework_foundry_hosting.ResponsesHostServer` | `chat_agent/init.py`, `feedback_agent/init.py` | Hosts the composed `Agent` as a Foundry container exposing the Responses protocol — this is how the agent runs server-side. |

### 3.2 Azure AI Foundry SDK (`azure-ai-projects`)

Used for **out-of-band agent lifecycle and invocation** — anything that talks
to the Foundry control/data plane directly rather than going through the
authoring framework:

| Import | Where it's used | Purpose |
|---|---|---|
| `AIProjectClient` | [`scripts/deploy_hosted_agent.py`](../azure-sdk-qa-bot-agent/scripts/deploy_hosted_agent.py) | Creates/updates the hosted agent version on the Foundry project at deploy time. |
| `AIProjectClient.aio` + `AgentVersionDetails` | [`services/chat_service.py`](../azure-sdk-qa-bot-agent/services/chat_service.py) | The FastAPI backend resolves the deployed agent version and invokes it (Conversations + Responses) to answer chat requests. |

### 3.3 How the two layers fit together

```
┌──────────────────────────────────────────────────┐
│  Agent Framework (agent_framework)               │  ← authoring layer
│  Agent + Skills + Tools + Memory + Compaction    │
│           │                                      │
│           ▼ hosted by                            │
│  agent_framework_foundry_hosting                 │  ← runtime layer
│  (ResponsesHostServer container on Foundry)      │
└──────────────────────────────────────────────────┘
                       ▲
                       │ invoked via
┌──────────────────────────────────────────────────┐
│  Azure AI Foundry SDK (azure-ai-projects)        │  ← deploy + call layer
│  AIProjectClient: create_version, get, response  │
└──────────────────────────────────────────────────┘
```

### 3.4 Why this combination

- **Agent Framework** gives us a Pythonic, composable API for tools, skills,
  context providers, and compaction — much higher leverage than building
  directly on the Foundry SDK primitives.
- **Foundry hosting** moves the agent loop server-side: tool-call execution,
  retries, conversation state, and observability are handled by Foundry rather
  than the FastAPI backend.
- **`azure-ai-projects`** is the supported, GA path for managing Foundry agent
  versions and calling them from production services with Entra ID auth.

We evaluated the **GitHub Copilot SDK** as an alternative but it targets
developer-tool workflows (CLIs, code review, file-system tools) rather than the
enterprise Teams chatbot scenario we need.

## 4 Memory — Hybrid Foundry + Cosmos DB

We use a **hybrid memory architecture**:

| Memory Type | Store | Scope | Purpose |
|---|---|---|---|
| User profile (static) | **AI Foundry Memory Store** | per user | Personal preferences, SDK/language, project context. Fetched once per session. |
| User contextual (chat summary) | **AI Foundry Memory Store** | per user | Conversation-relevant memories retrieved every turn (incremental via `previous_search_id`). |
| Expert episodes | **Cosmos DB** (`experience-episodes`) | per tenant | Structured problem-solution pairs extracted from expert-resolved threads, retrieved by vector similarity. |

Foundry Memory handles per-user personalization with minimal code. Cosmos DB
holds the structured expert knowledge base because we need a custom schema,
deterministic upsert IDs, vector embeddings, and tenant-scoped partitioning —
which Foundry Memory does not currently support.

`MemoryContextProvider` injects both stores' results as a system message before
each agent turn, and writes the latest exchange back to Foundry after each run.

## 5 Evaluation

Evaluation lives in
[`azure-sdk-qa-bot-evaluation/`](../azure-sdk-qa-bot-evaluation/README.md). Because
the backend response shape (`answer` + `references`) was preserved during the
migration, the existing accuracy harness continues to work against the new
agent.

### 5.1 Library — Azure AI Evaluation SDK (`azure-ai-evaluation`)

The harness is built on Microsoft's **Azure AI Evaluation SDK**
(`azure-ai-evaluation >= 1.10.0`, pinned in
[`requirements.txt`](../azure-sdk-qa-bot-evaluation/requirements.txt)). It
provides the model-graded evaluators and the batch `evaluate(...)` driver that
runs them over a dataset and reports results to Azure AI Foundry.

**Built-in evaluators we use** (from `azure.ai.evaluation`):

| Evaluator | Where it's wired | Role |
|---|---|---|
| `SimilarityEvaluator` | [`evals_run.py`](../azure-sdk-qa-bot-evaluation/evals_run.py), [`AzureBotEvaluator`](../azure-sdk-qa-bot-evaluation/eval/evaluator/azure_bot_evaluator.py) | Semantic similarity of response vs ground truth. |
| `ResponseCompletenessEvaluator` | same | Coverage of ground-truth key points in the response. |
| `GroundednessEvaluator` | `evals_run.py` | Whether the response is grounded in the provided context. |

Each evaluator is instantiated with a `model_config` pointing at our Azure
OpenAI judge deployment (`AZURE_EVALUATION_MODEL_NAME`) and a pass threshold.

**Custom evaluators** (composed on top of the SDK):

| Evaluator | Purpose |
|---|---|
| [`AzureBotEvaluator`](../azure-sdk-qa-bot-evaluation/eval/evaluator/azure_bot_evaluator.py) | Composite "Bot Eval" score — a weighted blend of `SimilarityEvaluator` and `ResponseCompletenessEvaluator` outputs into a single `bot_evals` metric with pass/fail. |
| `AzureBotReferenceEvaluator` | Non-LLM check that the response's `references` / `knowledges` match the expected citations for each test case. |

### 5.2 How the harness runs

[`evals_run.py`](../azure-sdk-qa-bot-evaluation/evals_run.py) +
[`_evals_runner.py`](../azure-sdk-qa-bot-evaluation/_evals_runner.py) drive a
standard SDK pipeline:

1. **Collect responses** — call the bot API for each test case (or reuse cached
   responses with `--retrieve_response false`) and write a JSONL dataset of
   `{query, response, ground_truth, context, references, ...}` rows.
2. **Register evaluators** — wrap each evaluator as an `EvaluatorClass` with a
   `column_mapping` (e.g., `query → ${data.query}`, `response → ${data.response}`).
3. **Run `azure.ai.evaluation.evaluate(...)`** — the SDK fans the dataset out
   across all evaluators, calls the judge model in parallel, aggregates
   per-row scores and pass/fail, and (when `--send_result true`) uploads the
   evaluation run to the Azure AI Foundry project for tracking and comparison
   in the Foundry UI.
4. **Baseline + suppression** — `_evals_result.py` compares the run against a
   stored baseline (`baseline_check`) and applies
   [`suppression.json`](../azure-sdk-qa-bot-evaluation/suppression.json) to
   filter known noise before failing CI.

The harness runs both **locally** (against `DefaultAzureCredential` /
`AzureCliCredential`) and in **CI pipelines** (`AzurePipelinesCredential`),
with selectable evaluator subsets via `--evaluators`.

### 5.3 Metrics

| Metric | Range | Pass | What it measures |
|---|---|---|---|
| Bot Eval | 1–5 | ≥ 4 | Overall quality (relevance + completeness + usefulness). |
| Similarity | 1–5 | ≥ 4 | Semantic closeness to ground truth. |
| Response Completeness | 1–5 | ≥ 4 | Coverage of all key points in the ground truth. |
| Intent Resolution | 1–5 | ≥ 4 | How well the bot matched user intent. |
| Relevance | 1–5 | ≥ 3 | On-topic-ness (non-GT-anchored). |
| Coherence | 1–5 | ≥ 3 | Logical structure and clarity (non-GT-anchored). |

### 5.4 Dataset

110+ real cases collected from Teams channels over a 2-month window, covering
API Spec Review, General, Onboarding, Python, Release Support, and TypeSpec.

### 5.5 Agent vs RAG result (May 2026)

The agent-based bot improved every metric over the previous RAG backend:

| Metric | RAG | Agent | Delta |
|---|---:|---:|---:|
| Bot Eval | 3.49 | 3.64 | **+0.15** |
| Similarity | 3.80 | 3.87 | +0.07 |
| Response Completeness | 3.02 | 3.30 | **+0.28** |
| Intent Resolution | 4.78 | 4.99 | **+0.21** |
| Relevance | 4.85 | 5.00 | +0.15 |
| Coherence | 4.71 | 4.96 | **+0.25** |

Pass-rate gains include Bot Eval +7.2%, Response Completeness +7.2%, and Intent
Resolution +8.1% (reaching 100%). See the full
[performance report](../azure-sdk-qa-bot-evaluation/agent-vs-rag-performance-report.md).

### 5.6 Next: Usefulness evaluator

We plan to add a **usefulness evaluator** that analyzes full conversation
threads (not just single Q&A turns) to measure end-to-end helpfulness in real
Teams interactions.

## 6 Observability

Every agent turn, tool call, and memory operation is traced via OpenTelemetry
into Application Insights, giving us per-request visibility into model latency,
tool execution, and search-result quality.
