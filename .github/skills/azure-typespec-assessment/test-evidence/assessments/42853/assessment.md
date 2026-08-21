# 📋 TypeSpec Assessment

**PR:** [#42853 - Add 2026-02-01 API version for RecoveryServices and RecoveryServicesBackup](https://github.com/Azure/azure-rest-api-specs/pull/42853)

**Overall confidence:** 🟢 high<br>
**Overall code safety:** 🔴 Low

**Baseline:** `519e87e016492a37ce9ea6ac0fdf80d26767f47d`<br>
**Head:** `efe76fb07ac03d9c54e2c64de15ef3ff90fc4030`; working-tree changes: false<br>
**Total assessment time:** 13m 33s

## 📌 Executive Summary

| Dimension | Result | Findings |
| --- | --- | ---: |
| Semantic understanding | ✅ Assessed — 1 intent(s), 4 operation(s) | n/a |
| REST compatibility | ✅ No breaks detected | 0 |
| Downstream compatibility | ❌ Issues found | 1 |
| Azure compliance | ⚠️ not-assessed | 0 |

**Scope:** 1 intent(s), 4 affected operation(s), 2 project(s).<br>
**Highest severity:** high.

## 🎯 Action Required

| Severity | Area | Finding | Why it matters | Code | Guidance |
| --- | --- | --- | --- | --- | --- |
| high | Downstream | Existing Go methods move between generated clients | Existing Go construction and method calls can stop compiling although the REST contract is unchanged. | [back-compatible.tsp:L67-L112](https://github.com/Azure/azure-rest-api-specs/blob/efe76fb07ac03d9c54e2c64de15ef3ff90fc4030/specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/back-compatible.tsp#L67-L112) | n/a |

## 🧠 Semantic Understanding

### Change Overview

| # | Intent | Operations | API versions | Details |
| ---: | --- | ---: | --- | --- |
| 1 | Add a stable API version while changing Go client placement for four existing operations. | 4 | 2026-02-01 | [details](#intent-1-add-a-stable-api-version-while-changing-go-clien) |

### Operation Details

<a id="intent-1-add-a-stable-api-version-while-changing-go-clien"></a>
### 1. Add a stable API version while changing Go client placement for four existing operations.

**Confidence:** high<br>
**REST summary:** Operation paths, parameters, requests, and responses remain unchanged; only generated client ownership changes.

#### `BackupResourceStorageConfigsNonCRR_BMSPrepareDataMove`

- **HTTP path:** `POST /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.RecoveryServices/vaults/{vaultName}/backupstorageconfig/vaultstorageconfig/prepareDataMove`
- **API versions:** `2026-02-01`
- **Parameters:** path subscriptionId, resourceGroupName, vaultName: string, required; query api-version: string, required
- **Request payload:** application/json body: PrepareDataMoveRequest
- **Response payloads:** 200: no body; default: ErrorResponse
- **Service behavior:** Validates and prepares backup data movement.
- **LRO:** No.
- **Paging:** No.
- **TypeSpec source:** [back-compatible.tsp:L67-L112](https://github.com/Azure/azure-rest-api-specs/blob/efe76fb07ac03d9c54e2c64de15ef3ff90fc4030/specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/back-compatible.tsp#L67-L112)

#### `BackupResourceStorageConfigsNonCRR_BMSTriggerDataMove`

- **HTTP path:** `POST /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.RecoveryServices/vaults/{vaultName}/backupstorageconfig/vaultstorageconfig/triggerDataMove`
- **API versions:** `2026-02-01`
- **Parameters:** path subscriptionId, resourceGroupName, vaultName: string, required; query api-version: string, required
- **Request payload:** application/json body: TriggerDataMoveRequest
- **Response payloads:** 202: Azure-AsyncOperation and Retry-After headers; default: ErrorResponse
- **Service behavior:** Starts asynchronous movement of backup data.
- **LRO:** arm; via azure-async-operation; Poll the emitted async endpoint after Retry-After until a terminal state.; final result: Use the final response contract described for this operation.
- **Paging:** No.
- **TypeSpec source:** [back-compatible.tsp:L67-L112](https://github.com/Azure/azure-rest-api-specs/blob/efe76fb07ac03d9c54e2c64de15ef3ff90fc4030/specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/back-compatible.tsp#L67-L112)

#### `BackupResourceConfigOperationStatuses_GetOperationStatus`

- **HTTP path:** `GET /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.RecoveryServices/vaults/{vaultName}/backupstorageconfig/vaultstorageconfig/operationStatus/{operationId}`
- **API versions:** `2026-02-01`
- **Parameters:** path subscriptionId, resourceGroupName, vaultName, operationId: string, required; query api-version: string, required
- **Request payload:** none
- **Response payloads:** 200: OperationStatus; default: ErrorResponse
- **Service behavior:** Returns the current status of a backup storage configuration operation.
- **LRO:** No.
- **Paging:** No.
- **TypeSpec source:** [back-compatible.tsp:L67-L112](https://github.com/Azure/azure-rest-api-specs/blob/efe76fb07ac03d9c54e2c64de15ef3ff90fc4030/specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/back-compatible.tsp#L67-L112)

#### `RecoveryPoints_MoveRecoveryPoint`

- **HTTP path:** `POST /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.RecoveryServices/vaults/{vaultName}/backupFabrics/{fabricName}/protectionContainers/{containerName}/protectedItems/{protectedItemName}/recoveryPoints/{recoveryPointId}/move`
- **API versions:** `2026-02-01`
- **Parameters:** all ARM resource path identifiers: string, required; query api-version: string, required
- **Request payload:** application/json body: MoveRecoveryPointRequest
- **Response payloads:** 202: Azure-AsyncOperation and Retry-After headers; default: ErrorResponse
- **Service behavior:** Starts asynchronous movement of a recovery point.
- **LRO:** arm; via azure-async-operation; Poll the emitted async endpoint after Retry-After until a terminal state.; final result: Use the final response contract described for this operation.
- **Paging:** No.
- **TypeSpec source:** [back-compatible.tsp:L67-L112](https://github.com/Azure/azure-rest-api-specs/blob/efe76fb07ac03d9c54e2c64de15ef3ff90fc4030/specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/back-compatible.tsp#L67-L112)
## 🛡️ Compatibility Assessment

### REST Breaking Changes

None detected.

### Downstream Breaking Changes

### Existing Go methods move between generated clients

- **Severity:** high
- **Confidence:** high
- **Summary:** Existing Go construction and method calls can stop compiling although the REST contract is unchanged.
- **Evidence:** The clientLocation scope changes from !csharp to !csharp,!go.
- **TypeSpec source:** [back-compatible.tsp:L67-L112](https://github.com/Azure/azure-rest-api-specs/blob/efe76fb07ac03d9c54e2c64de15ef3ff90fc4030/specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/back-compatible.tsp#L67-L112)


## ☁️ Azure Compliance

**Status:** `not-assessed`

The versioning changes follow fetched guidance, but the shared catalog has no authoritative document for the language-scoped @@clientLocation exclusions changed in the same PR.

### Compliance Findings

No compliance mismatches found.

## 📎 Appendix

### Assessment Errors

None.

### Code-to-Guidance Evidence

| Result | Document section | Fetched guidance | Observed TypeSpec | Evidence |
| --- | --- | --- | --- | --- |
| Matched | [Versioning overview - Version progression](https://azure.github.io/typespec-azure/docs/howtos/versioning/01-about-versioning/) | Every api-version uses a unique `YYYY-MM-DD` date and versions are declared in strictly increasing chronological order from top to bottom of the `Versions` enum. | The new stable version follows the existing version sequence. | [main.tsp:L55-L59](https://github.com/Azure/azure-rest-api-specs/blob/efe76fb07ac03d9c54e2c64de15ef3ff90fc4030/specification/recoveryservices/resource-manager/Microsoft.RecoveryServices/RecoveryServices/main.tsp#L55-L59), [main.tsp:L66-L70](https://github.com/Azure/azure-rest-api-specs/blob/efe76fb07ac03d9c54e2c64de15ef3ff90fc4030/specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/main.tsp#L66-L70) |
| Matched | [Evolving APIs - Adding API elements](https://azure.github.io/typespec-azure/docs/howtos/versioning/06-evolving-apis/) | You can add new models, properties, or operations in a specific version and all subsequent versions using the `@added` decorator. | Preview-only recovery shapes remain scoped out of the new stable version. | [main.tsp:L55-L59](https://github.com/Azure/azure-rest-api-specs/blob/efe76fb07ac03d9c54e2c64de15ef3ff90fc4030/specification/recoveryservices/resource-manager/Microsoft.RecoveryServices/RecoveryServices/main.tsp#L55-L59), [RecoveryPointResource.tsp:L80-L80](https://github.com/Azure/azure-rest-api-specs/blob/efe76fb07ac03d9c54e2c64de15ef3ff90fc4030/specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/RecoveryPointResource.tsp#L80-L80), [main.tsp:L66-L70](https://github.com/Azure/azure-rest-api-specs/blob/efe76fb07ac03d9c54e2c64de15ef3ff90fc4030/specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/main.tsp#L66-L70), +29 more |

### Tooling Used

- `@azure-tools/typespec-autorest`
- `@azure-tools/typespec-client-generator-core`

### Repository Validation

| Project | Tool | Status | Duration | Log |
| --- | --- | --- | ---: | --- |
| `specification/recoveryservices/resource-manager/Microsoft.RecoveryServices/RecoveryServices` | `TypeSpecValidation` | succeeded | 28s | `validation-logs/specification__recoveryservices__resource-manager__Microsoft.RecoveryServices__RecoveryServices-head.log` |
| `specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup` | `TypeSpecValidation` | succeeded | 36s | `validation-logs/specification__recoveryservicesbackup__resource-manager__Microsoft.RecoveryServices__RecoveryServicesBackup-head.log` |

### Artifact Evidence

- **autorest:** base/head succeeded for both projects; a new stable OpenAPI version is emitted
- **tcgc:** base/head succeeded; generic model generated, while Go-scoped movement is confirmed from source decorators

### Changed TypeSpec

- `specification/recoveryservices/resource-manager/Microsoft.RecoveryServices/RecoveryServices/main.tsp`: [main.tsp:L55-L59](https://github.com/Azure/azure-rest-api-specs/blob/efe76fb07ac03d9c54e2c64de15ef3ff90fc4030/specification/recoveryservices/resource-manager/Microsoft.RecoveryServices/RecoveryServices/main.tsp#L55-L59)
- `specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/RecoveryPointResource.tsp`: [RecoveryPointResource.tsp:L80-L80](https://github.com/Azure/azure-rest-api-specs/blob/efe76fb07ac03d9c54e2c64de15ef3ff90fc4030/specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/RecoveryPointResource.tsp#L80-L80)
- `specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/back-compatible.tsp`: [back-compatible.tsp:L70-L70](https://github.com/Azure/azure-rest-api-specs/blob/efe76fb07ac03d9c54e2c64de15ef3ff90fc4030/specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/back-compatible.tsp#L70-L70), [back-compatible.tsp:L74-L74](https://github.com/Azure/azure-rest-api-specs/blob/efe76fb07ac03d9c54e2c64de15ef3ff90fc4030/specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/back-compatible.tsp#L74-L74), [back-compatible.tsp:L78-L78](https://github.com/Azure/azure-rest-api-specs/blob/efe76fb07ac03d9c54e2c64de15ef3ff90fc4030/specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/back-compatible.tsp#L78-L78), [back-compatible.tsp:L110-L110](https://github.com/Azure/azure-rest-api-specs/blob/efe76fb07ac03d9c54e2c64de15ef3ff90fc4030/specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/back-compatible.tsp#L110-L110)
- `specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/main.tsp`: [main.tsp:L66-L70](https://github.com/Azure/azure-rest-api-specs/blob/efe76fb07ac03d9c54e2c64de15ef3ff90fc4030/specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/main.tsp#L66-L70)
- `specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/models.tsp`: [models.tsp:L271-L271](https://github.com/Azure/azure-rest-api-specs/blob/efe76fb07ac03d9c54e2c64de15ef3ff90fc4030/specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/models.tsp#L271-L271), [models.tsp:L289-L289](https://github.com/Azure/azure-rest-api-specs/blob/efe76fb07ac03d9c54e2c64de15ef3ff90fc4030/specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/models.tsp#L289-L289), [models.tsp:L310-L310](https://github.com/Azure/azure-rest-api-specs/blob/efe76fb07ac03d9c54e2c64de15ef3ff90fc4030/specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/models.tsp#L310-L310), [models.tsp:L327-L327](https://github.com/Azure/azure-rest-api-specs/blob/efe76fb07ac03d9c54e2c64de15ef3ff90fc4030/specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/models.tsp#L327-L327), [models.tsp:L351-L351](https://github.com/Azure/azure-rest-api-specs/blob/efe76fb07ac03d9c54e2c64de15ef3ff90fc4030/specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/models.tsp#L351-L351), [models.tsp:L372-L372](https://github.com/Azure/azure-rest-api-specs/blob/efe76fb07ac03d9c54e2c64de15ef3ff90fc4030/specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/models.tsp#L372-L372), [models.tsp:L393-L393](https://github.com/Azure/azure-rest-api-specs/blob/efe76fb07ac03d9c54e2c64de15ef3ff90fc4030/specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/models.tsp#L393-L393), [models.tsp:L741-L741](https://github.com/Azure/azure-rest-api-specs/blob/efe76fb07ac03d9c54e2c64de15ef3ff90fc4030/specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/models.tsp#L741-L741), [models.tsp:L851-L851](https://github.com/Azure/azure-rest-api-specs/blob/efe76fb07ac03d9c54e2c64de15ef3ff90fc4030/specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/models.tsp#L851-L851), [models.tsp:L866-L866](https://github.com/Azure/azure-rest-api-specs/blob/efe76fb07ac03d9c54e2c64de15ef3ff90fc4030/specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/models.tsp#L866-L866), [models.tsp:L1010-L1010](https://github.com/Azure/azure-rest-api-specs/blob/efe76fb07ac03d9c54e2c64de15ef3ff90fc4030/specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/models.tsp#L1010-L1010), [models.tsp:L1015-L1015](https://github.com/Azure/azure-rest-api-specs/blob/efe76fb07ac03d9c54e2c64de15ef3ff90fc4030/specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/models.tsp#L1015-L1015), [models.tsp:L1302-L1302](https://github.com/Azure/azure-rest-api-specs/blob/efe76fb07ac03d9c54e2c64de15ef3ff90fc4030/specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/models.tsp#L1302-L1302), [models.tsp:L2388-L2388](https://github.com/Azure/azure-rest-api-specs/blob/efe76fb07ac03d9c54e2c64de15ef3ff90fc4030/specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/models.tsp#L2388-L2388), [models.tsp:L2420-L2420](https://github.com/Azure/azure-rest-api-specs/blob/efe76fb07ac03d9c54e2c64de15ef3ff90fc4030/specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/models.tsp#L2420-L2420), [models.tsp:L2427-L2427](https://github.com/Azure/azure-rest-api-specs/blob/efe76fb07ac03d9c54e2c64de15ef3ff90fc4030/specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/models.tsp#L2427-L2427), [models.tsp:L3227-L3227](https://github.com/Azure/azure-rest-api-specs/blob/efe76fb07ac03d9c54e2c64de15ef3ff90fc4030/specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/models.tsp#L3227-L3227), [models.tsp:L3239-L3239](https://github.com/Azure/azure-rest-api-specs/blob/efe76fb07ac03d9c54e2c64de15ef3ff90fc4030/specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/models.tsp#L3239-L3239), [models.tsp:L3251-L3251](https://github.com/Azure/azure-rest-api-specs/blob/efe76fb07ac03d9c54e2c64de15ef3ff90fc4030/specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/models.tsp#L3251-L3251), [models.tsp:L3591-L3591](https://github.com/Azure/azure-rest-api-specs/blob/efe76fb07ac03d9c54e2c64de15ef3ff90fc4030/specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/models.tsp#L3591-L3591), [models.tsp:L4636-L4636](https://github.com/Azure/azure-rest-api-specs/blob/efe76fb07ac03d9c54e2c64de15ef3ff90fc4030/specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/models.tsp#L4636-L4636), [models.tsp:L4643-L4643](https://github.com/Azure/azure-rest-api-specs/blob/efe76fb07ac03d9c54e2c64de15ef3ff90fc4030/specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/models.tsp#L4643-L4643), [models.tsp:L4652-L4652](https://github.com/Azure/azure-rest-api-specs/blob/efe76fb07ac03d9c54e2c64de15ef3ff90fc4030/specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/models.tsp#L4652-L4652), [models.tsp:L5176-L5176](https://github.com/Azure/azure-rest-api-specs/blob/efe76fb07ac03d9c54e2c64de15ef3ff90fc4030/specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/models.tsp#L5176-L5176), [models.tsp:L5344-L5344](https://github.com/Azure/azure-rest-api-specs/blob/efe76fb07ac03d9c54e2c64de15ef3ff90fc4030/specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/models.tsp#L5344-L5344), [models.tsp:L5352-L5352](https://github.com/Azure/azure-rest-api-specs/blob/efe76fb07ac03d9c54e2c64de15ef3ff90fc4030/specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/models.tsp#L5352-L5352), [models.tsp:L5360-L5360](https://github.com/Azure/azure-rest-api-specs/blob/efe76fb07ac03d9c54e2c64de15ef3ff90fc4030/specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/models.tsp#L5360-L5360), [models.tsp:L5777-L5777](https://github.com/Azure/azure-rest-api-specs/blob/efe76fb07ac03d9c54e2c64de15ef3ff90fc4030/specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/models.tsp#L5777-L5777), [models.tsp:L7720-L7720](https://github.com/Azure/azure-rest-api-specs/blob/efe76fb07ac03d9c54e2c64de15ef3ff90fc4030/specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/models.tsp#L7720-L7720)
