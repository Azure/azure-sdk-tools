# Plan: Codeowners Audit Command for azsdk-cli

## Problem Statement

The Azure SDK codeowners management system has migrated from hand-edited CODEOWNERS files to
Azure DevOps work items as the source of truth (`config codeowners` commands in azsdk-cli).
The old `CodeownersLinter` (in `tools/codeowners-utils/`) validates the rendered CODEOWNERS
file, but there is no equivalent auditing of the ADO work item data itself. Invalid owners
accumulate over time, and structural issues in the work item graph can cause the generated
CODEOWNERS to fail the linter.

## Approach

Add a `config codeowners audit` command to azsdk-cli that:

1. Fetches all Owner, Label, Label Owner, and Package work items from ADO.
2. Runs a configurable set of audit rules against them.
3. Reports violations in a structured format.
4. With `--fix`, applies automated fixes where safe (Invalid Since field updates and orphan
   cleanup).

## Reference Artifacts

- **`rules/`**: Summarizes all 17 rules from the existing linter. Maps each to ADO audit
  applicability.
- **`plans/`**: Detailed plans for each of the 12 new audit rules, including criteria, fix
  behavior, and dependencies.

---

## Linter Rules Not In Audit

Three linter rules were excluded from the audit because they require repo file access, and one
is structurally impossible:

| Excluded Rule | Why Excluded |
|---------------|-------------|
| PATH-001 (path must exist in repo) | Requires repo file access. Out of scope. |
| PATH-002 (glob syntax valid) | Deferred. Legacy `Custom.RepoPath` values may contain invalid glob syntax that flows into generated CODEOWNERS unchecked. New writes are validated by `add-label-owner --path` input validation, but existing data is not audited. A report-only syntax check (no repo access needed) could be added later. |
| PATH-003 (glob must match repo files) | Requires repo file access. Out of scope. |
| BLK-001 (duplicate moniker) | Structurally prevented by the ADO data model — cannot occur. |

### Generator's Defensive Behavior

The generator silently drops entries that would produce invalid CODEOWNERS blocks:

1. **Zero owners on a path entry**: `FormatCodeownersEntry` requires `sourceOwners.Count > 0` to emit a path block. If a Package has zero owners, it produces no output — the linter never sees it, but the package is missing from CODEOWNERS entirely.
2. **Label Owner with zero owners**: Service-level entries with `RepoPath` skip entries where `sourceOwners.Count == 0` (line 247-250 in `CodeownersGenerateHelper`).
3. **Pathless entries with zero labels**: Skipped at line 291-293.
4. **ServiceOwners/AzureSdkOwners without ServiceLabel**: The formatter only emits these blocks when `hasServiceLabels` is true — if labels are missing, the block is silently dropped.

This means **most structural linter violations are impossible** from generated output because incomplete data is silently dropped. However, this creates a **coverage gap** — packages/paths disappear from CODEOWNERS without warning. The audit's structure rules (AUD-STR-001 and deferred rules) catch this at the ADO data level before generation.

### Rules That CAN Fail From Generated Output

The linter rules that CAN fail on generated CODEOWNERS are:

| Rule | Scenario | Audit Coverage |
|------|----------|----------------|
| OWN-002 (team must be write team) | Invalid team in Owner WI → appears in generated output | AUD-OWN-003 |
| OWN-003 (user must be public Azure member) | Invalid individual in Owner WI → appears in generated output | AUD-OWN-001 |
| OWN-004 (malformed team entry) | Malformed team alias → appears in generated output | AUD-OWN-002 |
| OWN-005 (invalid user) | Invalid user → appears in generated output | AUD-OWN-001 |
| LBL-003 (ServiceLabel only Service Attention) | Label Owner with only "Service Attention" → emitted | AUD-LBL-002 |
| LBL-004 (repo label must exist) | Label WI references non-existent GitHub label → emitted | AUD-LBL-001 |
| PATH-001 (path must exist) | Label Owner with stale `RepoPath` → emitted without validation | Not covered (requires repo context) |

