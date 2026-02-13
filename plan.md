# CODEOWNERS Management Commands — PRD & Implementation Plan

## Problem Statement

The existing `CodeownersTool.cs` has `update`, `validate`, and `render` commands that operate on the CODEOWNERS file directly. There is no way to:
- **View** a user's, label's, or package's CODEOWNERS associations across repos via DevOps work items
- **Add/Remove** ownership relationships at the work item level (Owner↔Package, Owner↔Label, Label↔Path, etc.)

We need three new commands (`view`, `add`, `remove`) that operate on Azure DevOps work items, providing a data-centric management layer. The CODEOWNERS file is then regenerated via the existing `render` command.

## Proposed Approach

Add three new subcommands to `CodeownersTool.cs`: `view`, `add`, and `remove`. These operate on DevOps work items (Owner, Package, Label, Label Owner) and their relationships. The existing `update`, `validate`, and `render` commands remain unchanged.

---

## Command Specifications

### 1. `view` Command

**MCP Tool Name**: `azsdk_engsys_codeowner_view`

**Purpose**: Query and display CODEOWNERS associations for a user, label, or package. Always shows detailed output.

**Parameters**:
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--user` | string | Mutually exclusive | GitHub alias to look up |
| `--label` | string | Mutually exclusive | Label name to look up |
| `--package` | string | Mutually exclusive | Package name to look up |
| `--path` | string | Mutually exclusive | Repository path to look up (e.g., `sdk/formrecognizer/`) |
| `--repo` | string | Optional | Repository name filter (e.g., `Azure/azure-sdk-for-python`). When omitted, queries across all repos. |

**Exactly one of `--user`, `--label`, `--package`, or `--path` must be specified.**

**Scenarios**:

#### 1a. View by User (`--user johndoe`)
Query all Owner work items matching the GitHub alias. Follow relationships to find:
- **Packages**: Where the user is a direct source owner (Owner→Package relation), with associated labels
- **Label Owners**: Where the user is linked, grouped by label type

#### 1b. View by Label (`--label "Cognitive - Form Recognizer"`)
Query the Label work item. Follow relationships to find:
- **Packages**: Directly related to this label, with their source owners
- **Label Owners**: Related to this label, with their owners and type
- If `--repo` is specified, filter Label Owners to that repo

#### 1c. View by Path (`--path "sdk/formrecognizer/"`)
Query Label Owner work items whose `Custom.RepoPath` matches the given path.
- If `--repo` is specified, filter to that repo; otherwise show across all repos
- Show all matching Label Owners grouped by type, with their owners and labels

#### 1d. View by Package (`--package "Azure.AI.FormRecognizer"`)
Query the Package work item (latest version). Show:
- **Source Owners**: Direct Owner relations
- **Labels**: Direct Label relations
- **Label Owners**: Related Label Owners with their owners and type

#### View Output Format

All view scenarios produce a structured report with two sections:

**1. Packages** — A list of packages with:
- Package name, language, package type
- Source owners (direct Owner work item relations)
- Labels (direct Label work item relations)

**2. Label Owners** — Displayed in two sub-sections:

**2a. Path-based Label Owners** (Label Owners that have a `RepoPath`):
- Grouped by path — all Label Owners sharing the same `RepoPath` are combined into one group
- For each Label Owner in the group:
  - Label type (Azure SDK Owner, Service Owner, PR Label)
  - Associated owners (sorted alphabetically)
  - Associated labels (sorted alphabetically)
- Groups sorted by path

**2b. Pathless Label Owners** (Label Owners without a `RepoPath`):
- Grouped by their alphabetized set of labels — Label Owners sharing the same set of labels are combined into one group
- For each Label Owner in the group:
  - Label type (Azure SDK Owner, Service Owner, PR Label)
  - Associated owners (sorted alphabetically)
- Groups sorted by primary label (first label alphabetically in the set)

All owner lists throughout the output are sorted alphabetically.

---

### 2. `add` Command

**MCP Tool Name**: `azsdk_engsys_codeowner_add`

**Purpose**: Add ownership relationships between DevOps work items.

**Parameters**:
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--repo` | string | Yes | Repository name (e.g., `Azure/azure-sdk-for-python`) |
| `--user` | string | Conditional | GitHub alias of the owner to add |
| `--package` | string | Conditional | Package name |
| `--label` | string[] | Conditional | Label name(s). Can be specified multiple times to form a list. |
| `--path` | string | Conditional | Repository path (e.g., `sdk/formrecognizer/`) |
| `--owner-type` | string | Conditional | One of: `service-owner`, `azsdk-owner`, `pr-label` |

