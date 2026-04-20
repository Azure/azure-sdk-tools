# AUD-OWN-004: MSFT Identity Missing

## Summary
Populate the MSFT Identity field on Owner work items by looking up the GitHub alias in the
Open Source Portal's people links API.

## Criteria
1. Fetch all individual (non-team) Owner work items.
2. Check if the MSFT Identity field (`Custom.MicrosoftIdentity`) is empty/null.
3. Flag each Owner with a missing MSFT Identity.

## Fix (`--fix`)
- Fetch the full GitHub → AAD mapping from `repos.opensource.microsoft.com/api/people/links`.
- For each Owner with missing identity:
  - Look up `GitHubAlias` in the people links cache.
  - If found, set the `Custom.MicrosoftIdentity` field to the AAD identity descriptor
    (this is an Identity field in ADO, requiring the proper serialization format).
  - Log: `"Set MSFT Identity for owner '{alias}' to '{upn}'"`.
  - If not found, log a warning (owner may be an external contributor or service account).

## New Field Required
- `Custom.MicrosoftIdentity` (Identity type) must exist on the Owner work item type in ADO.
- `OwnerWorkItem.cs` model must be extended with a `[FieldName("Custom.MicrosoftIdentity")]`
  property.

## Prerequisite: ADO Identity Spike

**This rule has a prerequisite spike that must be completed before implementation.**

See "AUD-OWN-004: MSFT Identity Spike (Prerequisite)" in `plan.md`.

Key questions to resolve:
1. Can `Custom.MicrosoftIdentity` accept a UPN string via PATCH, or does it need an
   `IdentityRef` with descriptor?
2. Does `IDevOpsService.UpdateWorkItemAsync` need a non-string overload?
3. If descriptor is required, use `GraphHttpClient.CreateUserAsync` from identity-resolution's
   `AzureDevOpsService.cs`.

## Implementation Notes

### Project Dependency
Add a `<ProjectReference>` from `Azure.Sdk.Tools.Cli.csproj` to
`tools/identity-resolution/identity-resolution.csproj`. This provides direct access to
`GitHubToAADConverter` and the `UserLink`/`AadUserDetail` models without duplicating code.

**Test impact**: This reference makes identity-resolution a build-time dependency of azsdk-cli.
Changes to identity-resolution now affect 3 consumers:
- `azsdk-cli` (new)
- `pipeline-owners-extractor` (existing)
- `notification-creator` (existing)

### Identity Field Serialization
Resolution depends on spike results. Two paths:
- **Path A (UPN string accepted)**: Set `Custom.MicrosoftIdentity` directly as UPN string
  via existing `UpdateWorkItemAsync`.
- **Path B (descriptor required)**: Use `GraphHttpClient` to resolve UPN → descriptor,
  extend `UpdateWorkItemAsync` with non-string overload.

### Using GitHubToAADConverter
- Inject `GitHubToAADConverter` (from `identity-resolution` project) into the audit helper.
- Call `EnsureCacheExistsAsync()` once to load the full GitHub → AAD mapping.
- Call `GetUserPrincipalNameFromGithubAsync(alias)` for each Owner with missing identity.
- Auth scope: `2efaf292-00a0-426c-ba7d-f5d2b214b8fc/.default` (Open Source Portal API).
- The converter returns `Aad.UserPrincipalName`; the full `UserLink` also has `Aad.Alias`,
  `Aad.PreferredName`, `Aad.Id`, `Aad.EmailAddress`.

## Dependencies
- `identity-resolution` project reference (provides `GitHubToAADConverter`).
- ADO work item update API (existing `IDevOpsService`).
- `Custom.MicrosoftIdentity` ADO field must be provisioned on the Owner work item type.
