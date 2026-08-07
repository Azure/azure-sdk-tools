# Chatbot Evolution Agent Design

## 1 Background

When the chatbot's answer is wrong, today there is no systematic way to understand what the correct answer is, identify whether the underlying knowledge base (KB) is missing or stale, or close the gap so future similar queries are answered correctly. All follow-up is done manually by vendors investigating explicit thumbs-down feedback. The much more common implicit failure mode: an expert had to step in and answer — is never captured.

We are introducing a **Chatbot Evolution Agent** — a new hosted agent in the Foundry project. It automatically analyzes wrong answers, classifies the root cause, and proposes a concrete fix. For knowledge-base (KB) issues, the agent loops through candidate generation and validation until the original bad case passes, then files an issue. For chatbot self-issues, the agent files an issue with its diagnosis and suggested fix without entering the validation loop. The daily job also revisits agent-created issues after they are closed and reruns the original bad case to verify that the implemented fix works in the deployed dev Chat Agent. Rather than firing in real time off an endpoint, the flow is driven by a **daily batch scan** (see §2.3): the Evolution Agent first decides whether each QA thread has concluded and whether its bot answer has a real problem; only confirmed failures continue into diagnosis and remediation. Two human signals in the thread inform that judgement and the agent's analysis:

| Signal | Source | Description |
| --- | --- | --- |
| **Explicit** | User feedback card (`reaction == bad`) | User clicked thumbs-down on a bot answer; captured in the thread and surfaced by `fetch_conversation`. |
| **Implicit** | Expert reply in the thread | A human expert (non-author, non-bot) replies in a thread the bot has already answered — a strong implicit "the bot didn't fully solve it." |

## 2 Design

### 2.1 Architecture

![Chatbot Evolution Agent architecture](chatbot_evolution_agent_architecture.png)

The architecture has five execution planes:

- **Detection:** the daily feedback job ingests QA threads, and the Evolution Agent identifies concluded conversations with an incorrect or unconfirmed bot answer.
- **Evolution loop:** the Evolution agent diagnoses the failure. For a KB issue, it writes a candidate to the existing dev knowledge source, validates the original bad case against the deployed dev Chat Agent, and revises until the case passes or the attempt limit is reached.
- **Issue creation:** after KB validation passes, the same Evolution agent creates the GitHub issue with the diagnosis, proposed source change, answer, trace ID, and validation evidence. For chatbot self-issues, it creates the issue immediately after diagnosis without validation.
- **Restoration:** the feedback orchestrator queues the knowledge-sync pipeline at most once per run when an Evolution-agent session mutated the KB or a closed KB issue is ready for validation, then waits for it to restore dev storage and search from authoritative upstream sources.
- **Closed-issue validation:** for an agent-created issue whose QA record is `pending_validation`, the pipeline waits for closure, then the Evolution Agent reruns the original bad case, comments the result and trace ID on the issue, labels the fix as passed or failed, and persists the terminal Cosmos state.

```text
ongoing QA record
    │
    ▼
Evolution agent completion/correctness gates
    │
    ├── ongoing → keep qa_status=ongoing
    ├── correct → qa_status=finished
    └── problem → qa_status=failed → diagnosis
    │
    ├── KB issue
    │     └── update_knowledge
    │           └── ask_chat_agent(original bad case)
    │                 ├── fail → revise candidate and retry
    │                 └── pass → issue_write
    │
    └── chatbot self-issue → issue_write

after feedback sessions → if KB was mutated or a closed KB issue is pending → sync dev knowledge
issue created → feedback.status=pending_validation
closed agent-created issue → validate original bad case → comment evidence → label and persist passed or failed
```

### 2.2 Agent Design

The Chatbot Evolution Agent is built on the `agent_framework` library and deployed as a Foundry hosted agent alongside the Chat Agent.

| Component | Purpose |
| --- | --- |
| **Instruction** | System prompt that tells the agent how to act as a feedback analyst: what the root-cause categories are, how to back up findings with evidence, and how to propose a safely testable fix. |
| **Tools** | Analysis: `fetch_chat_trace`, `fetch_conversation`, `search_knowledge_base` (reused), `web_fetch` (reused), and `resolve_kb_source`. Validation: guarded `update_knowledge` and the shared Chat Agent invocation tool, temporarily named `ask_chat_agent`. Issue creation and closed-issue updates: existing GitHub MCP. |

