# Spec: 8-Operations - Codeowners Ownership Audit

## Table of Contents

- [Overview](#overview)
- [Definitions](#definitions)
- [Background / Problem Statement](#background--problem-statement)
- [Goals and Exceptions/Limitations](#goals-and-exceptionslimitations)
- [Design Proposal](#design-proposal)
- [Open Questions](#open-questions)
- [Success Criteria](#success-criteria)
- [Agent Prompts](#agent-prompts)
- [CLI Commands](#cli-commands)
- [Implementation Plan](#implementation-plan)
- [Testing Strategy](#testing-strategy)
- [Documentation Updates](#documentation-updates)
- [Metrics/Telemetry](#metricstelemetry)

---

## Overview

This document records the **current implemented ownership-audit model** for `azsdk-cli`.
It is a companion to
[`8-operations-codeowners-management.spec.md`](./8-operations-codeowners-management.spec.md),
which covers the broader CODEOWNERS management surface.

The legacy `CodeownersLinter` validates the **rendered CODEOWNERS file**. The new
`config codeowners audit` flow validates the **Azure DevOps work items** that now serve as the
source of truth. Some legacy linter failures are now prevented structurally by the generator,
while others are replaced by explicit audit rules.

---

## Definitions

- **Legacy linter**: The CODEOWNERS validation logic in `tools/codeowners-utils/`, primarily in
  `Owners.cs`, `Labels.cs`, `CodeownersLinter.cs`, and `DirectoryUtils.cs`.
- **Ownership audit**: The `azsdk config codeowners audit` command implemented by
  `CodeownersTool`, `CodeownersAuditHelper`, and the audit rules under
  `Helpers/Codeowners/Rules/`.
- **Generator**: `CodeownersGenerateHelper`, which projects Azure DevOps ownership data into a
  rendered `.github/CODEOWNERS` section.
- **Structurally prevented**: A legacy linter violation that the current generator does not emit
  as invalid rendered syntax, even if the underlying Azure DevOps data is incomplete.

---

## Background / Problem Statement

Ownership data has moved away from direct manual editing of CODEOWNERS and into Azure DevOps work
items. That changed the validation problem:

1. The legacy linter still validates the rendered CODEOWNERS artifact.
2. The new audit validates the work-item graph before or alongside generation.
3. The generator itself now suppresses some invalid rendered shapes instead of emitting malformed
   CODEOWNERS text.

As a result, the old linter rule set no longer maps 1:1 to the new implementation. This document
explains which linter rules are:

- replaced by implemented audit rules,
- prevented structurally by the generator, or
- still outside the implemented audit scope.

---

## Goals and Exceptions/Limitations

### Goals

- Document the implemented audit rule set.
- Map each legacy linter rule to its current status under the Azure DevOps-backed workflow.
- Clarify where the generator now enforces structure implicitly.

### Exceptions and Limitations

- This document describes the **implemented** rule set only. It does not re-open deferred rule
  proposals from earlier planning artifacts.
- `AUD-LBL-002` remains **report only**. No automated fix is implemented for Service Attention
  misuse in this project.
- MSFT identity population and identity-resolution integration are **out of scope** for this
  project.
- Repo-file-system path validation rules remain outside the current audit implementation.

---

## Design Proposal

### Design Overview

The current ownership validation model has three layers:

1. **Audit** validates Azure DevOps work items.
2. **Generate** renders Azure DevOps work items into CODEOWNERS.
3. **check-package** validates rendered CODEOWNERS cache content for package-level gate checks.

The implemented audit rule set is:

| Rule ID | Description | Fix Behavior |
|---------|-------------|--------------|
| `AUD-OWN-001` | Individual owner fails GitHub validation | Set or clear `Custom.InvalidSince` |
| `AUD-OWN-002` | Team alias does not match `Azure/<team>` format | Report only |
| `AUD-OWN-003` | Team does not descend from `azure-sdk-write` | Set or clear `Custom.InvalidSince` |
| `AUD-LBL-001` | Label work item does not exist as a GitHub repo label | Report only |
| `AUD-LBL-002` | Service Attention misused as PR label or sole service label | Report only |
| `AUD-STR-001` | Label Owner has zero owner relations | Delete the Label Owner work item |
| `AUD-STR-002` | Label Owner has zero label relations | Report only |

### Legacy Linter Rule Mapping

The legacy linter contains 17 rules. The table below records how each rule maps to the
implemented Azure DevOps-backed model.

| Legacy Rule | Legacy Meaning | New Generator Outcome | Implemented Auditor Mapping |
|-------------|----------------|------------------------|-----------------------------|
| `OWN-001` | Source path line must have owners | The generator does not emit a source line with zero owners; broken data disappears instead of rendering an empty owner line | No 1:1 implemented replacement. `AUD-STR-001` covers zero-owner Label Owners; package-level owner minimum work is separate |
| `OWN-002` | Team owner must descend from `azure-sdk-write` | Still renderable if bad data exists | `AUD-OWN-003` |
| `OWN-003` | Individual user must satisfy Azure org / write validation | Still renderable if bad data exists | `AUD-OWN-001` |
| `OWN-004` | Team alias must match `Azure/<team>` | Still renderable if bad data exists | `AUD-OWN-002` |
| `OWN-005` | Individual user must be valid / resolvable | Still renderable if bad data exists | `AUD-OWN-001` |
| `LBL-001` | `PRLabel` / `ServiceLabel` monikers must contain at least one label | The generator does not emit empty label comments | No 1:1 implemented replacement. `AUD-STR-002` covers zero-label Label Owners; package PR-label minimum checks remain separate |
| `LBL-002` | `PRLabel` must not include `Service Attention` | Still renderable if bad data exists | `AUD-LBL-002` |
| `LBL-003` | `ServiceLabel` cannot be only `Service Attention` | Still renderable if bad data exists | `AUD-LBL-002` |
| `LBL-004` | Referenced label must exist in the GitHub repo | Still renderable if bad data exists | `AUD-LBL-001` |
| `PATH-001` | Path must exist in the repo | The generator does not validate repo contents | No implemented replacement; remains out of scope because it requires repo file access |
| `PATH-002` | Glob syntax must be valid | Stored `RepoPath` values flow through generation unchanged | No implemented replacement in the current audit; path validation is separate follow-up work |
| `PATH-003` | Glob must match repo files | The generator does not validate repo contents | No implemented replacement; remains out of scope because it requires repo file access |
| `BLK-001` | Duplicate moniker within a block | Structurally prevented by the ADO-backed data model and generator formatting | No auditor rule needed |
| `BLK-002` | `AzureSdkOwners` requires `ServiceLabel` | Can still arise from zero-label `Azure SDK Owner` data linked into generation | `AUD-STR-002` |
| `BLK-003` | `ServiceOwners` requires `ServiceLabel` | Standalone invalid `ServiceOwners` blocks are not emitted by the generator | `AUD-STR-002` is the underlying data-level check for zero-label `Service Owner` records |
| `BLK-004` | `PRLabel` block must end with a source path line | Structurally prevented; the generator only emits `PRLabel` as part of source-path entries | No auditor rule needed |
| `BLK-005` | `ServiceLabel` block must have exactly one valid owner source | Incomplete pathless blocks are suppressed; missing owner/label relations are represented in data instead | `AUD-STR-001` / `AUD-STR-002` |

### Generator Interaction

The current generator changes the failure model in a few important ways:

1. `generate` accepts `--invalid-owner-lookback-days` (default `90`).
2. Owners whose `InvalidSince` is older than the cutoff are excluded from generated output.
3. Some malformed rendered CODEOWNERS shapes are never emitted:
   - empty source-owner lines,
   - empty label comments,
   - orphaned pathless metadata blocks,
   - PR-label blocks without a terminating source path line.

That means some legacy linter rules are now better understood as **generator invariants** rather
than audit rules.

### Rule Family Summary

The rule families now break down as follows:

| Family | Current Owner of Responsibility |
|--------|---------------------------------|
| Owner validity | `AUD-OWN-*` |
| Label validity | `AUD-LBL-*` |
| Structural work-item integrity | `AUD-STR-*` |
| Repo-path existence / glob matching | Still handled only by file-system-aware validation |
| Rendered package gate checks | `check-package` against CODEOWNERS cache |

---

## Open Questions

No new open design questions are introduced by this document.

Known follow-up areas remain separate from this project:

- repo-path validation (`PATH-*` style checks),
- additional structural package rules,
- MSFT identity population / identity-resolution integration.

---

## Success Criteria

This document is successful when:

- every legacy linter rule is accounted for,
- every implemented audit rule is listed with its current fix behavior,
- the document clearly distinguishes:
  - structural prevention by the generator,
  - implemented audit replacement, and
  - current out-of-scope gaps.

---

## Agent Prompts

Example prompts this document is intended to support:

- “Explain how the old CODEOWNERS linter rules map to the new ownership audit.”
- “Which legacy linter checks are now enforced by the audit versus structurally prevented by generation?”
- “What does `azsdk config codeowners audit --fix` actually repair today?”

---

## CLI Commands

Current ownership-audit command surface:

```bash
azsdk config codeowners audit [--fix] [--force] [--repo Azure/<repo>]
```

Supporting generation behavior:

```bash
azsdk config codeowners generate --invalid-owner-lookback-days 90
```

Notes:

- `--fix` only applies to rules that currently support automated repair.
- `AUD-LBL-002` is intentionally report-only.
- `check-package` remains a separate rendered-output validation flow.

---

## Implementation Plan

This document reflects the **implemented** ownership-audit state rather than proposing a new
feature rollout.

Current implementation lives in:

- `Azure.Sdk.Tools.Cli/Tools/Config/CodeownersTool.cs`
- `Azure.Sdk.Tools.Cli/Helpers/Codeowners/CodeownersAuditHelper.cs`
- `Azure.Sdk.Tools.Cli/Helpers/Codeowners/CodeownersGenerateHelper.cs`
- `Azure.Sdk.Tools.Cli/Helpers/Codeowners/Rules/*.cs`

No new implementation work is introduced by this documentation change.

---

## Testing Strategy

Current implementation coverage relevant to this document includes:

- audit rule coverage in `AuditRuleTests.cs`,
- audit harness coverage in `AuditRuleTests.cs`,
- generator lookback coverage in `CodeownersGenerateHelperTests.cs`.

Notably:

- the generator has test coverage for invalid-owner lookback filtering,
- CLI-level audit/generate integration coverage was not added in this phase by design,
- Service Attention misuse remains documented as report-only to match implementation.

---

## Documentation Updates

This document complements rather than replaces:

- [`8-operations-codeowners-management.spec.md`](./8-operations-codeowners-management.spec.md)

Recommended usage:

- Use the management spec for the broader CODEOWNERS management surface.
- Use this document when discussing the ownership-audit rule model and legacy-linter mapping.

---

## Metrics/Telemetry

No audit-specific telemetry changes are introduced by this document.

The current implementation relies on command output and logger diagnostics rather than dedicated
ownership-audit telemetry.
