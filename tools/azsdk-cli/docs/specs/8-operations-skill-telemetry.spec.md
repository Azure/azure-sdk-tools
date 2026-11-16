<!-- cspell:words azsdk AZSDKTOOLS contoso reentrancy revalidate typespec worktree worktrees -->

# Spec: 8-Operations - Common Skill Telemetry

## Table of Contents

- [Background / Problem Statement](#background--problem-statement)
- [Design Proposal](#design-proposal)
- [TypeSpec Consumers](#typespec-consumers)
- [Alternatives Considered](#alternatives-considered)
- [Open Questions](#open-questions)

## Background / Problem Statement

Existing [`azsdk_tool_telemetry.ps1`](https://github.com/Azure/azure-sdk-tools/blob/main/eng/common/scripts/azsdk_tool_telemetry.ps1) and [`TelemetryIngestionTool.cs`](https://github.com/Azure/azure-sdk-tools/blob/main/tools/azsdk-cli/Azure.Sdk.Tools.Cli/Tools/Core/TelemetryIngestionTool.cs) record skill loads, but do not support typed skill-outcome payloads or distinguish verified `copilot-app` sessions from `copilot-cli`.

Companion proposal [#16828](https://github.com/Azure/azure-sdk-tools/pull/16828) defines general SDK Agent lifecycle hooks, user consent, and Application Insights storage. Stateless hooks work for tool/skill call counts, but cannot reconstruct multi-step edit windows or validated skill artifacts on their own.

[#17228](https://github.com/Azure/azure-sdk-tools/issues/17228) requires reusable skill outcome telemetry—covering both pure-prompt and scripted skills across SDK languages and data/management planes—starting with `azure-typespec-author` and `azure-typespec-assessment`, without collecting prompts or file contents.

## Design Proposal

### Architecture

![Common skill telemetry architecture](8-operations-skill-telemetry-architecture.svg)

| Component | Responsibility |
| --- | --- |
| Skill contract | Versioned JSON schema (`telemetry/result.schema.json`) plus rules for allowed sources, scope, and when an event is valid |
| Producer | Agent following skill instructions or a deterministic collector script; checks opt-out and writes a complete local event (no direct upload) |
| Local ready queue | Untracked per-worktree directory (`<git-dir>/azsdk-skill-telemetry/`) holding atomic `<eventId>.ready.json` files |
| Common `PostToolUse` hook | Deterministic script that checks consent, claims ready files, validates schema/paths, and invokes the shared CLI sender |
| Shared `azsdk` sender | Revalidates against pinned schemas, redacts wrapper args, preserves `eventId` for deduplication, and flushes to Application Insights |

### 1. Contract, Envelope, and Privacy

Each participating skill declares a pinned schema (`schemaId` + `schemaVersion`) covering both common fields and skill-specific `data`. Unknown schemas, unapproved versions, or extra properties are rejected. Prefer deterministic scripts over model-authored counts whenever measurable artifacts exist.

Illustrative `skill.result` event:

```json
{
  "eventId": "38498015-0233-48a5-b672-14d6b3f94174",
  "invocationId": "4fd2c4ac-6c77-4639-a89a-ecb7ecae8320",
  "skill": { "name": "azure-typespec-assessment", "version": "0.0.0-beta" },
  "eventName": "skill.result",
  "schemaId": "azure-typespec-assessment.result",
  "schemaVersion": 1,
  "occurredAtUtc": "2026-11-02T07:00:00Z",
  "hostContext": { "invocationSurface": "copilot-app", "verification": "host-adapter" },
  "data": {
    "specificationPath": "specification/contoso/resource-manager/Microsoft.Contoso",
    "mode": "fast",
    "status": "succeeded",
    "dimensions": {
      "semantic": { "status": "skipped", "findingCount": null },
      "rest": { "status": "passed", "findingCount": 0 },
      "downstream": { "status": "failed", "findingCount": 1 },
      "azureGuidelines": { "status": "skipped", "findingCount": null },
      "documentQuality": { "status": "passed", "findingCount": 0 }
    }
  }
}
```

- **Identifiers and surface**: `invocationId` groups events from one skill run; `eventId` is an immutable random UUID preserved across retries for KQL deduplication. Host adapters verify `invocationSurface` (`vscode`, `copilot-cli`, `copilot-app`, or `unknown`).
- **Privacy boundary**: Never collect prompts, source code, free-text findings, absolute paths, or raw tool arguments. Disable `UserPromptSubmit` prompt collection where required, enforce repository-relative path allowlists, honor `AZSDKTOOLS_COLLECT_TELEMETRY=false` and host consent ([#16828](https://github.com/Azure/azure-sdk-tools/pull/16828)), and redact `--event-file` from `CommandExecuted` wrapper telemetry.

### 2. Atomic Queue, Hook Drain, and Failure Semantics

1. **Publish**: When telemetry is enabled, the producer writes `<eventId>.writing`, closes it, and atomically renames it to `<git-dir>/azsdk-skill-telemetry/<eventId>.ready.json` (resolved via `git rev-parse --git-dir` for worktree isolation; private OS app-data is an alternative outside Git).
2. **Claim and validate**: On `PostToolUse`, the common hook checks consent and a reentrancy guard, sweeps bounded ready files (e.g., <= 32 KiB/event, 24h TTL, <= 3 attempts), atomically renames `.ready.json` to `.processing`, and rejects symlinks, traversal, or invalid schemas.
3. **Export**: The hook calls `azsdk ingest-telemetry --client-type <verified-surface> --event-file <claimed-file>` (never repurposing `--body`). The CLI revalidates the pinned schema and exports approved tags to Application Insights (`dependencies.customDimensions`).
4. **Fail-open**: Telemetry never blocks user work. Invalid payloads are dropped with a safe reason code; transient send failures retry on later hooks within retry/TTL bounds; opt-out skips collection and deletes queued files without replay.

## TypeSpec Consumers

Canonical TypeSpec skills live in `Azure/azure-rest-api-specs` and sync downstream to `Azure/azure-sdk-tools`.

- **`azure-typespec-author`**: Uses `begin` and `complete` collector steps around its edit window. `begin` snapshots pre-edit local state (including dirty/staged/untracked files, never `HEAD` alone); `complete` diffs the final working tree to emit repository-relative `updatedFiles` (`path`, `changeType` in `modified|created|deleted|renamed`, `addedLineCount`, `deletedLineCount`, and unambiguous `{ old: { startLine, lineCount }, new: { startLine, lineCount } }` `changedRanges`) plus `totals`. Oversized file lists split into bounded `skill.detail` events rather than truncating.
- **`azure-typespec-assessment`**: Runs inside guarded `assessment.json` finalization and deterministically extracts `specificationPath`, `mode` (`full` or `fast`), and `semantic`, `rest`, `downstream`, `azureGuidelines`, and `documentQuality`. Skipped/unassessed dimensions emit `null` counts (for example, `semantic` and `azureGuidelines` in `fast` mode), `0` means assessed with zero items, and semantic change intents remain distinct from defects.

## Alternatives Considered

| Area | Primary: ready-event queue + common hook/sender | Alternative: per-skill post-hook + direct emission |
| --- | --- | --- |
| Skill coverage | Works for both pure-prompt skills and scripted collectors | Fits scripted skills, awkward for pure-prompt skills without custom runners |
| Privacy & governance | Single reviewed export boundary, consent check, and schema registry | Every skill script must duplicate consent, allowlist, and destination enforcement |
| Reliability | Atomic local handoff with bounded retries and deduplication | Direct network sends risk hook timeouts or lost events without per-skill buffering |
| Maintenance | Shared transport; low marginal cost to onboard new skills | Repeated cross-host hook dispatch, testing, and exporter maintenance per skill |

**Recommendation:** Use the local ready-event queue with the common `PostToolUse` hook and `azsdk` sender by default; keep only metric calculation inside skill-specific collectors.

## Open Questions

- [ ] Which host versions support `PostToolUse` reliably and expose verified `copilot-app` attribution?
- [ ] Should non-Git workspaces fall back to a private OS app-data queue alongside `<git-dir>/azsdk-skill-telemetry/`?
- [ ] Does privacy review allow plain repository-relative `specification/...` paths, or require partial hashing/redaction?
- [ ] Should `semantic.findingCount` be renamed to `intentCount` to avoid conflating change intents with defects?

