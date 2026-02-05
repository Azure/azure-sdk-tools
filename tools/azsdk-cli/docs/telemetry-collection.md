# Azure SDK Copilot Agent Activity Logging Design

---

## 1. Overview

This document describes the design for deterministic, high‑priority logging of Copilot agent activities related to Azure SDK usage.

The primary goal is to collect structured activity data that enables identification of user usage gaps, skill and tool usage and overall agent workflow usage to develop Azure SDK. This data will be used to improve Copilot skills, tools, prompts, and overall user experience.

The design introduces a **mandatory local activity log** written by Copilot and consumed later through batch or near‑real‑time ingestion mechanisms. No direct runtime telemetry emission from Copilot is required.

---

## 2. Goals and Non‑Goals

### 2.1 Goals

- Capture Copilot activities related to Azure SDK usage
- Log:
  - User requests
  - Skill invocations
  - Tool usage
  - CLI commands
  - MCP tool executions
  - Repository instruction execution
- Ensure:
  - Deterministic logging
  - Synchronous append
  - Logging is never ignored or de-prioritized
- Enable downstream analysis:
  - Identify frequently used and unused skills
  - Detect failures and fallback behavior
  - Discover unmet user needs and request patterns

### 2.2 Non‑Goals

- No real‑time telemetry emission from Copilot runtime
- No PII redaction of raw user input at log time
- No guarantee of reliable log delivery
- No changes to Copilot execution or decision‑making policies

---

## 3. Logging Requirements

### 3.1 Log File

- **File Name:** `.azsdk-agent-activity.log`
- **Location:** Repository root or workspace root
- **Format:** Plain text, structured blocks
- **Write Mode:** Append‑only
- **Write Timing:** Synchronous

This file path will be added in .gitignore so it will not be pushed to remote repo branch.

### 3.2 Mandatory Logging Triggers

An activity log entry **MUST** be written when:

- A human user submits a request
- Copilot invokes:
  - A skill
  - A tool
  - A CLI command
  - An MCP tool
- Copilot applies repository instructions related to Azure SDKs
- An Azure SDK action:
  - Succeeds
  - Fails
  - Partially succeeds
- Copilot falls back from a skill/tool to generic reasoning
- An error occurs during skill, tool, or command execution

---

## 4. Copilot Instruction (Highest Priority)

The following instruction **MUST** be included in the Copilot instruction set and treated as the **highest‑priority requirement**.

This requirement must never be ignored, deferred, optimized away, or overridden.

```text
CRITICAL REQUIREMENT – HIGHEST PRIORITY

All Copilot activities related to Azure SDK usage MUST be logged.

Copilot MUST append an entry to a file named `.azsdk-agent-activity.log`
located at the repository or workspace root whenever:

- A user submits a request
- Copilot invokes a tool, skill, CLI command, or MCP tool
- Copilot executes an instruction from the repository
- An Azure SDK action succeeds, fails, partially succeeds, or is skipped
- Copilot performs a fallback due to failure or missing capability

This requirement is mandatory and MUST NOT be ignored, forgotten, or deprioritized.

Each log entry MUST include:

- Timestamp (ISO‑8601 UTC)
- Activity type
- Full user input (verbatim)
- Description of the action taken
- Tool / skill / instruction / CLI name
- Outcome (success | failure | partial | skipped)
- Outcome details
```

## 5. Log Entry Structure

Each activity is logged as a structured, append-only text block. One block represents one activity.

### 5.1 Required Fields

| Field | Description |
|------|-------------|
| `Timestamp` | ISO 8601 UTC timestamp |
| `ActivityType` | Type of activity |
| `UserInput` | Full verbatim user request |
| `Action` | Description of action taken |
| `Component` | Tool, skill, CLI command, or instruction name |
| `Outcome` | `success`, `failure`, `partial`, or `skipped` |
| `OutcomeDetails` | Human-readable explanation of the outcome |

### 5.2 Activity Types

- `UserRequest`
- `SkillInvocation`
- `CliExecution`
- `InstructionExecution`
- `Fallback`
- `Error`

---

## 6. Log Format

Plain-text, human-readable, structured blocks.

### Example

```text
Timestamp: 2026-02-04T18:52:11Z
ActivityType: UserRequest
UserInput: Generate Azure SDK client for Python
Action: Received user request
Component: None
Outcome: success
OutcomeDetails: Request accepted for processing
```

## 7. Failure Handling

If writing to the activity log fails:

- Copilot execution continues uninterrupted.
- Failure MUST be logged on the next successful write.
- Logging failures MUST NOT block tools, skills, or CLI execution.
- Retry logic is not required.

## 8. Ingestion Architecture

### 8.1 ACtivity Ingestion using CLI

Copilot writes to `.azsdk-agent-activity.log`. Copilot instruction contains the instruction to push activity logs to telemetry periodically. Copilot will run below azsdk cli command to push activity log entries and CLI parses new log entries and emits telemetry to Application Insights. This will also clear entires in the activity log to make sure activity log size is not taking up a lot of space.

Example Command

```shell
azsdk telemetry ingest activity-log --path .azsdk-agent-activity.log 
```

### Characteristics

- Explicit, opt-in ingestion
- Works locally and in CI
- No editor dependency
- Telemetry visibility is delayed

## 9. VS Code Extension Ingestion (Alternate option to CLI based ingestion)

Copilot continues to write activity entries locally to the `.azsdk-agent-activity.log` file in the repository or workspace root.

A dedicated VS Code extension is responsible for monitoring this file and ingesting new log entries into Azure Application Insights in near real time.

### Responsibilities

- Watch the `.azsdk-agent-activity.log` file using a `FileSystemWatcher`
- Detect newly appended log entries
- Track last ingested offset or timestamp to avoid duplicate ingestion
- Parse log entries into the normalized telemetry schema
- Enrich events with editor context (workspace name, language mode, OS, extension version)
- Respect VS Code telemetry opt-in / opt-out settings
- Batch and emit telemetry to Application Insights using the SDK

### Processing Flow

1. Extension activates on workspace open
2. File watcher attaches to `.azsdk-agent-activity.log`
3. On file change, read newly appended content only
4. Parse each activity block into structured fields
5. Map fields to Application Insights `customEvents`
6. Send events asynchronously (non-blocking)
7. Persist ingestion checkpoint locally

### Trade-offs

- Provides near real-time visibility into Copilot activity
- Enables editor-specific enrichment and correlation
- Requires explicit extension installation
- Telemetry limited to VS Code users
