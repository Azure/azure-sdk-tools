---
mode: 'agent'
tools: ['azsdk_engsys_codeowner_view', 'azsdk_engsys_codeowner_add', 'azsdk_engsys_codeowner_remove']
---

## Goal:
Add or remove ownership relationships in the CODEOWNERS work item database.

## Available Tools

### `azsdk_engsys_codeowner_add`
**When to use**: When a user wants to establish an ownership relationship.

**Parameter combinations** (each is a distinct scenario):
1. `--github-user` + `--package` → Add user as source owner of package
2. `--label` + `--package` → Add PR label to package
3. `--github-user` + `--label` + `--owner-type` → Add user as service/SDK owner for label (pathless triage entry)
4. `--github-user` + `--label` + `--path` + `--owner-type` → Add user and label to a specific repo path

### `azsdk_engsys_codeowner_remove`
Same parameter rules as `add`. Confirm the user truly wants to remove before calling.

**Parameter combinations**:
1. `--github-user` + `--package` → Remove user from package
2. `--label` + `--package` → Remove PR label from package
3. `--github-user` + `--label` + `--owner-type` → Remove user as service/SDK owner for label
4. `--github-user` + `--label` + `--path` + `--owner-type` → Remove user and label from a path

## `--owner-type` Values
- `service-owner` — Service owner (creates pathless triage entries: `# ServiceOwners`)
- `azsdk-owner` — Azure SDK owner (creates pathless triage entries)
- `pr-label` — PR label owner (creates path-based entries with `# PRLabel`)

## `--repo` Parameter
`--repo` is always optional. If not provided, the tool infers it from the current git context.
Format: `Azure/azure-sdk-for-python` (owner/repo-name).

## Workflow
1. Always use `azsdk_engsys_codeowner_view` first to show the current state.
2. After add/remove, the tool returns the updated state automatically.
3. Remind the user to run the `generate` command to update the CODEOWNERS file after making changes.

## Important Notes
- Labels are never auto-created. If a label is not found, the operation will fail.
- Owners are auto-created after validating the GitHub alias against Azure SDK requirements.
- LabelOwners are auto-created when no matching one exists for the given repo + owner-type + path.
- Duplicate links are detected and reported without making changes.
