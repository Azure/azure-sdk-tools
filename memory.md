# CODEOWNERS Management Commands — Implementation Notes

## Architecture

### New Files Created
- `Azure.Sdk.Tools.Cli/Helpers/ICodeownersManagementHelper.cs` — Interface for business logic
- `Azure.Sdk.Tools.Cli/Helpers/CodeownersManagementHelper.cs` — Full implementation
- `Azure.Sdk.Tools.Cli/Models/Codeowners/CodeownersViewResult.cs` — View output models
- `Azure.Sdk.Tools.Cli.Tests/Helpers/CodeownersManagementHelperTests.cs` — Test scaffolding
- `Azure.Sdk.Tools.Cli.Tests/Tools/Config/CodeownersToolCommandTests.cs` — Validation test scaffolding
- `eng/common/instructions/azsdk-tools/codeowners.md` — MCP agent instructions

### Modified Files
- `Azure.Sdk.Tools.Cli/Services/DevOpsService.cs` — Added 4 generic methods to `IDevOpsService` and `DevOpsService`:
  - `QueryWorkItemsByTypeAndFieldAsync` — Generic WIQL query by type+field
  - `CreateTypedWorkItemAsync` — Create any work item type with arbitrary fields
  - `AddRelatedLinkAsync` — Idempotent "Related" link creation
  - `RemoveRelatedLinkAsync` — Idempotent "Related" link removal
- `Azure.Sdk.Tools.Cli/Services/ServiceRegistrations.cs` — Registered `ICodeownersManagementHelper`
- `Azure.Sdk.Tools.Cli/Tools/Config/CodeownersTool.cs` — Added view/add/remove commands with MCP tools
- `Azure.Sdk.Tools.Cli.Tests/Tools/Config/CodeownersToolsTests.cs` — Updated constructor calls

## Design Decisions

1. **Generic DevOps methods**: Rather than per-type methods (e.g., `GetOwnerByAliasAsync`, `GetPackageByNameAsync`), we added generic `QueryWorkItemsByTypeAndFieldAsync` and `CreateTypedWorkItemAsync` methods. This minimizes IDevOpsService surface area while supporting all needed operations.

2. **Business logic in helper**: All work item query logic, relationship management, and view result building lives in `CodeownersManagementHelper`. `CodeownersTool.cs` only handles input validation and delegation.

3. **Owner type mapping**: CLI uses `service-owner`, `azsdk-owner`, `pr-label` (kebab-case). These map to DevOps field values `Service Owner`, `Azure SDK Owner`, `PR Label`.

4. **Constructor change**: `CodeownersTool` constructor now takes `ICodeownersManagementHelper` as the last parameter. All test instantiations were updated.

5. **Code style**: The project enforces `IDE0011` (braces required on all if/else statements). No single-line if statements allowed.

## Known Considerations

- Test stubs are marked with `[Ignore]` — they need full implementation with DevOps mock setup
- The `QueryPackagesAsync` method in the helper fetches all packages, which could be slow for large datasets. A targeted query would be better for production use.
- View hydration queries all owners and labels separately. This could be optimized with batch fetching.
