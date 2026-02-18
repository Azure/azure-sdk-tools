# Product Requirements Document: CODEOWNERS Work Item Management Commands

## 1. Overview

### 1.1 Problem
The Azure SDK CLI (`azsdk-cli`) has CODEOWNERS commands (`update`, `validate`, `generate`) that operate on the CODEOWNERS file directly. There is no way to query or modify the underlying Azure DevOps work item relationships (Owner, Package, Label, Label Owner) that are the source of truth for CODEOWNERS data.

### 1.2 Solution
Add three new subcommands to `CodeownersTool.cs` — `view`, `add`, and `remove` — that operate on Azure DevOps work items and their relationships. The CODEOWNERS file is regenerated separately via the existing `generate` command.

### 1.3 Scope
- **In scope**: View, add, and remove operations on work item relationships; MCP tool exposure; test scaffolding; MCP agent instructions.
- **Out of scope**: Modifying the CODEOWNERS file directly; package path validation against repository files (no dependency on `Get-AllPkgProperties.ps1`); writing test implementations.

---

## 2. Work Items

### WI-1: DevOps Service — Query Owner by GitHub Alias

**Description**: Add a method to `IDevOpsService` that queries Owner work items by `Custom.GitHubAlias`, returning the work item with hydrated relations.

**Acceptance Criteria**:
- [x] New method signature: `Task<OwnerWorkItem?> GetOwnerByGitHubAliasAsync(string gitHubAlias)`
- [x] Queries `[System.WorkItemType] = 'Owner' AND [Custom.GitHubAlias] = '<alias>'` in the Release project
- [x] Returns `null` if no matching Owner exists
- [x] Fetches with `WorkItemExpand.Relations` to include relationship data

---

### WI-2: DevOps Service — Create Owner Work Item

**Description**: Add a method to `IDevOpsService` that creates a new Owner work item.

**Acceptance Criteria**:
- [x] New method signature: `Task<OwnerWorkItem> CreateOwnerWorkItemAsync(string gitHubAlias)`
- [x] Creates work item of type `Owner` with `Custom.GitHubAlias` set
- [x] Title format: `Owner <gitHubAlias>`
- [x] Returns the created work item mapped to `OwnerWorkItem`
- [x] Checks for existing Owner before creating (idempotent — returns existing if found)

---

### WI-3: DevOps Service — Query Label Owner Work Items

**Description**: Add methods to query Label Owner work items by various criteria.

**Acceptance Criteria**:
- [x] Method: `Task<List<LabelOwnerWorkItem>> GetLabelOwnersByRepoAndPathAsync(string repo, string repoPath)` — queries by `Custom.Repository` and `Custom.RepoPath`
- [x] Method: `Task<List<LabelOwnerWorkItem>> GetLabelOwnersByRepoAsync(string repo)` — queries all Label Owners for a repo
- [x] Both fetch with `WorkItemExpand.Relations`
- [x] Results are mapped via `WorkItemMappers.MapToLabelOwnerWorkItem`

---

### WI-4: DevOps Service — Create Label Owner Work Item

**Description**: Add a method to create a new Label Owner work item.

**Acceptance Criteria**:
- [x] New method signature: `Task<LabelOwnerWorkItem> CreateLabelOwnerWorkItemAsync(string repo, string labelType, string repoPath, List<string> labelNames)`
- [x] Creates work item of type `Label Owner` with fields: `Custom.LabelType`, `Custom.Repository`, `Custom.RepoPath`
- [x] Title format: `Label Owner: <repo> - <labelType> - <label1>, <label2>, ...`
- [x] Returns the created work item mapped to `LabelOwnerWorkItem`

---

### WI-5: DevOps Service — Add Related Link Between Work Items

**Description**: Add a method to create a "Related" link between two work items.

**Acceptance Criteria**:
- [x] New method signature: `Task AddRelatedLinkAsync(int sourceWorkItemId, int targetWorkItemId)`
- [x] Creates a `System.LinkTypes.Related` link
- [x] Skips silently if the link already exists (idempotent)
- [x] Uses the DevOps work item patch API to add the relation

---

### WI-6: DevOps Service — Remove Related Link Between Work Items

