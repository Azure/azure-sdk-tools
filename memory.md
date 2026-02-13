# Memory — CODEOWNERS Management Commands Implementation

## Work Completed

### WI-1 through WI-8: DevOps Service Methods
- Added 9 new methods to `IDevOpsService` and `DevOpsService`:
  - `GetOwnerByGitHubAliasAsync` — WIQL query by `Custom.GitHubAlias`
  - `CreateOwnerWorkItemAsync` — idempotent, checks existing first
  - `GetLabelOwnersByRepoAndPathAsync` — queries by `Custom.Repository` + `Custom.RepoPath`
  - `GetLabelOwnersByRepoAsync` — all Label Owners for a repo
  - `CreateLabelOwnerWorkItemAsync` — creates with title pattern `Label Owner: <repo> - <type> - <labels>`
  - `AddRelatedLinkAsync` — idempotent, skips if link exists
  - `RemoveRelatedLinkAsync` — idempotent, skips if no link
  - `GetPackageByNameAsync` — returns latest version via `GetLatestPackageVersions`
  - `GetLabelByNameAsync` — queries by `Custom.Label`
- All queries escape single quotes in user input with `Replace("'", "''")`
- All queries scope to `Constants.AZURE_SDK_DEVOPS_RELEASE_PROJECT`

### WI-9: Response Models
- `CodeownersViewResult` extends `CommandResponse` with `Packages`, `PathBasedLabelOwners`, `PathlessLabelOwners`, `Message`
- `PackageViewItem`, `LabelOwnerGroup`, `LabelOwnerViewItem` — flat JSON-serializable models
- All in `Azure.Sdk.Tools.Cli.Models.Codeowners` namespace

### WI-10 & WI-12: Validation and Command Wiring
- Added `view`, `add`, `remove` subcommands to `CodeownersTool.cs`
- MCP tools: `azsdk_engsys_codeowner_view`, `azsdk_engsys_codeowner_add`, `azsdk_engsys_codeowner_remove`
- Input validation: mutual exclusivity checks, required parameter validation, alias normalization
- Constructor updated to accept `ICodeownersManagementHelper`
- Existing tests updated to pass new constructor parameter

### WI-11: CodeownersManagementHelper
- Interface `ICodeownersManagementHelper` with 12 public methods (4 view, 4 add, 4 remove)
- Implementation uses DI for `IDevOpsService`, `ICodeownersValidatorHelper`, `ILogger`
- View methods: query owner/label/path/package and follow relationships
- Add methods: find-or-create patterns for Owner and LabelOwner work items
- Remove methods: find and remove related links
- Hydration helpers fetch related work items in batch via WIQL `WHERE [System.Id] IN (...)`
- Registered as singleton in `ServiceRegistrations.cs`

### WI-13: Test Scaffolding
- `CodeownersManagementHelperTests.cs` — 19 `[Ignore]` test stubs
- `CodeownersToolCommandTests.cs` — 11 implemented validation tests

### WI-14: MCP Agent Instructions
- `eng/common/instructions/azsdk-tools/codeowners.md` — covers view/add/remove tools, parameter rules, workflow guidance

## Key Patterns

- CodeownersTool constructor requires: `IGitHubService`, `ILogger<CodeownersTool>`, `ILoggerFactory?`, `ICodeownersValidatorHelper`, `ICodeownersRenderHelper`, `ICodeownersManagementHelper`
- Work item types in DevOps: "Owner", "Package", "Label", "Label Owner"
- `WorkItemMappers` static methods for mapping DevOps `WorkItem` → domain models
- `WorkItemData.HydrateRelationships()` populates Owner/Label/LabelOwner references
- Related links use `System.LinkTypes.Related` relation type
- Test project uses NUnit + Moq + `TestLogger<T>` + `WorkItemDataBuilder`
