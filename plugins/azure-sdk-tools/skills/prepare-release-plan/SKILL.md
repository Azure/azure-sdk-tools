---
name: prepare-release-plan
license: MIT
metadata:
  version: "1.0.0"
  distribution: shared
description: 'Manage Azure SDK release plans. WHEN: "create release plan", "get release plan", "update release plan", "update API spec in release plan", "update SDK details in release plan", "update target release month", "abandon release plan", "link SDK PR to plan", "namespace approval", "check release plan status".'
compatibility: "azure-sdk-mcp server, API spec PR in Azure/azure-rest-api-specs"
---

# Prepare Release Plan

Create, get, update, abandon, and link SDK PRs to release plans.

## Triggers

USE FOR: create release plan, get release plan, update release plan, update API spec in release plan, update SDK details in release plan, abandon release plan, link SDK PR to plan, namespace approval, check release plan status
WHEN: "create release plan", "get release plan", "update release plan", "abandon release plan", "link SDK PR to plan", "namespace approval", "check release plan status"
DO NOT USE FOR: SDK code generation, pipeline troubleshooting, API review feedback

## Rules

- Do not display Azure DevOps work item URLs; only provide the Release Plan Link and ID.
- Public Preview and GA spec PRs must be in `Azure/azure-rest-api-specs`; Private Preview spec PRs must be in `Azure/azure-rest-api-specs-pr`.
- Create and metadata updates derive the API version and packages automatically. `apiVersion` is a lookup selector only, never an input to create, metadata update, or spec-PR update.
- Public spec pinning uses the PR's source HEAD when open or merge SHA when merged. Optional `specCommitSha` defaults to that commit and must match it when supplied.
- Public pinning requires a clean local TypeSpec checkout at that commit. Never switch, reset, or stash user files automatically. Valid metadata may report no single API version or no emitters; leave the version unset rather than asking the user to choose one. Compilation failures still stop the update.
- Create and metadata/spec-PR updates each use one call once required inputs are known, without a target approval roundtrip.
- After a successful write, report the saved API version, spec commit, SDK release type, and package names returned by the tool, alongside the plan link and ID. Do not describe only the target month.
- Reusing create leaves the existing target unchanged; advancing it requires a separate update. Never change a Finished, Abandoned, Closed, or Duplicate plan's target. A different project or API version requires a separate plan. No-PR tracking plans and private previews remain unpinned.
- Metadata and spec-PR updates require the exact Azure DevOps `workItemId`, not a Release Plan ID. Look up other selectors first and use the returned `WorkItemId`; never fall back to project-only resolution inside a write. Reuse a known work item ID without asking again.
- Always relay schedule-risk `warnings` and `next_steps` returned by release plan tools. For each past-due plan, show its Release Plan ID and dashboard link, then present both choices: update its target release month or abandon it and record the reason in the dashboard.

## MCP Tools

| Tool                                                               | Purpose                            |
| ------------------------------------------------------------------ | ---------------------------------- |
| `azure-sdk-mcp:azsdk_create_release_plan`                          | Create a new release plan          |
| `azure-sdk-mcp:azsdk_get_release_plan`                             | Get plan by ID, path, or spec PR   |
| `azure-sdk-mcp:azsdk_update_release_plan`                          | Update release plan metadata       |
| `azure-sdk-mcp:azsdk_update_release_plan_target`                   | Update the target release month    |
| `azure-sdk-mcp:azsdk_update_api_spec_pull_request_in_release_plan` | Update API spec PR URL in plan     |
| `azure-sdk-mcp:azsdk_update_sdk_details_in_release_plan`           | Update SDK/package details in plan |
| `azure-sdk-mcp:azsdk_abandon_release_plan`                         | Abandon a release plan             |
| `azure-sdk-mcp:azsdk_link_sdk_pull_request_to_release_plan`        | Link SDK PR to release plan        |
| `azure-sdk-mcp:azsdk_link_namespace_approval_issue`                | Link namespace approval issue      |

---

## Use Cases

### 1. Create Release Plan

**When**: User wants to create a release plan for a TypeSpec project.

**Steps**:

1. **Locate Project** — Obtain the local TypeSpec project directory. Relative paths work from the specs checkout; otherwise use an absolute local path.
2. **Gather Info** — Collect only missing details:
   - Target release month/year (format: "Month YYYY", e.g. "June 2026"). Do NOT use formats like "2026-06" or "06/2026" — these are invalid.
   - API release type: Value must be one of the following: "Private Preview", "Public Preview", or "GA"
   - Spec PR URL (optional)
   - Service Tree ID (GUID) — optional if previously created
   - Product Tree ID (GUID) — optional if previously created
3. **Create** — Run `azure-sdk-mcp:azsdk_create_release_plan` once. It derives API version and packages from metadata, and SDK type from API release type (preview → beta, GA → stable). Do not pass `sdkReleaseType`. For public PRs, `specCommitSha` is optional and defaults from the PR.
4. **Reuse Safely** — Let create resolve reuse by project, metadata API version, and API release type. Do not use a project-only lookup or force-create mode. A returned existing plan keeps its target; use a separate update if advancement was requested.
5. **Namespace** — For first management plane releases, link namespace approval issue using `azure-sdk-mcp:azsdk_link_namespace_approval_issue`.

> **IMPORTANT**: Do not change an existing plan's API release type, API version, or project to represent a separate release. Use create for that release. Do not generate or publish SDKs unless separately requested.

**Tool**: `azure-sdk-mcp:azsdk_create_release_plan`

