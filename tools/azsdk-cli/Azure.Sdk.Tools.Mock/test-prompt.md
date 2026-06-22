# TypeSpec-to-SDK Workflow Test Prompt

Use this prompt to test the full `typespec-to-sdk` workflow against the mock MCP server.

## Prerequisites

Start the mock server before testing:

```bash
dotnet run --project tools/azsdk-cli/Azure.Sdk.Tools.Mock -- --urls "http://localhost:5050"
```

## Test Prompt

Paste the following into a Copilot agent session configured to use the mock server:

---

I have a TypeSpec project at `specification/contosowidgetmanager/Contoso.WidgetManager` in the `azure-rest-api-specs` repo. The spec PR is https://github.com/Azure/azure-rest-api-specs/pull/12345 and this is a data plane service.

Please generate SDKs for all languages using the pipeline approach. The API version is `2024-01-01-preview` and the release type is `beta`.

After generation is complete, create a release plan for the generated SDKs. Use service tree ID `12345678-1234-1234-1234-123456789012` and product tree ID `87654321-4321-4321-4321-210987654321`. The target release month is `06/2026`.

Then link all the SDK PRs to the release plan.

---

## Expected Tool Call Sequence

The workflow should call these tools in roughly this order:

### Step 1: Generate SDKs (4 languages for data plane)
1. `azsdk_run_generate_sdk` × 4 — one per language (.NET, Python, JavaScript, Java)
2. `azsdk_get_pipeline_status` × 4 — check each pipeline
3. `azsdk_get_sdk_pull_request_link` × 4 — get PR links

### Step 2: Create Release Plan
4. `azsdk_create_release_plan` × 1
5. `azsdk_update_sdk_details_in_release_plan` × 1 (optional)

### Step 3: Link SDK PRs
6. `azsdk_link_sdk_pull_request_to_release_plan` × 4

## Mock Response Summary

| Tool | Mock Response |
|------|---------------|
| `azsdk_run_generate_sdk` | Returns build IDs 90001–90005 per language |
| `azsdk_get_pipeline_status` | Always returns "Succeeded" |
| `azsdk_get_sdk_pull_request_link` | Returns PR URLs like `azure-sdk-for-net/pull/45001` |
| `azsdk_create_release_plan` | Returns work item 35000, release plan ID 50001 |
| `azsdk_get_release_plan` | Returns same release plan with SDK info populated |
| `azsdk_update_sdk_details_in_release_plan` | Returns success message |
| `azsdk_link_sdk_pull_request_to_release_plan` | Returns "Linked" status per language |
| `azsdk_release_sdk` | Returns readiness or pipeline trigger depending on `checkReady` |
| `azsdk_link_namespace_approval_issue` | Returns success (management plane only) |