All of these except PATH-001 are covered by audit rules. PATH-001 (stale repo paths) requires
repo file access and is explicitly out of scope for the audit.

---

## Audit Rules Summary

### Owner Validation

| Rule | Description | Fix? |
|------|-------------|------|
| AUD-OWN-001 | Individual Owner fails GitHub validation (not in Azure/Microsoft orgs, no write access) | Yes — set/clear `Invalid Since` field |
| AUD-OWN-002 | Team alias doesn't match `Azure/<team>` format | Report only |
| AUD-OWN-003 | Team doesn't descend from `azure-sdk-write` | Yes — set/clear `Invalid Since` field |

### Label Validation

| Rule | Description | Fix? |
|------|-------------|------|
| AUD-LBL-001 | Label work item doesn't exist as a GitHub label | Report only |
| AUD-LBL-002 | Service Attention misused as PR label or sole service label | Report only |

### Structure Validation

| Rule | Description | Fix? |
|------|-------------|------|
| AUD-STR-001 | Label Owner has zero Owner relations | Yes — delete |
| AUD-STR-002 | Label Owner has zero Label relations | Report only |

### Deferred Rules

These rules are necessary but deferred to a later phase (e.g., when notifications are added).
See `plans/deferred/` for details.

| Rule | Description | Fix? |
|------|-------------|------|
| AUD-STR-003 | Package has fewer than 2 individual owners | Report only |
| AUD-STR-004 | Package has zero PR labels | Report only |
| AUD-STR-005 | Label Owner left under-minimum after fix | Yes — delete if zero owners |

---

## CLI Design

### Command

```bash
azsdk config codeowners audit [--fix]
```

### Options

| Option | Description |
|--------|-------------|
| `--fix` | Apply automated fixes (set/clear `Invalid Since` on invalid owners, delete orphaned entries). Without this flag, the command reports all violations and what would be fixed — effectively a preview of what `--fix` would do. |
| `--force` | Override safety thresholds. Required when `--fix` would act on more than the allowed number of violations (e.g., AUD-OWN-001 throws if >5 newly invalid owners detected). |
| `--repo` | Optional. Scopes evaluation and fixes of Package and Label Owner work items to a specific repo (e.g., `Azure/azure-sdk-for-net`). Must be of the form `Azure/<repo>`. Packages are filtered by language via `RepoToLanguageString`; Label Owners are filtered by exact `Custom.Repository` match. Owner and Label work items are always audited globally — even in a repo-scoped run, all owner validation and fixes still apply. If `--repo` does not map to a known SDK language repo, the command rejects it with an error. Validated in `CodeownersTool.cs`. |

### Behavior

- **Without `--fix`**: Evaluates all rules, reports violations, and for fixable rules shows
  what the fix action would be. This serves as a preview/dry-run. Exit code 1 if violations found,
  0 if clean.
- **With `--fix`**: Evaluates all rules, applies fixes where possible, reports what was fixed
  and what remains unfixable. Exit code 0 if no violations remain after fixes, 1 if unfixable
  violations remain.

### No MCP tool for now — CLI-only.

---

## Error Handling & Fix Safety

`--fix` makes changes to ADO work items (setting `Invalid Since` field, deleting orphaned
Label Owners). If a transient error occurs (rate limits, network failures, auth errors),
the audit **fails immediately** with a non-zero exit code. No retries. Operations that
succeeded before the failure are kept — the auditor is **not atomic** and can be re-run safely.

Each run re-fetches all work items from ADO and recomputes violations from scratch — there
is no resume state. Re-running after a failure recomputes current server state from ADO and
re-applies only the fixes that are still needed.

### Idempotent Fix Operations

- **Set Invalid Since**: If the field is already set, the rule reports `DoNothing` — no update.
  Setting the same value again would be harmless but is avoided for efficiency.
- **Clear Invalid Since**: If the field is already empty, no violation is raised. If the field
  is set and the owner is now valid, clearing it is idempotent.
