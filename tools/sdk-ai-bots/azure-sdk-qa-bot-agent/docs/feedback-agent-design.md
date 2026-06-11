# Feedback Agent Design

## 1 Background

When the chatbot's answer is wrong, today there is no systematic way to understand what the correct answer is, identify whether the underlying knowledge base (KB) is missing or stale, or close the gap so future similar queries are answered correctly. All follow-up is done manually by vendors investigating explicit thumbs-down feedback. The much more common implicit failure mode: an expert had to step in and answer — is never captured.

We are introducing a **Feedback Agent** — a new hosted agent in the Foundry project. It expects to automatically analyze both signals, classify the root cause, and file the KB-gap issues against the source-of-truth docs repo. Those signals will trigger the feedback agent:

| Signal | Source | Description |
| --- | --- | --- |
| **Explicit** | `POST /agent/feedback` with `reaction == bad` | User clicked thumbs-down on a bot answer. |
| **Implicit** | `POST /conversation/save` | A human expert (non-author, non-bot) replies in a thread the bot has already answered — a strong implicit "the bot didn't fully solve it." |

## 2 Design

### 2.1 Architecture

![alt text](feedback_agent_architecture.png)

### 2.2 Agent Design

The Feedback Agent is built on the `agent_framework` library and deployed as a Foundry hosted agent alongside the Chat Agent.

| Component | Purpose |
| --- | --- |
| **Instruction** | System prompt that tells the agent how to act as a feedback analyst: what the root-cause categories are, to back up its findings with evidence, and how to file KB-gap issues. |
| **Tools** | `fetch_chat_trace`, `fetch_conversation`, `search_knowledge_base` (reused), `web_fetch` (reused), `resolve_kb_source`, GitHub MCP. |

#### 2.2.1 Tools

| Tool | File | Type | Description |
| --- | --- | --- | --- |
| `fetch_chat_trace` | `tools/monitor_tools.py` (new) | `FunctionTool` | Fetches the Chat Agent's App Insights trace by `response_id` and returns the trace details: ordered tool calls (name, args summary, results summary, duration), retrieved chunks, final answer, prompt. |
| `fetch_conversation` | `tools/conversation_tools.py` (new) | `FunctionTool` | Returns the full thread transcript for the conversation under analysis (Cosmos `conversation-messages`). |
| `search_knowledge_base` | `tools/knowledge_tools.py` | `FunctionTool` | Re-runs targeted KB searches to confirm what is/isn't indexed today. Reused unchanged from the Chat Agent. |
| `web_fetch` | `tools/web_tools.py` | `FunctionTool` | Fetches the source-of-truth doc URL to detect drift between KB content and upstream docs. Reused unchanged from the Chat Agent. |
| `resolve_kb_source` | `tools/knowledge_tools.py` (extend) | `FunctionTool` | Maps the chunk's `source` folder to `{owner, repo, branch, path, labels}` by looking up `knowledge-config.json` from the `azure-sdk-qa-bot-knowledge-sync` project. |
| `create_issue` | `tools/github_mcp_tools.py` | MCP Server | The existing GitHub MCP tool|

#### 2.2.2 Issue Classification

The agent classifies each case into exactly one root cause and acts accordingly:

| Classification | Description | Category | Issue Repo |
| --- | --- | --- | --- |
| `missing_content` | No KB chunk covers the user's intent. | KB issue | KB source repo |
| `outdated_content` | KB has stale information vs. the source URL. | KB issue | KB source repo |
| `retrieval_mismatch` | Relevant chunks exist but were not retrieved. | Bot-quality issue | azure-sdk-pr |
| `reasoning_gap` | Chunks were retrieved but the bot reasoned poorly. | Bot-quality issue | azure-sdk-pr |
| `out_of_scope` | The intent is outside the tenant's scope. | Bot-quality issue | azure-sdk-pr |

#### 2.2.3 Agent Instruction

The hosted agent runs once per job. Its input is the `FeedbackJob` payload (serialized as JSON in a single user message); its output is a structured result the background task persists. Draft instruction:

```md
# Feedback Analyst Instructions

You are an Azure SDK QA feedback analyst. A user gave a thumbs-down, or an
expert had to step in on a thread the bot already answered. Your job is to
find the root cause of the bad answer and file a precise issue against the
right repository.

## Input

A JSON payload with: `trigger`, `tenant_id`, `conversation_id`,
`conversation_type`, `response_id`, and (for `bad_reaction`) the user's
`comment` and `reasons`.

## Workflow

1. **Gather evidence first — in parallel:**
   - `fetch_chat_trace(response_id)` — the bot's original tool calls,
     retrieved chunks, prompt, and final answer.
   - `fetch_conversation(conversation_id)` — the full thread, including the
     expert reply (the ground-truth correct answer for the `expert_reply`
     trigger).
2. **Reproduce the retrieval.** Call `search_knowledge_base` with the user's
   intent to confirm what is indexed today. If a chunk looks stale, call
   `web_fetch` on its source URL to check for drift.
3. **Classify** the case into exactly one root cause (see taxonomy below).
4. **File the issue.** Call `resolve_kb_source` on the relevant chunk source
   to pick the target repo (KB source repo for `missing_content` /
   `outdated_content`; `azure-sdk-pr` for bot-quality classifications), then
   `create_issue` with the structured body in §2.5.
5. **Return** the structured result.

## Classification

- `missing_content` — no KB chunk covers the intent → KB source repo.
- `outdated_content` — KB contradicts the source URL → KB source repo.
- `retrieval_mismatch` — relevant chunks exist but weren't retrieved →
  azure-sdk-pr.
- `reasoning_gap` — chunks were retrieved but the bot reasoned poorly →
  azure-sdk-pr.
- `out_of_scope` — the intent is outside the tenant's scope → azure-sdk-pr.

## Output

Return JSON: `classification`, `user_intent` (one sentence),
`suggested_fix` (one sentence), `corrected_answer` (grounded in the trace,
conversation, and search results — cite source URLs), and `issue_url`.

## Rules

- Ground every claim in tool results — never invent KB state or URLs.
- For `expert_reply`, treat the expert's message as the correct answer and
  work backward to why the bot missed it.
- Redact PII (names, emails, tokens) from anything you write into an issue.
- Be concise; this output feeds a dataset and an issue, not a chat reply.
```