#### 2.2.1 Tools

| Tool | File | Type | Description |
| --- | --- | --- | --- |
| `fetch_chat_trace` | `tools/monitor_tools.py` (new) | `FunctionTool` | Fetches the Chat Agent's App Insights trace by `trace_id` and returns the trace details: ordered tool calls (name, args summary, results summary, duration), retrieved chunks, final answer, prompt. |
| `fetch_conversation` | `tools/conversation_tools.py` (new) | `FunctionTool` | Returns the full thread transcript for the conversation under analysis (Cosmos `conversation-messages`); each bot message includes its `trace_id`. |
| `search_knowledge_base` | `tools/knowledge_tools.py` | `FunctionTool` | Re-runs targeted KB searches to confirm what is/isn't indexed today. Reused unchanged from the Chat Agent. |
| `web_fetch` | `tools/web_tools.py` | `FunctionTool` | Fetches the source-of-truth doc URL to detect drift between KB content and upstream docs. Reused unchanged from the Chat Agent. |
| `resolve_kb_source` | `tools/knowledge_tools.py` (extend) | `FunctionTool` | Maps the chunk's `source` folder to `{owner, repo, branch, path, labels}` by looking up `knowledge-config.json` from the `azure-sdk-qa-bot-knowledge-sync` project. |
| `issue_write` | `tools/github_mcp_tools.py` | MCP Server | The existing GitHub MCP tool. Creates a chatbot self-issue after diagnosis or a KB issue after validation passes. |
| `issue_read`, `add_issue_comment`, `issue_write` | `tools/github_mcp_tools.py` | MCP Server | Existing GitHub MCP tools used to read closed agent-created issues, record validation evidence, and replace the pending label with a passed or failed label. |
| `update_knowledge` | `tools/knowledge_tools.py` | `FunctionTool` | Writes candidate markdown to an existing tenant-configured folder in the dev knowledge container and refreshes the existing dev AI Search index. |
| `ask_chat_agent` | Shared Chat Agent tool (pending) | `FunctionTool` | Sends the original bad case to the deployed dev Chat Agent and returns its answer, trace ID, citations, retrieved documents, and pass/fail evidence. |

#### 2.2.2 Issue classification

The agent classifies each case into exactly one root cause and acts accordingly:

| Classification | Description | Category | Issue Repo |
| --- | --- | --- | --- |
| `missing_content` | No KB chunk covers the user's intent. | KB issue | `Azure/azure-sdk-pr` (cite KB source) |
| `outdated_content` | KB has stale information vs. the source URL. | KB issue | `Azure/azure-sdk-pr` (cite KB source) |
| `retrieval_mismatch` | Relevant chunks exist but were not retrieved. | System issue | `Azure/azure-sdk-pr` |
| `reasoning_gap` | Chunks were retrieved but the bot reasoned poorly. | System issue | `Azure/azure-sdk-pr` |
| `out_of_scope` | The intent is outside the tenant's scope. | System issue | `Azure/azure-sdk-pr` |

#### 2.2.3 Agent instruction

The Agent receives a small JSON payload identifying the QA thread and requested mode (serialized as a single user message). In analysis mode it first applies the completion and correctness gates, then runs the existing bounded validation loop for confirmed KB failures. For a KB classification it must prepare bounded candidate markdown and validation evidence for the issue. Chatbot self-issues skip the candidate-validation loop and are created directly. In validation mode the Agent reruns the original bad case after the stored issue closes. The Agent cannot perform arbitrary repository writes or write directly to production storage. Draft instruction:

```md
# Feedback Analyst Instructions

You are an Azure SDK QA feedback analyst. First decide whether the past
conversation is complete and whether the bot answer has a real problem.
For a confirmed failure, find the root cause and prepare a precise issue.
For a knowledge gap, validate the KB fix before creating the issue. After
the issue closes, validate the deployed fix against the original case.

## Input

A JSON payload with `mode`, `tenant_id`, `conversation_id`,
`conversation_type`, and `issue_url` (validation only).

## Workflow

1. **Reconstruct the thread.** Call
   `fetch_conversation(conversation_id, conversation_type)` first and use
   the full thread, including user feedback and expert replies.
2. **Apply the analysis gates.** Decide whether the conversation is
   complete, then whether the answer has a problem. Stop before diagnosis
   for `conversation_ongoing` or `no_issue`.
3. **Gather failure evidence.** For a confirmed problem, call
   `fetch_chat_trace(trace_id)` using the failed bot turn from the
   transcript.
4. **Reproduce the retrieval.** Call `search_knowledge_base` with the user's
   intent to confirm what is indexed today. If a chunk looks stale, call
   `web_fetch` on its source URL to check for drift.
5. **Classify** the case into exactly one root cause (see taxonomy below).
6. **Propose a fix.** For `missing_content` and `outdated_content`, provide candidate markdown plus source/path and tenant metadata. For system issues, describe the suggested bot change.
7. **Validate KB remediation.** For `missing_content` and `outdated_content`,
   call `update_knowledge` to write the candidate into the matching existing
   dev knowledge folder, then call `ask_chat_agent` with the original bad
   case. If it fails, revise the candidate and repeat within the attempt limit.
8. **Create the KB issue after validation.** When the original bad case passes,
   call `resolve_kb_source`, build the issue with the validation evidence, and
   call `issue_write`. Never create a KB issue before validation passes.
9. **Handle chatbot self-issues.** Record the diagnosis and suggested fix,
   then call `issue_write` without entering the candidate-validation loop.
10. **Validate a closed issue.** In validation mode, read `issue_url`,
    replay the original bad case through `ask_chat_agent`, comment the
    evidence, replace the pending validation label, and return the result.
11. **Return** the fixed-schema result.

## Classification

- `missing_content` — no KB chunk covers the intent (KB issue, cite source).
- `outdated_content` — KB contradicts the source URL (KB issue, cite source).
- `retrieval_mismatch` — relevant chunks exist but weren't retrieved.
- `reasoning_gap` — chunks were retrieved but the bot reasoned poorly.
- `out_of_scope` — the intent is outside the tenant's scope.

## Output

Return only the fixed-schema JSON object containing `outcome`, `reasoning`,
`confidence`, `classification`, and `issue_url`.
Candidate content and validation evidence are recorded in the remediation
issue rather than duplicated in the status result.

## Rules

- Ground every claim in tool results — never invent KB state or URLs.
- Do not diagnose or mutate dev knowledge before the completion and
  correctness gates pass.
- When an expert corrected the bot, treat the expert's message as the
  correct answer and work backward to why the bot missed it.
- Redact PII (names, emails, tokens) from anything you write into an issue.
- Be concise; this output feeds a dataset and an issue, not a chat reply.
```

### 2.3 QA Status Table & Job Lifecycle

The feedback loop is driven by a **daily batch job** over a durable status table rather than by real-time endpoint triggers. Every QA thread the bot answered is recorded once in the `qa-records` Cosmos container (partition key `/tenant_id`, `id = {conversation_type}:{conversation_id}`), and each record carries **two status layers**:

| Layer | Field | States | Meaning |
| --- | --- | --- | --- |
| **1 — QA lifecycle** | `qa_status` | `ongoing` → `finished` \| `failed` | `ongoing` while the thread is still open; `finished` once it concluded with a **correct** bot answer (archived); `failed` once it concluded with an **incorrect/unknown** bot answer (worth a feedback analysis). |
| **2 — Feedback lifecycle** | `feedback.status` | `created` → `running` → `pending_validation` → `done` \| `failed` | Tracks Agent execution, issue remediation, and post-close validation. |

#### Daily scan (`scripts/run_feedback_jobs.py`, `pipelines/feedback-job.yml`)

1. **Ingest** — read conversation messages active in the window, aggregate them by `conversation_id` into threads, and upsert one QA record per thread (new threads start `ongoing`). Threads in **testing channels** identified from their configured `channel.yaml` display names are excluded so the loop never files issues for test traffic.
2. **Analyze** — invoke the hosted Evolution Agent for every eligible `ongoing` record. The Agent first decides whether the thread has **finished**, then whether the bot answer has a real problem:
    - still ongoing → clear transient feedback state and stay `ongoing`;
    - finished + correct → `qa_status=finished`, `feedback.status=done`;
    - finished + problem → set `qa_status=failed`, run diagnosis and the KB candidate-validation loop when applicable, then create the remediation issue and set `feedback.status=pending_validation`.