- **Work item delete (STR-001)**: No special wrapper. If the delete fails, the exception
  propagates and the audit fails so the issue can be investigated.

### InvalidOwnerRule Fix Logic

The audit uses a **soft-invalidation** approach instead of relation removal:

- **If owner is invalid and `InvalidSince` is not set** → Detail = `SetInvalidDetail` → fix sets `Custom.InvalidSince` to current UTC date/time (ISO 8601)
- **If owner is invalid and `InvalidSince` is already set** → Detail = `DoNothingDetail` → no fix action
- **If owner is valid and `InvalidSince` is set** → Detail = `ClearInvalidDetail` → fix clears the field (empty string)
- **If owner is valid and `InvalidSince` is not set** → no violation reported

TeamNotWriteRule (AUD-OWN-003) follows the identical pattern for team owners.

Both rules have a safety threshold of 5: if >5 newly invalid owners/teams are detected with `--fix`, the rule throws unless `--force` is specified.

### Invalid Owner Lookback (Generate Command)

The `generate` command accepts `--invalid-owner-lookback-days` (default 90). Owners whose
`InvalidSince` date is older than the lookback window are excluded from generated CODEOWNERS
entries via `IsOwnerExpired(owner, cutoff)`. Owners within the window are still treated as
valid during generation, giving time for investigation before they disappear from CODEOWNERS.

### Required Changes to `CodeownersValidatorHelper`

The current helper catches `RateLimitExceededException` and `SecondaryRateLimitExceededException`
and converts them into `Status = "Error"` results (`CodeownersValidatorHelper.cs:79-95`).

For the audit, transient exceptions should propagate so the audit fails immediately.
Add an **audit-specific overload or option** — do not change the existing method's contract,
since `CodeownersTool` relies on the current behavior (`CodeownersTool.cs:767-774`). The
overload should:
- Return `Valid` / `Invalid` results for deterministic outcomes.
- Let `RateLimitExceededException`, network, and auth exceptions propagate as thrown exceptions.

### Delete Safety

Work item deletions (for orphaned Label Owners) use `IDevOpsService.DeleteWorkItemAsync`.
A generic delete method must be added to `IDevOpsService` (currently only release-plan-specific
deletes exist at `DevOpsService.cs:469-473`).

---

## State Management for Cascading Effects

The audit runs all rules and applies all fixes in a **single execution**. When `--fix` is
active, fixes in one rule can create violations detectable by later rules. The harness
**refreshes `AuditContext` from ADO after each rule that applied fixes**, so the next rule
always operates on accurate data.

### Design

```
AuditContext
└── WorkItemData (all fetched work items, hydrated — the single source of truth)
```

No separate change-tracking sets. Instead:

1. A rule evaluates `AuditContext` and returns its list of violations and fixes to apply.
2. The **rule harness** applies each fix sequentially (ADO API call).
3. After the rule completes, if any fixes were applied, the harness **performs a full rebuild
   of `AuditContext.WorkItemData`** by re-fetching all work items from ADO and re-hydrating
   relations from scratch. This avoids stale or duplicated data from incremental updates.
4. The next rule sees the fully rebuilt state — no stale data, no duplicate relations.

Rules only read from `AuditContext` and return fixes; the harness owns mutation and refresh.

### Execution Flow (Single Pass)

```
1. Fetch all work items into AuditContext

2. AUD-OWN-001: Invalid owner detection
   // No dependencies — runs first. Sets/clears Invalid Since.

3. AUD-OWN-002: Malformed team alias
   // No dependencies — pure string validation

4. AUD-OWN-003: Team not under azure-sdk-write
   // Skips malformed aliases internally (OWN-002 is report-only, can't rely on it fixing them)
   // Sets/clears Invalid Since.

5. AUD-LBL-001: Label not in GitHub
   // No dependencies on owner rules

6. AUD-LBL-002: Service Attention misuse
   // No dependencies on owner rules

7. AUD-STR-001: Label Owner missing owners
   // Evaluates current owner relations (not affected by Invalid Since changes)

8. AUD-STR-002: Label Owner missing labels
   // Report only — no fix, requires human investigation

→ After each rule that applied fixes, harness performs a full rebuild of AuditContext from ADO

9. Output final report (all violations, fixes applied, remaining issues)
```