---

### 2. Get Release Plan

**When**: User wants to check the status or details of an existing release plan.

**Steps**:

1. **Identify Plan** — Use an identifier already in context, or obtain one:
   - Azure DevOps work item ID in `workItemId`
   - Release Plan ID in `releasePlanId`
   - Spec PR URL in `specPullRequestUrl`
   - Relative `typeSpecProjectPath` with `apiVersion` and `apiReleaseType`
2. **Query** — Run `azure-sdk-mcp:azsdk_get_release_plan` with the matching selector. Use a relative project path for lookup. For a metadata or spec-PR update, use the result's `WorkItemId`, not its `ReleasePlanId`; resolve an ambiguous lookup before writing.
3. **Display** — Show the release plan ID, status, linked PRs, and SDK details. Always relay schedule-risk warnings and recommended actions from the response.

**Tool**: `azure-sdk-mcp:azsdk_get_release_plan`

---

### 3. Update Release Plan / Update API Spec in Release Plan

**When**: User needs to update release plan metadata (spec PR URL, TypeSpec project path, SDK release type, service/product IDs) or update the API spec PR link.

**Steps**:

1. **Identify Plan** — Reuse the exact work item ID if known. Otherwise, get the plan by Release Plan ID, linked spec PR, or project/API version/API release type, and use the returned `WorkItemId`.
2. **Update Metadata** — Run `azure-sdk-mcp:azsdk_update_release_plan` with:
   - `typeSpecProjectPath` (required)
   - `workItemId` (required — exact Azure DevOps work item ID)
   - `specPullRequestUrl` (optional — omission uses the plan's existing spec link)
   - `sdkReleaseType` (required — preserve the user's explicit choice; otherwise use the plan's `SDKReleaseType`, reading the plan if needed. Do not ask for a new choice or assume a default.)
   - `specCommitSha` (optional for public pinning — defaults from the PR)
   - `serviceTreeId` (optional)
   - `productTreeId` (optional)
3. **Update API Spec PR** — If only the spec PR URL needs updating, run `azure-sdk-mcp:azsdk_update_api_spec_pull_request_in_release_plan` with:
   - `specPullRequestUrl` (required)
   - `workItemId` (required — exact Azure DevOps work item ID)
   - `typeSpecProjectPath` (required local checkout for public targets; optional for private previews)
   - `specCommitSha` (optional for public pinning — defaults from the PR)
4. **Report** — Call only the needed update tool, once. Report its saved result or configuration error, without an approval roundtrip or automatic SDK generation.

**Tools**: `azure-sdk-mcp:azsdk_update_release_plan`, `azure-sdk-mcp:azsdk_update_api_spec_pull_request_in_release_plan`

---

### 4. Update SDK/Package Details in Release Plan

**When**: User needs to update SDK language and package name details in the release plan after code generation or configuration changes.

**Steps**:

1. **Identify Plan** — Get the Release Plan ID or work item ID from the user (either is accepted).
2. **Identify TypeSpec Project** — Get or confirm the TypeSpec project path.
3. **Update** — Run `azure-sdk-mcp:azsdk_update_sdk_details_in_release_plan` with:
   - `workItemId` (required — accepts either the Release Plan ID or the work item ID)
   - `typeSpecProjectPath` (required)

**Tool**: `azure-sdk-mcp:azsdk_update_sdk_details_in_release_plan`

---

### 5. Abandon a Release Plan

**When**: User decides to cancel or discard a release plan that is no longer needed.

**Steps**:

1. **Identify Plan** — Get the work item ID or release plan ID from the user.
2. **Confirm** — Ask user to confirm abandonment: "Are you sure you want to abandon this release plan? This action updates the status to Abandoned."
3. **Abandon** — Run `azure-sdk-mcp:azsdk_abandon_release_plan` with:
   - `workItemId` or `releasePlanId`

**Tool**: `azure-sdk-mcp:azsdk_abandon_release_plan`

---

### 6. Link SDK Pull Request to Release Plan

**When**: SDK pull requests have been created and need to be associated with the release plan.

**Steps**:

1. **Identify Plan** — Get the Release Plan ID or work item ID from the user (either is accepted).
2. **Collect PR Info** — Get the SDK pull request URL and language from the user.
3. **Link** — Run `azure-sdk-mcp:azsdk_link_sdk_pull_request_to_release_plan` with:
   - `pullRequestUrl` (required)
   - `language` (required — e.g., ".NET", "Java", "JavaScript", "Python", "Go")
   - `workItemId` or `releasePlanId` (either accepts the Release Plan ID or the work item ID)
4. **Repeat** — If multiple SDK PRs exist for different languages, repeat for each.

**Tool**: `azure-sdk-mcp:azsdk_link_sdk_pull_request_to_release_plan`

---

## Examples

- "Create a release plan for my spec PR"
- "Get the release plan for work item 12345"
- "What is the status of my release plan?"
- "Update the API spec PR in my release plan"
- "Update SDK details in release plan 67890"
- "Abandon release plan 11111"
- "Link my SDK PR to release plan"
- "Link Python SDK PR #100 to release plan 67890"

## Troubleshooting

- Requires `azure-sdk-mcp` server; no CLI fallback — prompt user to configure MCP if unavailable.
- If creation fails, verify spec PR URL and Service Tree IDs.
- If a metadata or spec-PR update fails, verify the exact work item ID and that the plan is not already abandoned.
- If linking fails, verify the SDK PR URL is valid and the language matches a supported value.
