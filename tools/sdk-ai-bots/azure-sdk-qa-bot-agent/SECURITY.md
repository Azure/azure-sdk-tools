# Security Design

<!-- markdownlint-configure-file { "MD024": { "siblings_only": true } } -->

## 1. Overview

The agents run as Foundry hosted agents (custom containers, Responses protocol), built on the `agent_framework` SDK with an in-container tool loop. They share infrastructure and some security utilities, but **their tool permissions and risks differ**.

| Agent | Purpose | Tool permissions |
| --- | --- | --- |
| [Azure SDK Chat QA agent](#2-azure-sdk-chat-agent) | Answer SDK development and release questions in Teams. | Read-only: internal knowledge, web, GitHub, Azure DevOps, and pipeline analysis. |
| [Azure MCP Server QA agent](#3-azure-mcp-server-qa-agent) | Answer Azure MCP Server usage and troubleshooting questions. | Read-only: internal knowledge, web, GitHub, and Microsoft Learn. No Azure resource-management tools. |
| [Chatbot evolution agent](#4-chatbot-evolution-agent) | Review past answers and test improvements. | **Read and write**: candidate KB edits and index refreshes, validation against candidate/production agents, and GitHub issue/comment publication. |

## 2. Azure SDK Chat agent

### Capabilities

The agent answers developer questions using internal documentation, web content, GitHub repositories/discussions, and Azure DevOps builds/work items. Its registered tools cannot create, edit, delete, merge, or approve repository/work-item content.

### Risks and Safeguards

| Risk ID/name | How it applies | Implemented safeguards (C IDs) |
| --- | --- | --- |
| R1 — Harmful content | Answers could generate or repeat harmful content. | C6 instructs refusal of harmful requests. C5 policy attachment is implemented; effective blocking is deployment-dependent (see [Shared Controls](#5-shared-controls)). |
| R2 — User prompt attack (jailbreak) | User questions could override instructions. | C5 policy attachment is implemented; jailbreak coverage and effective blocking are deployment-dependent. |
| R3 — Document attack (indirect prompt injection) | Retrieved docs, GitHub, web, or ADO content could redirect answers. | C6 treats tool output as untrusted reference data. Shared C3 GitHub filtering and C4 external MCP spotlighting reduce exposure to indirect injection; C5 policy coverage is deployment-dependent. |
| R4 — Ungrounded content / hallucination | Answers could invent facts, links, or recommendations. | C6: the [SDK Chat instructions](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/azure-sdk-qa-bot-agent/agents/chat_agent/instruction.md) require retrieved evidence and prohibit unsupported facts, links, and claims of write actions. Prompt guidance controls model behavior, not resource permissions. |
| R5 — Protected material reproduction | Answers could reproduce protected source material. | C6 instructs avoidance of protected-material reproduction. C5 policy attachment is implemented; protected-material coverage and effective blocking are deployment-dependent. |
| R6 — Task adherence | Wrong read-tool selection or parameters could produce incorrect results. | C1: GitHub headers request read-only mode/selected toolsets; the client allow-lists approximately 15 reads, with approval disabled because no writes are registered. ADO allows only project, pipeline/build, artifact, and work-item/comment reads—not create/update/queue/delete. Native retrieval, web, and pipeline analysis expose no content modification. C6 guides tool use; C7 bounds execution below. |
| R7 — Agent hijacking | Redirected tool use could fetch internal URLs (SSRF) or misuse read access. | C1 limits available operations to reads; C7 allows at most **5 tool-loop iterations / 10 tool calls per turn**. Shared C2 hardens web fetching; C6 guides handling of untrusted output. |
| R8 — Sensitive data leakage | Answers or tool requests could disclose credentials or internal information to unauthorized recipients. | Shared C8 provides authentication and secret-handling paths; it does not by itself authorize disclosure of retrieved data. |

Sources: [agent registration](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/azure-sdk-qa-bot-agent/agents/chat_agent/init.py), [GitHub tool configuration](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/azure-sdk-qa-bot-agent/tools/github_mcp_tools.py), [Azure DevOps tool configuration](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/azure-sdk-qa-bot-agent/tools/ado_mcp_tools.py).

## 3. Azure MCP Server QA agent

Related PR: [#16729 — Add Azure MCP Server agent support](https://github.com/Azure/azure-sdk-tools/pull/16729).

### Capabilities

This agent answers questions **about Azure MCP Server**. It searches internal knowledge, wiki/web content, GitHub, and Microsoft Learn. It does **not** register Azure resource-management tools and cannot provision or modify Azure resources.

### Risks and Safeguards

| Risk ID/name | How it applies | Implemented safeguards (C IDs) |
| --- | --- | --- |
| R1 — Harmful content | Troubleshooting answers could contain harmful content. | C6 supplies safety guidance. C5 policy attachment is implemented; effective blocking is deployment-dependent (see [Shared Controls](#5-shared-controls)). |
| R2 — User prompt attack (jailbreak) | Questions could override instructions. | C5 policy attachment is implemented; jailbreak coverage and effective blocking are deployment-dependent. |
| R3 — Document attack (indirect prompt injection) | Knowledge, wiki/web, GitHub, or Learn results could redirect answers. | C6 supplies untrusted-output guidance. Shared C3 GitHub filtering and C4 external MCP spotlighting reduce exposure to indirect injection, including Learn MCP string results; C5 policy coverage is deployment-dependent. |
| R4 — Ungrounded content / hallucination | Usage advice or troubleshooting recommendations could lack evidence. | C6: the agent-specific [Azure MCP instructions](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/azure-sdk-qa-bot-agent/agents/azure_mcp_server_agent/instruction.md) require evidence grounding. Quick-first retrieval guidance is an efficiency measure, not a security boundary or wall-clock timeout. |
| R5 — Protected material reproduction | Troubleshooting answers could reproduce protected documentation. | C6 supplies safety guidance. C5 policy attachment is implemented; protected-material coverage and effective blocking are deployment-dependent. |
| R6 — Task adherence | Wrong retrieval tools or parameters could lead to irrelevant advice. | C1 registers retrieval and read-only GitHub tools, but no ADO, pipeline-analysis, or Azure management tools. Learn uses the fixed endpoint `https://learn.microsoft.com/api/mcp`, only `microsoft_docs_search` / `microsoft_docs_fetch`, and a **30-second timeout**. C6 guides retrieval; C7 bounds execution below. |
| R7 — Agent hijacking | An attacker could redirect web fetching (SSRF) or misuse read tools, not provision Azure resources. | C1 restricts available tools; C7 allows at most **5 tool-loop iterations / 10 tool calls per turn**. Shared C2 hardens web fetching; C6 guides handling of untrusted output. |
| R8 — Sensitive data leakage | Answers or tool requests could expose credentials or internal knowledge to unauthorized recipients. | Shared C8 provides authentication and secret-handling paths, not automatic permission to disclose internal knowledge. |

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

| Risk ID/name | How it applies | Implemented safeguards (C IDs) |
| --- | --- | --- |
| R1 — Harmful content | Analysis or publication could generate harmful content. | C5 policy attachment is implemented; classifiers and effective blocking are deployment-dependent, not verified (see [Shared Controls](#5-shared-controls)). |
| R2 — User prompt attack (jailbreak) | Feedback-job input could attempt to override instructions. | The internal feedback-job workflow limits direct exposure. C5 jailbreak coverage and effective blocking are deployment-dependent. |
| R3 — Document attack (indirect prompt injection) | Stored conversations, traces, KB, and external evidence could redirect later writes. | C6 treats retrieved content as data, not instructions. Shared C3 GitHub filtering and C4 external MCP spotlighting reduce exposure to indirect injection; C5 policy coverage is deployment-dependent. Internal scheduling does not make copied external text trustworthy. |
| R4 — Ungrounded content / hallucination | Diagnoses, dev KB changes, or published findings could lack evidence. | C6: the [evolution instructions](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/azure-sdk-qa-bot-agent/agents/chatbot_evolution_agent/instruction.md) require source resolution, authoritative evidence, candidate testing, and candidate/production answer comparison. These guide the improvement and publication workflow; comparison does not prove correctness. |
| R5 — Protected material reproduction | Analysis, dev KB edits, or GitHub publication could reproduce protected material. | C5 policy attachment is implemented; protected-material classifiers and effective blocking are deployment-dependent, not verified. |
| R6 — Task adherence | Incorrect reads, unintended dev KB edits, or GitHub writes could depart from the improvement task. | C9 selects candidate Search/Blob clients for KB updates and separate candidate/production validation targets; dev-only write scope depends on deployment configuration. C10 validates relative Markdown paths under a registered source folder. C11 requires a **nonempty, unique exact match** and **ETag-conditioned write**, not semantic validation or rollback. C6 guides the workflow; C1/C7 limits appear below. |
| R7 — Agent hijacking | An attacker could redirect web fetching (SSRF) or enabled KB/GitHub writes; Q&A read-only guarantees do not apply. | C1 adds only `issue_write` and `add_issue_comment` to GitHub reads; `Azure/azure-sdk-pr` is a **prompt-only** repository target, not code-enforced. C7 allows at most **21 tool-loop iterations / 20 tool calls per turn**. C9–C11 constrain KB operations; shared C2 hardens web fetching; C6 guides evidence handling and publication. |
| R8 — Sensitive data leakage | Analysis, validation calls, or GitHub publication could expose credentials or internal information to unauthorized recipients. | Shared C8 provides credential handling and authentication; resource/audience permissions remain deployment-managed, and publication can still disclose sensitive data. |

Sources: [evolution registration and candidate clients](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/azure-sdk-qa-bot-agent/agents/chatbot_evolution_agent/init.py), [KB validation and update implementation](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/azure-sdk-qa-bot-agent/tools/knowledge_tools.py), [ETag-conditioned storage writes](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/azure-sdk-qa-bot-agent/utils/azure_storage.py), [validation target selection](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/azure-sdk-qa-bot-agent/tools/chatagent_tools.py), [GitHub configuration](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/azure-sdk-qa-bot-agent/tools/github_mcp_tools.py).

## 5. Shared Controls

The three agents reuse the controls below where the corresponding tools are registered. C5 is a deployment-dependent control for all three agents, not an assertion of verified blocking. They provide defense in depth, not a guarantee that any single bypass is harmless.

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

### C8 — Authentication and secrets

- [Azure credential selection](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/azure-sdk-qa-bot-agent/utils/azure_credential.py) supports managed identity, Azure Pipelines, and Azure CLI paths. Verify the path and least-privilege permissions used by each deployment.
- In GitHub App mode, a Key Vault signing operation produces the JWT without exporting the signing key, and short-lived installation tokens are refreshed as needed.
- A configured static `GITHUB_TOKEN` overrides GitHub App mode, so the no-long-lived-token property is conditional ([GitHub token selection](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/azure-sdk-qa-bot-agent/tools/github_mcp_tools.py)).
- Separate agent names, configuration endpoints, and routing do not themselves prove separate identities, storage, or audience authorization.

### C5 — Platform content safety

The [deployment script](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/azure-sdk-qa-bot-agent/scripts/deploy_hosted_agent.py) requires `AI_FOUNDRY_RAI_POLICY_ID` and attaches that existing policy. It does not provision the policy or inspect its classifier settings.

Verify the effective deployed classifiers, blocking behavior, and coverage of in-container pre/post-tool traffic. Policy attachment alone does not prove protection against harmful output, jailbreaks, indirect injection, or protected-material reproduction.

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
