# Plan: `check-package-owners` Command

## Problem Statement

The spec requires: "PRs and releases are blocked for packages that do not have owner information." We need a CLI command that validates a package's codeowners data in Azure DevOps and returns a pass/fail result usable by CI pipelines to block PRs/releases.

## Command

```bash
azsdk config codeowners check-package-owners --package <name> --directory-path <relative-path> [--repo <owner/repo>]
```

- `--package` (required): Package name (maps to `Custom.Package` work item field).
- `--directory-path` (required): Relative path from repo root to the package directory (e.g., `sdk/contoso/Azure.Contoso.WidgetManager`).
- `--repo` (optional): Repository in `owner/repo` format. Auto-resolved from git if omitted.
- MCP tool: `azsdk_engsys_codeowner_check_package_owners`

## Validation Rules

Two paths to validity — **Package-level** (primary) and **Path-based fallback**:

### Primary Path: Package-Level Ownership

If the Package work item has sufficient direct owners, use this path. All three checks must pass:

1. **Package Owners ≥ 2 unique individuals**: The Package work item must have at least 2 unique individual GitHub users as owners. Teams are expanded via `ITeamUserCache`. If the package has 1 or more owners but fewer than 2 unique individuals, the check **fails** (no fallback).
2. **Has PR Label**: The Package work item must have at least 1 linked Label.
3. **Service Owners ≥ 2 unique individuals**: There must exist a `Label Owner` work item of type `Service Owner` in the same repo whose linked labels are a **superset** of all the package's PR labels, and whose owners (after team expansion) include at least 2 unique individuals.

If Service Owner coverage is split across multiple `Label Owner` records such that rendering or human inspection might imply equivalent coverage, but no **single** `Service Owner` record has labels covering the full required set, the check **fails**. Cleanup/canonicalization of that data shape is out of scope for this command and belongs in a future validator feature.

### Fallback Path: Path-Based Label Owner Ownership

If the package has **zero direct owners**, fall back to checking Label Owners whose `RepoPath` glob expression matches the package's `directoryPath`. Glob matching uses `DirectoryUtils.PathExpressionMatchesTargetPath` from `Azure.Sdk.Tools.CodeownersUtils`.

Before path comparison, normalize both `Label Owner`.`RepoPath` and `directoryPath` to a single leading-slash form (for example, `sdk/contoso` -> `/sdk/contoso`).

The check proceeds in two steps for **at least one label**:

1. **PR Label owners ≥ 2 unique individuals**: Find `Label Owner` work items of type `PR Label` whose `RepoPath` glob matches `directoryPath`. If multiple PR Label Label Owners match, sort them by normalized `RepoPath` and choose the last matching `Label Owner` as the required PR Label source. That selected Label Owner must have ≥ 2 unique individuals (after team expansion), and only that selected Label Owner's linked labels become the required PR label set.
2. **Service Owners ≥ 2 unique individuals with matching labels**: Find a `Label Owner` work item of type `Service Owner` whose linked labels are a **superset** of the required PR label set from the selected PR Label Label Owner (no path restriction — can be pathless or have any path). That Service Owner must have ≥ 2 unique individuals (after team expansion).

The PR Label and Service Owner sets **can be the same people**. If the fallback path succeeds (matching PR Label owners found with a label satisfies the "has PR label" requirement too).

### Summary Decision Tree

```
Package has any direct owners?
  YES (≥ 1) → Check ≥ 2 unique individuals (fail if < 2, no fallback)
               → Check PR label(s) exist on Package
               → Check a single Service Owner Label Owner exists in repo
                 whose labels ⊇ all PR labels AND has ≥ 2 unique individuals
  NO (0)   → Find PR Label type Label Owners where RepoPath glob matches directoryPath
              → Sort matching PR Label Label Owners by normalized path and choose the last one
              → Use that selected PR Label Label Owner's labels as the required PR label set
              → Check selected PR Label owners have ≥ 2 unique individuals
              → Check a single Service Owner Label Owner exists
                whose labels ⊇ the selected PR Label Label Owner's labels AND has ≥ 2 unique individuals
                (Service Owners do NOT need to match the path)
```

Team expansion uses the existing `ITeamUserCache` infrastructure.

> **Note:** All ownership data is determined from the Azure DevOps work item data model (Package, Owner, Label, Label Owner work items and their relations), not from parsing the CODEOWNERS file directly. See [Spec: Codeowners Management](docs/specs/7-operations-codeowners-management.spec.md) for the full data model definition.

## Proposed Approach

### 1. Response Model — `CheckPackageOwnersResponse`

New file: `Models/Responses/Codeowners/CheckPackageOwnersResponse.cs`

