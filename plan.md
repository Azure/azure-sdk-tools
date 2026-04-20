# Plan: Codeowners Audit Command for azsdk-cli

## Problem Statement

The Azure SDK codeowners management system has migrated from hand-edited CODEOWNERS files to
Azure DevOps work items as the source of truth (`config codeowners` commands in azsdk-cli).
The old `CodeownersLinter` (in `tools/codeowners-utils/`) validates the rendered CODEOWNERS
file, but there is no equivalent auditing of the ADO work item data itself. Invalid owners
accumulate over time, MSFT identities are not tracked, and structural issues in the work item
graph can cause the generated CODEOWNERS to fail the linter.

## Approach

Add a `config codeowners audit` command to azsdk-cli that:

1. Fetches all Owner, Label, Label Owner, and Package work items from ADO.
2. Runs a configurable set of audit rules against them.
3. Reports violations in a structured format.
4. With `--fix`, applies automated fixes where safe (relation removal, field population,
   orphan cleanup).

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
| AUD-OWN-001 | Individual Owner fails GitHub validation (not in Azure/Microsoft orgs, no write access) | Yes — remove relations |
| AUD-OWN-002 | Team alias doesn't match `Azure/<team>` format | Report only |
| AUD-OWN-003 | Team doesn't descend from `azure-sdk-write` | Yes — remove relations |

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
| `--fix` | Apply automated fixes (remove invalid owner relations, delete orphaned entries). Without this flag, the command reports all violations and what would be fixed — effectively a preview of what `--fix` would do. |
| `--force` | Override safety thresholds. Required when `--fix` would act on more than the allowed number of violations (e.g., AUD-OWN-001 throws if >5 invalid owners detected). |
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

`--fix` makes destructive changes (relation removal, work item deletion). If a transient
error occurs (rate limits, network failures, auth errors), the audit **fails immediately**
with a non-zero exit code. No retries. Operations that succeeded before the failure are
kept — the auditor is **not atomic** and can be re-run safely.

Each run re-fetches all work items from ADO and recomputes violations from scratch — there
is no resume state. All fix operations are designed to be idempotent against current server
state (see below), so re-running after a failure produces correct results.

### Idempotent Removal & Delete Operations

`RemoveWorkItemRelationAsync` currently throws if the relation is already absent
(`DevOpsService.cs:622-635`). `DeleteWorkItemAsync` has no 404 handling. For the audit,
these operations must be safe on re-run:

- **Relation removal**: If the relation is not found on the source work item, treat as
  success (already removed). Do not throw.
- **Work item delete**: If the target work item returns 404, treat as success (already
  deleted). Do not throw.

Implement this as **audit-specific wrappers** (not by changing the existing service methods)
so other callers are unaffected. The wrappers catch the specific "not found" exceptions and
return a result indicating `AlreadyApplied`.

### Relation Removal Concurrency

`RemoveWorkItemRelationAsync` finds a relation by URL match, gets its index, then PATCHes
`/relations/{index}` (`DevOpsService.cs:627-644`). If another process modifies the work
item's relations between GET and PATCH, the index may be stale — either removing the wrong
relation or getting a 409 conflict. The audit-specific wrapper should handle 409 by
re-fetching the work item and recomputing the relation index.

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
   // No dependencies — runs first

3. AUD-OWN-002: Malformed team alias
   // No dependencies — pure string validation

4. AUD-OWN-003: Team not under azure-sdk-write
   // Skips malformed aliases internally (OWN-002 is report-only, can't rely on it fixing them)

5. AUD-LBL-001: Label not in GitHub
   // No dependencies on owner rules

6. AUD-LBL-002: Service Attention misuse
   // No dependencies on owner rules

7. AUD-STR-001: Label Owner missing owners
   // Depends on: AUD-OWN-001, AUD-OWN-003 (owner removals may leave Label Owners orphaned)

8. AUD-STR-002: Label Owner missing labels
   // Depends on: AUD-LBL-001, AUD-LBL-002 (label changes may leave Label Owners without labels)
   // Report only — no fix, requires human investigation

