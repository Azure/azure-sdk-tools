---
mode: 'agent'
tools: ['azsdk_engsys_codeowner_view', 'azsdk_engsys_codeowner_add', 'azsdk_engsys_codeowner_remove']
---

## Goal:
Add or remove ownership relationships in the CODEOWNERS work item database.

## Available Tools

### `azsdk_engsys_codeowner_view`
**When to use**: To inspect the current ownership state before or after making changes.

**Parameter combinations** (exactly one axis at a time):
- `githubUser` — show all packages and label-owners for a specific GitHub user
- `labels` — show all items associated with one or more labels (AND semantics)
- `package` — show all owners and labels for a package
- `path` — show all label-owners for a repository path

Optional: `repo` (format: `Azure/azure-sdk-for-python`). If omitted the tool infers the repo from the current git context.

### `azsdk_engsys_codeowner_add`
**When to use**: When a user wants to establish an ownership relationship.

**Parameter combinations** (each is a distinct scenario):
1. `githubUsers` + `package` → Add user(s) as source owner of package
2. `label` + `package` → Add PR label to package
3. `githubUsers` + `label` + `ownerType` → Add user(s) as service/SDK owner for label (pathless triage entry)
4. `githubUsers` + `label` + `path` + `ownerType` → Add user(s) and label to a specific repo path

### `azsdk_engsys_codeowner_remove`
Same parameter rules as `add`. Confirm the user truly wants to remove before calling.

**Parameter combinations**:
1. `githubUsers` + `package` → Remove user(s) from package
2. `label` + `package` → Remove PR label from package
3. `githubUsers` + `label` + `ownerType` → Remove user(s) as service/SDK owner for label
4. `githubUsers` + `label` + `path` + `ownerType` → Remove user(s) and label from a path

## `ownerType` Values
- `service-owner` — Service owner (creates pathless triage entries)
- `azsdk-owner` — Azure SDK owner (creates pathless triage entries)
- `pr-label` — PR label owner (creates path-based entries)

## `repo` Parameter
`repo` is always optional. If not provided, the tool infers it from the current git context.
Format: `Azure/azure-sdk-for-python` (owner/repo-name).

## Workflow
1. Always use `azsdk_engsys_codeowner_view` first to show the current state.
2. After add/remove, the tool returns the updated state automatically.

## Important Notes
- Labels are never auto-created. If a label is not found, the operation will fail.
- Owners are auto-created after validating the GitHub alias against Azure SDK requirements.
- LabelOwners are auto-created when no matching one exists for the given repo + ownerType + path.
- Duplicate links are detected and reported without making changes.
- When multiple `githubUsers` are provided to add/remove, all are processed and partial success is reported.