A structured response inheriting from `CommandResponse` with:
- `PackageName` (string)
- `DirectoryPath` (string)
- `Repo` (string)
- `ValidationPath` (string) — `"Package"` or `"PathFallback"` indicating which path was used
- `OwnerCheck` — sub-object: `{ passed, required, actual, owners[] }`
- `PrLabelCheck` — sub-object: `{ passed, labels[] }` (only in Package path)
- `ServiceOwnerCheck` — sub-object: `{ passed, required, actual, owners[], requiredLabels[], matchedLabels[] }` (primary and fallback paths)
- `PathFallbackCheck` — sub-object: `{ prLabelOwnerCheck: { passed, required, actual, owners[], labels[] }, serviceOwnerCheck: { passed, required, actual, owners[], requiredLabels[], matchedLabels[] } }` (only in fallback path)
- `AllPassed` (bool) — summary

Prefer shared nested check models to avoid duplicating the same payload shape in multiple places, but only collapse fields if the resulting response stays straightforward to read in both JSON and formatted text output.

Exit code: 0 if all pass, 1 if any fail.

Formatted text output shows each check with PASS/FAIL and details.

### 2. Helper Method — `ICodeownersManagementHelper.CheckPackageOwners`

New method on the existing interface + implementation in `CodeownersManagementHelper`:

```csharp
Task<CheckPackageOwnersResponse> CheckPackageOwners(
    string packageName, string directoryPath, string repo, CancellationToken ct);
```

Logic:
1. Look up Package work items by name + repo (consistent with existing `FindPackageByName` logic). If multiple matches exist, select the latest version using `Custom.PackageVersionMajorMinor` via `WorkItemMappers.GetLatestPackageVersions`.
2. If not found, return error response.
3. Hydrate the package to get owners and labels.
4. **Owner check**: Expand team owners via `ITeamUserCache`, collect distinct individual aliases, check count ≥ 2.
5. **If owners ≥ 1 (primary path — fail if < 2, no fallback)**:
   - **PR Label check**: Check that `package.Labels.Count >= 1`.
   - **Service Owner check**: Query `Label Owner` work items of type `Service Owner` in the repo. Find one whose linked labels are a superset of all the package's PR labels. Hydrate, expand teams, check ≥ 2 unique individuals. Do not aggregate coverage across multiple Service Owner records.
6. **If owners == 0 (fallback path)**:
   - Query `Label Owner` work items of type `PR Label` in the repo. Filter to those whose `RepoPath` glob matches `directoryPath` using `DirectoryUtils.PathExpressionMatchesTargetPath`.
   - Sort the matching PR Label Label Owners by normalized `RepoPath` and choose the last matching Label Owner.
   - Hydrate the selected PR Label Label Owner to get its owners and labels. Expand teams, check ≥ 2 unique individuals.
   - Use the selected PR Label Label Owner's linked labels as the required PR label set. Do not union labels across multiple matching PR Label Label Owners.
   - Query `Label Owner` work items of type `Service Owner` in the repo. Find one whose linked labels are a superset of the required PR label set (no path restriction). Hydrate, expand teams, check ≥ 2 unique individuals. Do not aggregate coverage across multiple Service Owner records.
   - If both checks pass, the fallback passes.

### 3. Command Registration in `CodeownersTool`

Add to `GetCommands()`:
- New command `check-package-owners` with a new required `checkPackageOption`, a new required `directoryPathOption`, and `optionalRepoOption`.

Add to `HandleCommand()`:
- Route to new `CheckPackageOwners` method that calls `ResolveRepo` then delegates to the helper.

### 4. `checkPackageOption` and `directoryPath` option

New required option in `CodeownersTool` for this command only:
```csharp
private readonly Option<string> checkPackageOption = new("--package")
{
    Description = "Package name",
    Required = true,
};
```

New option in `CodeownersTool`:
```csharp
private readonly Option<string> directoryPathOption = new("--directory-path")
{
    Description = "Relative path to the package directory from the repo root",
    Required = true,
};
```

## File Changes

| File | Change |
|------|--------|
| `Models/Responses/Codeowners/CheckPackageOwnersResponse.cs` | **New** — Response model |
| `Helpers/ICodeownersManagementHelper.cs` | Add `CheckPackageOwners` method to interface |
| `Helpers/CodeownersManagementHelper.cs` | Implement `CheckPackageOwners` |
| `Tools/Config/CodeownersTool.cs` | Add command, option, routing, and public method |
| `Tests/Helpers/CodeownersManagementHelperTests.cs` | Helper-level tests |

## Testing Strategy

### Unit Tests — Helper Level (`CodeownersManagementHelperTests.cs`)

Tests for `CheckPackageOwners` with mocked `IDevOpsService` and `ITeamUserCache`:

**Primary Path Tests:**