**Description**: Add a method to remove a "Related" link between two work items.

**Acceptance Criteria**:
- [x] New method signature: `Task RemoveRelatedLinkAsync(int sourceWorkItemId, int targetWorkItemId)`
- [x] Removes the `System.LinkTypes.Related` link if it exists
- [x] Skips silently if the link does not exist (idempotent)
- [x] Uses the DevOps work item patch API with `Operation.Remove`

---

### WI-7: DevOps Service — Query Packages by Name

**Description**: Add a method to query Package work items by package name, returning the latest version.

**Acceptance Criteria**:
- [x] New method signature: `Task<PackageWorkItem?> GetPackageByNameAsync(string packageName)`
- [x] Queries `[System.WorkItemType] = 'Package' AND [Custom.Package] = '<name>'`
- [x] Uses `WorkItemMappers.GetLatestPackageVersions` to return only the latest version
- [x] Fetches with `WorkItemExpand.Relations`
- [x] Returns `null` if no matching package exists

---

### WI-8: DevOps Service — Query Label by Name

**Description**: Add a method to query Label work items by label name.

**Acceptance Criteria**:
- [x] New method signature: `Task<LabelWorkItem?> GetLabelByNameAsync(string labelName)`
- [x] Queries `[System.WorkItemType] = 'Label' AND [Custom.Label] = '<name>'`
- [x] Case-insensitive matching
- [x] Returns `null` if no matching label exists

---

### WI-9: Response Models for View Output

**Description**: Create response model classes in `Models/Codeowners` for the structured view output.

**Acceptance Criteria**:
- [x] `CodeownersViewResult` class containing:
  - `List<PackageViewItem> Packages` — each with: package name, language, package type, source owners (sorted), labels (sorted)
  - `List<LabelOwnerGroup> PathBasedLabelOwners` — grouped by `RepoPath`, sorted by path. Each group contains: path, repo, and a list of `LabelOwnerViewItem` (label type, owners sorted alphabetically, labels sorted alphabetically)
  - `List<LabelOwnerGroup> PathlessLabelOwners` — grouped by alphabetized label set, sorted by primary label. Each group contains the label set and a list of `LabelOwnerViewItem`
- [x] All model classes live in `Azure.Sdk.Tools.Cli.Models.Codeowners` namespace
- [x] Models inherit from or compose with `CommandResponse` for CLI/MCP output

---

### WI-10: Input Validation Helpers

**Description**: Add validation logic for parameter combinations and input normalization.

**Acceptance Criteria**:
- [x] GitHub alias normalization: strip leading `@` if present (both `@johndoe` and `johndoe` → `johndoe`)
- [x] View command: exactly one of `--user`, `--label`, `--package`, `--path` must be specified; error with clear message if zero or multiple
- [x] Add command — User+Package: error if `--owner-type` is specified
- [x] Add command — User+Label: error if `--owner-type` is missing; error if `pr-label` without `--path`
- [x] Add command — User+Path: error if `--owner-type` is missing
- [x] Add command — Label+Path: error if `--user` or `--owner-type` is specified
- [x] Remove command: same validation rules as add
- [x] All validation errors return descriptive messages indicating what was wrong and what's expected

---

### WI-11: CodeownersManagementHelper Implementation

**Description**: Create `CodeownersManagementHelper.cs` and `ICodeownersManagementHelper.cs` in `Helpers/`. This is the business logic layer with public, testable methods. Uses dependency injection for `IDevOpsService` and `ICodeownersValidatorHelper`.

**Architecture**:
- `CodeownersTool.cs` handles: input validation, parameter parsing, procedural orchestration
- `CodeownersManagementHelper.cs` handles: business logic, work item queries, relationship management
- All business methods are public and testable via the `ICodeownersManagementHelper` interface