3. **Closed-issue scan** — read `pending_validation` records and find issues whose stored GitHub issue is closed. Issue closure, not labels, determines validation eligibility.
4. **Restore** — if any analysis session mutated the KB or a closed KB issue needs validation, queue the knowledge-sync pipeline once and wait for successful restoration.
5. **Validate fixes** — rerun each closed issue's original bad case, comment the evidence, replace `fix-validation:pending` with `fix-validation:passed` or `fix-validation:failed`, and persist `feedback.status=done` or terminal `failed`.

The whole feature is gated by `CHATBOT_EVOLUTION_AGENT_ENABLED` so it can be disabled without a code rollback.

#### 2.3.1 Feedback-session invocation

The daily batch job (`scripts/run_feedback_jobs.py`) invokes `ChatbotEvolutionAgentService` synchronously in `analysis` or `validation` mode. Analysis starts with the completion and correctness gates. For a confirmed KB issue, the hosted Agent performs the existing bounded loop: it gathers evidence, proposes a candidate, writes it to the existing dev knowledge source, calls the deployed dev Chat Agent, interprets the result, and revises the candidate when needed. The Agent calls `issue_write` only after the original bad case passes. Chatbot self-issues skip candidate validation and are created after diagnosis. In validation mode, the same Agent validates closed agent-created issues without proposing another fix.

The Agent is invoked through the Responses API (`store=True`) with bounded analysis and iteration limits. Its fixed-schema result drives the Cosmos status transition. The guarded tools own dev-storage writes, indexing, chatbot invocation, and evidence collection. No public issue is created for an unvalidated KB candidate. Interrupted `created` or `running` records and terminal `failed` records are not retried automatically.

The feedback orchestrator runs mutating sessions serially and validates each case immediately after its candidate update. Before `update_knowledge` writes anything, its wrapper sets a run-scoped `restore_required` Azure Pipelines output variable to `true`, ensuring that partial writes or later failures still trigger cleanup without adding fields to `QARecord`. After those sessions finish, the orchestrator queues `sync_knowledge.yml` once through the Azure DevOps Build REST API using `$(System.AccessToken)` when `restore_required` is `true` or a closed KB issue needs validation, waits for completion, and then validates the closed issues. The feedback job is not complete until required restoration and closed-issue validation finish.

#### QA record

Each row is a `QARecord` in the `qa-records` Cosmos container.

```typespec
@doc("Layer-1 lifecycle state of a QA thread")
union QAStatus {
  @doc("Thread still open / not yet conclusively judged")
  Ongoing: "ongoing",

  @doc("Thread concluded with a correct bot answer (archived)")
  Finished: "finished",

  @doc("Thread concluded with a wrong/unconfirmed answer (needs feedback)")
  Failed: "failed",
}

@doc("Layer-2 lifecycle state of the chatbot-evolution-agent analysis")
union FeedbackStatus {
  @doc("A feedback session has been requested/persisted")
  Created: "created",

  @doc("The hosted agent accepted and is processing")
  Running: "running",

  @doc("An issue was created; wait for it to close before validating")
  PendingValidation: "pending_validation",

  @doc("The agent finished and the result was persisted")
  Done: "done",

  @doc("The agent errored, timed out, or was cancelled")
  Failed: "failed",
}

@doc("Embedded Layer-2 feedback lifecycle")
model FeedbackState {
  status: FeedbackStatus;

  @doc("Failure context; absent while healthy")
  error?: string;

  issue_url?: string;
  classification?: string;
  validation_reasoning?: string;
  validated_at?: utcDateTime;
  created_at?: utcDateTime;
  updated_at?: utcDateTime;
}

@doc("Two-layer status row for one QA thread, in the `qa-records` Cosmos container")
model QARecord {
  @doc("Deterministic thread key `{conversation_type}:{conversation_id}`")
  id: string;

  @doc("Tenant the thread belongs to (partition key)")
  tenant_id: string;

  conversation_id: string;
  conversation_type: ConversationType;

  @doc("Teams channel the thread belongs to (used to exclude testing channels)")
  channel_id?: string;

  @doc("Deep link back to the conversation thread")
  message_link?: string;

  // -- Layer 1 --
  qa_status: QAStatus;

  @doc("The Evolution Agent's verdict on the bot's answer")
  verdict?: string;
  reasoning?: string;
  confidence?: float32;
  has_expert_reply: boolean;
  message_count: int32;

  // -- Layer 2 --
  feedback?: FeedbackState;

  last_activity_at?: utcDateTime;
  first_seen_at: utcDateTime;
  evaluated_at?: utcDateTime;
  created_at: utcDateTime;
  updated_at: utcDateTime;
}
```