**Scenarios**:

#### 2a. Add User as Owner of Package (`--user johndoe --package "Azure.AI.FormRecognizer"`)
- `--owner-type` must NOT be specified (error if provided — packages only have source owners)
- Validate user is a valid code owner (via `ValidateCodeOwnerAsync`)
- Find or create the Owner work item for the GitHub alias
- Find the Package work item (latest version, error if not found)
- Check if relationship already exists (skip if duplicate)
- Add "Related" link between Owner and Package work items

#### 2b. Add User as Owner of Label (`--user johndoe --label "Cognitive - Form Recognizer" --owner-type service-owner`)
- `--owner-type` is REQUIRED (one of: `service-owner`, `azsdk-owner`, `pr-label`)
- If `--owner-type` is `pr-label`, then `--path` is also REQUIRED
- Validate user is a valid code owner
- Find or create Owner work item
- Find the Label work item (error if not found — do NOT auto-create labels)
- Find or create the Label Owner work item for this label set+repo+owner-type combination
  - Title format: `Label Owner: <repo-name> - <owner-type> - <label1>, <label2>, ...`
  - Set `Custom.LabelType`, `Custom.Repository`, and `Custom.RepoPath` (for pr-label type)
- Add "Related" link: Owner → Label Owner
- Add "Related" link: Label → Label Owner for each label (if not already linked)

#### 2c. Add User as Owner of Path (`--user johndoe --path "sdk/formrecognizer/"`)
- `--owner-type` is REQUIRED (one of: `pr-label`, `service-owner`, `azsdk-owner`; error if omitted)
- Validate user is a valid code owner
- Find or create Owner work item
- Find existing Label Owner with matching repo+path+owner-type, or create new one
  - Title format: `Label Owner: <repo-name> - <owner-type> - <label1>, <label2>, ...` (labels may be empty)
  - Set `Custom.LabelType`, `Custom.Repository`, `Custom.RepoPath`
- Add "Related" link: Owner → Label Owner

#### 2d. Add Label to Path (`--label "Cognitive - Form Recognizer" --path "sdk/formrecognizer/"`)
- `--label` can be specified multiple times
- `--user` must NOT be specified (error if provided)
- `--owner-type` must NOT be specified (error if provided)
- Find the Label work item for each label (error if not found)
- Find existing Label Owner for this repo+path, or create new one
  - Title format: `Label Owner: <repo-name> - <owner-type> - <label1>, <label2>, ...`
- Add "Related" link: Label → Label Owner for each label

---

### 3. `remove` Command

**MCP Tool Name**: `azsdk_engsys_codeowner_remove`

**Purpose**: Remove ownership relationships between DevOps work items.

**Parameters**: Same as `add` command.

**Scenarios** mirror `add` but in reverse:

#### 3a. Remove User from Package (`--user johndoe --package "Azure.AI.FormRecognizer"`)
- Find the Owner and Package work items
- Remove the "Related" link between them
- Do NOT delete the Owner or Package work items

#### 3b. Remove User from Label (`--user johndoe --label "Cognitive - Form Recognizer" --owner-type service-owner`)
- Find the Owner and the Label Owner work item matching label+repo+owner-type
- Remove the "Related" link: Owner → Label Owner
- If Label Owner has no remaining owners, optionally warn but do NOT auto-delete

#### 3c. Remove User from Path (`--user johndoe --path "sdk/formrecognizer/"`)
- `--owner-type` is REQUIRED (consistent with add scenario 2c)
- Find the Owner and Label Owner matching repo+path+owner-type
- Remove the "Related" link

