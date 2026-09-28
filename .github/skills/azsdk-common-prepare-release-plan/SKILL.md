---
name: azsdk-common-prepare-release-plan
license: MIT
metadata:
  version: "1.0.0"
  distribution: shared
description: 'Create, get, update, abandon, and link SDK PRs to release plan work items for Azure SDK releases. **UTILITY SKILL**. USE FOR: "create release plan", "get release plan", "update release plan", "update API spec in release plan", "update SDK details in release plan", "abandon release plan", "link SDK PR to plan", "namespace approval", "check release plan status". DO NOT USE FOR: SDK code generation, pipeline troubleshooting, API review feedback. INVOKES: azure-sdk-mcp:azsdk_create_release_plan, azure-sdk-mcp:azsdk_get_release_plan, azure-sdk-mcp:azsdk_update_release_plan, azure-sdk-mcp:azsdk_update_release_plan_target, azure-sdk-mcp:azsdk_update_api_spec_pull_request_in_release_plan, azure-sdk-mcp:azsdk_update_sdk_details_in_release_plan, azure-sdk-mcp:azsdk_abandon_release_plan, azure-sdk-mcp:azsdk_link_sdk_pull_request_to_release_plan, azure-sdk-mcp:azsdk_link_namespace_approval_issue.'
compatibility: "azure-sdk-mcp server, API spec PR in Azure/azure-rest-api-specs"
---

# Prepare Release Plan

This skill creates, gets, updates, abandons, and links SDK PRs to release plan work items for Azure SDK releases, helping gather required release data, validate spec inputs, and link related approvals or SDK pull requests without exposing internal work item URLs.

## Triggers

USE FOR: create release plan, get release plan, update release plan, update API spec in release plan, update SDK details in release plan, abandon release plan, link SDK PR to plan, namespace approval, check release plan status
WHEN: "create release plan", "get release plan", "update release plan", "abandon release plan", "link SDK PR to plan", "namespace approval", "check release plan status"
DO NOT USE FOR: SDK code generation, pipeline troubleshooting, API review feedback

## Rules

- Do not display Azure DevOps work item URLs; only provide the Release Plan Link and ID.
- Require an API spec PR link or a TypeSpec project path before creating or updating a plan.
- Validate that the spec PR repository matches the requested API release type before creation.
- Public SDK targets default to a **no-write preview**. Show `proposed_spec_target` (project, packages, API version, SDK release type, PR, SHA and commit link), then obtain approval before sending `confirmTarget: true` and the approved `specCommitSha`.
- Create/update/link target validation needs a local TypeSpec project in a clean checkout of the PR's source HEAD (open PR) or merge commit (merged PR). Never switch, stash, or reset user files automatically. Metadata is compiled at that snapshot; no API version means an error, one distinct version is selected, and multiple versions require a user choice from `AvailableApiVersions`. Never guess `latest` or override missing metadata.
- Public updates also require the preview's `ExpectedTargetRevision` as `expectedTargetRevision`. Preserve it verbatim; a changed parent or API Spec revision requires a fresh preview and approval, even at the same SHA. Reusing create must not advance an existing pin. A different API version or project requires a separate plan; private-preview plans remain spec-link-only.
- Release plan tools accept **either** a Release Plan ID or an Azure DevOps work item ID — pass whichever the user provides. Each tool resolves the value automatically (trying it as a Release Plan ID first, then as a work item ID), so you do not need to call `azure-sdk-mcp:azsdk_get_release_plan` first just to translate one ID into the other.
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

1. **Locate Project** — Obtain the local TypeSpec project directory at the intended PR SHA. Relative paths work from the specs checkout; otherwise use an absolute local path. Without a PR, a tracking-only plan may be created without commit inputs.
2. **Reuse Safely** — Let create resolve reuse by project, API version and API release type. Do not use a project-only lookup to decide that another version already has a plan, and do not use force-create modes. If an existing plan is returned, its target is unchanged; advancing it needs an explicit update preview and confirmation.
3. **Gather Info** — Collect required details from the user. See [details](references/release-plan-details.md):
   - Target release month/year (format: "Month YYYY", e.g. "June 2026"). Do NOT use formats like "2026-06" or "06/2026" — these are invalid.
   - API release type: Value must be one of the following: "Private Preview", "Public Preview", or "GA"
   - Spec PR URL (optional)
   - Service Tree ID (GUID) — optional if previously created
   - Product Tree ID (GUID) — optional if previously created
4. **Preview, Then Confirm** — Run `azure-sdk-mcp:azsdk_create_release_plan` without confirmation. For a public target, present the returned target and ask for approval, then repeat with `specCommitSha` and `confirmTarget: true`. Supply `apiVersion` only to choose a version reported by metadata. Create derives SDK type from API release type (preview → beta, GA → stable); there is no create `sdkReleaseType` parameter.
5. **Namespace** — For first management plane releases, link namespace approval issue using `azure-sdk-mcp:azsdk_link_namespace_approval_issue`.

> **IMPORTANT**: Do not change an existing plan's API release type, API version, or project to represent a separate release. Use create for the separate target without force modes. Target confirmation does not generate or publish SDKs.

**Tool**: `azure-sdk-mcp:azsdk_create_release_plan`

---

### 2. Get Release Plan

**When**: User wants to check the status or details of an existing release plan.

**Steps**:

1. **Identify Plan** — Ask user for one of:
   - Release plan ID or work item ID
   - Relative TypeSpec project path (e.g. `specification/contosowidgetmanager/Contoso.WidgetManager`)
   - Spec PR URL
2. **Query** — Run `azure-sdk-mcp:azsdk_get_release_plan` with the provided identifier. Always use a relative path for `typeSpecProjectPath`; use `specPullRequestUrl` when the user provides only a spec PR URL.
3. **Display** — Show the release plan ID, status, linked PRs, and SDK details. Always relay schedule-risk warnings and recommended actions from the response.

**Tool**: `azure-sdk-mcp:azsdk_get_release_plan`

---

### 3. Update Release Plan / Update API Spec in Release Plan

**When**: User needs to update release plan metadata (spec PR URL, TypeSpec project path, SDK release type, service/product IDs) or update the API spec PR link.

**Steps**:

1. **Identify Plan** — Get the work item ID or TypeSpec project path from the user.
2. **Update Metadata** — Run `azure-sdk-mcp:azsdk_update_release_plan` with:
   - `typeSpecProjectPath` (required)
   - `workItemId` (optional — resolved from TypeSpec path or spec PR if not provided)
   - `specPullRequestUrl` (optional)
   - `sdkReleaseType` (required — do NOT default this from API release type; always ask user explicitly)
   - `serviceTreeId` (optional)
   - `productTreeId` (optional)
3. **Update API Spec PR** — If only the spec PR URL needs updating, run `azure-sdk-mcp:azsdk_update_api_spec_pull_request_in_release_plan` with:
   - `specPullRequestUrl` (required)
   - `workItemId` or `releasePlanId`
   - `typeSpecProjectPath` (local checkout required for public targets)
4. **Confirm Public Target** — For either update tool, first preview. After approval repeat the same inputs with the returned `specCommitSha`, `confirmTarget: true`, and `expectedTargetRevision`. A missing revision is still a preview; stale revisions or changed PR sources require fresh approval. Use `apiVersion` only for an explicit choice from snapshot metadata. Omitted PR in metadata updates uses the existing public spec link.

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
- If update fails, ensure the Release Plan ID or work item ID is correct and the plan is not already abandoned.
- If linking fails, verify the SDK PR URL is valid and the language matches a supported value.