### 2.3 Triggers & Job Lifecycle

| Trigger | Endpoint | Behavior |
| --- | --- | --- |
| **Explicit `bad` reaction** | `POST /agent/feedback` | The endpoint synchronously persists the feedback, then resolves `response_id` from the **most recent bot message** in the conversation and writes a job record. |
| **Expert reply** | `POST /conversation/save` | Only If the expert message meets the episode extraction gate, then locates the **most recent bot message** preceding the reply, resolve its `response_id`, and write a job record. |

Both triggers are gated by `FEEDBACK_AGENT_ENABLED` so the feature can be disabled without a code rollback.

A job record in the `feedback-jobs` Cosmos container progresses through `created → running → done | failed`. There is no separate queue or broker: the endpoint writes the record in `created` and hands it to an in-process asyncio task that calls the agent.

![alt text](job_lifecycle.png)

The **background task is the writer** of the record. The agent's responsibility is analysis and `create_issue`, it returns a structured result (classification, user-intent summary, suggested-fix summary, corrected answer, optional `issue_url`).

#### Feedback Job

Each job is a row in the `feedback-jobs` Cosmos container (partition key `/tenant_id`). The job is a **lifecycle marker** — the hosted agent owns the analysis and issue filing through its own tools; the service only tracks status and logs the agent reply for triage.

```typespec
@doc("Which signal created the feedback job")
union FeedbackJobTrigger {
  @doc("User clicked thumbs-down on a bot answer")
  BadReaction: "bad_reaction",

  @doc("An expert replied in a thread the bot had already answered")
  ExpertReply: "expert_reply",
}

@doc("Lifecycle state of a feedback job")
union FeedbackJobStatus {
  @doc("Record written, not yet picked up")
  Created: "created",

  @doc("Background task is invoking the agent")
  Running: "running",

  @doc("Agent returned a result and it was persisted")
  Done: "done",

  @doc("Agent errored or timed out")
  Failed: "failed",
}

@doc("Durable feedback-analysis job persisted in the `feedback-jobs` Cosmos container")
model FeedbackJob {
  @doc("Deterministic ID `{conversation_id}:{trigger}:{timestamp}` — also the dedup key")
  id: string;

  @doc("Tenant the job belongs to (partition key)")
  tenant_id: string;

  @doc("Which signal created the job")
  trigger: FeedbackJobTrigger;

  @doc("The bot response under analysis (joins to the App Insights trace)")
  response_id: string;

  @doc("Source thread identifier")
  conversation_id: string;

  @doc("Channel / chat type, used to resolve the trace and message link")
  conversation_type: ConversationType;

  @doc("Free-text feedback (explicit `bad_reaction` only)")
  comment?: string;

  @doc("Structured reason tags (explicit `bad_reaction` only)")
  reasons?: string[];

  @doc("Lifecycle state (see diagram above)")
  status: FeedbackJobStatus;

  @doc("The GitHub issue filed by the agent for `missing_content` / `outdated_content`, if any")
  issue_url?: string;

  @doc("UTC timestamp when the job was created")
  created_at: utcDateTime;

  @doc("UTC timestamp of the last status change")
  updated_at: utcDateTime;

  @doc("Failure context (e.g. `agent_invocation_failed`, `timeout`); absent on success")
  error?: string;
}
```

### 2.5 Create KB Issue

Issue creation reuses the existing **GitHub MCP tool** ([`tools/github_mcp_tools.py`](../tools/github_mcp_tools.py)), we will add `create_issue` tool into the allowlist.

For `missing_content` / `outdated_content` issues, the agent calls `resolve_kb_source` to map the chunk source to a target repo, then `create_issue`. Example:

> **Title:** [Doc] No guidance on the TypeSpec `@added` versioning decorator
>
> **Labels:** `doc`
>
> **Gap:** There is no documentation covering the `@added` decorator; the bot answered with a generic versioning explanation that did not address the question.
>
> **Suggested change:** Add a section to `versioning.md` documenting `@added`/`@removed`, with an example. Source: https://typespec.io/docs/libraries/versioning/reference/decorators
>
> **Evidence:** Bot Response ID: `resp_abc123`, Message Link: `https://xxxx`

Folders mapped to non-issue-fileable targets (internal ADO, wikis) fall back to azure-sdk-pr repo.