#### 3d. Remove Label from Path (`--label "Cognitive - Form Recognizer" --path "sdk/formrecognizer/"`)
- Find the Label and Label Owner matching repo+path
- Remove the "Related" link: Label → Label Owner

---

## MCP Agent Instructions

Below are instructions for an AI agent invoking these tools via MCP:

### General Guidelines
- Always confirm the repository name format: `Azure/azure-sdk-for-<language>` (e.g., `Azure/azure-sdk-for-python`)
- GitHub aliases accept both `@githubalias` and `githubalias` forms; the tool normalizes by stripping the leading `@` if present
- Label names are case-insensitive
- Package names are case-insensitive

### Tool: `azsdk_engsys_codeowner_view`

**When to use**: When a user asks "who owns X?", "what does user X own?", "show me the codeowners for label Y", or similar queries about existing ownership.

**Parameter selection**:
- Use `--user` when the user asks about a specific person's ownership
- Use `--label` when asking about owners/packages associated with a service label
- Use `--package` when asking about owners of a specific package
- Use `--path` when asking about owners or labels associated with a specific repository path
- Add `--repo` only when the user specifies a particular repository; omit for a cross-repo report
- Only one of `--user`, `--label`, `--package`, `--path` can be specified per invocation

**Example invocations**:
```
azsdk_engsys_codeowner_view --user "johndoe"
azsdk_engsys_codeowner_view --user "johndoe" --repo "Azure/azure-sdk-for-python"
azsdk_engsys_codeowner_view --label "Cognitive - Form Recognizer"
azsdk_engsys_codeowner_view --package "Azure.AI.FormRecognizer"
azsdk_engsys_codeowner_view --path "sdk/formrecognizer/"
azsdk_engsys_codeowner_view --path "sdk/formrecognizer/" --repo "Azure/azure-sdk-for-python"
```

### Tool: `azsdk_engsys_codeowner_add`

**When to use**: When a user wants to add someone as an owner, associate a label with a path, or establish any ownership relationship.

**Parameter selection rules**:
1. **User + Package**: Use `--user` and `--package`. Do NOT include `--owner-type` (source owners only).
2. **User + Label**: Use `--user`, `--label`, and `--owner-type` (required: `service-owner`, `azsdk-owner`, or `pr-label`). If `pr-label`, also include `--path`.
3. **User + Path**: Use `--user` and `--path`. Do NOT include `--owner-type`.
4. **Label + Path**: Use `--label` and `--path`. Do NOT include `--user` or `--owner-type`.

**Always include `--repo`.**

**Before calling**: Confirm the user's intent — particularly the owner type when adding to a label.

**Example invocations**:
```
azsdk_engsys_codeowner_add --repo "Azure/azure-sdk-for-python" --user "johndoe" --package "azure-ai-formrecognizer"
azsdk_engsys_codeowner_add --repo "Azure/azure-sdk-for-python" --user "johndoe" --label "Cognitive - Form Recognizer" --owner-type "service-owner"
azsdk_engsys_codeowner_add --repo "Azure/azure-sdk-for-python" --user "johndoe" --label "Cognitive - Form Recognizer" --owner-type "pr-label" --path "sdk/formrecognizer/"
azsdk_engsys_codeowner_add --repo "Azure/azure-sdk-for-python" --user "johndoe" --path "sdk/formrecognizer/"
azsdk_engsys_codeowner_add --repo "Azure/azure-sdk-for-python" --label "Cognitive - Form Recognizer" --path "sdk/formrecognizer/"
```

### Tool: `azsdk_engsys_codeowner_remove`

**When to use**: When a user wants to remove ownership associations.

**Same parameter rules as `add`.**

**Before calling**: Confirm the user truly wants to remove the association. Warn them that this affects the data model — the CODEOWNERS file won't change until `render` is run.

**Example invocations**:
```
azsdk_engsys_codeowner_remove --repo "Azure/azure-sdk-for-python" --user "johndoe" --package "azure-ai-formrecognizer"
azsdk_engsys_codeowner_remove --repo "Azure/azure-sdk-for-python" --user "johndoe" --label "Cognitive - Form Recognizer" --owner-type "service-owner"
```

