# Security Design

<!-- markdownlint-configure-file { "MD024": { "siblings_only": true } } -->

## 1. Overview

The agents run as Foundry hosted agents (custom containers, Responses protocol), built on the `agent_framework` SDK with an in-container tool loop. They share infrastructure and some security utilities, but **their tool permissions and risks differ**.

All knowledge sources are either **documents broadly available within the organization (internal-public documents)** or **publicly available documents**.

| Agent | Purpose | Tool permissions |
| --- | --- | --- |
| [Azure SDK Chat QA agent](#2-azure-sdk-chat-agent) | Answer SDK development and release questions in Teams. | Read-only: internal knowledge, web, GitHub, Azure DevOps, and pipeline analysis. |
| [Azure MCP Server QA agent](#3-azure-mcp-server-qa-agent) | Answer Azure MCP Server usage and troubleshooting questions. | Read-only: internal knowledge, web, GitHub, and Microsoft Learn. No Azure resource-management tools. |
| [Chatbot evolution agent](#4-chatbot-evolution-agent) | Review past answers and test improvements. | **Read and write**: candidate KB edits and index refreshes, validation against candidate/production agents, and GitHub issue/comment publication. |

## 2. Azure SDK Chat agent

### Capabilities

The agent answers developer questions using internal documentation, web content, GitHub repositories/discussions, and Azure DevOps builds/work items. Its registered tools cannot create, edit, delete, merge, or approve repository/work-item content.

### Risks and Safeguards

| Risk ID/name | How it applies | How we reduce the risk |
| --- | --- | --- |
| R1 — Harmful content | Answers could generate or repeat harmful content. | The prompt tells the agent to refuse harmful requests (C6). The agent is configured with a Foundry content-safety guardrail (C5; see [Shared Controls](#5-shared-controls)). |
| R2 — User prompt attack (jailbreak) | User questions could override instructions. | The agent is configured with a Foundry content-safety guardrail (C5). |
| R3 — Document attack (indirect prompt injection) | Retrieved docs, GitHub, web, or ADO content could redirect answers. | The prompt tells the agent to use tool output as reference data, not instructions (C6). Code filters authored GitHub body text by author association (C3) and labels external MCP string results as untrusted (C4). The agent is also configured with a Foundry content-safety guardrail (C5). |
| R4 — Ungrounded content / hallucination | Answers could invent facts, links, or recommendations. | The [SDK Chat instructions](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/azure-sdk-qa-bot-agent/agents/chat_agent/instruction.md) tell the agent to retrieve evidence and avoid unsupported facts, links, or claims of write actions (C6). This guides model behavior; it does not enforce resource permissions. |
| R5 — Protected material reproduction | Answers could reproduce protected source material. | The prompt tells the agent to avoid reproducing protected material (C6). The agent is configured with a Foundry content-safety guardrail (C5). |
| R6 — Task adherence | Wrong read-tool selection or parameters could produce incorrect results. | Tool registration exposes reads and analysis, not content changes (C1). GitHub requests read-only mode and selected toolsets, and the client allows approximately 15 read operations; approvals are disabled because no writes are registered. ADO permits project, pipeline/build, artifact, and work-item/comment reads only. Native retrieval, web, and pipeline analysis cannot modify content. The prompt guides tool selection (C6), and execution limits cap tool use (C7; see R7). |
| R7 — Agent hijacking | Redirected tool use could fetch internal URLs (SSRF) or misuse read access. | Only read operations are available (C1), with at most **5 tool-loop iterations / 10 tool calls per turn** (C7). Web-fetch code rejects private/internal destinations and rechecks redirects (C2). The prompt guides handling of untrusted output (C6). |
| R8 — Sensitive data leakage | Answers or tool requests could disclose credentials or internal information to unauthorized recipients. | Credential handling supports managed identity and short-lived GitHub App tokens; a configured static GitHub token takes precedence (C8). This protects service authentication, but does not decide which retrieved information a recipient may see. |

Sources: [agent registration](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/azure-sdk-qa-bot-agent/agents/chat_agent/init.py), [GitHub tool configuration](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/azure-sdk-qa-bot-agent/tools/github_mcp_tools.py), [Azure DevOps tool configuration](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/azure-sdk-qa-bot-agent/tools/ado_mcp_tools.py).

## 3. Azure MCP Server QA agent

Related PR: [#16729 — Add Azure MCP Server agent support](https://github.com/Azure/azure-sdk-tools/pull/16729).

### Capabilities

This agent answers questions **about Azure MCP Server**. It searches internal knowledge, wiki/web content, GitHub, and Microsoft Learn. It does **not** register Azure resource-management tools and cannot provision or modify Azure resources.

### Risks and Safeguards

| Risk ID/name | How it applies | How we reduce the risk |
| --- | --- | --- |
| R1 — Harmful content | Troubleshooting answers could contain harmful content. | The agent's prompt includes safety guidance (C6). The agent is configured with a Foundry content-safety guardrail (C5; see [Shared Controls](#5-shared-controls)). |
| R2 — User prompt attack (jailbreak) | Questions could override instructions. | The agent is configured with a Foundry content-safety guardrail (C5). |
| R3 — Document attack (indirect prompt injection) | Knowledge, wiki/web, GitHub, or Learn results could redirect answers. | The prompt guides handling of untrusted tool output (C6). Code filters authored GitHub body text by author association (C3) and labels external MCP string results, including Learn results, as untrusted (C4). The agent is also configured with a Foundry content-safety guardrail (C5). |
| R4 — Ungrounded content / hallucination | Usage advice or troubleshooting recommendations could lack evidence. | The [Azure MCP instructions](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/azure-sdk-qa-bot-agent/agents/azure_mcp_server_agent/instruction.md) tell the agent to ground answers in evidence (C6). Instructions to retrieve quickly improve efficiency; they do not enforce a time limit or security boundary. |
| R5 — Protected material reproduction | Troubleshooting answers could reproduce protected documentation. | The agent's prompt includes safety guidance (C6). The agent is configured with a Foundry content-safety guardrail (C5). |
| R6 — Task adherence | Wrong retrieval tools or parameters could lead to irrelevant advice. | Tool registration permits retrieval and read-only GitHub access, but no ADO, pipeline-analysis, or Azure management operations (C1). Learn access uses the fixed endpoint `https://learn.microsoft.com/api/mcp`, only `microsoft_docs_search` / `microsoft_docs_fetch`, and a **30-second timeout**. The prompt guides retrieval (C6), and execution limits cap tool use (C7; see R7). |
| R7 — Agent hijacking | An attacker could redirect web fetching (SSRF) or misuse read tools, not provision Azure resources. | Only the registered read tools are available (C1), with at most **5 tool-loop iterations / 10 tool calls per turn** (C7). Web-fetch code rejects private/internal destinations and rechecks redirects (C2). The prompt guides handling of untrusted output (C6). |
| R8 — Sensitive data leakage | Answers or tool requests could expose credentials or internal knowledge to unauthorized recipients. | Credential handling supports managed identity and short-lived GitHub App tokens; a configured static GitHub token takes precedence (C8). This protects service authentication, but does not decide which internal knowledge a recipient may see. |

Sources: [agent registration](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/azure-sdk-qa-bot-agent/agents/azure_mcp_server_agent/init.py), [Microsoft Learn tool configuration](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/azure-sdk-qa-bot-agent/tools/mslearn_mcp_tools.py).

## 4. Chatbot evolution agent

Related PRs:

- [#16777 — Add chatbot evolution agent](https://github.com/Azure/azure-sdk-tools/pull/16777)
- [#16986 — Fix evolution agent validation lifecycle](https://github.com/Azure/azure-sdk-tools/pull/16986)

### Capabilities

The evolution agent reads stored conversations, execution traces, knowledge, and external evidence to diagnose poor answers. It can **update the dev (candidate) KB and refresh its search index**, compare candidate and production answers, and **create/update GitHub issues and add comments**. These write capabilities are enabled today.

The team's operating model is an **ADO scheduled feedback job processing internal-channel conversations**, not a public interactive agent. The [feedback pipeline](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/azure-sdk-qa-bot-agent/pipelines/feedback-job.yml) runs daily with CI and PR triggers disabled. Pipeline run permissions and hosted-agent access are deployment-managed; the schedule itself is not an authorization check and does not prevent authorized manual runs.

**KB edits affect only the dev environment in this deployment, not the production KB.** The team maintains the dev/production configuration separation. Production-agent calls are used to compare answers, not to apply KB changes; this does not mean the workflow makes no production requests.

### Risks and Safeguards

| Risk ID/name | How it applies | How we reduce the risk |
| --- | --- | --- |
| R1 — Harmful content | Analysis or publication could generate harmful content. | The agent is configured with a Foundry content-safety guardrail (C5; see [Shared Controls](#5-shared-controls)). |
| R2 — User prompt attack (jailbreak) | Feedback-job input could attempt to override instructions. | The agent runs through an internal feedback job rather than a public interactive interface, limiting direct exposure. The agent is configured with a Foundry content-safety guardrail (C5). |
| R3 — Document attack (indirect prompt injection) | Stored conversations, traces, KB, and external evidence could redirect later writes. | The prompt tells the agent to treat retrieved content as data, not instructions (C6). Code filters authored GitHub body text by author association (C3) and labels external MCP string results as untrusted (C4). The agent is also configured with a Foundry content-safety guardrail (C5). Internal scheduling does not make copied external text trustworthy. |
| R4 — Ungrounded content / hallucination | Diagnoses, dev KB changes, or published findings could lack evidence. | The [evolution instructions](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/azure-sdk-qa-bot-agent/agents/chatbot_evolution_agent/instruction.md) tell the agent to identify the knowledge source, use authoritative evidence, test candidate changes, and compare candidate and production answers before publication (C6). This guides the workflow; comparison does not prove correctness. |
| R5 — Protected material reproduction | Analysis, dev KB edits, or GitHub publication could reproduce protected material. | The agent is configured with a Foundry content-safety guardrail (C5). |
| R6 — Task adherence | Incorrect reads, unintended dev KB edits, or GitHub writes could depart from the improvement task. | KB updates use candidate Search/Blob clients, with separate candidate/production targets for answer comparison; dev-only writes depend on deployment configuration (C9). Code checks that relative Markdown paths stay under a registered source folder (C10). Each edit must match one nonempty passage exactly, and an ETag check rejects writes if the stored file changed in the meantime (C11). These checks do not validate meaning or provide rollback. The prompt guides the workflow (C6); tool and execution limits are described in R7 (C1, C7). |
| R7 — Agent hijacking | An attacker could redirect web fetching (SSRF) or enabled KB/GitHub writes; Q&A read-only guarantees do not apply. | GitHub writes are limited to `issue_write` and `add_issue_comment` (C1). The prompt names `Azure/azure-sdk-pr`, but code does not enforce that repository target (C6). Execution stops after at most **21 tool-loop iterations / 20 tool calls per turn** (C7). Candidate clients, path checks, and concurrency checks constrain KB edits (C9–C11). Web-fetch code rejects private/internal destinations and rechecks redirects (C2). The prompt guides evidence handling and publication (C6). |
| R8 — Sensitive data leakage | Analysis, validation calls, or GitHub publication could expose credentials or internal information to unauthorized recipients. | Credential handling supports managed identity and short-lived GitHub App tokens; a configured static GitHub token takes precedence (C8). Resource access and audience permissions are deployment-managed. Authentication does not prevent sensitive information from being included in a publication. |

Sources: [evolution registration and candidate clients](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/azure-sdk-qa-bot-agent/agents/chatbot_evolution_agent/init.py), [KB validation and update implementation](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/azure-sdk-qa-bot-agent/tools/knowledge_tools.py), [ETag-conditioned storage writes](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/azure-sdk-qa-bot-agent/utils/azure_storage.py), [validation target selection](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/azure-sdk-qa-bot-agent/tools/chatagent_tools.py), [GitHub configuration](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/azure-sdk-qa-bot-agent/tools/github_mcp_tools.py).

## 5. Shared Controls

The three agents reuse the controls below where the corresponding tools are registered. Foundry content-safety guardrail attachment (C5) is included in the deployment process for all three agents.

### C2 — SSRF-hardened web fetch

The [web-fetch implementation](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/azure-sdk-qa-bot-agent/tools/web_tools.py) uses HTTP GET and:

- Accepts only `http` and `https` URLs; rejects localhost/loopback and any hostname resolving to a non-global IP address.
- Pins validated DNS results for the request and bypasses proxy settings.
- Follows redirects manually and revalidates each hop, up to five redirects.
- Supports `WEB_FETCH_ALLOWED_DOMAINS`: when configured, only matching domain suffixes are allowed. When unset, any public destination passing the other checks is allowed.

These checks mitigate [SSRF (CWE-918)](https://cwe.mitre.org/data/definitions/918.html). Public destinations can still host malicious content or receive sensitive query parameters; URL validation is not data-loss prevention.

### C3 — GitHub authored-body filtering

The [GitHub result parser](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/azure-sdk-qa-bot-agent/tools/github_mcp_tools.py) recursively redacts body fields in authored JSON objects unless the author is classified as a repository owner, member, collaborator, or bot. Structural metadata is retained.

This reduces exposure to external contributors' free text. It is a heuristic, not proof that retained content is safe: bot authors are trusted, and non-JSON payloads such as file content do not receive authored-body filtering.

### C4 — External MCP spotlighting

The [tool-output middleware](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/azure-sdk-qa-bot-agent/utils/tool_security.py) wraps nonempty external MCP string results in a labelled, JSON-escaped block:

```text
<untrusted_tool_output>
"...json-escaped tool text..."
</untrusted_tool_output>
```

This [spotlighting technique](https://arxiv.org/abs/2403.14720) cues the model to treat the content as reference data, not instructions. It does not guarantee resistance to injection. Native registered tools bypass the wrapper to preserve server-side decoding; their transcript, KB, trace, and web text remains untrusted.

### C5 — Platform content safety

Foundry provides [guardrails and controls](https://learn.microsoft.com/azure/foundry/guardrails/guardrails-overview) for models and agents. A guardrail defines which risks to detect, where to check for them, and what action to take when a risk is detected.

For hosted agents, **a Foundry guardrail is defined in a Responsible AI (RAI) policy**. The official [Add guardrails to a hosted agent](https://learn.microsoft.com/azure/foundry/agents/how-to/add-hosted-agent-guardrails) guide documents how the policy's full Azure resource ID is assigned to `rai_config.rai_policy_name` on the agent definition.

Our [deployment script](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/azure-sdk-qa-bot-agent/scripts/deploy_hosted_agent.py) reads that resource ID from `AI_FOUNDRY_RAI_POLICY_ID` and passes it to `RaiConfig(rai_policy_name=rai_policy_id)`, attaching the existing guardrail to the hosted agent. The script requires this value but does not provision the policy or inspect its classifier settings.

The attached policy defines the enabled risk checks, the input/output and supported tool-traffic stages to scan, and the response actions. This document describes the policy attachment; deployed policy settings and runtime validation results are outside its scope.

### C8 — Authentication and secrets

In [GitHub App mode](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/azure-sdk-qa-bot-agent/tools/github_mcp_tools.py), the signing key stays in Key Vault. A Key Vault signing operation creates the authentication JWT without exporting the key. The app uses short-lived installation tokens to access GitHub and refreshes them as needed.

## 6. References

- [Azure AI Content Safety overview](https://learn.microsoft.com/azure/ai-services/content-safety/overview)
- [Content Safety harm categories](https://learn.microsoft.com/azure/ai-services/content-safety/concepts/harm-categories)
- [Prompt Shields and Spotlighting](https://learn.microsoft.com/azure/foundry/openai/concepts/content-filter-prompt-shields)
- [Groundedness detection](https://learn.microsoft.com/azure/ai-services/content-safety/concepts/groundedness)
- [Protected material detection](https://learn.microsoft.com/azure/ai-services/content-safety/concepts/protected-material)
- [Task adherence](https://learn.microsoft.com/azure/ai-services/content-safety/concepts/task-adherence)
- [Defending Against Indirect Prompt Injection Attacks With Spotlighting](https://arxiv.org/abs/2403.14720) — Hines et al., Microsoft, 2024.
- [Safety system messages](https://learn.microsoft.com/azure/ai-foundry/openai/concepts/system-message)
- [Safety system message templates](https://learn.microsoft.com/azure/foundry/openai/concepts/safety-system-message-templates)
- [Responsible AI for Azure OpenAI](https://learn.microsoft.com/azure/ai-foundry/responsible-ai/openai/overview)
- [Reduce autonomous agentic AI risk](https://learn.microsoft.com/security/zero-trust/sfi/manage-agentic-risk)
- [Foundry agent tool best practices](https://learn.microsoft.com/azure/foundry/agents/concepts/tool-best-practice)
- [GitHub MCP server: toolsets and read-only mode](https://github.com/github/github-mcp-server)
- [CWE-918: Server-Side Request Forgery](https://cwe.mitre.org/data/definitions/918.html)
