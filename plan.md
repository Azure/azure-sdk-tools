# CODEOWNERS Add & Remove — Implementation Plan

> **Data model reference**: Sections 1-9 of the original plan (data model, relationships,
> hydration, generator algorithm, output format) have been archived. See git history for
> the full reference documentation.

---

## 1. Current State

The `view` command is fully implemented. We need `add` and `remove` commands that
create/remove `System.LinkTypes.Related` links between ADO work items — and, when
necessary, create new Owner or LabelOwner work items.

**Key principles:**
- Work items are the source of truth; the CODEOWNERS file is *generated* from them.
- Labels are never auto-created (managed centrally).
- Owners are auto-created after validating the GitHub alias.
- LabelOwners are auto-created when no matching one exists.
- `--repo` is always optional — inferred from git context via `IGitHelper.GetRepoFullNameAsync(".")`.
- After a successful add/remove, return a view-like summary of the affected entity.

---

## 2. The 4 Scenarios

All parameters shown are **required** for that scenario. `--repo` is always optional.

### Scenario 1: Add user to package
**Command**: `add --github-user @user1 --package pkg1`
**Links created**: `Owner ↔ Package`
**CODEOWNERS effect**: `@user1` appears as a `SourceOwner` on the package's line.

**Algorithm**:
1. Resolve `--repo` (infer if not provided)
2. Find the Package work item by name + language (error if not found)
3. FindOrCreateOwner(`@user1`) — look up Owner WI by alias; create if not found (validate first)
4. Check if Owner ID is already in Package's `RelatedIds` (if so, report "already linked")
5. Create `System.LinkTypes.Related` link: Package → Owner
6. Return view of the package

---

### Scenario 2: Add label to package
**Command**: `add --label label1 --package pkg1`
**Links created**: `Label ↔ Package`
**CODEOWNERS effect**: `%label1` appears as a `PRLabel` on the package's line.

**Algorithm**:
1. Resolve `--repo` (infer if not provided)
2. Find the Package work item by name + language (error if not found)
3. Find the Label work item by name (error if not found — labels are never auto-created)
4. Check if Label ID is already in Package's `RelatedIds` (if so, report "already linked")
5. Create `System.LinkTypes.Related` link: Package → Label
6. Return view of the package

---

### Scenario 3: Add user as service/SDK owner for a label (pathless)
**Command**: `add --github-user @user1 --label label1 --owner-type service-owner`
**Links created**: `Owner ↔ LabelOwner` + `Label ↔ LabelOwner`
**CODEOWNERS effect**: Creates a pathless triage entry:
```
# ServiceLabel: %label1
# ServiceOwners: @user1
```

**Algorithm**:
1. Resolve `--repo` (infer if not provided)
2. Find the Label work item by name (error if not found)
3. FindOrCreateOwner(`@user1`)
4. FindOrCreateLabelOwner — match on repo + owner-type + no path + this label; create if not found
5. Link Owner ↔ LabelOwner (if not already linked)
6. Link Label ↔ LabelOwner (if not already linked)
7. Return view of the label owner

**`--owner-type` values**: `service-owner` or `azsdk-owner`

---

### Scenario 4: Add user and label to a path
**Command**: `add --github-user @user1 --label label1 --path sdk/service/ --owner-type service-owner`
**Links created**: `Owner ↔ LabelOwner` + `Label ↔ LabelOwner`
**CODEOWNERS effect**: Path entry with user and label:
```
# PRLabel: %label1
/sdk/service/    @user1
```

**Algorithm**:
1. Resolve `--repo` (infer if not provided)
2. Find the Label work item by name (error if not found)
3. FindOrCreateOwner(`@user1`)
4. FindOrCreateLabelOwner — match on repo + path + owner-type; create if not found
5. Link Owner ↔ LabelOwner (if not already linked)
6. Link Label ↔ LabelOwner (if not already linked)
7. Return view of the path

**`--owner-type` values**: `service-owner`, `azsdk-owner`, or `pr-label`

---

## 3. Parameter Validation