→ After each rule that applied fixes, harness performs a full rebuild of AuditContext from ADO

9. Output final report (all violations, fixes applied, remaining issues)
```

### Crash Recovery

Each run re-fetches all work items from ADO and recomputes violations from scratch. There is
no resume state. This is safe because all fix operations are designed to be idempotent against
current server state (see "Error Handling" section).

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
  3. Applies each fix action **sequentially** via idempotent service wrappers.
  4. After all fixes for a rule are applied, refreshes affected work items in `AuditContext`.
  5. Proceeds to the next rule, which sees updated state.
- `AuditContext` also holds `--fix` and `--force` flags so rules can check them.
- Define `AuditViolation`, `AuditFixAction`, and `AuditFixResult` models.
- Implement rule runner that executes rules in dependency order.

### Phase 3: Owner Validation Rules

**Todo: `rule-aud-own-001`** — Invalid owner detection and relation removal.
- Uses `ICodeownersValidatorHelper` (existing, with audit-specific overload).
- **Evaluates ALL owners first**, logs all invalid owners, then applies fixes.
- **Safety threshold**: If >5 invalid owners detected with `--fix`, throws unless `--force`
  is also specified. See `plans/AUD-OWN-001-invalid-owner.md` for details.
- Returns fix actions; harness applies via idempotent wrappers.

**Todo: `rule-aud-own-002`** — Malformed team alias detection.
- Pure string validation, no API calls.

**Todo: `rule-aud-own-003`** — Team not under azure-sdk-write.
- Uses `ITeamUserCache` (existing) with fallback to `IGitHubService` parent-chain validation.
- Transient errors fail the process immediately.

### Phase 4: Label & Structure Rules

**Todo: `rule-aud-lbl-001`** — Label not in GitHub.
- **Prerequisite**: Add `GetRepoLabels` method to `IGitHubService` / `GitHubService`.
**Todo: `rule-aud-lbl-002`** — Service Attention misuse.
- Check **both** Label Owner service labels AND Package PR labels.
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
- Test cascade: invalid owner removal → Label Owner becomes orphaned → detected by STR-001.
- Test STR-002: Label Owner with zero labels is reported (not fixed).
- Test AUD-OWN-001 safety threshold: >5 invalid owners with `--fix` throws without `--force`.
- Test AUD-OWN-001 safety threshold: >5 invalid owners with `--fix --force` proceeds.
- Test AUD-OWN-001 always reports all invalid owners (even above threshold) before throwing.
- Test that transient errors fail the process immediately.
- Test idempotent wrappers: removing already-absent relation returns success.
- Test idempotent wrappers: deleting already-deleted WI returns success.
- Test 409 conflict handling on relation removal.
- Test rule harness performs full rebuild of context after fixes.
- Test AUD-OWN-003 skips malformed team aliases without crashing.
- Test `--repo` filtering: Packages filtered by language, Label Owners by `Custom.Repository`.
- Test `--repo` with unsupported repo is rejected.
- Test `--repo` scoped run still evaluates/fixes owners globally.
- Test AUD-LBL-001 repo derivation: labels checked only in repos where they're referenced.

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
2. **Fix behavior**: Hard removal of relations from invalid owners (no soft tag/review step).
3. **Rule ordering**: Owner rules run before structure rules so cascade effects are detected.
4. **CLI-only**: No MCP tool exposure for now.
5. **Command location**: `config codeowners audit` alongside existing codeowners commands.
6. **Deferred rules**: AUD-STR-003/004/005 deferred to later phase. AUD-OWN-004 (MSFT
   Identity) and identity-resolution integration deferred separately.

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
| GitHub API rate limits during bulk owner validation | Fail immediately; re-run is safe due to idempotent fixes |
| Cascade fixes could remove too many relations | Rule harness performs full rebuild of AuditContext from ADO after each rule's fixes |
| Partial completion on transient failure | Audit re-fetches from ADO on re-run; idempotent wrappers handle already-applied fixes |
| Double-delete of work items on re-run | Idempotent delete wrapper treats 404 as success |
| Concurrent ADO modification during relation removal | 409 conflict handling with re-fetch and index recomputation |