| # | Test | Scenario |
|---|------|----------|
| 1 | `CheckPackageOwners_AllChecksPass` | Package has 2+ owners, 1+ label, and one Service Owner Label Owner whose labels ⊇ all package PR labels and whose owners expand to ≥ 2 individuals |
| 2 | `CheckPackageOwners_PackageNotFound_ReturnsError` | No matching package work item |
| 3 | `CheckPackageOwners_InsufficientOwners_Fails` | Package has 1 owner (< 2 unique individuals) → fails immediately, no fallback |
| 4 | `CheckPackageOwners_NoOwners_TriggersPathFallback` | Package has 0 owners → falls to path-based check |
| 5 | `CheckPackageOwners_NoPrLabel_Fails` | Package has 2+ owners but no labels → PrLabelCheck fails |
| 6 | `CheckPackageOwners_InsufficientServiceOwners_Fails` | Matching Service Owner Label Owner has only 1 individual |
| 7 | `CheckPackageOwners_NoServiceOwners_Fails` | No Service Owner Label Owner has labels ⊇ package's PR labels |
| 8 | `CheckPackageOwners_TeamExpansion_CountsIndividuals` | 1 team owner with 3 members satisfies 2-owner requirement |
| 9 | `CheckPackageOwners_TeamExpansion_ServiceOwners` | 1 team-type service owner with 3 members satisfies 2-service-owner requirement |
| 10 | `CheckPackageOwners_MultipleLabels_ServiceOwnerSupersetRequired` | Package has 2 PR labels; Service Owner must have both labels on a single Label Owner record to pass |
| 11 | `CheckPackageOwners_FragmentedServiceOwners_Fails` | Multiple Service Owner Label Owner records collectively cover the labels, but no single record has the full label superset → fails |
| 12 | `CheckPackageOwners_MultiplePackageVersions_UsesLatestByName` | Multiple Package work items match by name; helper selects the one with the latest `Custom.PackageVersionMajorMinor` |
| 13 | `CheckPackageOwners_OverlappingOwners_Deduplication` | Same user in team + individual doesn't double-count |
**Path-Based Fallback Tests:**

| # | Test | Scenario |
|---|------|----------|
| 14 | `CheckPackageOwners_Fallback_BothPrLabelAndServiceOwnerPass` | No package owners, but the selected path-matching PR Label Label Owner and a matching Service Owner Label Owner each have ≥ 2 individuals → passes |
| 15 | `CheckPackageOwners_Fallback_PrLabelOwnersInsufficient` | A PR Label path match is selected, but its owners expand to < 2 individuals → fails |
| 16 | `CheckPackageOwners_Fallback_ServiceOwnersInsufficient` | The selected PR Label path match is valid, but the matching single Service Owner record has < 2 individuals → fails |
| 17 | `CheckPackageOwners_Fallback_NoMatchingPaths` | No PR Label type Label Owners have paths matching directoryPath → fails |
| 18 | `CheckPackageOwners_Fallback_GlobMatch` | Label Owner path is a glob (e.g., `/sdk/contoso/`) that matches `sdk/contoso/Azure.Contoso.WidgetManager` |
| 19 | `CheckPackageOwners_Fallback_MultipleMatchingPrLabelOwners_UsesLastByPath` | Multiple PR Label Label Owners match the path; helper sorts them by normalized path, chooses the last one, and uses only that record's labels |
| 20 | `CheckPackageOwners_Fallback_MultipleLabels_ServiceOwnerSupersetRequired` | The selected PR Label Label Owner has labels ["A", "B"]; Service Owner must have both labels on a single Label Owner record to pass |
| 21 | `CheckPackageOwners_Fallback_FragmentedServiceOwners_Fails` | Multiple Service Owner Label Owner records collectively cover the labels from the selected PR Label Label Owner, but no single record has the full label superset → fails |
| 22 | `CheckPackageOwners_Fallback_SameOwnersForBothTypes` | Same individuals are PR Label owners and Service Owners → passes |
| 23 | `CheckPackageOwners_Fallback_ServiceOwnerPathless` | Service Owner for the selected PR Label Label Owner's label set has no RepoPath (pathless) but still satisfies the check → passes |

## Notes

- The command has both CLI and MCP bindings (`[McpServerTool]` with name `azsdk_engsys_codeowner_check_package_owners`).
- Team expansion reuses the existing `ITeamUserCache` / `ExpandTeamOwners` pattern already in `CodeownersManagementHelper`.
- Package lookup for this command is by package name + repo only.
- The `directoryPath` parameter is used as the target path for glob matching against Label Owner `RepoPath` in the path-based fallback.
- When multiple Package work items match by package name + repo, resolve to the latest package version using `Custom.PackageVersionMajorMinor`, consistent with existing package lookup behavior.
- Normalize `directoryPath` and Label Owner `RepoPath` to a consistent leading-slash form before glob matching.
- When multiple PR Label Label Owners match the fallback path, sort them by normalized path and use the last matching Label Owner as the required PR Label source rather than unioning labels across all matches.
- Glob matching uses `DirectoryUtils.PathExpressionMatchesTargetPath` from the existing `Azure.Sdk.Tools.CodeownersUtils` project reference.
- Service Owner coverage is intentionally evaluated against a single `Label Owner` record with a label superset; cleanup of fragmented-but-equivalent Label Owner data is future validator work rather than part of this command.
- Exit code behavior (0 = pass, 1 = fail) makes this directly usable as a CI gate step.
- When the primary path is used (package has ≥ 2 owners), the response includes `ValidationPath = "Package"`. When the fallback is used, `ValidationPath = "PathFallback"`.
