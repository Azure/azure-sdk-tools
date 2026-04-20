# Audit Rule Plans

This folder contains implementation plans for each audit rule in the
`config codeowners audit` command. Rules are grouped by category.

## Rule Categories

Items in deferred are not part of this work and should not be implemented.

### Owner Validation (AUD-OWN)

Rules that validate Owner work items. 

| Rule | Description | Fix? |
|------|-------------|------|
| [AUD-OWN-001](AUD-OWN-001-invalid-owner.md) | Owner fails GitHub validation | Yes |
| [AUD-OWN-002](AUD-OWN-002-malformed-team.md) | Team alias format invalid | Report only |
| [AUD-OWN-003](AUD-OWN-003-team-not-write.md) | Team not under azure-sdk-write | Yes |
| DEFERRED [AUD-OWN-004](deferred/AUD-OWN-004-msft-identity-missing.md) | MSFT Identity field not populated | Yes |

### Label Validation (AUD-LBL)

Rules that validate Label work items and their usage.

| Rule | Description | Fix? |
|------|-------------|------|
| [AUD-LBL-001](AUD-LBL-001-label-not-in-github.md) | Label WI not found in GitHub | Report only |
| [AUD-LBL-002](AUD-LBL-002-service-attention-misuse.md) | Service Attention used as primary label | Report only |

### Structure Validation (AUD-STR)

Rules that validate relationships and structural integrity of the work item graph.

| Rule | Description | Fix? |
|------|-------------|------|
| [AUD-STR-001](AUD-STR-001-label-owner-missing-owners.md) | Label Owner has no Owner relations | Yes |
| [AUD-STR-002](AUD-STR-002-label-owner-missing-labels.md) | Label Owner has no Label relations | Report only |
| DEFERRED [AUD-STR-003](deferred/AUD-STR-003-package-missing-owners.md) | Package has fewer than 2 individual owners | Report only |
| DEFERRED [AUD-STR-004](deferred/AUD-STR-004-package-missing-labels.md) | Package has no PR labels | Report only |
| DEFERRED [AUD-STR-005](deferred/AUD-STR-005-orphaned-label-owner.md) | Label Owner with invalid owners after fix | Yes |