### Workflow Guidance for Agents

1. **Before modifying**: Always use `view` first to show the current state to the user
2. **After modifying**: Use `view` again to confirm the change was applied
3. **To update CODEOWNERS file**: After add/remove operations, remind the user to run `render` to regenerate the CODEOWNERS file from the updated work items
4. **Error handling**: If a tool returns an error about missing work items, explain what's missing and suggest the user create them through the appropriate process

---

## Implementation Todos

### Prerequisites
1. **Add DevOps methods** — Add methods to `IDevOpsService` for:
   - Querying Owner work items by GitHub alias
   - Creating Owner work items
   - Creating Label Owner work items
   - Adding/removing "Related" links between work items
   - Querying Label Owner by repo+path or repo+label+type

2. **Add response models** — Create response models in `Models/Codeowners` for view output

### Commands
3. **Implement `CodeownersManagementHelper.cs`** in `Helpers/` — Business logic layer with public, testable methods. Uses DI for `IDevOpsService` and `ICodeownersValidatorHelper`. Contains:
   - `FindOrCreateOwnerAsync(gitHubAlias)` — validates alias, finds or creates Owner work item
   - `FindPackageByNameAsync(packageName)` — queries latest version
   - `FindLabelByNameAsync(labelName)` — queries by name (case-insensitive)
   - `FindOrCreateLabelOwnerAsync(repo, labelType, repoPath, labels)` — finds or creates Label Owner
   - `AddOwnerToPackageAsync(ownerAlias, packageName)` — find/create owner, find package, add link
   - `RemoveOwnerFromPackageAsync(ownerAlias, packageName)` — find owner, find package, remove link
   - `AddOwnerToLabelAsync(ownerAlias, labels, repo, ownerType, path?)` — full label association flow
   - `RemoveOwnerFromLabelAsync(...)` — reverse of add
   - `AddOwnerToPathAsync(ownerAlias, repo, path, ownerType)` — path-based association
   - `RemoveOwnerFromPathAsync(...)` — reverse
   - `AddLabelToPathAsync(labels, repo, path)` — label-to-path association
   - `RemoveLabelFromPathAsync(...)` — reverse
   - `GetViewByUserAsync(alias, repo?)` — view query logic
   - `GetViewByLabelAsync(label, repo?)` — view query logic
   - `GetViewByPathAsync(path, repo?)` — view query logic
   - `GetViewByPackageAsync(packageName)` — view query logic
   - Interface: `ICodeownersManagementHelper` for testability
4. **Implement `view`, `add`, `remove` commands** in `CodeownersTool.cs` — Input validation, parameter parsing, and procedural orchestration. Delegates business logic to `ICodeownersManagementHelper`.

### Supporting
6. **Add validation helpers** — Parameter combination validation, alias normalization
7. **Write test scaffolding** — Create test class files with stubs: `CodeownersManagementHelperTests.cs` (business logic) and `CodeownersToolCommandTests.cs` (input validation). Leverage existing `WorkItemDataBuilder` for work item state setup.
8. **Write MCP agent instructions** — Document at `eng/common/instructions/azsdk-tools/codeowners.md`

---

## Design Decisions (FAQ)

**Q: Why work item operations instead of CODEOWNERS file edits?**
A: Work items are the source of truth. The `render` command generates CODEOWNERS from work items. Editing work items ensures data integrity and consistency across repos.

**Q: Should Labels be auto-created?**
A: No. Per the data model guidelines, Labels are managed through a central process. Surface an error if a label doesn't exist.

**Q: Should Owner work items be auto-created?**
A: Yes, but only after validating the GitHub alias is a valid code owner. Use `ValidateCodeOwnerAsync` first.

**Q: Should Label Owner work items be auto-created?**
A: Yes. When adding an owner to a label for a specific repo+type combination, create the Label Owner if one doesn't exist.

**Q: When adding a user to a package, what role?**
A: Source owner only. Specifying `--owner-type` with `--package` is an error.

**Q: When adding an owner to a label, is type required?**
A: Yes. Must be one of `service-owner`, `azsdk-owner`, `pr-label`. For `pr-label`, `--path` is also required.
