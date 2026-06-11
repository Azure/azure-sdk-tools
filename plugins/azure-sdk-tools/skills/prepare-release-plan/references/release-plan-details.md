# Release Plan Detailed Steps

> **CRITICAL**: Do not mention or display Azure DevOps work item links/URLs. Only provide Release Plan Link and Release Plan ID to the user. All manual updates must be made through the Release Planner Tool (https://aka.ms/sdk-release-planner).

## Release Plan ID vs Work Item ID

A release plan has two distinct identifiers and they are **not** interchangeable:

- **Release Plan ID**: the value users typically refer to (e.g. in a prompt).
- **Work Item ID**: the Azure DevOps work item backing the release plan, used by the MCP tools.

The tools that update a release plan, update SDK details, run SDK generation, and
link an SDK PR need the **work item ID** (parameter `workItemId` or
`releasePlanWorkItemId`). Never pass the Release Plan ID into these parameters.

When you only have a Release Plan ID (or a TypeSpec project path / spec PR),
always run `azsdk_get_release_plan` first and read the `workItemId` field from
the returned plan, then pass that value to the subsequent tool calls.

## Required Information

Collect these details (do not use temporary values):

- **Service Tree ID**: GUID format - confirm with user
- **Product Service Tree ID**: GUID format - confirm with user
- **Expected Release Timeline**: "Month YYYY" format
- **SDK Release Type**: "beta" (preview) or "stable" (GA)

## SDK Details Update

To update SDK details in the release plan:

- First resolve the work item ID via `azsdk_get_release_plan` if you only have a Release Plan ID.
- Run `azsdk_update_sdk_details_in_release_plan` with the release plan **work item ID** (`releasePlanWorkItemId`, not the Release Plan ID) and TypeSpec project path.

## Namespace Approval (Management Plane Only)

For first release of management plane SDK:

1. Check if namespace approval issue already exists
2. If not, collect GitHub issue from Azure/azure-sdk repo
3. Run `azsdk_link_namespace_approval_issue`

## Linking SDK Pull Requests

If SDK PRs exist:

1. Ensure GitHub CLI authentication (`gh auth login`)
2. Resolve the work item ID via `azsdk_get_release_plan` if you only have a Release Plan ID.
3. Run `azsdk_link_sdk_pull_request_to_release_plan` for each PR, passing the work item ID as `workItemId` (or the Release Plan ID as `releasePlanId`) — never the Release Plan ID as `workItemId`.