### 2.4 Conversation Evaluation

In the feedback loop, the Evolution Agent judges each `ongoing` thread and decides **two things, in order**: first whether the conversation is **finished** (vs. still ongoing), then whether the bot answer has a real problem. The `finished` gate lets a thread stay `ongoing` across runs until it actually concludes, and the correctness gate prevents diagnosis or KB mutation for a successful answer.

The existing `conversation-eval` pipeline remains an independent reporting workflow and continues to use `prompts/conversation_evaluation.md` and `ConversationService.evaluate_conversation`. Its result does not drive the feedback lifecycle.


### 2.5 KB validation loop

Only KB classifications enter the automated validation loop. A knowledge-gap issue cannot be validated by rerunning the current production bot because the missing content is still absent. In the dev environment, the agent therefore writes candidate markdown into the existing tenant-configured knowledge folder, refreshes the existing dev index, and calls the deployed dev Chat Agent.

The Evolution agent owns the loop through two guarded tools:

1. Call `update_knowledge` to write the candidate to the existing dev knowledge source and refresh the index.
2. Call the shared Chat Agent tool, temporarily named `ask_chat_agent`, with the original bad case.
3. If validation fails, revise the candidate and repeat within the attempt limit. If it passes, create the issue with the answer and trace ID as evidence.

After all agent sessions finish, fail, or time out, the feedback pipeline triggers the knowledge-sync pipeline once if any session mutated the KB. The feedback job waits for restoration from the authoritative sources and is marked failed if restoration does not succeed.

### 2.6 Issue creation

The Evolution Agent may prepare the issue content during analysis, but it must complete the KB validation loop before creating the issue. Only after `ask_chat_agent` shows that the original bad case passes may the Agent call `issue_write` in **`Azure/azure-sdk-pr`** through the existing GitHub MCP tool ([`tools/github_mcp_tools.py`](../tools/github_mcp_tools.py)).

Every Agent-created issue includes the sanitized original bad case, expected behavior, and the `fix-validation:pending` label. The backend also stores the issue URL and `feedback.status=pending_validation` in the QA record so the daily job can find and validate it after closure. For KB issues (`missing_content` / `outdated_content`), the Agent calls `resolve_kb_source` and cites the upstream source in the issue:

> **Title:** [Doc] No guidance on the TypeSpec `@added` versioning decorator
>
> **Labels:** `feedback-agent`, `classification:missing_content`, `fix-validation:pending`
>
> **KB source:** `Azure/azure-rest-api-specs-pr` — `documentation/typespec/versioning.md`
>
> **Gap:** There is no documentation covering the `@added` decorator; the bot answered with a generic versioning explanation that did not address the question.
>
> **Suggested change:** Add a section to `versioning.md` documenting `@added`/`@removed`, with an example. Source: https://typespec.io/docs/libraries/versioning/reference/decorators
>
> **Validation:** The deployed dev Chat Agent passed the original bad case. Trace ID: `abc123def456`.

When the KB source is unmapped or non-GitHub, `resolve_kb_source` returns `resolved=false` and the agent records the raw folder name. Chatbot self-issues include the diagnosis and suggested fix but no validation evidence.

### 2.7 Closed-issue validation

The daily feedback job reads `pending_validation` QA records and checks their stored issues for closure; labels do not gate validation eligibility. For a KB issue, it first waits for the knowledge-sync pipeline so the deployed dev Chat Agent uses the authoritative fixed content rather than the temporary candidate. The Evolution Agent then calls `ask_chat_agent` with the sanitized original bad case stored in the issue.

The Agent comments the returned answer, trace ID, and pass/fail evidence on the closed issue. It replaces the pending label with `fix-validation:passed` when the original case now succeeds or `fix-validation:failed` when it does not; a failed validation does not automatically reopen the issue. The backend also persists `feedback.status=done` for a pass or terminal `feedback.status=failed` for a failure. The historical `qa_status` remains `failed` because the original answer was wrong. The terminal Cosmos status and issue label prevent the same closed issue from being validated again on later daily runs.