**Acceptance Criteria**:
- [x] Interface `ICodeownersManagementHelper` defined with all public methods
- [x] Constructor takes `IDevOpsService`, `ICodeownersValidatorHelper`, `ILogger<CodeownersManagementHelper>` via DI
- [x] **View methods** (return `CodeownersViewResult`):
  - `GetViewByUserAsync(string alias, string? repo)` — queries Owner by alias, follows relations to Packages and Label Owners
  - `GetViewByLabelAsync(string label, string? repo)` — queries Label, follows relations
  - `GetViewByPathAsync(string path, string? repo)` — queries Label Owners by RepoPath
  - `GetViewByPackageAsync(string packageName)` — queries Package (latest version), shows owners/labels/label owners
- [x] **Add methods**:
  - `AddOwnerToPackageAsync(string alias, string packageName, string repo)` — validate, find/create Owner, find Package, add link
  - `AddOwnerToLabelAsync(string alias, List<string> labels, string repo, string ownerType, string? path)` — full label association flow
  - `AddOwnerToPathAsync(string alias, string repo, string path, string ownerType)` — path-based association
  - `AddLabelToPathAsync(List<string> labels, string repo, string path)` — label-to-path association
- [x] **Remove methods** (mirror add):
  - `RemoveOwnerFromPackageAsync`, `RemoveOwnerFromLabelAsync`, `RemoveOwnerFromPathAsync`, `RemoveLabelFromPathAsync`
- [x] **Shared helpers** (public for testability):
  - `FindOrCreateOwnerAsync(string alias)` — validates alias, finds or creates Owner work item
  - `FindPackageByNameAsync(string packageName)` — queries latest version
  - `FindLabelByNameAsync(string labelName)` — case-insensitive query
  - `FindOrCreateLabelOwnerAsync(string repo, string labelType, string repoPath, List<string> labels)` — finds or creates Label Owner
- [x] All owner lists sorted alphabetically in view output
- [x] Path-based Label Owners grouped by path; pathless grouped by alphabetized label set

---

### WI-12: View/Add/Remove Command Wiring in CodeownersTool.cs

**Description**: Add `view`, `add`, and `remove` subcommands to `CodeownersTool.cs`. These handle input validation and delegate business logic to `ICodeownersManagementHelper`.

**Acceptance Criteria**:
- [x] `view` subcommand registered under `config codeowners` and as MCP tool `azsdk_engsys_codeowner_view`
  - Parameters: `--user`, `--label`, `--package`, `--path` (mutually exclusive), `--repo` (optional)
  - Input validation: exactly one lookup axis; alias normalization
  - Delegates to `ICodeownersManagementHelper.GetViewBy*Async` methods
- [x] `add` subcommand registered under `config codeowners` and as MCP tool `azsdk_engsys_codeowner_add`
  - Parameters: `--repo` (required), `--user`, `--package`, `--label` (multi-value), `--path`, `--owner-type`
  - Input validation per scenario (see WI-10)
  - Delegates to `ICodeownersManagementHelper.Add*Async` methods
- [x] `remove` subcommand registered under `config codeowners` and as MCP tool `azsdk_engsys_codeowner_remove`
  - Parameters: same as add
  - Input validation: same rules as add; remove 3c requires `--owner-type`
  - Delegates to `ICodeownersManagementHelper.Remove*Async` methods
- [x] `ICodeownersManagementHelper` injected via constructor DI
- [x] No business logic in CodeownersTool.cs — only validation, parsing, and delegation

---

### WI-13: Test Scaffolding

**Description**: Create test class files with test method stubs for the management helper and command wiring.

**Acceptance Criteria**:
- [x] Test file created at `Azure.Sdk.Tools.Cli.Tests/Helpers/CodeownersManagementHelperTests.cs` with stub methods for:
  - `FindOrCreateOwner_ExistingOwner_ReturnsExisting`
  - `FindOrCreateOwner_NewOwner_CreatesAndReturns`
  - `FindOrCreateOwner_InvalidAlias_ThrowsError`
  - `AddOwnerToPackage_CreatesRelatedLink`
  - `AddOwnerToPackage_DuplicateLink_SkipsSilently`
  - `AddOwnerToLabel_ServiceOwner_CreatesRelationships`
  - `AddOwnerToLabel_PrLabel_CreatesWithPath`
  - `AddOwnerToPath_CreatesLabelOwnerAndLink`
  - `AddLabelToPath_CreatesRelationship`
  - `AddLabelToPath_LabelNotFound_ThrowsError`
  - `RemoveOwnerFromPackage_RemovesRelatedLink`
  - `RemoveOwnerFromLabel_RemovesRelatedLink`
  - `RemoveOwnerFromLabel_LastOwner_Warns`
  - `RemoveOwnerFromPath_RemovesRelatedLink`
  - `RemoveLabelFromPath_RemovesRelatedLink`
  - `GetViewByUser_ReturnsPackagesAndLabelOwners`
  - `GetViewByLabel_ReturnsPackagesAndLabelOwners`
  - `GetViewByPath_ReturnsMatchingLabelOwners`
  - `GetViewByPackage_ReturnsOwnersAndLabels`
