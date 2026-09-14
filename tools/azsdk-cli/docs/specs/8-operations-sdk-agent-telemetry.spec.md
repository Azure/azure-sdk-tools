<!-- cspell:words azsdk AZSDKTOOLS -->

# Spec: 8-Operations - SDK Agent Telemetry

## Table of Contents

- [Background / Problem Statement](#background--problem-statement)
- [Design Proposal](#design-proposal)
- [Open Questions](#open-questions)

## Background / Problem Statement

The current [`azsdk_tool_telemetry.ps1`](../../../../eng/common/scripts/azsdk_tool_telemetry.ps1) hook observes some skill calls and file reads, but failure coverage and delivery semantics are inconsistent. Product telemetry does not need user content.

GitHub Copilot for Azure's [`track-telemetry.ps1`](https://github.com/microsoft/GitHub-Copilot-for-Azure/blob/d3959f310d1b6dbd4ac722ac01bc1cce8c7b325d/plugins/azure-skills/hooks/scripts/track-telemetry.ps1) is the prior art for recognizing relevant post-tool events, honoring disablement, and always allowing the workflow to continue.

The design must work for every SDK language and for data-plane and management-plane work.

## Design Proposal

### Consideration Areas
#### 1. Information to Collect
| Signal | Hook source | Available input | SDK Agent telemetry |
| --- | --- | --- | --- |
| Tool call | `PostToolUse` or `PostToolUseFailure` | Tool name, arguments, result or error, timestamp, and working directory | Allowlisted tool name and provider only |
| Skill call | Skill tool call or packaged `SKILL.md` read | Tool name and arguments | Allowlisted skill name only |
| Tool result | Success and failure post-tool hooks | Full result or error | `succeeded`, `failed`, `cancelled`, or `unknown`, plus an optional bounded failure category |
| User prompt | User-prompt-submitted hook | Raw prompt and working directory | Event count and approved bounded classification only; never raw prompt text |

#### 2. User Approval

Users must be notified that SDK Agent telemetry is collected. The notice should state what is collected, what is excluded, where the data is stored, and how to disable telemetry.

#### 3. Telemetry Data Store

GitHub Copilot for Azure sends hook events through Azure MCP's [`plugin-telemetry`](https://github.com/microsoft/mcp/blob/main/core/Microsoft.Mcp.Core/src/Areas/Server/Commands/PluginTelemetryCommand.cs) command to Application Insights.

The SDK Agent has two practical Azure storage options:

| Option | Pros | Cons | Fit |
| --- | --- | --- | --- |
| [Application Insights](https://learn.microsoft.com/azure/azure-monitor/app/app-insights-overview) | Reuses the SDK Agent's existing OpenTelemetry and Azure Monitor pipeline; supports traces, metrics, sampling, dashboards, alerts, and KQL queries; lowest implementation and operational cost | Ingestion and retention costs require sampling and bounded dimensions; application-oriented schemas are less flexible for large analytical datasets | Best initial store for operational telemetry and failure-rate monitoring |
| [Azure Data Explorer (Kusto)](https://learn.microsoft.com/azure/data-explorer/data-explorer-overview) | Designed for high-volume telemetry and log analytics; flexible schemas, retention policies, and advanced KQL analysis across large or long-lived datasets | Requires a separate ingestion path, cluster or database administration, access controls, schema governance, monitoring, and cost ownership | Consider when telemetry volume, retention, or cross-source analytics outgrow Application Insights |

Application Insights also uses KQL for log queries, but it is not the same as operating a dedicated Azure Data Explorer store.

**Recommendation:** use Application Insights initially. Hooks submit minimized events to the existing hidden [`azsdk ingest-telemetry`](../../Azure.Sdk.Tools.Cli/Tools/Core/TelemetryIngestionTool.cs) command, which validates the schema and creates OpenTelemetry activities. The existing [`TelemetryRegistration`](../../Azure.Sdk.Tools.Cli/Telemetry/TelemetryRegistration.cs) exports them through Azure Monitor to the Azure SDK Tools Application Insights resource. Do not persist raw hook payloads or use a local disk queue.

Revisit Azure Data Explorer only if measured volume, retention, or analytical requirements justify the additional system. For either option, retention, access, deletion, ownership, regional storage, and cost controls must be explicitly defined before rollout.

### Approach 1: Automatic Hooks

Use agent lifecycle hooks to automatically observe SDK Agent skill calls, file reads, and tool outcomes. The hooks convert host-specific payloads into privacy-minimized, allowlisted events and send them through the SDK Agent telemetry pipeline without interrupting the user's workflow.

#### 1. What Information Do Agent Hooks Contain?

Both GitHub Copilot and Claude Code expose lifecycle hooks that can observe prompts and tool execution:

| Platform | Relevant hooks | Available information |
| --- | --- | --- |
| [GitHub Copilot](https://docs.github.com/en/copilot/reference/hooks-reference) | `userPromptSubmitted`, `postToolUse`, `postToolUseFailure` | Session and tool identifiers, tool name, tool arguments, successful result or failure, prompt, timestamp, and working directory, depending on the event |
| [Claude Code](https://code.claude.com/docs/en/hooks) | `UserPromptSubmit`, `PostToolUse`, `PostToolUseFailure` | Session and tool identifiers, tool name, tool input, successful response or error, prompt, and working directory, depending on the event |

GitHub Copilot for Azure demonstrates the implementation pattern:

1. Register cross-platform `PostToolUse` scripts in the [hook manifest](https://github.com/microsoft/azure-skills/blob/main/hooks/copilot-hooks.json).
2. Read hook JSON from standard input and normalize host-specific field names and tool prefixes.
3. Recognize skill calls, Azure tool calls, `SKILL.md` reads, and packaged reference-file reads.
4. Send allowlisted fields through a telemetry command and always return the host's continue response. See the [PowerShell](https://github.com/microsoft/azure-skills/blob/main/hooks/scripts/track-telemetry.ps1) and [shell](https://github.com/microsoft/azure-skills/blob/main/hooks/scripts/track-telemetry.sh) implementations.

**Recommendation:** hooks are a good way to collect automatic, aggregate operational telemetry because both supported hook systems expose prompt submission, successful tool completion, and failed tool completion. A single hook layer can observe skills and tools without requiring every SDK Agent tool to implement telemetry.

Hooks are not a safe telemetry payload by themselves. Their inputs can contain prompts, arguments, results, errors, working directories, and paths. Treat every hook payload as sensitive input: construct a new event from allowlisted fields, never export the raw payload, and fail open if parsing or delivery fails. Host payload differences also require separate adapters and tests for each supported host.

Azure MCP already owns telemetry for its tool calls. SDK Agent hooks must not emit a duplicate tool-call event for Azure MCP; they may still record the surrounding SDK Agent skill invocation.

#### 2. How Do We Get User Approval?

The approval experience must be implemented by the host before telemetry hooks begin exporting events. Hooks should not ask for approval after each operation.

| Host | Proposed approval experience |
| --- | --- |
| VS Code Agent panel | Show a first-use notice in the Agent panel before enabling SDK Agent telemetry. Provide **Enable telemetry**, **Not now**, and a documentation link. Persist the choice in the SDK Agent or extension settings. |
| GitHub Copilot app | Show the same first-use notice as an app banner or dialog before enabling telemetry for the first SDK Agent session. Persist the choice in app settings and expose a way to change it later. |
| GitHub Copilot CLI | Show the notice in the terminal on first SDK Agent use. In an interactive session, ask once before enabling telemetry. In non-interactive mode, do not prompt; use the persisted setting or keep telemetry disabled until explicitly enabled. |

The notice should state what is collected, what is excluded, where the data is stored, and how to disable telemetry with `AZSDKTOOLS_COLLECT_TELEMETRY=false`. A user's decision should apply consistently across these hosts when they share the same SDK Agent installation and configuration.

**GitHub Copilot for Azure today:** it does not proactively notify users that telemetry is collected. Its [Telemetry section](https://github.com/microsoft/azure-skills#telemetry) only explains how to disable collection with `AZURE_MCP_COLLECT_TELEMETRY=false`.

**Recommendation:** improve on the current GitHub Copilot for Azure behavior by obtaining a one-time first-use decision before the first export. Do not ask for approval on every low-sensitivity event because repeated prompts create friction and approval fatigue. Raw prompts, arguments, results, file contents, and paths remain outside this proposal regardless of the approval model.

### Approach 2: User-Approved MCP Failure Report

After an SDK operation fails, the agent tells the user and asks whether to send a privacy-minimized report. Only an affirmative response causes a separate MCP `report_failure` tool call. Refusal, silence, or ambiguous wording does not authorize the call.

The reporting tool accepts only allowlisted identifiers and bounded outcome categories. It must not accept prompts, tool arguments or results, error text, file contents, or paths. It sends the event through the same SDK Agent OpenTelemetry and Application Insights pipeline described in Approach 1.

This approach gives users control at the point of failure, but it adds interaction and under-reports failures because reporting is optional. It cannot provide a reliable denominator for overall success or failure rates.

### Comparison

| Area | Approach 1: Automatic hooks | Approach 2: User-approved MCP report |
| --- | --- | --- |
| User approval | Notice and opt-out, subject to privacy/legal review | Explicit approval for every report |
| Coverage | Broad adoption and outcome coverage | Only failures users choose to report |
| User interaction | No per-event prompt | Additional prompt after a failure |
| Best use | Aggregate product health | User-controlled diagnostic reporting |

## Open Questions

- [ ] Does privacy/legal review approve notice plus opt-out, or require explicit opt-in?
- [ ] What retention, access, deletion, and ownership policies apply to the Azure SDK Tools Application Insights resource?
