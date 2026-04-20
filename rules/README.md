# Existing Codeowners Linter Rules

This folder documents the rules from the existing `CodeownersLinter` tool in
`tools/codeowners-utils/`. These rules validate the **rendered CODEOWNERS file**
(the text artifact checked into each repo). They are summarized here so the new
ADO-backed audit system can determine which rules still apply.

## Source Files

| File | Responsibility |
|------|----------------|
| `CodeownersLinter.cs` | Orchestrates block-level structural checks |
| `Owners.cs` | Validates owner entries on source lines |
| `Labels.cs` | Validates label metadata comments |
| `DirectoryUtils.cs` | Validates path expressions and globs |

## Rule Index

| ID | Name | Source | Applies to ADO Audit? |
|----|------|--------|-----------------------|
| OWN-001 | [Source line owners required](OWN-001-source-owners-required.md) | Owners.cs | Yes |
| OWN-002 | [Team must be write team](OWN-002-team-must-be-write-team.md) | Owners.cs | Yes |
| OWN-003 | [User must be public Azure member](OWN-003-user-public-azure-member.md) | Owners.cs | Yes |
| OWN-004 | [Malformed team entry](OWN-004-malformed-team-entry.md) | Owners.cs | Yes |
| OWN-005 | [Invalid user](OWN-005-invalid-user.md) | Owners.cs | Yes |
| LBL-001 | [Labels required](LBL-001-labels-required.md) | Labels.cs | Yes |
| LBL-002 | [PRLabel forbids ServiceAttention](LBL-002-prlabel-forbids-service-attention.md) | Labels.cs | Partially |
| LBL-003 | [ServiceLabel cannot be only ServiceAttention](LBL-003-servicelabel-only-service-attention.md) | Labels.cs | Partially |
| LBL-004 | [Repo label must exist](LBL-004-repo-label-must-exist.md) | Labels.cs | Yes |
| PATH-001 | [Path must exist in repo](PATH-001-path-must-exist.md) | DirectoryUtils.cs | No* |
| PATH-002 | [Glob syntax valid](PATH-002-glob-syntax-valid.md) | DirectoryUtils.cs | Yes* |
| PATH-003 | [Glob must match repo files](PATH-003-glob-must-match.md) | DirectoryUtils.cs | No* |
| BLK-001 | [Duplicate moniker in block](BLK-001-duplicate-moniker.md) | CodeownersLinter.cs | No** |
| BLK-002 | [AzureSdkOwners requires ServiceLabel](BLK-002-azuresdkowners-requires-servicelabel.md) | CodeownersLinter.cs | Yes |
| BLK-003 | [ServiceOwners requires ServiceLabel](BLK-003-serviceowners-requires-servicelabel.md) | CodeownersLinter.cs | Yes |
| BLK-004 | [PRLabel block must end with source path](BLK-004-prlabel-block-ending.md) | CodeownersLinter.cs | Partially |
| BLK-005 | [ServiceLabel block completeness](BLK-005-servicelabel-completeness.md) | CodeownersLinter.cs | Yes |

\* Path rules validate the rendered file against the repo. ADO stores `RepoPath` on `Label Owner`
work items, but path validation against the actual repository is a separate concern from data
model auditing. These may be added in a future phase.

\*\* Duplicate moniker detection applies to the rendered file syntax. The ADO data model prevents
duplicates structurally (relations are unique).