- [x] Test file created at `Azure.Sdk.Tools.Cli.Tests/Tools/Config/CodeownersToolCommandTests.cs` with stub methods for:
  - `ViewCommand_NoAxisSpecified_ReturnsError`
  - `ViewCommand_MultipleAxesSpecified_ReturnsError`
  - `AddCommand_UserPackage_WithOwnerType_ReturnsError`
  - `AddCommand_UserLabel_MissingOwnerType_ReturnsError`
  - `AddCommand_UserLabel_PrLabel_MissingPath_ReturnsError`
  - `AddCommand_UserPath_MissingOwnerType_ReturnsError`
  - `AddCommand_LabelPath_WithUser_ReturnsError`
  - `RemoveCommand_UserPath_MissingOwnerType_ReturnsError`
- [x] All test stubs reference `WorkItemDataBuilder` from `TestHelpers` for work item state setup
- [x] Test stubs compile but are marked with `[Fact(Skip = "Not yet implemented")]` or equivalent

---

### WI-14: MCP Agent Instructions

**Description**: Document MCP tool invocation instructions for AI agents.

**Acceptance Criteria**:
- [x] Instructions documented at `eng/common/instructions/azsdk-tools/codeowners.md`
- [x] Covers general guidelines:
  - Repository name format: `Azure/azure-sdk-for-<language>`
  - GitHub alias normalization (`@alias` → `alias`)
  - Case-insensitivity for labels and packages
- [x] Covers `azsdk_engsys_codeowner_view`:
  - When to use; parameter selection rules; example invocations
  - Mutually exclusive: `--user`, `--label`, `--package`, `--path`
- [x] Covers `azsdk_engsys_codeowner_add`:
  - When to use; parameter selection rules per scenario; example invocations
  - Always requires `--repo`
- [x] Covers `azsdk_engsys_codeowner_remove`:
  - Same parameter rules as add; example invocations
- [x] Covers workflow guidance: view before modifying → modify → view to confirm → remind about render

---

## 3. Dependencies

```
WI-1  ──┐
WI-2  ──┤
WI-3  ──┤
WI-4  ──┼──→ WI-11 (management helper)
WI-5  ──┤    WI-12 (command wiring)  ──→ WI-13 (test scaffolding)
WI-6  ──┤
WI-7  ──┤
WI-8  ──┘
WI-9  ──────→ WI-11
WI-10 ──────→ WI-12
WI-14 (agent instructions) — no code dependencies, can be done in parallel
```

## 4. Design Decisions

| Decision | Resolution | Rationale |
|----------|-----------|-----------|
| Work items vs CODEOWNERS file | Work items only | Work items are the source of truth; `generate` regenerates the file |
| Auto-create Labels? | No | Labels are centrally managed; surface error if missing |
| Auto-create Owners? | Yes, after validation | Validate GitHub alias is a valid code owner first |
| Auto-create Label Owners? | Yes | Create when repo+type+label combination doesn't exist |
| Owner type for packages? | Source owner only | `--owner-type` with `--package` is an error |
| Owner type for label associations? | Required | Must be `service-owner`, `azsdk-owner`, or `pr-label` |
| Owner type for path associations? | Required | Must be specified; no default |
| Default for --owner-type on paths? | No default; error if omitted | User must explicitly choose the type |
| Dependency on Get-AllPkgProperties.ps1? | None | No package path validation against repo files |
| Label/package case sensitivity? | Case-insensitive | Both labels and packages are matched case-insensitively |