### Crash Recovery

Each run re-fetches all work items from ADO and recomputes violations from scratch. There is
no resume state. This is safe because the audit always rebuilds its view from current ADO state
before evaluating rules.

---

## Implementation Plan

### Phase 0: Folder Reorganization

**Todo: `reorganize-codeowners-helpers`**
- Move all Codeowners-specific helpers into `Helpers/Codeowners/`:
  - `CodeownersManagementHelper.cs`
  - `CodeownersGenerateHelper.cs`
  - `CodeownersValidatorHelper.cs`
  - `CheckPackageHelper.cs`
  - Any related interfaces (`ICodeownersManagementHelper`, etc.)
- Create `Helpers/Codeowners/Rules/` folder for audit rule implementations.
- Update namespaces to match new folder structure.
- Update all `using` statements and DI registrations.
- **Validate**: Build succeeds (`dotnet build`) and all tests pass (`dotnet test`) before
  proceeding to any other work.
- **Commit**: Commit just the folder reorganization changes before proceeding, so the diff
  is clean and reviewable separately from behavioral changes.

### Phase 1: Infrastructure & Data Fetching

**Todo: `setup-audit-command`**
- Add `audit` subcommand to `CodeownersTool.cs` command list.
- Wire `--fix`, `--force`, and `--repo` options.
- Validate `--repo` format in `CodeownersTool.cs`: must match `Azure/<repo>` format AND map to
  a known SDK language via `RepoToLanguageString`. Reject unknown repos with an error.
- Create `ICodeownersAuditHelper` interface and `CodeownersAuditHelper` class.
- Register in DI container.
- Follow the pattern of existing helpers (`CodeownersManagementHelper`, etc.).

**Todo: `fetch-work-items`**
- Implement bulk work item fetching in the audit helper:
  - If `--repo` is specified:
    - Use `CodeownersManagementHelper.RepoToLanguageString` to convert the repo to a language
      identifier, then filter **Package** work items to those matching that language.
    - Filter **Label Owner** work items by exact `Custom.Repository == --repo` match.
    - **Owner** and **Label** work items are still fetched globally (they are shared across repos).
    - If `RepoToLanguageString` cannot map the repo to a known SDK language, reject with an error.
  - If `--repo` is omitted, fetch all work items across all repos.
- Reuse existing `IDevOpsService` WIQL queries and `ExtractRelatedIds` hydration.
- Cache results in memory for the duration of the audit run.

### Phase 2: Audit Rule Engine

**Todo: `rule-engine`**
- Define `IAuditRule` interface:
  ```csharp
  public interface IAuditRule
  {
      string RuleId { get; }
      string Description { get; }
      bool CanFix { get; }
      Task<List<AuditViolation>> Evaluate(AuditContext context, CancellationToken ct);
      Task<List<AuditFixAction>> GetFixes(AuditContext context, List<AuditViolation> violations, CancellationToken ct);
  }
  ```
- Rules return fix actions but do **not** apply them. The **rule harness**:
  1. Calls `Evaluate` to get violations.
  2. If `--fix`, calls `GetFixes` to get fix actions.
  3. Applies each fix action **sequentially**. Any thrown exception fails the audit immediately.
  4. After all fixes for a rule are applied, refreshes affected work items in `AuditContext`.
  5. Proceeds to the next rule, which sees updated state.
- `AuditContext` also holds `--fix` and `--force` flags so rules can check them.
- Define `AuditViolation`, `AuditFixAction`, and `AuditFixResult` models.
- Implement rule runner that executes rules in dependency order.

### Phase 3: Owner Validation Rules

