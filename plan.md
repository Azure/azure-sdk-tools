# Codeowners Release Gate Tool — Implementation Plan

## Problem
We need a new CLI command in `CodeownersTool.cs` that acts as a **release gate** — verifying that a package has sufficient codeowners before allowing a release to proceed. No MCP bindings.

## Parameters
- `--package` (required): The package name to check (e.g., `Azure.AI.FormRecognizer`)
- `--repo` (optional): Repository in `owner/repo` format (e.g., `Azure/azure-sdk-for-python`). If omitted, inferred from the current git context via `IGitHelper`.
- `--package-directory` (required): Path from the repo root to the package (e.g., `sdk/formrecognizer/Azure.AI.FormRecognizer`). Used for glob matching against LabelOwner `RepoPath`.

## Algorithm
1. **Resolve language/repo**: Infer from current git context if `--repo` not provided. Convert repo name to language string via `SdkLanguageHelpers`.
2. **Find the latest package work item**: Query ADO for `Package` work items matching `--package` + language, pick latest version (via `GetLatestPackageVersions`).
3. **Hydrate the package**: Fetch related Owner work items for the package. Expand GitHub team owners (e.g., `Azure/team-name`) to individual members via `ITeamUserCache`.
4. **Check package-level owners**:
   - If **≥ 2 owners**: **PASS** (exit 0)
5. **If <2 owners on package — collect LabelOwners**:
   - Query all `LabelOwner` work items where `Repository` matches the inferred repo and `RepoPath` is not empty.
   - Hydrate LabelOwner owners (including expanding GitHub teams like `Azure/team-name` to individual members via `ITeamUserCache`).
   - Sort by `RepoPath` ascending.
   - Find all the LabelOwner objects whose `RepoPath` glob-matches the `--package-directory`.
6. **Check LabelOwner-level owners plus Package-level owners meets requirements**:
   - Collect all unique owners from the package and the matching LabelOwner(s).
   - If unique owners ≥ 2: **PASS** (exit 0)
   - If **no matching LabelOwner** found: **FAIL** (exit 1)
1. **Error cases**:
   - Package work item not found: **FAIL** (exit 1) with a message indicating that a package work item could not be found for the specified package and repo/language.

## Implementation Todos

### 1. Add helper method to `CodeownersManagementHelper`
- New method: `CheckReleaseGateAsync(string packageName, string repo, string packageDirectory)`
- Reuse existing `FindPackageByName`
- Add a new method to query all LabelOwners by repo (similar to `QueryLabelOwnersByPath` but without path filter)
- Hydrate owners on the found package and/or LabelOwner
- Perform glob matching using `DirectoryUtils.PathExpressionMatchesTargetPath` from CodeownersUtils (consistent with existing CODEOWNERS path matching)
- Return a result object with pass/fail status and descriptive message

### 2. Add interface method to `ICodeownersManagementHelper`
- Declare `CheckReleaseGateAsync` in the interface

### 3. Add the CLI command to `CodeownersTool.cs`
- New command name: `"release-gate"` with description like "Check codeowners release gate for a package"
- Options: `--package` (required), `--repo` (optional), `--package-directory` (required)
- Wire into `HandleCommand` and `GetCommands`
- No `[McpServerTool]` attribute — CLI only
- Use a `CheckReleaseGate` method in `CodeownersTool` to validate inputs, call `CheckReleaseGateAsync` on the helper, and return results

### 4. Add unit tests
- Test: package with ≥ 2 owners → pass
- Test: package with 0 owners, matching LabelOwner with ≥ 2 owners → pass
- Test: package with 0 owners, matching LabelOwner with ≤ 1 owner → fail
- Test: package with 0 owners, no matching LabelOwner → fail
- Test: package with 1 owner and LabelOwner with 1 owner (same owner) → fail (only 1 unique owner)
- Test: package with 1 owner and LabelOwner with 1 owner (different owners) → pass (2 unique owners)
- Test: package not found → fail with error message
- Test: glob matching picks the most-specific (last sorted) LabelOwner
- Test: LabelOwner filtering by LabelType (empty and "PR Label" included, others excluded)
- Test: GitHub team owner (e.g., `Azure/team-name`) is expanded via cache and expanded members count toward the ≥ 2 threshold

## Key Design Decisions
- **Glob matching**: Use `DirectoryUtils.PathExpressionMatchesTargetPath` from `Azure.Sdk.Tools.CodeownersUtils` (which internally uses `Microsoft.Extensions.FileSystemGlobbing.Matcher` with `StringComparison.Ordinal`). The `RepoPath` on LabelOwner is the glob pattern; `--package-directory` is the target path. This ensures consistency with how CODEOWNERS path expressions are matched elsewhere in the tooling.
- **Team expansion**: GitHub team owners (of the form `Azure/team-name`) are expanded to individual members using `ITeamUserCache` (via the existing `ExpandTeamOwners` helper). When counting owners, expanded individual members are counted (e.g., 1 team with 3 members = 3 owners toward the ≥ 2 threshold).
- **Owner uniqueness**: Only **unique** owners are counted. If the same individual appears both directly and via team expansion, they count once. Deduplication is by GitHub alias (case-insensitive).
- **Sorting**: Sort LabelOwners by `RepoPath` ascending, then iterate backwards. The last match wins (most specific path).
- **No MCP bindings**: This command is CLI-only, no `[McpServerTool]` attribute.
- **Repo inference**: Use `IGitHelper.GetRepoNameAsync` → `SdkLanguageHelpers.GetLanguageForRepo` to build the full repo identifier for ADO queries.
