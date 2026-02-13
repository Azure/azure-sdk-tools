---
mode: 'agent'
tools: ['azsdk_engsys_codeowner_view', 'azsdk_engsys_codeowner_add', 'azsdk_engsys_codeowner_remove']
---

## Goal
Manage CODEOWNERS data by viewing, adding, and removing ownership relationships between Azure DevOps work items (Owners, Packages, Labels, Label Owners).

## General Guidelines
- Repository name format: `Azure/azure-sdk-for-<language>` (e.g., `Azure/azure-sdk-for-python`)
- GitHub aliases accept both `@githubalias` and `githubalias` — the tool strips the leading `@`
- Label names are case-insensitive
- Package names are case-insensitive
- These commands operate on Azure DevOps work items, not the CODEOWNERS file directly
- After making changes, remind the user to run `render` to regenerate the CODEOWNERS file

## Tool: azsdk_engsys_codeowner_view

**When to use**: When a user asks "who owns X?", "what does user X own?", "show me the codeowners for label Y", or similar queries about existing ownership.

**Parameter selection**:
- Use `--user` when asking about a specific person's ownership
- Use `--label` when asking about owners/packages associated with a service label
- Use `--package` when asking about owners of a specific package
- Use `--path` when asking about owners or labels associated with a specific repository path
- Add `--repo` only when the user specifies a particular repository; omit for cross-repo results
- **Only one** of `--user`, `--label`, `--package`, `--path` can be specified per invocation

**Example invocations**:
```
azsdk_engsys_codeowner_view --user "johndoe"
azsdk_engsys_codeowner_view --user "johndoe" --repo "Azure/azure-sdk-for-python"
azsdk_engsys_codeowner_view --label "Cognitive - Form Recognizer"
azsdk_engsys_codeowner_view --package "Azure.AI.FormRecognizer"
azsdk_engsys_codeowner_view --path "sdk/formrecognizer/" --repo "Azure/azure-sdk-for-python"
```

## Tool: azsdk_engsys_codeowner_add

**When to use**: When a user wants to add someone as an owner, associate a label with a path, or establish any ownership relationship.

**Always requires `--repo`.**

**Parameter rules by scenario**:
1. **User + Package**: `--user` and `--package`. Do NOT include `--owner-type` (packages only have source owners).
2. **User + Label**: `--user`, `--label`, and `--owner-type` (required: `service-owner`, `azsdk-owner`, or `pr-label`). If `pr-label`, also include `--path`.
3. **User + Path**: `--user`, `--path`, and `--owner-type` (required).
4. **Label + Path**: `--label` and `--path`. Do NOT include `--user` or `--owner-type`.

**Before calling**: Confirm the user's intent, especially the owner type when adding to a label.

**Example invocations**:
```
azsdk_engsys_codeowner_add --repo "Azure/azure-sdk-for-python" --user "johndoe" --package "azure-ai-formrecognizer"
azsdk_engsys_codeowner_add --repo "Azure/azure-sdk-for-python" --user "johndoe" --label "Cognitive - Form Recognizer" --owner-type "service-owner"
azsdk_engsys_codeowner_add --repo "Azure/azure-sdk-for-python" --user "johndoe" --label "Cognitive - Form Recognizer" --owner-type "pr-label" --path "sdk/formrecognizer/"
azsdk_engsys_codeowner_add --repo "Azure/azure-sdk-for-python" --user "johndoe" --path "sdk/formrecognizer/" --owner-type "service-owner"
azsdk_engsys_codeowner_add --repo "Azure/azure-sdk-for-python" --label "Cognitive - Form Recognizer" --path "sdk/formrecognizer/"
```

## Tool: azsdk_engsys_codeowner_remove

**When to use**: When a user wants to remove ownership associations.

**Same parameter rules as `add`.**

**Before calling**: Confirm the user truly wants to remove the association. Warn that the CODEOWNERS file won't change until `render` is run.

**Example invocations**:
```
azsdk_engsys_codeowner_remove --repo "Azure/azure-sdk-for-python" --user "johndoe" --package "azure-ai-formrecognizer"
azsdk_engsys_codeowner_remove --repo "Azure/azure-sdk-for-python" --user "johndoe" --label "Cognitive - Form Recognizer" --owner-type "service-owner"
```

## Workflow Guidance

1. **Before modifying**: Always use `view` first to show the current state
2. **After modifying**: Use `view` again to confirm the change was applied
3. **To update CODEOWNERS file**: After add/remove operations, remind the user to run `render` to regenerate the CODEOWNERS file from the updated work items
4. **Error handling**: If a tool returns an error about missing work items, explain what's missing and suggest next steps
