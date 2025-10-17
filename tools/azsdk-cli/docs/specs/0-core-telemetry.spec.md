# Spec: Core - Telemetry & Dashboard Requirements

## Table of Contents

- [Definitions](#definitions)
- [Background / Problem Statement](#background--problem-statement)
- [Goals and Exceptions/Limitations](#goals-and-exceptionslimitations)
- [Design Proposal](#design-proposal)
- [Telemetry Schema](#telemetry-schema)
- [System Lifecycle Events](#system-lifecycle-events)
- [Agent Prompts](#agent-prompts)
- [CLI Commands](#cli-commands)
- [Dashboard Requirements](#dashboard-requirements)
- [Open Questions](#open-questions)
- [Success Criteria](#success-criteria)
- [Implementation Plan](#implementation-plan)
- [Testing Strategy](#testing-strategy)
- [Documentation Updates](#documentation-updates)
- [Privacy and Security Considerations](#privacy-and-security-considerations)

---

## Definitions

**Telemetry**: The automated collection, transmission, and analysis of data from the MCP server and CLI application to measure usage, performance, reliability, and user behavior patterns.

**MCP Server**: The Model Context Protocol server that provides tools and capabilities to AI agents (like GitHub Copilot) for Azure SDK development workflows.

**CLI Application**: The command-line interface application (`azsdk`) that provides direct command-line access to Azure SDK tools and workflows.

**Tool Invocation**: The execution of a specific MCP tool or CLI command, tracked from initiation through completion or failure.

**MAU (Monthly Active Users)**: Unique users who have used the MCP server or CLI at least once during a calendar month.

**MEU (Monthly Engaged Users)**: Unique users who have used the MCP server or CLI on 2 or more separate days within a calendar month.

**MDU (Monthly Dedicated Users)**: Unique users who have used the MCP server or CLI on 10 or more separate days within a calendar month.

**System Event**: A telemetry event related to the lifecycle of the MCP server or CLI application itself (install, start, stop, etc.) rather than specific tool invocations.

**User Event**: A telemetry event triggered by explicit user actions through agent prompts or CLI commands.

**DevDeviceId**: A privacy-preserving hashed identifier unique to a user's development device, used for aggregating telemetry without storing personally identifiable information.

**Custom SDK**: An SDK that includes hand-written code customizations beyond the auto-generated code from TypeSpec specifications.

**Standard SDK**: An SDK generated directly from TypeSpec specifications without additional custom code layers.

---

## Background / Problem Statement

### Current State

The Azure SDK Tools MCP server and CLI application currently lack comprehensive telemetry capabilities. This creates several challenges:

1. **No Visibility into Usage**: We don't know who is using the tools, how often, or which features are most valuable
2. **No Performance Metrics**: We can't measure tool invocation latency, success rates, or reliability
3. **No Adoption Tracking**: We can't identify which teams or services are adopting MCP/AI workflows vs. traditional approaches
4. **No Data-Driven Decisions**: Leadership questions about MAU, tool effectiveness, and ROI cannot be answered
5. **No Error Insights**: When tools fail, we lack data to understand failure patterns and root causes
6. **No Platform Breakdown**: We don't know which platforms (VS Code, GitHub, JetBrains) or agents (Copilot, Claude) are being used

### Why This Matters

Without telemetry, we operate blindly. We need data to:

- **Justify Investment**: Demonstrate ROI to leadership with MAU/MEU metrics and usage trends
- **Improve Quality**: Identify reliability issues and performance bottlenecks
- **Guide Prioritization**: Focus on features that users actually use
- **Measure Adoption**: Track which teams are adopting MCP/AI workflows and which need outreach
- **Optimize User Experience**: Understand where users struggle and where they succeed
- **Support Platform Teams**: Provide data to language teams about SDK generation patterns

The telemetry system must balance collecting actionable data with respecting user privacy and minimizing performance impact.

---

## Goals and Exceptions/Limitations

### Goals

What are we trying to achieve with this telemetry design?

- [x] Collect comprehensive usage metrics (MAU, MEU, MDU)
- [x] Track tool invocation success rates and latency
- [x] Identify adoption gaps (packages released without MCP/AI)
- [x] Break down usage by platform, agent, language, and service
- [x] Enable data-driven prioritization and decision-making
- [x] Respect user privacy with hashed identifiers
- [x] Provide real-time and historical dashboards for leadership
- [x] Track both user events (tool calls) and system events (lifecycle)
- [x] Support both MCP server and CLI application telemetry
- [x] Enable correlation between telemetry events in a user session

### Exceptions and Limitations

#### Exception 1: No Personally Identifiable Information (PII)

- **Description**: Telemetry must not collect usernames, email addresses, file paths, or any other PII.
- **Impact**: We cannot track individual users by name, only by hashed device identifiers.
- **Workaround**: Use DevDeviceId and MAC address hashes for user aggregation while maintaining privacy.

#### Exception 2: Optional Telemetry Collection

- **Description**: Users must be able to opt out of telemetry collection.
- **Impact**: Telemetry data may be incomplete if significant users opt out.
- **Workaround**: Make telemetry opt-out rather than opt-in to maximize data collection while respecting user choice.

#### Exception 3: Network Failures

- **Description**: Telemetry transmission may fail due to network issues or firewall restrictions.
- **Impact**: Some telemetry events may be lost and never reach the backend.
- **Workaround**: Implement local queuing and retry logic with exponential backoff. Telemetry failures should never block user workflows.

#### Exception 4: Tool Argument Sensitivity

- **Description**: Some tool arguments may contain sensitive data (API keys, connection strings, internal URLs).
- **Impact**: Full tool argument logging could expose sensitive information.
- **Workaround**: Implement argument sanitization to remove or redact sensitive patterns before transmission.

---

## Design Proposal

### Overview

The telemetry system consists of three main components:

1. **Telemetry Collection Layer**: Embedded in the MCP server and CLI application
2. **Telemetry Transmission Layer**: Sends events to Azure Application Insights
3. **Telemetry Analysis Layer**: Kusto queries and dashboards for reporting

All telemetry flows through Azure Application Insights' `RawEventsDependencies` table, with events structured to enable rich querying and analysis.

### Architecture Diagram

```text
┌─────────────────────────────────────────────────────────────┐
│                        User Device                           │
│                                                              │
│  ┌──────────────┐              ┌──────────────┐            │
│  │  MCP Server  │              │  CLI App     │            │
│  │              │              │              │            │
│  │  - Tool      │              │  - Command   │            │
│  │    Calls     │              │    Execution │            │
│  │  - System    │              │  - System    │            │
│  │    Events    │              │    Events    │            │
│  └──────┬───────┘              └──────┬───────┘            │
│         │                             │                     │
│         └──────────┬──────────────────┘                     │
│                    │                                         │
│         ┌──────────▼──────────┐                            │
│         │ Telemetry Collector │                            │
│         │                     │                            │
│         │ - Event queuing     │                            │
│         │ - Sanitization      │                            │
│         │ - Batching          │                            │
│         └──────────┬──────────┘                            │
└────────────────────┼──────────────────────────────────────┘
                     │
                     │ HTTPS
                     ▼
         ┌───────────────────────┐
         │ Application Insights  │
         │                       │
         │ - Event storage       │
         │ - RawEventsDependencies│
         └───────────┬───────────┘
                     │
                     │ Kusto Queries
                     ▼
         ┌───────────────────────┐
         │   Power BI Dashboard  │
         │                       │
         │ - MAU/MEU/MDU metrics │
         │ - Tool success rates  │
         │ - Platform breakdown  │
         │ - Performance metrics │
         └───────────────────────┘
```

---

## Telemetry Schema

### Application Insights Schema

All telemetry events are sent to Azure Application Insights and stored in the `RawEventsDependencies` table with the following top-level columns:

| Column Name | Description | Sample Data |
|-------------|-------------|-------------|
| `timestamp` | Time at which the event occurred (UTC) | 2025-10-16T23:44:57.217Z |
| `name` | Telemetry event type | `ToolExecuted`, `SystemStarted`, `SystemStopped` |
| `client_Type` | Device type | `PC`, `Mac` |
| `client_OS` | Operating system | `Windows 10`, `macOS 14.1`, `Ubuntu 22.04` |
| `success` | True if operation succeeded, false otherwise | `true`, `false` |
| `duration` | Duration of operation in milliseconds | `10008`, `245` |
| `customDimensions` | JSON object with event-specific data | `{...}` (see below) |
| `client_CountryOrRegion` | Geographic region | `United States`, `India` |

### Event Types

The `name` column contains one of the following event types:

#### User Events (Tool Invocations)

- **`ToolExecuted`**: A user invoked a tool via MCP server or CLI

#### System Events (Lifecycle)

- **`SystemInstalled`**: MCP server or CLI was installed
- **`SystemStarted`**: MCP server or CLI process started
- **`SystemStopped`**: MCP server or CLI process stopped normally
- **`SystemCrashed`**: MCP server or CLI process crashed unexpectedly
- **`SystemConfigured`**: User changed configuration settings
- **`SystemUpdated`**: MCP server or CLI was updated to a new version

### Custom Dimensions Schema

The `customDimensions` column contains a JSON object with the following properties:

#### Common Properties (All Events)

| Property | Description | Sample Data |
|----------|-------------|-------------|
| `version` | Version of MCP server or CLI | `0.5.6.0`, `1.0.0` |
| `sessionId` | Unique identifier for user session | `a1b2c3d4-e5f6-7890-abcd-ef1234567890` |
| `eventId` | Unique identifier for this event | `b6523abb-8b23-48f4-9395-6e0745a50d27` |
| `macAddressHash` | SHA-256 hash of MAC address | `7b1a2a70a41d8154f6aeaf50e67b052fb9ca194de22c...` |
| `devDeviceId` | Hash of DevDeviceId | `9b705c2e5b927992ab862a50e3552cadcc35f0fa0000...` |
| `platform` | Execution platform | `VSCode`, `CLI`, `GitHub`, `JetBrains` |
| `isOptedIn` | User opted into telemetry | `true`, `false` |

#### Agent-Specific Properties (MCP Server Only)

| Property | Description | Sample Data |
|----------|-------------|-------------|
| `clientName` | MCP client application name | `Visual Studio Code`, `Claude Desktop` |
| `clientVersion` | MCP client version | `1.100.1`, `0.8.2` |
| `agentName` | AI agent name | `GitHub Copilot`, `Claude Code` |
| `agentVersion` | AI agent version | `1.234.0` |
| `agentModel` | LLM model used | `gpt-4`, `claude-sonnet-4` |

#### Tool Execution Properties (ToolExecuted Event)

| Property | Description | Sample Data |
|----------|-------------|-------------|
| `toolName` | Name of the tool executed | `azsdk_generate_sdk`, `azsdk_run_tests` |
| `toolCategory` | Category of tool | `generating`, `testing`, `validating` |
| `toolArgs` | Sanitized tool arguments (JSON string) | `{"language":".NET","apiVersion":"2025-09-01"}` |
| `toolResponse` | Tool response summary | `Azure DevOps pipeline initiated` |
| `language` | SDK language (if applicable) | `.NET`, `Java`, `JavaScript`, `Python`, `Go` |
| `planeType` | Data plane or management plane | `data`, `mgmt` |
| `serviceName` | Azure service name | `healthdataaiservices`, `storage` |
| `isCustomSdk` | Whether SDK has custom code | `true`, `false` |
| `errorType` | Type of error if failed | `ValidationError`, `NetworkError` |
| `errorMessage` | Sanitized error message | `Missing required parameter: typespecProjectRoot` |

#### System Event Properties

| Property | Description | Sample Data |
|----------|-------------|-------------|
| `eventType` | Type of system event | `install`, `start`, `stop`, `crash`, `update` |
| `previousVersion` | Previous version (for updates) | `0.5.5.0` |
| `crashReason` | Reason for crash (if applicable) | `UnhandledException` |
| `configChanges` | Configuration changes (JSON) | `{"telemetryEnabled":false}` |

### Example Telemetry Events

#### Example 1: Tool Execution - Generate SDK

```json
{
  "timestamp": "2025-10-16T23:44:57.217Z",
  "name": "ToolExecuted",
  "client_Type": "PC",
  "client_OS": "Windows 10",
  "success": true,
  "duration": 12450,
  "client_CountryOrRegion": "United States",
  "customDimensions": {
    "version": "0.5.6.0",
    "sessionId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "eventId": "b6523abb-8b23-48f4-9395-6e0745a50d27",
    "macAddressHash": "7b1a2a70a41d8154f6aeaf50e67b052fb9ca194de22c...",
    "devDeviceId": "9b705c2e5b927992ab862a50e3552cadcc35f0fa0000...",
    "platform": "VSCode",
    "isOptedIn": true,
    "clientName": "Visual Studio Code",
    "clientVersion": "1.100.1",
    "agentName": "GitHub Copilot",
    "agentVersion": "1.234.0",
    "agentModel": "gpt-4",
    "toolName": "azsdk_generate_sdk",
    "toolCategory": "generating",
    "toolArgs": "{\"typespecProjectRoot\":\"specification/healthdataaiservices\",\"language\":\".NET\",\"apiVersion\":\"2025-09-01\",\"sdkReleaseType\":\"preview\"}",
    "toolResponse": "SDK generated successfully",
    "language": ".NET",
    "planeType": "data",
    "serviceName": "healthdataaiservices",
    "isCustomSdk": false
  }
}
```

#### Example 2: Tool Execution - Failed Test Run

```json
{
  "timestamp": "2025-10-16T23:50:12.543Z",
  "name": "ToolExecuted",
  "client_Type": "PC",
  "client_OS": "Windows 10",
  "success": false,
  "duration": 8234,
  "client_CountryOrRegion": "United States",
  "customDimensions": {
    "version": "0.5.6.0",
    "sessionId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "eventId": "c7634bcc-9c34-59g5-a4a6-7f1856b61e38",
    "macAddressHash": "7b1a2a70a41d8154f6aeaf50e67b052fb9ca194de22c...",
    "devDeviceId": "9b705c2e5b927992ab862a50e3552cadcc35f0fa0000...",
    "platform": "VSCode",
    "isOptedIn": true,
    "clientName": "Visual Studio Code",
    "clientVersion": "1.100.1",
    "agentName": "GitHub Copilot",
    "agentVersion": "1.234.0",
    "agentModel": "gpt-4",
    "toolName": "azsdk_run_tests",
    "toolCategory": "testing",
    "toolArgs": "{\"serviceName\":\"healthdataaiservices\",\"language\":\"Java\",\"testMode\":\"playback\"}",
    "toolResponse": "Test execution failed",
    "language": "Java",
    "planeType": "data",
    "serviceName": "healthdataaiservices",
    "isCustomSdk": false,
    "errorType": "TestFailure",
    "errorMessage": "3 tests failed: test_authentication_failure, test_invalid_request, test_timeout"
  }
}
```

#### Example 3: System Event - MCP Server Started

```json
{
  "timestamp": "2025-10-16T08:15:30.123Z",
  "name": "SystemStarted",
  "client_Type": "PC",
  "client_OS": "Windows 10",
  "success": true,
  "duration": 450,
  "client_CountryOrRegion": "United States",
  "customDimensions": {
    "version": "0.5.6.0",
    "sessionId": "d8e9f0a1-b2c3-4567-89ab-cdef01234567",
    "eventId": "e8745ddd-ad45-6ah6-b5b7-8g2967c72f49",
    "macAddressHash": "7b1a2a70a41d8154f6aeaf50e67b052fb9ca194de22c...",
    "devDeviceId": "9b705c2e5b927992ab862a50e3552cadcc35f0fa0000...",
    "platform": "VSCode",
    "isOptedIn": true,
    "clientName": "Visual Studio Code",
    "clientVersion": "1.100.1",
    "eventType": "start"
  }
}
```

#### Example 4: CLI Command Execution

```json
{
  "timestamp": "2025-10-16T14:20:45.678Z",
  "name": "ToolExecuted",
  "client_Type": "PC",
  "client_OS": "Ubuntu 22.04",
  "success": true,
  "duration": 3210,
  "client_CountryOrRegion": "India",
  "customDimensions": {
    "version": "1.0.0",
    "sessionId": "f9g0h1i2-j3k4-5678-90lm-nop123456789",
    "eventId": "a9856eee-be56-7bi7-c6c8-9h3078d83g50",
    "macAddressHash": "8c2b3b81b52e9265g7bgbg61f78c163gc0db205ef33d...",
    "devDeviceId": "a0816d3f6c038aa3bc973b61f4663dbedd46g1gb1111...",
    "platform": "CLI",
    "isOptedIn": true,
    "toolName": "azsdk_verify_setup",
    "toolCategory": "env-setup",
    "toolArgs": "{\"languages\":\".NET,Java,JavaScript,Python,Go\"}",
    "toolResponse": "All 5 languages verified successfully"
  }
}
```

---

## System Lifecycle Events

In addition to user-triggered tool invocations, the telemetry system tracks key lifecycle events for both the MCP server and CLI application.

### MCP Server Lifecycle Events

#### Installation Event

**When**: First time MCP server is installed or added to a user's environment

**Purpose**: Track new installations, measure adoption rate, understand installation success/failure

**Event Properties**:
```json
{
  "name": "SystemInstalled",
  "customDimensions": {
    "eventType": "install",
    "version": "0.5.6.0",
    "installMethod": "npm", // or "manual", "vscode-extension"
    "platform": "VSCode"
  }
}
```

#### Startup Event

**When**: MCP server process starts

**Purpose**: Track server availability, measure startup performance, understand usage patterns by time of day

**Event Properties**:
```json
{
  "name": "SystemStarted",
  "duration": 450, // startup time in ms
  "customDimensions": {
    "eventType": "start",
    "version": "0.5.6.0",
    "clientName": "Visual Studio Code",
    "clientVersion": "1.100.1",
    "platform": "VSCode"
  }
}
```

#### Shutdown Event

**When**: MCP server process stops normally

**Purpose**: Understand session duration, track graceful vs. ungraceful shutdowns

**Event Properties**:
```json
{
  "name": "SystemStopped",
  "duration": 3600000, // total session duration in ms
  "customDimensions": {
    "eventType": "stop",
    "version": "0.5.6.0",
    "sessionDuration": 3600000,
    "toolCallsInSession": 12,
    "platform": "VSCode"
  }
}
```

#### Crash Event

**When**: MCP server crashes or terminates unexpectedly

**Purpose**: Track reliability issues, identify crash patterns

**Event Properties**:
```json
{
  "name": "SystemCrashed",
  "success": false,
  "customDimensions": {
    "eventType": "crash",
    "version": "0.5.6.0",
    "crashReason": "UnhandledException",
    "errorMessage": "Cannot read property 'path' of undefined",
    "stackTrace": "Error: Cannot read property...\n  at generateSdk...",
    "platform": "VSCode"
  }
}
```

#### Update Event

**When**: User updates to a new version of MCP server

**Purpose**: Track update adoption, measure version distribution, understand update patterns

**Event Properties**:
```json
{
  "name": "SystemUpdated",
  "customDimensions": {
    "eventType": "update",
    "version": "0.5.7.0",
    "previousVersion": "0.5.6.0",
    "updateMethod": "automatic", // or "manual"
    "platform": "VSCode"
  }
}
```

#### Configuration Change Event

**When**: User modifies MCP server configuration settings

**Purpose**: Understand which settings users change, track opt-out rate for telemetry

**Event Properties**:
```json
{
  "name": "SystemConfigured",
  "customDimensions": {
    "eventType": "configure",
    "version": "0.5.6.0",
    "configChanges": "{\"telemetryEnabled\":false,\"logLevel\":\"debug\"}",
    "platform": "VSCode"
  }
}
```

### CLI Application Lifecycle Events

#### CLI Installation

**When**: CLI is installed via package manager or script

**Event Properties**:
```json
{
  "name": "SystemInstalled",
  "customDimensions": {
    "eventType": "install",
    "version": "1.0.0",
    "installMethod": "npm", // or "brew", "pip", "manual"
    "platform": "CLI"
  }
}
```

#### CLI Command Execution Start

**When**: User runs any `azsdk` CLI command

**Purpose**: Track CLI usage distinct from MCP server usage

**Event Properties**:
```json
{
  "name": "ToolExecuted",
  "customDimensions": {
    "platform": "CLI",
    "toolName": "azsdk_generate_sdk",
    "executionMode": "direct" // vs "agent-invoked"
  }
}
```

#### CLI Update

**When**: CLI is updated to a new version

**Event Properties**:
```json
{
  "name": "SystemUpdated",
  "customDimensions": {
    "eventType": "update",
    "version": "1.1.0",
    "previousVersion": "1.0.0",
    "platform": "CLI"
  }
}
```

### Session Tracking

Each user session (from MCP server start to stop, or from first CLI command to 30 minutes of inactivity) receives a unique `sessionId`. This enables:

- Calculating session duration
- Counting tool calls per session
- Understanding user workflows (sequence of tools called)
- Measuring time to first successful operation

---

## Agent Prompts

This section demonstrates how user prompts in agent mode translate to tool invocations and the telemetry events that result.

### Prompt 1: Generate SDK for a Service

**User Prompt:**
```text
Generate the Health Deidentification SDK for .NET from the TypeSpec specifications.
```

**Agent Activity:**
1. Agent identifies need to call `azsdk_generate_sdk` tool
2. Agent constructs tool arguments from prompt context
3. Tool executes SDK generation
4. Tool returns success or error response

**Telemetry Event Generated:**
```json
{
  "timestamp": "2025-10-16T23:44:57.217Z",
  "name": "ToolExecuted",
  "success": true,
  "duration": 12450,
  "customDimensions": {
    "toolName": "azsdk_generate_sdk",
    "toolCategory": "generating",
    "toolArgs": "{\"typespecProjectRoot\":\"specification/healthdataaiservices\",\"language\":\".NET\",\"apiVersion\":\"2025-09-01\"}",
    "language": ".NET",
    "serviceName": "healthdataaiservices",
    "planeType": "data",
    "agentName": "GitHub Copilot",
    "agentModel": "gpt-4"
  }
}
```

**Key Telemetry Insights:**
- Which services are being generated
- Which languages are most popular
- SDK generation success rate
- Average generation latency
- Which AI models users prefer

### Prompt 2: Run Tests in Playback Mode

**User Prompt:**
```text
Run the tests for the Java healthdataaiservices SDK in playback mode.
```

**Agent Activity:**
1. Agent calls `azsdk_run_tests` tool
2. Agent specifies playback mode and Java language
3. Tests execute using pre-recorded interactions
4. Results returned to user

**Telemetry Event Generated:**
```json
{
  "timestamp": "2025-10-16T23:50:12.543Z",
  "name": "ToolExecuted",
  "success": false,
  "duration": 8234,
  "customDimensions": {
    "toolName": "azsdk_run_tests",
    "toolCategory": "testing",
    "toolArgs": "{\"serviceName\":\"healthdataaiservices\",\"language\":\"Java\",\"testMode\":\"playback\"}",
    "language": "Java",
    "serviceName": "healthdataaiservices",
    "errorType": "TestFailure",
    "errorMessage": "3 tests failed"
  }
}
```

**Key Telemetry Insights:**
- Testing adoption rate (how many users test after generation?)
- Test success rate by language
- Most common test failure types
- Playback vs. live test usage

### Prompt 3: Verify Environment Setup

**User Prompt:**
```text
Check if my environment is ready for SDK development in all languages.
```

**Agent Activity:**
1. Agent calls `azsdk_verify_setup` tool
2. Tool checks for required SDKs, tools, and dependencies
3. Returns list of met and unmet requirements

**Telemetry Event Generated:**
```json
{
  "timestamp": "2025-10-16T14:20:45.678Z",
  "name": "ToolExecuted",
  "success": true,
  "duration": 3210,
  "customDimensions": {
    "toolName": "azsdk_verify_setup",
    "toolCategory": "env-setup",
    "toolArgs": "{\"languages\":\".NET,Java,JavaScript,Python,Go\"}",
    "toolResponse": "All 5 languages verified successfully"
  }
}
```

**Key Telemetry Insights:**
- Which languages users work with most
- Common environment setup issues
- Verification success rate
- Time spent on environment setup

### Prompt 4: Full Workflow - Generate, Test, and Validate

**User Prompt:**
```text
I want to prepare a preview release of the Storage SDK for Python. Generate it, run tests, and validate everything is ready for a PR.
```

**Agent Activity:**
1. Agent calls `azsdk_generate_sdk` for Python storage
2. Agent calls `azsdk_run_tests` in playback mode
3. Agent calls `azsdk_run_pr_checks` to validate
4. Multiple telemetry events generated (one per tool call)

**Telemetry Events Generated:**
```json
// Event 1: SDK Generation
{
  "timestamp": "2025-10-16T15:00:00.000Z",
  "name": "ToolExecuted",
  "duration": 10500,
  "customDimensions": {
    "sessionId": "xyz123",
    "toolName": "azsdk_generate_sdk",
    "language": "Python",
    "serviceName": "storage"
  }
}

// Event 2: Test Execution
{
  "timestamp": "2025-10-16T15:00:11.500Z",
  "name": "ToolExecuted",
  "duration": 6200,
  "customDimensions": {
    "sessionId": "xyz123",
    "toolName": "azsdk_run_tests",
    "language": "Python",
    "serviceName": "storage"
  }
}

// Event 3: PR Checks
{
  "timestamp": "2025-10-16T15:00:18.700Z",
  "name": "ToolExecuted",
  "duration": 15000,
  "customDimensions": {
    "sessionId": "xyz123",
    "toolName": "azsdk_run_pr_checks",
    "language": "Python",
    "serviceName": "storage"
  }
}
```

**Key Telemetry Insights:**
- Complete workflow adoption (how many users do full workflow vs. partial?)
- Tool call sequences (what order do users execute tools?)
- Where users drop off in the workflow
- Time to complete full workflow
- Success rate of end-to-end workflow

### Prompt 5: Update Package Metadata

**User Prompt:**
```text
Update the package version to 2.0.0-beta.1 for the Cosmos DB management plane SDK in all languages.
```

**Agent Activity:**
1. Agent calls `azsdk_update_package` tool
2. Tool updates version in metadata files
3. Tool updates changelogs (mgmt plane only)

**Telemetry Event Generated:**
```json
{
  "timestamp": "2025-10-16T16:30:22.890Z",
  "name": "ToolExecuted",
  "success": true,
  "duration": 2100,
  "customDimensions": {
    "toolName": "azsdk_update_package",
    "toolCategory": "package",
    "toolArgs": "{\"serviceName\":\"cosmosdb\",\"version\":\"2.0.0-beta.1\",\"languages\":\"all\"}",
    "planeType": "mgmt",
    "serviceName": "cosmosdb"
  }
}
```

**Key Telemetry Insights:**
- Package update frequency by service
- Preview vs. GA release ratio
- Which services release most frequently

---

## CLI Commands

This section demonstrates direct CLI usage and the telemetry events generated. CLI telemetry uses the same schema as MCP server telemetry but sets `platform: "CLI"`.

### Command 1: Verify Setup

**CLI Command:**
```bash
azsdk verify-setup --languages .NET,Java,JavaScript,Python,Go --verbose
```

**Telemetry Event Generated:**
```json
{
  "timestamp": "2025-10-16T14:20:45.678Z",
  "name": "ToolExecuted",
  "success": true,
  "duration": 3210,
  "customDimensions": {
    "platform": "CLI",
    "toolName": "azsdk_verify_setup",
    "toolCategory": "env-setup",
    "toolArgs": "{\"languages\":\".NET,Java,JavaScript,Python,Go\",\"verbose\":true}",
    "executionMode": "direct"
  }
}
```

### Command 2: Generate SDK

**CLI Command:**
```bash
azsdk generate-sdk --service healthdataaiservices --language .NET --api-version 2025-09-01
```

**Telemetry Event Generated:**
```json
{
  "timestamp": "2025-10-16T23:44:57.217Z",
  "name": "ToolExecuted",
  "success": true,
  "duration": 12450,
  "customDimensions": {
    "platform": "CLI",
    "toolName": "azsdk_generate_sdk",
    "toolCategory": "generating",
    "toolArgs": "{\"service\":\"healthdataaiservices\",\"language\":\".NET\",\"apiVersion\":\"2025-09-01\"}",
    "language": ".NET",
    "serviceName": "healthdataaiservices",
    "executionMode": "direct"
  }
}
```

### Command 3: Run Tests

**CLI Command:**
```bash
azsdk run-tests --service storage --language Python --mode playback
```

**Telemetry Event Generated:**
```json
{
  "timestamp": "2025-10-16T15:10:30.123Z",
  "name": "ToolExecuted",
  "success": true,
  "duration": 5600,
  "customDimensions": {
    "platform": "CLI",
    "toolName": "azsdk_run_tests",
    "toolCategory": "testing",
    "toolArgs": "{\"service\":\"storage\",\"language\":\"Python\",\"mode\":\"playback\"}",
    "language": "Python",
    "serviceName": "storage",
    "executionMode": "direct"
  }
}
```

### Command 4: Run PR Checks

**CLI Command:**
```bash
azsdk run-pr-checks --service cosmosdb --languages .NET,Java --parallel
```

**Telemetry Event Generated:**
```json
{
  "timestamp": "2025-10-16T16:45:12.456Z",
  "name": "ToolExecuted",
  "success": true,
  "duration": 18900,
  "customDimensions": {
    "platform": "CLI",
    "toolName": "azsdk_run_pr_checks",
    "toolCategory": "validating",
    "toolArgs": "{\"service\":\"cosmosdb\",\"languages\":\".NET,Java\",\"parallel\":true}",
    "serviceName": "cosmosdb",
    "executionMode": "direct"
  }
}
```

### Command 5: Update Package

**CLI Command:**
```bash
azsdk update-package --service keyvault --version 4.5.0-beta.2 --update-changelog
```

**Telemetry Event Generated:**
```json
{
  "timestamp": "2025-10-16T17:00:00.789Z",
  "name": "ToolExecuted",
  "success": true,
  "duration": 1800,
  "customDimensions": {
    "platform": "CLI",
    "toolName": "azsdk_update_package",
    "toolCategory": "package",
    "toolArgs": "{\"service\":\"keyvault\",\"version\":\"4.5.0-beta.2\",\"updateChangelog\":true}",
    "serviceName": "keyvault",
    "executionMode": "direct"
  }
}
```

---

## Dashboard Requirements

Based on the telemetry data collected, we need dashboards to answer key business questions. This section maps requirements from the issue to specific queries and visualizations.

### Top 5 Priority Reports for Leadership

#### 1. Rolling 28-Day MAU, MEU, and MDU

**Purpose**: Track active user growth and engagement trends

**Metrics**:
- **MAU**: Count of unique `devDeviceId` with at least 1 event in last 28 days
- **MEU**: Count of unique `devDeviceId` with events on ≥2 distinct days in last 28 days
- **MDU**: Count of unique `devDeviceId` with events on ≥10 distinct days in last 28 days

**Kusto Query**:
```kusto
RawEventsDependencies
| where timestamp >= ago(28d)
| where name in ("ToolExecuted", "SystemStarted")
| extend devDeviceId = tostring(customDimensions.devDeviceId)
| summarize 
    DaysActive = dcount(format_datetime(timestamp, 'yyyy-MM-dd')),
    EventCount = count()
    by devDeviceId
| summarize 
    MAU = count(),
    MEU = countif(DaysActive >= 2),
    MDU = countif(DaysActive >= 10)
```

**Visualization**: Line chart showing MAU/MEU/MDU trends over time (daily calculation with 28-day rolling window)

#### 2. SDK Generation Success & Latency

**Purpose**: Measure reliability and performance of SDK generation

**Metrics**:
- Total SDK generations (count of `ToolExecuted` where `toolName` = `azsdk_generate_sdk`)
- Success rate (% where `success` = true)
- Average latency (`avg(duration)`)
- P95 latency (`percentile(duration, 95)`)
- Breakdown by language

**Kusto Query**:
```kusto
RawEventsDependencies
| where name == "ToolExecuted"
| where customDimensions.toolName == "azsdk_generate_sdk"
| extend language = tostring(customDimensions.language)
| summarize 
    TotalGenerations = count(),
    Successes = countif(success == true),
    AvgLatencyMs = avg(duration),
    P95LatencyMs = percentile(duration, 95)
    by language
| extend SuccessRate = round(100.0 * Successes / TotalGenerations, 2)
```

**Visualization**: 
- Bar chart: Success rate by language
- Box plot: Latency distribution by language

#### 3. Adoption Gap: Packages Released Without MCP/AI

**Purpose**: Identify teams not using MCP/AI workflows

**Approach**:
1. Get list of all packages with at least one MCP tool call (from telemetry)
2. Get list of all packages released (from release pipeline data - external to this spec)
3. Calculate difference to find packages released without MCP

**Kusto Query** (Telemetry portion):
```kusto
RawEventsDependencies
| where name == "ToolExecuted"
| extend serviceName = tostring(customDimensions.serviceName)
| extend language = tostring(customDimensions.language)
| where isnotempty(serviceName) and isnotempty(language)
| summarize by serviceName, language
| project PackageWithMCP = strcat(serviceName, "-", language)
```

**Note**: Requires integration with release pipeline data to complete the calculation

**Visualization**: 
- Table: Packages released without MCP (serviceName, language, release date)
- Trend: % of releases without MCP over time

#### 4. Platform & Agent Usage Breakdown

**Purpose**: Understand which platforms and AI agents users prefer

**Metrics**:
- Event count by platform (VSCode, CLI, GitHub, JetBrains)
- Event count by agent (GitHub Copilot, Claude Code, etc.)
- Event count by AI model (GPT-4, Claude Sonnet 4, etc.)

**Kusto Query**:
```kusto
RawEventsDependencies
| where name == "ToolExecuted"
| extend platform = tostring(customDimensions.platform)
| extend agentName = tostring(customDimensions.agentName)
| extend agentModel = tostring(customDimensions.agentModel)
| summarize EventCount = count() by platform, agentName, agentModel
| order by EventCount desc
```

**Visualization**:
- Pie chart: Platform distribution
- Stacked bar chart: Agent usage by platform
- Table: Model usage breakdown

#### 5. Tool Invocation Reliability

**Purpose**: Track success rate and performance of all tools

**Metrics**:
- Success rate by tool
- Average latency by tool
- Error type distribution

**Kusto Query**:
```kusto
RawEventsDependencies
| where name == "ToolExecuted"
| extend toolName = tostring(customDimensions.toolName)
| extend errorType = tostring(customDimensions.errorType)
| summarize 
    TotalCalls = count(),
    Successes = countif(success == true),
    AvgLatencyMs = avg(duration),
    Errors = countif(success == false),
    ErrorBreakdown = make_bag_if(errorType, success == false)
    by toolName
| extend SuccessRate = round(100.0 * Successes / TotalCalls, 2)
| order by TotalCalls desc
```

**Visualization**:
- Table: Tool name, total calls, success rate, avg latency
- Heat map: Success rate by tool over time

### Comprehensive Telemetry Questions & Queries

#### A. Usage and Adoption Metrics

**Question**: How many users are active each month?
- **Query**: See MAU/MEU/MDU query above

**Question**: How many PRs were created or updated?
- **Note**: PR creation is out of scope for Scenario 1 (inner loop only). This requires integration with GitHub API or Azure DevOps telemetry.

**Question**: How many release plans were generated?
- **Query**: Count `ToolExecuted` events where `toolName` = `azsdk_create_release_plan`

**Question**: How many SDKs were generated by language?
```kusto
RawEventsDependencies
| where name == "ToolExecuted"
| where customDimensions.toolName == "azsdk_generate_sdk"
| extend language = tostring(customDimensions.language)
| summarize GenerationCount = count() by language
```

**Question**: What is the data-plane vs. management-plane ratio?
```kusto
RawEventsDependencies
| where name == "ToolExecuted"
| extend planeType = tostring(customDimensions.planeType)
| where isnotempty(planeType)
| summarize EventCount = count() by planeType
```

#### B. Performance and Reliability Metrics

**Question**: What is the average, median, and P95 latency for key operations?
```kusto
RawEventsDependencies
| where name == "ToolExecuted"
| extend toolCategory = tostring(customDimensions.toolCategory)
| summarize 
    AvgLatencyMs = avg(duration),
    MedianLatencyMs = percentile(duration, 50),
    P95LatencyMs = percentile(duration, 95)
    by toolCategory
```

**Question**: What is the success and error rate for each tool?
- **Query**: See Tool Invocation Reliability query above

**Question**: What is the breakdown of errors per tool?
```kusto
RawEventsDependencies
| where name == "ToolExecuted"
| where success == false
| extend toolName = tostring(customDimensions.toolName)
| extend errorType = tostring(customDimensions.errorType)
| summarize ErrorCount = count() by toolName, errorType
| order by ErrorCount desc
```

#### C. Agent and Tool Insights

**Question**: Which tools are most frequently called?
```kusto
RawEventsDependencies
| where name == "ToolExecuted"
| extend toolName = tostring(customDimensions.toolName)
| summarize CallCount = count() by toolName
| order by CallCount desc
```

**Question**: What is the sequence of tools called in a typical user session?
```kusto
RawEventsDependencies
| where name == "ToolExecuted"
| extend sessionId = tostring(customDimensions.sessionId)
| extend toolName = tostring(customDimensions.toolName)
| order by sessionId, timestamp asc
| summarize ToolSequence = make_list(toolName) by sessionId
| summarize SessionCount = count() by ToolSequence
| order by SessionCount desc
| take 20
```

**Question**: What is the distribution of MCP calls by category?
```kusto
RawEventsDependencies
| where name == "ToolExecuted"
| extend toolCategory = tostring(customDimensions.toolCategory)
| summarize CallCount = count() by toolCategory
| order by CallCount desc
```

**Question**: Which MCP clients and AI models have higher success rates vs. failure rates?
```kusto
RawEventsDependencies
| where name == "ToolExecuted"
| extend clientName = tostring(customDimensions.clientName)
| extend agentModel = tostring(customDimensions.agentModel)
| where isnotempty(clientName) and isnotempty(agentModel)
| summarize 
    TotalCalls = count(),
    Successes = countif(success == true),
    Failures = countif(success == false),
    AvgDurationMs = avg(duration)
    by clientName, agentModel
| extend SuccessRate = round(100.0 * Successes / TotalCalls, 2)
| order by TotalCalls desc
```

**Question**: Are certain types of errors or failures more common with specific MCP clients or AI models?
```kusto
RawEventsDependencies
| where name == "ToolExecuted"
| where success == false
| extend clientName = tostring(customDimensions.clientName)
| extend agentModel = tostring(customDimensions.agentModel)
| extend errorType = tostring(customDimensions.errorType)
| where isnotempty(clientName) and isnotempty(agentModel) and isnotempty(errorType)
| summarize ErrorCount = count() by clientName, agentModel, errorType
| order by ErrorCount desc
| take 50
```

#### D. Workflow and Process Analysis

**Question**: How long does it take users to complete each workflow step?
```kusto
RawEventsDependencies
| where name == "ToolExecuted"
| extend toolName = tostring(customDimensions.toolName)
| extend sessionId = tostring(customDimensions.sessionId)
| summarize AvgDurationMs = avg(duration) by toolName
```

**Question**: Where do users drop off in multi-step workflows?
```kusto
// Example: Users who generate but don't test
let UsersWhoGenerate = 
    RawEventsDependencies
    | where name == "ToolExecuted"
    | where customDimensions.toolName == "azsdk_generate_sdk"
    | extend devDeviceId = tostring(customDimensions.devDeviceId)
    | distinct devDeviceId;
let UsersWhoTest =
    RawEventsDependencies
    | where name == "ToolExecuted"
    | where customDimensions.toolName == "azsdk_run_tests"
    | extend devDeviceId = tostring(customDimensions.devDeviceId)
    | distinct devDeviceId;
UsersWhoGenerate
| join kind=leftanti UsersWhoTest on devDeviceId
| count
```

#### E. Segmentation and Breakdown

**Question**: What is usage breakdown by language?
```kusto
RawEventsDependencies
| where name == "ToolExecuted"
| extend language = tostring(customDimensions.language)
| where isnotempty(language)
| summarize EventCount = count() by language
```

**Question**: What is the geographic distribution of users?
```kusto
RawEventsDependencies
| where name in ("ToolExecuted", "SystemStarted")
| summarize UserCount = dcount(tostring(customDimensions.devDeviceId)) 
    by client_CountryOrRegion
| order by UserCount desc
```

---

## Open Questions

- [x] **Question 1: Custom vs. Standard SDK Tracking**
  - **Context**: Issue comments ask how to distinguish custom SDKs from standard SDKs
  - **Options**: 
    1. Add `isCustomSdk` boolean to telemetry based on presence of customization files
    2. Infer from tool calls (e.g., if user calls customization-related tools)
    3. Don't track this distinction initially
  - **Proposal**: Option 1 - Add `isCustomSdk` boolean field based on detection of custom code files in SDK directory

- [x] **Question 2: NPS Score Collection**
  - **Context**: Issue asks about NPS tracking but current telemetry doesn't support it
  - **Options**:
    1. Add NPS prompt as system event after first successful SDK generation
    2. Collect NPS via separate survey tool and correlate by `devDeviceId`
    3. Defer NPS to future iteration
  - **Proposal**: Option 3 - Defer to future iteration. Focus on usage metrics first.

- [x] **Question 3: Agent Suggestion Tracking**
  - **Context**: Issue asks about tracking suggestions presented vs. applied vs. overridden
  - **Options**:
    1. Track when Copilot suggests a tool (requires Copilot integration)
    2. Track only executed tools (current approach)
    3. Add response-based suggestion tracking (tools suggest next steps in responses)
  - **Proposal**: Option 2 for initial implementation. Option 3 can be added later if tools return structured JSON with suggestions.

- [x] **Question 4: Release Pipeline Integration**
  - **Context**: Adoption gap calculation requires package release data
  - **Options**:
    1. Integrate with Azure DevOps pipeline telemetry
    2. Integrate with package manager APIs (PyPI, npm, Maven, NuGet)
    3. Manual correlation initially
  - **Proposal**: Option 3 initially, plan for Option 1 integration in Phase 2

- [ ] **Question 5: Telemetry Opt-Out UX**
  - **Context**: Users must be able to disable telemetry
  - **Options**:
    1. Environment variable (`AZSDK_TELEMETRY_ENABLED=false`)
    2. Configuration file setting
    3. Interactive prompt on first run
    4. All of the above
  - **Proposal**: TBD - Need to align with team preferences

- [ ] **Question 6: Sensitive Data Sanitization**
  - **Context**: Tool arguments may contain secrets, internal URLs, etc.
  - **Options**:
    1. Allowlist approach (only include known-safe fields)
    2. Blocklist approach (redact known patterns like API keys)
    3. Hash all string values
  - **Proposal**: TBD - Need security review

---

## Success Criteria

This telemetry design is complete when:

- [x] Telemetry schema is fully defined for all event types
- [x] System lifecycle events are documented for MCP server and CLI
- [x] Agent prompt examples include expected telemetry events
- [x] CLI command examples include expected telemetry events
- [x] Dashboard requirements map to specific Kusto queries
- [x] Top 5 priority reports are specified with visualizations
- [x] Privacy considerations (no PII, opt-out) are addressed
- [ ] Implementation plan is approved by stakeholders
- [ ] Open questions are resolved
- [ ] Language teams review and approve schema
- [ ] Security review approves data sanitization approach

---

## Implementation Plan

### Phase 1: Core Telemetry Infrastructure (Weeks 1-3)

**Milestone**: Basic telemetry collection working in MCP server

**Deliverables**:
- Application Insights connection and configuration
- Telemetry collector class with event queuing
- System lifecycle events (start, stop, crash)
- Basic tool execution events (name, duration, success)
- Environment variable opt-out mechanism

**Dependencies**: 
- Application Insights workspace provisioned
- Instrumentation key securely distributed

### Phase 2: Rich Event Data (Weeks 4-6)

**Milestone**: Complete telemetry schema implemented

**Deliverables**:
- Full `customDimensions` schema for all event types
- Platform/agent/language detection and logging
- Argument sanitization for security
- Error type classification and logging
- Session tracking and correlation

**Dependencies**:
- Phase 1 complete
- Security review of argument sanitization

### Phase 3: CLI Telemetry (Weeks 7-8)

**Milestone**: CLI application sends telemetry

**Deliverables**:
- Telemetry collection in CLI application
- CLI lifecycle events
- Direct command execution tracking
- Unified schema with MCP server

**Dependencies**:
- Phase 2 complete

### Phase 4: Dashboards and Reporting (Weeks 9-12)

**Milestone**: Leadership dashboards available

**Deliverables**:
- Kusto queries for all priority reports
- Power BI dashboards with visualizations
- Automated daily/weekly reporting
- Documentation for dashboard usage

**Dependencies**:
- Phase 3 complete
- 2-4 weeks of production data collected

### Phase 5: Advanced Analytics (Weeks 13-16)

**Milestone**: Advanced insights and correlations

**Deliverables**:
- Adoption gap calculation (requires release data integration)
- Workflow sequence analysis
- Predictive analytics (e.g., churn prediction)
- A/B test framework for tool improvements

**Dependencies**:
- Phase 4 complete
- Release pipeline data integration

---

## Testing Strategy

### Unit Tests

**Telemetry Collector**:
- Test event formatting and schema validation
- Test argument sanitization (ensure secrets are removed)
- Test opt-out mechanism (no events sent when disabled)
- Test error handling (telemetry failures don't crash app)

**Event Queuing**:
- Test queue overflow behavior
- Test retry logic with exponential backoff
- Test batch transmission

### Integration Tests

**End-to-End Telemetry Flow**:
- Test MCP server sends events to Application Insights
- Test CLI sends events to Application Insights
- Test events appear in RawEventsDependencies table
- Test session correlation across multiple events

**Cross-Platform Testing**:
- Test on Windows, macOS, Linux
- Test with different MCP clients (VS Code, Claude Desktop)
- Test with different AI agents (Copilot, Claude)

### Manual Testing

**Privacy Validation**:
- Manually inspect telemetry events to confirm no PII
- Verify opt-out completely stops telemetry
- Verify sensitive arguments are sanitized

**Dashboard Validation**:
- Run Kusto queries against test data
- Verify dashboard visualizations are accurate
- Test with different date ranges and filters

### Monitoring and Alerting

**Production Monitoring**:
- Alert if telemetry event rate drops significantly (indicates collection failure)
- Alert if error rate exceeds threshold
- Monitor Application Insights ingestion latency

---

## Documentation Updates

### User-Facing Documentation

- [ ] **Privacy Policy**: Document what telemetry is collected and how to opt out
- [ ] **Configuration Guide**: Document telemetry settings and environment variables
- [ ] **FAQ**: Answer common questions about telemetry

### Internal Documentation

- [ ] **Telemetry Schema Reference**: Complete reference of all event types and fields
- [ ] **Dashboard User Guide**: How to use Power BI dashboards and Kusto queries
- [ ] **Integration Guide**: How to add telemetry to new tools
- [ ] **Debugging Guide**: How to test telemetry locally and troubleshoot issues

### Code Documentation

- [ ] Inline documentation for telemetry collector classes
- [ ] Examples of adding telemetry to new tools
- [ ] Sanitization patterns for different argument types

---

## Privacy and Security Considerations

### No Personally Identifiable Information (PII)

**What We Don't Collect**:
- Usernames, email addresses, or real names
- Full file paths or directory structures
- Internal company URLs or endpoints
- Code snippets or TypeSpec content
- Specific error messages that might contain PII

**What We Do Collect**:
- Hashed identifiers (MAC address hash, DevDeviceId hash)
- Service names and language choices (non-sensitive)
- Tool names and categories
- Performance metrics (duration, success/failure)
- Sanitized error types (not full messages)

### Opt-Out Mechanism

Users can disable telemetry by:
1. Setting environment variable: `AZSDK_TELEMETRY_ENABLED=false`
2. Configuration file setting: `telemetry.enabled: false`
3. CLI flag: `azsdk config set telemetry.enabled false`

When opted out, no telemetry events are collected or transmitted.

### Argument Sanitization

Before logging tool arguments, we:
1. **Blocklist patterns**: Remove fields matching known secret patterns (keys, tokens, passwords)
2. **Allowlist fields**: For complex objects, only include known-safe fields
3. **Hash sensitive IDs**: Replace internal IDs with hashes where appropriate

**Example Sanitization**:
```javascript
// Before sanitization
{
  "apiKey": "abc123-secret",
  "connectionString": "Server=...",
  "serviceName": "storage",
  "language": "Python"
}

// After sanitization
{
  "serviceName": "storage",
  "language": "Python"
  // apiKey and connectionString removed
}
```

### Data Retention

- Telemetry data retained for 90 days in Application Insights
- Aggregated metrics (MAU, success rates) retained indefinitely
- No raw event data stored beyond 90 days

### Compliance

- GDPR compliant (no PII, opt-out available)
- Follows Microsoft's telemetry standards
- Security review required before production deployment

---

## Related Issues and Links

- [Issue #12486: Telemetry & Dashboard Requirements](https://github.com/Azure/azure-sdk-tools/issues/12486)
- [Scenario 1 Spec](./0-v1-scenario.spec.md)
- [Spec Template](./spec-template.md)
- [Spec README](./README.md)