| Scenario | `--github-user` | `--package` | `--label` | `--path` | `--owner-type` | `--repo` |
|----------|:---:|:---:|:---:|:---:|:---:|:---:|
| 1. User→Package | ✅ | ✅ | ✗ | ✗ | ✗ | optional |
| 2. Label→Package | ✗ | ✅ | ✅ | ✗ | ✗ | optional |
| 3. User→Label (pathless) | ✅ | ✗ | ✅ | ✗ | ✅ | optional |
| 4. User+Label→Path | ✅ | ✗ | ✅ | ✅ | ✅ | optional |

✅ = required, ✗ = must NOT be specified

**Scenario detection** (in priority order):
1. `--github-user` + `--package` (no others) → Scenario 1
2. `--label` + `--package` (no others) → Scenario 2
3. `--github-user` + `--label` + `--owner-type` + `--path` → Scenario 4
4. `--github-user` + `--label` + `--owner-type` (no path) → Scenario 3

If no combination matches → error describing valid combinations.

---

## 4. Remove Operations

Each `remove` scenario mirrors its `add` counterpart exactly (same required params).

| Scenario | Command | Links removed |
|----------|---------|---------------|
| 1. User from Package | `remove --github-user @u --package pkg` | Owner ↔ Package |
| 2. Label from Package | `remove --label lbl --package pkg` | Label ↔ Package |
| 3. User from Label (pathless) | `remove --github-user @u --label lbl --owner-type svc` | Owner ↔ LabelOwner |
| 4. User+Label from Path | `remove --github-user @u --label lbl --path p --owner-type svc` | Owner ↔ LabelOwner + Label ↔ LabelOwner |

**Remove behaviors**:
- All remove operations error if the target work items don't exist
- All remove operations error if the link doesn't exist ("not linked")
- Remove does NOT delete work items — only removes links
- After successful remove, returns a view of the affected entity

---

## 5. Implementation Steps

### Step 1: Response Type

Create `CodeownersModifyResponse : CommandResponse` with:
- `Operation` — what was done (e.g., "Added owner @user to package pkg")
- `View` — the `CodeownersViewResponse` showing the resulting state
- Override `Format()` to display both

### Step 2: Add Methods to `ICodeownersManagementHelper`

```csharp
// Find-or-create helpers
Task<OwnerWorkItem> FindOrCreateOwnerAsync(string gitHubAlias);
Task<LabelOwnerWorkItem> FindOrCreateLabelOwnerAsync(string repo, string ownerType, string? repoPath, string label);

// Scenario methods
Task<CodeownersModifyResponse> AddOwnerToPackageAsync(string ownerAlias, string packageName, string repo);
Task<CodeownersModifyResponse> AddLabelToPackageAsync(string label, string packageName, string repo);
Task<CodeownersModifyResponse> AddOwnerToLabelAsync(string ownerAlias, string label, string repo, string ownerType);
Task<CodeownersModifyResponse> AddOwnerAndLabelToPathAsync(string ownerAlias, string label, string repo, string path, string ownerType);

Task<CodeownersModifyResponse> RemoveOwnerFromPackageAsync(string ownerAlias, string packageName, string repo);
Task<CodeownersModifyResponse> RemoveLabelFromPackageAsync(string label, string packageName, string repo);
Task<CodeownersModifyResponse> RemoveOwnerFromLabelAsync(string ownerAlias, string label, string repo, string ownerType);
Task<CodeownersModifyResponse> RemoveOwnerAndLabelFromPathAsync(string ownerAlias, string label, string repo, string path, string ownerType);
```

**Note**: `IDevOpsService` already has `CreateWorkItemRelationAsync` and `RemoveWorkItemRelationAsync` — no new DevOps methods needed.

### Step 3: Business Logic in `CodeownersManagementHelper`

**Reuse existing private methods**: `FindOwnerByGitHubAlias`, `FindPackageByName`,
`FindLabelByName`, `QueryLabelOwnersByPath`, `NormalizeGitHubAlias`.

**New methods needed**:

1. **`FindOrCreateOwnerAsync(alias)`**:
   - Normalize alias via `NormalizeGitHubAlias`
   - Validate via `ICodeownersValidatorHelper.ValidateCodeOwnerAsync`
   - `FindOwnerByGitHubAlias` → return if found
   - Create via `IDevOpsService.CreateWorkItemAsync` with type `"Owner"`, title = alias