**Todo: `rule-aud-own-001`** — Invalid owner detection and `Invalid Since` field management.
- Uses `ICodeownersValidatorHelper` (existing, with audit-specific overload).
- **Evaluates ALL owners first**, logs all invalid owners, then applies fixes.
- Fix sets `Custom.InvalidSince` on newly invalid owners, clears it on recovered owners.
- **Safety threshold**: If >5 newly invalid owners detected with `--fix`, throws unless `--force`
  is also specified. See `plans/AUD-OWN-001-invalid-owner.md` for details.

**Todo: `rule-aud-own-002`** — Malformed team alias detection.
- Pure string validation, no API calls.

**Todo: `rule-aud-own-003`** — Team not under azure-sdk-write.
- Uses `ITeamUserCache` (existing) with fallback to `IGitHubService` parent-chain validation.
- Fix sets `Custom.InvalidSince` on newly invalid teams, clears it on recovered teams.
- **Safety threshold**: If >5 newly invalid teams detected with `--fix`, throws unless `--force`.
- Transient errors fail the process immediately.

### Phase 4: Label & Structure Rules

**Todo: `rule-aud-lbl-001`** — Label not in GitHub.
- **Prerequisite**: Add `GetRepoLabels` method to `IGitHubService` / `GitHubService`.
**Todo: `rule-aud-lbl-002`** — Service Attention misuse.
- Check **both** Label Owner service labels AND Package PR labels.
- Remains **report only** in this project. No automated fix is planned.
**Todo: `rule-aud-str-001`** — Label Owner missing owners.
**Todo: `rule-aud-str-002`** — Label Owner missing labels (report only, no fix — requires
  human investigation for data integrity issues).

### Phase 5: Rule Ordering & Integration

**Todo: `rule-ordering`**
- Implement rule execution ordering per "State Management for Cascading Effects" section:
  1. Owner validation rules (AUD-OWN-*) — must run first.
  2. Label validation rules (AUD-LBL-*).
  3. Structure validation rules (AUD-STR-*).
- Harness performs a full rebuild of `AuditContext` from ADO after each rule that applied fixes.
- All rules and fixes run in a single execution.

**Todo: `audit-reporting`**
- Output structured results via the azsdk-cli harness (handles JSON/text formatting).
- Exit code: 0 = no violations remain, 1 = violations found/remaining after fix.

### Phase 6: Testing

**Todo: `unit-tests`**
- Unit tests for each audit rule using mocked services.
- Update `MockGitHubService` and `MockDevOpsService` with new interface members
  (`GetRepoLabels`, generic `DeleteWorkItemAsync`) so tests compile.
- Test fix behavior with and without `--fix`.
- Test InvalidOwnerRule set/clear/do-nothing detail transitions.
- Test TeamNotWriteRule set/clear/do-nothing detail transitions.
- Test STR-001: Label Owner with zero owners is deleted.
- Test STR-002: Label Owner with zero labels is reported (not fixed).
- Test AUD-OWN-001 safety threshold: >5 newly invalid owners with `--fix` throws without `--force`.
- Test AUD-OWN-001 safety threshold: >5 newly invalid owners with `--fix --force` proceeds.
- Test AUD-OWN-003 safety threshold: >5 newly invalid teams with `--fix` throws without `--force`.
- Test AUD-OWN-001 always reports all invalid owners (even above threshold) before throwing.
- Test that transient errors fail the process immediately.
- Test STR-001 delete failure propagation: failed deletes surface as exceptions and stop the audit.
- Test invalid Detail value throws in OWN-001 and OWN-003.
- Test rule harness performs full rebuild of context after fixes.
- Test AUD-OWN-003 skips malformed team aliases without crashing.
- Test `--repo` filtering: Packages filtered by language, Label Owners by `Custom.Repository`.
- Test `--repo` scoped run still evaluates/fixes owners globally.
- Test AUD-LBL-001 repo derivation: labels checked only in repos where they're referenced.
- Test generator lookback behavior: expired owners are excluded from generated entries, owners
  still inside the lookback window remain, and linked label-owner metadata owners are filtered
  by the same cutoff.

