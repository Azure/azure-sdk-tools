<!-- cspell:words allowlist allowlists azsdk AZSDKTOOLS contoso deduplicate deduplication opentelemetry OTLP reentrancy revalidate typespec untracked worktree worktrees -->

# Spec: 8-Operations - Common Skill Telemetry

## Table of Contents

- [Existing Skill Telemetry and Gaps](#existing-skill-telemetry-and-gaps)
- [Design Proposal](#design-proposal)
- [What Skill Owners Do to Add Customized Telemetry](#what-skill-owners-do-to-add-customized-telemetry)
- [Alternatives Considered](#alternatives-considered)
- [Open Questions](#open-questions)

## Existing Skill Telemetry and Gaps

Today, [`azsdk_tool_telemetry.ps1`](https://github.com/Azure/azure-sdk-tools/blob/main/eng/common/scripts/azsdk_tool_telemetry.ps1) runs on `PostToolUse` and calls [`azsdk ingest-telemetry`](https://github.com/Azure/azure-sdk-tools/blob/main/tools/azsdk-cli/Azure.Sdk.Tools.Cli/Tools/Core/TelemetryIngestionTool.cs), which uses OpenTelemetry (`System.Diagnostics.Activity` + `Azure.Monitor.OpenTelemetry.Exporter`) whenever the agent invokes the `skill` tool or reads `/skills/<name>/SKILL.md`:

| Category | Already recorded automatically (zero skill-owner work) | Not supported today (gap) |
| --- | --- | --- |
| Skill invocation | OpenTelemetry Span (`name = skill_invocation`), `SkillName`, and timestamp in Application Insights `dependencies` | Whether the skill finished, succeeded, or failed, and its execution duration |
| Client & session info | `ClientName` (`vscode` if `tool_use_id` contains `_vscode-`, otherwise `copilot-cli`), optional `SessionId`, plus CLI `Version`, `DevDeviceId`, `MacAddressHash`, and `EventId` | Distinguishing `copilot-app` from `copilot-cli` (today both are recorded as `copilot-cli`) |
| Customized skill metrics | Separate MCP tool calls record their own tool-level OpenTelemetry Spans | **Skill-specific customized telemetry** (e.g., [#17228](https://github.com/Azure/azure-sdk-tools/issues/17228): changed `.tsp` files and line ranges, finding counts, or workflow outcomes) without collecting user prompts |

Because Azure SDK has many pure-prompt and script-backed skills across languages and planes, this spec extends the existing hook and OpenTelemetry pipeline so any skill needing **customized telemetry** can emit OpenTelemetry-compliant structured data—using `azure-typespec-author` throughout as the running example.

## Design Proposal

### Architecture

![Common skill telemetry architecture](8-operations-skill-telemetry-architecture.svg)

| Component | Owner | Responsibility |
| --- | --- | --- |
| Skill schema & collector | Skill owner | Define an OpenTelemetry attribute/event schema (`telemetry/result.schema.json`) and write a local `.ready.json` file when enabled (no direct upload) |
| Local queue folder | Shared helper | Untracked per-worktree folder (`<git-dir>/azsdk-skill-telemetry/`) holding `<eventId>.ready.json` files |
| Common `PostToolUse` hook | Shared framework | Check opt-out, claim `.ready.json` files, validate schema and relative paths, and call `azsdk ingest-telemetry` |
| Shared `azsdk` sender | Shared framework | Revalidate schema, map the payload to an OpenTelemetry `Activity` + `ActivityEvent`s, and flush to Application Insights |

### 1. OpenTelemetry-Compliant Data Format and Privacy

Each `.ready.json` file follows the **OpenTelemetry Span data model** (`name`, `kind`, `startTimeUtc`, `endTimeUtc`, `status`, `schemaUrl`, `attributes`, and optional `events`) so it maps 1:1 onto `.NET System.Diagnostics.Activity` (and standard OTLP exporters):

- **Span `attributes`** (exported to `dependencies.customDimensions`): Keys use OpenTelemetry dotted `snake_case` namespaces (`event.id`, `skill.name`, `skill.version`, `skill.invocation_id`, `<domain>.*`). Values strictly follow OpenTelemetry attribute rules—**primitives (`string`, `boolean`, `number`) or homogeneous primitive arrays (`string[]`, `number[]`, `boolean[]`) only**, with no nested JSON objects.
- **Span `events`** (exported via `Activity.AddEvent` to Application Insights `traces` and correlated to the parent Span via OpenTelemetry `trace_id` / `span_id`): Used for per-item details (such as each modified file in `azure-typespec-author`), where each event's `attributes` also contains only OpenTelemetry primitive or primitive-array values.

Example OpenTelemetry-compliant `.ready.json` written by `azure-typespec-author`:

```json
{
  "schemaUrl": "urn:azsdk:telemetry:azure-typespec-author:1.0.0",
  "name": "skill.result",
  "kind": "INTERNAL",
  "startTimeUtc": "2026-11-02T06:59:45Z",
  "endTimeUtc": "2026-11-02T07:00:00Z",
  "status": { "code": "Ok" },
  "attributes": {
    "event.id": "38498015-0233-48a5-b672-14d6b3f94174",
    "skill.name": "azure-typespec-author",
    "skill.version": "0.1.0",
    "skill.invocation_id": "4fd2c4ac-6c77-4639-a89a-ecb7ecae8320",
    "typespec.updated_file_count": 1,
    "typespec.added_line_count": 12,
    "typespec.deleted_line_count": 3
  },
  "events": [
    {
      "name": "typespec.file_updated",
      "timestampUtc": "2026-11-02T07:00:00Z",
      "attributes": {
        "file.path": "specification/contoso/resource-manager/Microsoft.Contoso/main.tsp",
        "file.change_type": "modified",
        "file.added_lines": 12,
        "file.deleted_lines": 3,
        "file.old_ranges": ["42:3"],
        "file.new_ranges": ["42:12"]
      }
    }
  ]
}
```

- **Correlation & deduplication**: `skill.invocation_id` groups Spans from one skill run; `event.id` is a fixed UUID preserved across retries (mapped to `EventId`) so KQL queries can deduplicate; OpenTelemetry `operation_Id` / `operation_ParentId` joins `dependencies` and `traces`.
- **Privacy rules**: Never collect user prompts, source code, free-text findings, or local absolute paths. Only allow repo-relative paths, respect `AZSDKTOOLS_COLLECT_TELEMETRY=false`, and never echo raw event payloads in CLI wrapper telemetry.

### 2. Local Queue and Hook Delivery

1. **Write ready file**: When telemetry is enabled, the skill writes `<eventId>.writing` and renames it to `<git-dir>/azsdk-skill-telemetry/<eventId>.ready.json` (using `git rev-parse --git-dir`, e.g., `azure-rest-api-specs\.git\azsdk-skill-telemetry\`, keeping `git status` clean and worktrees isolated).
2. **Claim & validate**: On `PostToolUse`, the common hook checks opt-out, renames `.ready.json` to `.processing`, and drops oversized, expired, or schema-invalid files.
3. **Export & fail-open**: The hook runs `azsdk ingest-telemetry --client-type <client> --session-id <id> --event-file <file>`, which validates the schema and exports the OpenTelemetry `Activity` and `ActivityEvent`s to Application Insights. Failures never block user work; transient send errors retry within a small cap, and opting out deletes queued files.

### 3. Changes to Existing Framework Code

No changes are needed in `TelemetryRegistration.cs` or the `Azure.Monitor.OpenTelemetry.Exporter` pipeline. Only the following existing files in `Azure/azure-sdk-tools` need targeted updates:

| Existing file | Current behavior | Required change |
| --- | --- | --- |
| [`eng/common/scripts/azsdk_tool_telemetry.ps1`](https://github.com/Azure/azure-sdk-tools/blob/main/eng/common/scripts/azsdk_tool_telemetry.ps1) | Returns early for `run_in_terminal`, `apply_patch`, and `azure-sdk-mcp` tools; only emits fire-and-forget `skill_invocation` events | Before the tool-ignore early return (guarded by a reentrancy env var), sweep `<git-dir>/azsdk-skill-telemetry/*.ready.json`, claim to `.processing`, invoke `azsdk ingest-telemetry --event-file`, delete on exit `0` (or retry/expire within bounds), and purge queued files when `AZSDKTOOLS_COLLECT_TELEMETRY=false` |
| [`TelemetryIngestionTool.cs`](https://github.com/Azure/azure-sdk-tools/blob/main/tools/azsdk-cli/Azure.Sdk.Tools.Cli/Tools/Core/TelemetryIngestionTool.cs) & [`TelemetryConstants.cs`](https://github.com/Azure/azure-sdk-tools/blob/main/tools/azsdk-cli/Azure.Sdk.Tools.Cli/Telemetry/TelemetryConstants.cs) | Requires `--event-type` and only accepts flat CLI flags (`--skill-name`, `--body`) to create a basic `Activity` with a newly generated `EventId` | Add `--event-file <path>` (keeping existing `--event-type` flags unchanged), validate `schemaUrl` and OpenTelemetry attribute types, and map Span `name`, start/end times, `status`, `attributes` (`SetTag`), and `events` (`AddEvent`) onto `Activity`—preserving `event.id` as `EventId` and populating `SkillName` for existing KQL queries |
| [`TelemetryProcessor.cs`](https://github.com/Azure/azure-sdk-tools/blob/main/tools/azsdk-cli/Azure.Sdk.Tools.Cli/Telemetry/TelemetryProcessor.cs) & [`TelemetryPathSanitizer.cs`](https://github.com/Azure/azure-sdk-tools/blob/main/tools/azsdk-cli/Azure.Sdk.Tools.Cli/Telemetry/TelemetryPathSanitizer.cs) | `SanitizeTags` only sanitizes scalar `string` tags on `Activity.TagObjects`, and allowlists `specification`, `sdk`, `azure-rest-api-specs`, `github.com`, `@azure*` | Extend `TelemetryProcessor` to also sanitize `string[]` attributes and `Activity.Events` (`ActivityEvent.Tags`), and allowlist any additional approved repo-relative roots needed by skills (e.g., `eng`, `tools`, `.github`) |
| [`MCPToolBase.cs`](https://github.com/Azure/azure-sdk-tools/blob/main/tools/azsdk-cli/Azure.Sdk.Tools.Cli/Tools/Core/MCPToolBase.cs) & [`TelemetryIngestionResponse.cs`](https://github.com/Azure/azure-sdk-tools/blob/main/tools/azsdk-cli/Azure.Sdk.Tools.Cli/Models/Responses/TelemetryIngestionResponse.cs) | Wraps every CLI command in a `CommandExecuted` `Activity` recording `CommandArgs` and serialized `CommandResponse` | Keep `TelemetryIngestionResponse` summary-only (event name, skill name, status) so `CommandExecuted` never duplicates or leaks raw `--event-file` contents |

## What Skill Owners Do to Add Customized Telemetry

- **If a skill only needs invocation counts**: **Do nothing.** The existing `PostToolUse` hook already records `skill_invocation` automatically.
- **If a skill needs customized telemetry**: Skill owners do **not** write hooks, retry logic, or OpenTelemetry exporter code. They only add three things in their skill:

| Step | What the skill owner does | Example: `azure-typespec-author` |
| --- | --- | --- |
| 1. Define OpenTelemetry schema | Add `telemetry/result.schema.json` (`schemaUrl`, allowed Span `attributes`, and optional Span `events` using OpenTelemetry primitive/array types) | Defines `urn:azsdk:telemetry:azure-typespec-author:1.0.0` with `typespec.*` summary `attributes` and `typespec.file_updated` Span `events` (`file.path`, `file.old_ranges`, `file.new_ranges`) |
| 2. Write `.ready.json` | Add a small collector script (preferred when reading local files/diffs) or JSON-write step that checks opt-out and writes `<eventId>.ready.json` | Script provides `begin` (records pre-edit git state before touching `.tsp` files) and `complete` (diffs final changes and writes the OpenTelemetry Span JSON to `.ready.json`). *(Skills that only summarize final outputs skip `begin`.)* |
| 3. Call from `SKILL.md` | Add a step in `SKILL.md` (or existing script) to run the collector when the skill finishes | `SKILL.md` runs `begin` before editing TypeSpec files and `complete` right after TypeSpec validation |

Once `<eventId>.ready.json` is written, the shared `PostToolUse` hook and `azsdk` CLI automatically validate and export it via OpenTelemetry to Application Insights.

## Alternatives Considered

| Area | Primary: local `.ready.json` queue + shared hook/sender | Alternative: custom post-hook per skill + direct send |
| --- | --- | --- |
| Skill support | Works for both pure-prompt skills and script-backed skills | Easy for scripted skills, awkward for pure-prompt skills |
| Privacy review | One shared validation and OpenTelemetry export point in `azsdk` | Every skill script must implement its own opt-out, path sanitizer, and exporter |
| Reliability | Fast local file write; hook handles retries and deduplication | Network sends inside skill scripts can slow down or fail the skill |
| Skill-owner effort | Only define OpenTelemetry schema + write local `.ready.json` | Maintain hook scripts, client detection, and App Insights upload per skill |

**Recommendation:** Use the local `.ready.json` queue with the shared `PostToolUse` hook and `azsdk` OpenTelemetry sender, keeping only domain metric calculation inside each skill.

## Open Questions

- [ ] **Is the `azsdk-cli` telemetry command (`azsdk ingest-telemetry`) compliant**: Does sending skill telemetry through `azsdk ingest-telemetry` already meet Microsoft security and privacy compliance requirements (e.g., consent/opt-out, secret or personal-data redaction, and Application Insights ingestion protection), or are additional reviews and safeguards needed?
- [ ] **Does telemetry have to go through MCP (`azsdk`)**: Should telemetry always be sent through the `azsdk` MCP CLI (`azsdk ingest-telemetry`), or can the shared hook script export the OpenTelemetry `.ready.json` Span directly to Application Insights / OTLP when `azsdk` is not installed?
- [ ] **How to detect `copilot-app`**: Today `azsdk_tool_telemetry.ps1` only checks whether `tool_use_id` contains `_vscode-` and labels everything else `copilot-cli`. What hook field or environment variable identifies `copilot-app`?
- [ ] **Where to store schemas across repos**: `azsdk` CLI lives in `azure-sdk-tools`, while some skills (like `azure-typespec-author`) live in `azure-rest-api-specs`. Should `azsdk` keep approved schemas in `azure-sdk-tools`, or read `telemetry/result.schema.json` from the skill folder?
- [ ] **Are repo-relative paths allowed in private repos**: Can repo-relative paths (e.g., `specification/contoso/.../main.tsp`) always be sent as plain text, or do private repos need path hashing before public release?

