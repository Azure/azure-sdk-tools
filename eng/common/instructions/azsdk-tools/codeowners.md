# CODEOWNERS Management — MCP Tool Instructions

## Overview

The CODEOWNERS management tools allow querying and modifying Azure DevOps work item relationships that are the source of truth for CODEOWNERS data. The CODEOWNERS file itself is regenerated separately via the `generate` command.

## General Guidelines

- Repository name format: `Azure/azure-sdk-for-<language>` (e.g., `Azure/azure-sdk-for-python`)
- GitHub aliases accept both `@githubalias` and `githubalias` forms; the tool normalizes by stripping the leading `@`
- Label names are case-insensitive
- Package names are case-insensitive

---

## Tool: `azsdk_engsys_codeowner_view`

**When to use**: When a user asks "who owns X?", "what does user X own?", "show me the codeowners for label Y", or similar queries about existing ownership.

**Parameter selection**:
- Use `--user` when the user asks about a specific person's ownership
- Use `--label` when asking about owners/packages associated with a service label
- Use `--package` when asking about owners of a specific package
- Use `--path` when asking about owners or labels associated with a specific repository path
- Add `--repo` only when the user specifies a particular repository; omit for a cross-repo report
- **Only one** of `--user`, `--label`, `--package`, `--path` can be specified per invocation

**Example invocations**:
```
azsdk_engsys_codeowner_view --user "johndoe"
azsdk_engsys_codeowner_view --user "johndoe" --repo "Azure/azure-sdk-for-python"
azsdk_engsys_codeowner_view --label "Cognitive - Form Recognizer"
azsdk_engsys_codeowner_view --package "Azure.AI.FormRecognizer"
azsdk_engsys_codeowner_view --path "sdk/formrecognizer/"
azsdk_engsys_codeowner_view --path "sdk/formrecognizer/" --repo "Azure/azure-sdk-for-python"
```

---

## Tool: `azsdk_engsys_codeowner_add`

**When to use**: When a user wants to add someone as an owner, associate a label with a path, or establish any ownership relationship.

**Always include `--repo`.**

**Parameter selection rules**:
1. **User + Package**: Use `--user` and `--package`. Do NOT include `--owner-type` (source owners only).
2. **User + Label**: Use `--user`, `--label`, and `--owner-type` (required: `service-owner`, `azsdk-owner`, or `pr-label`). If `pr-label`, also include `--path`.
3. **User + Path**: Use `--user`, `--path`, and `--owner-type` (required).
4. **Label + Path**: Use `--label` and `--path`. Do NOT include `--user` or `--owner-type`.

**Before calling**: Confirm the user's intent — particularly the owner type when adding to a label.

**Example invocations**:
```
azsdk_engsys_codeowner_add --repo "Azure/azure-sdk-for-python" --user "johndoe" --package "azure-ai-formrecognizer"
azsdk_engsys_codeowner_add --repo "Azure/azure-sdk-for-python" --user "johndoe" --label "Cognitive - Form Recognizer" --owner-type "service-owner"
azsdk_engsys_codeowner_add --repo "Azure/azure-sdk-for-python" --user "johndoe" --label "Cognitive - Form Recognizer" --owner-type "pr-label" --path "sdk/formrecognizer/"
azsdk_engsys_codeowner_add --repo "Azure/azure-sdk-for-python" --user "johndoe" --path "sdk/formrecognizer/" --owner-type "service-owner"
azsdk_engsys_codeowner_add --repo "Azure/azure-sdk-for-python" --label "Cognitive - Form Recognizer" --path "sdk/formrecognizer/"
```

---

## Tool: `azsdk_engsys_codeowner_remove`

**When to use**: When a user wants to remove ownership associations.

**Same parameter rules as `add`.**

**Before calling**: Confirm the user truly wants to remove the association. Warn them that this affects the data model — the CODEOWNERS file won't change until `generate` is run.

**Example invocations**:
```
azsdk_engsys_codeowner_remove --repo "Azure/azure-sdk-for-python" --user "johndoe" --package "azure-ai-formrecognizer"
azsdk_engsys_codeowner_remove --repo "Azure/azure-sdk-for-python" --user "johndoe" --label "Cognitive - Form Recognizer" --owner-type "service-owner"
```

---

## Workflow Guidance

1. **Before modifying**: Always use `view` first to show the current state to the user
2. **After modifying**: Use `view` again to confirm the change was applied
3. **To update CODEOWNERS file**: After add/remove operations, remind the user to run `generate` to regenerate the CODEOWNERS file from the updated work items
4. **Error handling**: If a tool returns an error about missing work items, explain what's missing and suggest the user create them through the appropriate process