**Todo: `add-label-owner-path-validation`**
- Add glob/path syntax validation to the `add-label-owner` command when `--path` is specified.
- Reuse validation logic from `DirectoryUtils` to prevent invalid paths from entering ADO.
- This is input validation (not audit) — reject bad paths before they become work item data.

**Todo: `spec-update`**
- Update `8-operations-codeowners-management.spec.md` with audit command documentation.

---

## Code Reuse Analysis

The existing linter's `Verification/` classes (`Owners.cs`, `Labels.cs`, `CodeownersLinter.cs`)
are part of the `Azure.Sdk.Tools.CodeownersUtils` project, which azsdk-cli already references.

**Can the linter rule logic be included as a dependency and mapped directly?**

No — the existing linter rules are tightly coupled to their text-parsing inputs:
- `Owners.VerifyOwners()` takes an `OwnerDataUtils` object and a raw CODEOWNERS line string.
- `Labels.VerifyLabels()` operates on parsed label strings and repo label sets.
- `CodeownersLinter.LintCodeownersEntry()` takes a `CodeownersEntry` (parsed from CODEOWNERS text).

The audit operates on ADO work items (`OwnerWorkItem`, `LabelWorkItem`, `LabelOwnerWorkItem`,
`PackageWorkItem`). Mapping ADO objects back into the linter's text-parsing format would add
complexity without benefit.

**Recommendation: Re-implement** the audit rules cleanly against the ADO work item model.

Reasons:
1. The audit has richer context via direct ADO API, `ICodeownersValidatorHelper`, and
   `ITeamUserCache` — these are more suitable than the linter's `OwnerDataUtils`.
2. Most rule logic is simple (count checks, string format checks, set membership) and trivial
   to implement directly.
3. The `CodeownersUtils` project is still useful as a dependency for parsing/formatting (e.g.,
   `CodeownersParser`, `CodeownersEntry`), but its `Verification/` classes are designed for
   text-based linting, not ADO work item auditing.
4. Re-implementing avoids an adapter layer that would be more complex than the rules themselves.

---

## Key Design Decisions

1. **Scope**: Audits all work items by default; `--repo` narrows to a specific `Azure/<repo>`.
2. **Fix behavior**: Owner rules (OWN-001, OWN-003) set `Custom.InvalidSince` to current date/time on newly invalid owners, clear it when owners become valid again. No relation removal — invalid owners are excluded from CODEOWNERS generation via `--invalid-owner-lookback-days` (default 90 days). Structure rule STR-001 deletes orphaned Label Owner work items with zero owner relations.
3. **Rule ordering**: Owner rules run before structure rules so cascade effects are detected.
4. **CLI-only**: No MCP tool exposure for now.
5. **Command location**: `config codeowners audit` alongside existing codeowners commands.
6. **Deferred rules**: AUD-STR-003/004/005 deferred to later phase. MSFT identity population
   and identity-resolution integration are intentionally out of scope for this project.

## Key Dependencies

| Dependency | Source | Notes |
|------------|--------|-------|
| `CodeownersValidatorHelper` | Existing | GitHub user validation (audit-specific overload needed) |
| `ITeamUserCache` | Existing | Team hierarchy and member expansion |
| `IDevOpsService` | Existing | ADO work item queries, updates, relation management |
| `IGitHubService` | Existing | Org membership, write permission. **Must add `GetRepoLabels` method** for AUD-LBL-001. |

## Risks and Mitigations

| Risk | Mitigation |
|------|------------|
| GitHub API rate limits during bulk owner validation | Fail immediately; re-run after the transient error clears |
| Invalid Since field changes affect CODEOWNERS generation | `--invalid-owner-lookback-days` (default 90) provides a grace window before owners are excluded |
| Partial completion on transient failure | Audit re-fetches from ADO on re-run and recomputes violations from current state |
| Double-delete of work items on re-run | No silent masking — delete failures surface immediately and should be investigated |
| Concurrent ADO modification during field updates | Field set/clear is last-writer-wins; re-run produces correct results |

