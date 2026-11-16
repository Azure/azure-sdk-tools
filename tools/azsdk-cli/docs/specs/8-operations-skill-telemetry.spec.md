<!-- cspell:words allowlist allowlists azsdk AZSDKTOOLS contoso deduplicate deduplication finalizer reentrancy revalidate typespec untracked worktree worktrees -->

# Spec: 8-Operations - Common Skill Telemetry

## Table of Contents

- [Existing Skill Telemetry and Gaps](#existing-skill-telemetry-and-gaps)
- [Design Proposal](#design-proposal)
- [What Skill Owners Do to Add Customized Telemetry](#what-skill-owners-do-to-add-customized-telemetry)
- [Alternatives Considered](#alternatives-considered)
- [Open Questions](#open-questions)

## Existing Skill Telemetry and Gaps

Today, [`azsdk_tool_telemetry.ps1`](https://github.com/Azure/azure-sdk-tools/blob/main/eng/common/scripts/azsdk_tool_telemetry.ps1) runs on `PostToolUse` and calls [`azsdk ingest-telemetry`](https://github.com/Azure/azure-sdk-tools/blob/main/tools/azsdk-cli/Azure.Sdk.Tools.Cli/Tools/Core/TelemetryIngestionTool.cs) whenever the agent invokes the `skill` tool or reads `/skills/<name>/SKILL.md`:

| Category | Already recorded automatically (zero skill-owner work) | Not supported today (gap) |
| --- | --- | --- |
| Skill identity & timing | `event_type = skill_invocation`, `skill_name`, and activity timestamp in Application Insights `dependencies` | Whether the skill actually finished, succeeded, failed, or was only read without executing |
| Host & session metadata | `client_name` (`vscode` if `tool_use_id` contains `_vscode-`, otherwise `copilot-cli`), optional `session_id`, plus CLI `azsdk_tool_version`, `dev_device_id`, `mac_address_hash`, and `event_id` | Verified `copilot-app` surface attribution (currently falls back to `copilot-cli`) and stable per-event retry deduplication |
| Domain / outcome metrics | Separate MCP tool calls emit their own tool-level telemetry | **Skill-specific customized telemetry** (e.g., [#17228](https://github.com/Azure/azure-sdk-tools/issues/17228): which `.tsp` files and line ranges changed, artifact finding counts, or workflow outcomes) without collecting raw prompts |

Because Azure SDK has many pure-prompt and script-backed skills across languages and planes, this spec extends the existing hook and Application Insights pipeline so any skill needing **customized outcome telemetry** can emit validated structured data—using `azure-typespec-author` throughout as the running example.

## Design Proposal

### Architecture

![Common skill telemetry architecture](8-operations-skill-telemetry-architecture.svg)

| Component | Owner | Responsibility |
| --- | --- | --- |
| Skill contract & producer | Skill owner | Define a versioned JSON schema (`telemetry/result.schema.json`) and produce a local `.ready.json` event when enabled (no direct upload) |
| Local ready queue | Shared helper | Untracked per-worktree directory (`<git-dir>/azsdk-skill-telemetry/`) storing atomic `<eventId>.ready.json` files |
| Common `PostToolUse` hook | Shared framework | Check consent, atomically claim ready files, validate schema and path rules, and call the shared CLI sender |
| Shared `azsdk` sender | Shared framework | Revalidate against pinned schemas, redact wrapper args, preserve `eventId` for deduplication, and flush to Application Insights |

### 1. Event Envelope and Privacy

Each participating skill declares a pinned schema (`schemaId` + `schemaVersion`) covering common fields and skill-specific `data`. Unknown schemas, unapproved versions, or extra properties are rejected.

Illustrative `skill.result` event (using `azure-typespec-author` as the example):

```json
{
  "eventId": "38498015-0233-48a5-b672-14d6b3f94174",
  "invocationId": "4fd2c4ac-6c77-4639-a89a-ecb7ecae8320",
  "skill": { "name": "azure-typespec-author", "version": "0.1.0" },
  "eventName": "skill.result",
  "schemaId": "azure-typespec-author.result",
  "schemaVersion": 1,
  "occurredAtUtc": "2026-11-02T07:00:00Z",
  "hostContext": { "invocationSurface": "vscode", "verification": "host-adapter" },
  "data": {
    "status": "succeeded",
    "updatedFiles": [
      {
        "path": "specification/contoso/resource-manager/Microsoft.Contoso/main.tsp",
        "changeType": "modified",
        "addedLineCount": 12,
        "deletedLineCount": 3,
        "changedRanges": [
          { "old": { "startLine": 42, "lineCount": 3 }, "new": { "startLine": 42, "lineCount": 12 } }
        ]
      }
    ],
    "totals": { "updatedFileCount": 1, "addedLineCount": 12, "deletedLineCount": 3 }
  }
}
```

- **Identifiers & surface**: `invocationId` groups events from one skill run; `eventId` is an immutable UUID reused across retries for KQL deduplication. Host adapters verify `invocationSurface` (`vscode`, `copilot-cli`, `copilot-app`, or `unknown`).
- **Privacy boundary**: Never collect prompts, source code, free-text findings, absolute paths, or raw tool arguments. Disable `UserPromptSubmit` prompt collection where required, enforce repository-relative path allowlists, honor `AZSDKTOOLS_COLLECT_TELEMETRY=false` and host telemetry consent, and redact `--event-file` from `CommandExecuted` wrapper telemetry.

### 2. Shared Queue and Hook Delivery

1. **Publish**: When telemetry is enabled, the producer writes `<eventId>.writing`, closes it, and atomically renames it to `<git-dir>/azsdk-skill-telemetry/<eventId>.ready.json` (resolved via `git rev-parse --git-dir` for worktree isolation, e.g., `azure-rest-api-specs\.git\azsdk-skill-telemetry\`; private OS app-data is an alternative outside Git).
2. **Claim & validate**: On `PostToolUse`, the common hook checks consent and a reentrancy guard, sweeps bounded ready files (e.g., <= 32 KiB/event, 24h TTL, <= 3 attempts), atomically renames `.ready.json` to `.processing`, and rejects symlinks, traversal, or invalid schemas.
3. **Export & fail-open**: The hook invokes `azsdk ingest-telemetry --client-type <verified-surface> --event-file <claimed-file>`, which revalidates the pinned schema and exports approved tags to Application Insights (`dependencies.customDimensions`). Errors never block user work; transient failures retry within bounds, and opt-out purges queued files without replay.

## What Skill Owners Do to Add Customized Telemetry

- **If a skill only needs invocation counts**: **No action is needed.** The existing `PostToolUse` hook already records `skill_invocation` automatically.
- **If a skill needs customized outcome telemetry**: Skill owners never write hook registration, retry, or Application Insights upload code. They only implement three skill-local pieces:

| Step | What the skill owner provides | Example: `azure-typespec-author` |
| --- | --- | --- |
| 1. Define & register schema | Add `telemetry/result.schema.json` (`schemaId` + `schemaVersion`, typed `data` fields, value/path bounds) and register it for privacy/CLI validation | Defines `azure-typespec-author.result` v1 allowing repository-relative `.tsp` `updatedFiles` (`modified\|created\|deleted\|renamed`), hunk `changedRanges`, and `totals` (splitting oversized lists into `skill.detail` events) |
| 2. Compute & publish `.ready.json` | Provide a deterministic collector script (preferred when local files/artifacts exist) or a bounded JSON-write instruction that checks opt-out and atomically publishes `<eventId>.ready.json` | Provides a collector with `begin` (snapshots dirty/staged/untracked pre-edit state rather than assuming `HEAD`) and `complete` (diffs final working tree and writes `<git-dir>/azsdk-skill-telemetry/<eventId>.ready.json`). *(Single-step or artifact-producing skills skip `begin` and only publish at completion.)* |
| 3. Trigger from `SKILL.md` | Add a brief step in `SKILL.md` (or call from an existing finalizer script) at the exact boundary where the outcome is valid | `SKILL.md` runs `begin` once the target specification directory is selected, and runs `complete` right after TypeSpec validation finishes |

Once `<eventId>.ready.json` is written, the shared `PostToolUse` hook and `azsdk` sender automatically claim, validate, deduplicate, and export it to Application Insights.

## Alternatives Considered

| Area | Primary: ready-event queue + common hook/sender | Alternative: per-skill post-hook + direct emission |
| --- | --- | --- |
| Skill coverage | Works across pure-prompt, multi-edit, and scripted skills | Fits scripted skills, awkward for pure-prompt skills without custom runners |
| Privacy & governance | Single reviewed export boundary, consent check, and schema registry | Every skill script must duplicate consent, allowlist, and destination enforcement |
| Reliability | Atomic local handoff with bounded retries and deduplication | Direct network sends risk hook timeouts or lost events without per-skill buffering |
| Skill-owner effort | Define schema + write local `.ready.json` only | Own cross-host hook dispatch, retries, and exporter integration per skill |

**Recommendation:** Use the local ready-event queue with the common `PostToolUse` hook and `azsdk` sender by default; keep only domain metric calculation inside each skill.

## Open Questions

- [ ] Which host versions support `PostToolUse` reliably and expose verified `copilot-app` attribution?
- [ ] Should non-Git workspaces fall back to a private OS app-data queue alongside `<git-dir>/azsdk-skill-telemetry/`?
- [ ] Does privacy review allow plain repository-relative paths (e.g., `specification/...` or SDK package paths), or require partial hashing/redaction?
- [ ] How should pinned skill schemas be versioned and synced when canonical skills live in external repositories (such as `Azure/azure-rest-api-specs`)?