2. **`FindOrCreateLabelOwnerAsync(repo, ownerType, path?, label)`**:
   - Query for existing LabelOwner matching repo + type + path
   - If found, return
   - Create new LabelOwner with `Custom.LabelType`, `Custom.Repository`, `Custom.RepoPath`
   - Title: `<Owner Type>: <label> (<language>)` e.g., `Service Owner: Storage (python)`

3. **Owner type mapping** — CLI → work item field:
   - `service-owner` → `Service Owner`
   - `azsdk-owner` → `Azure SDK Owner`
   - `pr-label` → `PR Label`

4. **Duplicate detection** — Check `RelatedIds` before adding links.

5. **Post-operation view** — After add/remove, call the appropriate `GetViewBy*` method
   to populate the response.

**DI change**: Add `ICodeownersValidatorHelper` to `CodeownersManagementHelper` constructor.

### Step 4: Add Commands to `CodeownersTool.cs`

```csharp
private const string addCodeownersCommandName = "add";
private const string removeCodeownersCommandName = "remove";
private const string CodeownerAddToolName = "azsdk_engsys_codeowner_add";
private const string CodeownerRemoveToolName = "azsdk_engsys_codeowner_remove";

private readonly Option<string> ownerTypeOption = new("--owner-type")
{
    Description = "Owner type: service-owner, azsdk-owner, or pr-label"
};
```

**Repo inference** — When `--repo` is not provided:
1. Call `gitHelper.GetRepoFullNameAsync(".")`
2. Validate via `SdkLanguageHelpers.GetLanguageForRepo`
3. Error if not a recognized language repo

**Scenario detection** — `DetectScenario()` using priority order from §3.

**Both `add` and `remove` share** the same parameter validation and scenario detection.

### Step 5: Tests

**Business logic tests** (extend `CodeownersManagementHelperTests.cs`):

| Scenario | Test cases |
|----------|-----------|
| 1. User→Pkg | Create link, pkg not found, already linked |
| 2. Label→Pkg | Create link, label not found, already linked |
| 3. User→Label | Create LabelOwner + links, existing LabelOwner, label not found |
| 4. User+Label→Path | Create LabelOwner + links, existing LabelOwner |
| FindOrCreate | Owner exists, owner created, invalid user error |

Mirror test cases for all remove scenarios.

**Command-level tests** (extend `CodeownersToolsTests.cs`):
- Scenario detection from parameter combinations
- Parameter validation errors
- `--repo` inference from git context

### Step 6: Update MCP Agent Instructions

Update `eng/common/instructions/azsdk-tools/codeowners.md` with:

```markdown
#### Tool: `azsdk_engsys_codeowner_add`
**When to use**: When a user wants to establish an ownership relationship.

**Parameter combinations** (each is a distinct scenario):
1. `--github-user` + `--package` → Add user as source owner of package
2. `--label` + `--package` → Add PR label to package
3. `--github-user` + `--label` + `--owner-type` → Add user as service/SDK owner for label (pathless)
4. `--github-user` + `--label` + `--path` + `--owner-type` → Add user+label to path

#### Tool: `azsdk_engsys_codeowner_remove`
Same parameter rules as `add`. Confirm the user truly wants to remove before calling.

#### Workflow
1. Always use `view` first to show current state
2. After add/remove, the tool returns the updated state automatically
3. Remind user to run `generate` to update the CODEOWNERS file
```

---

## 6. Design Decisions

**Q: Why only 4 scenarios?**
A: Simplicity. The dropped scenarios (user→path without label, label→path without user,
service metadata→package) are edge cases that can be added later if needed.

**Q: How is `--repo` inferred?**
A: `IGitHelper.GetRepoFullNameAsync(".")`, validated against `SdkLanguageHelpers.GetLanguageForRepo`.

**Q: Should Labels be auto-created?**
A: No. Labels are managed centrally.

**Q: Should Owners be auto-created?**
A: Yes, after validating via `ValidateCodeOwnerAsync`.

**Q: Should LabelOwners be auto-created?**
A: Yes. Create when no matching one exists for the given repo + owner-type + path.

**Q: How does `add` handle duplicates?**
A: Checks `RelatedIds` before adding. Reports "already linked" and skips.

**Q: What does `remove` do with orphaned LabelOwners?**
A: Nothing. Orphan cleanup is out of scope.

**Q: What about `@alias` vs `alias`?**
A: Both accepted. The `@` prefix is stripped via existing `NormalizeGitHubAlias`.