---

## Implementation Status

### Completed ✅

| Phase | Description | Commit |
|-------|-------------|--------|
| Phase 0 | Folder reorganization — moved 6 files to `Helpers/Codeowners/`, created `Rules/` | `Move Codeowners helpers to Helpers/Codeowners/ subfolder` |
| Phase 1 | Infrastructure — AuditModels, IAuditRule, ICodeownersAuditHelper, CodeownersAuditHelper, audit subcommand, --fix/--force/--repo options | `Implement codeowners audit command with 7 rules and tests` |
| Phase 2 | All 7 audit rules — OWN-001/002/003, LBL-001/002, STR-001/002 | Same commit as Phase 1 |
| Phase 3 | Service additions — DeleteWorkItemAsync (IDevOpsService), GetRepoLabels (IGitHubService) | Same commit as Phase 1 |
| Phase 4 | DI registrations, mock updates, initial 25 tests | Same commit as Phase 1 |
| Review 1 | LabelNotInGitHubRule per-repo fix, ServiceAttentionMisuseRule type name, narrowed exception matching, harness try/catch, ConcurrentDictionary, 9 new tests | `Address review feedback: fix correctness and safety issues` |
| Review 2 | TeamNotWriteRule azure-sdk-write self-check, STR-001 safety threshold, cache staleness fix, remove 409 retry, 5 new tests | `Address final review: safety thresholds, cache staleness, remove retry` |
| Refactor | InvalidOwnerRule rewritten: removed relation removal, now sets/clears `Custom.InvalidSince` field on Owner work items | Uncommitted |
| Refactor | TeamNotWriteRule rewritten: removed relation removal, now sets/clears `Custom.InvalidSince` field on team Owner work items. Added safety threshold (>5 = requires --force). | Uncommitted |
| Feature | Added `InvalidSince` (DateTime?) property to `OwnerWorkItem` with `[FieldName("Custom.InvalidSince")]` | Uncommitted |
| Feature | Added `--invalid-owner-lookback-days` option (default 90) to `generate` command. `IsOwnerExpired` filter excludes owners past the lookback window from generated CODEOWNERS. | Uncommitted |
| Bugfix | Case-insensitive org membership comparison in `CodeownersValidatorHelper.cs` | Uncommitted |

### Test Coverage

- InvalidOwnerRule: 13 tests (valid, invalid, not-found, error propagation, set/clear/do-nothing detail, threshold, force override, team skip, invalid detail throws)
- MalformedTeamRule: 5 tests (valid format, invalid formats, fix output)
- TeamNotWriteRule: 10 tests (individual skip, malformed skip, cache hit, azure-sdk-write self, NotFoundException, set invalid, already marked, clear invalid, do-nothing, invalid detail throws, threshold)
- LabelNotInGitHubRule: 4 tests (label present, missing from one repo, unreferenced label, no fixes)
- ServiceAttentionMisuseRule: 5 tests (PR Label, Service Owner sole label, Service Owner multiple labels, Package, Azure SDK Owner)
- LabelOwnerMissingOwnersRule: 5 tests (with owners, zero owners, delete fix, threshold, force override)
- LabelOwnerMissingLabelsRule: 3 tests (with labels, zero labels, no fixes)
- CodeownersAuditHelper: 4 tests (empty report, repo filter, fix exception, rebuild after fix)
- CodeownersGenerateHelper: 13 tests (BuildCodeownersEntries coverage includes invalid-owner lookback filtering for package owners and linked label-owner metadata)

### Deferred / Out of Scope

- AUD-STR-003/004/005 — deferred structural rules
- Spec update (`8-operations-codeowners-management.spec.md`)
- Path validation for `add-label-owner --path`
- MSFT identity population / identity-resolution integration — out of scope for this project
