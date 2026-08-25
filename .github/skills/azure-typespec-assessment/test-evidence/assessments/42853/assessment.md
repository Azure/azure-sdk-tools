# 📋 TypeSpec Assessment

**PR:** [#42853 - Add 2026-02-01 API version for RecoveryServices and RecoveryServicesBackup](https://github.com/Azure/azure-rest-api-specs/pull/42853)

**Overall confidence:** 🟢 high<br>
**Overall code safety:** 🔴 Low

**Baseline:** `519e87e016492a37ce9ea6ac0fdf80d26767f47d`<br>
**Head:** `efe76fb07ac03d9c54e2c64de15ef3ff90fc4030`; working-tree changes: false<br>
**Total assessment time:** 3m 18s

## 📌 Executive Summary

| Dimension | Result | Findings |
| --- | --- | ---: |
| Semantic understanding | ✅ Assessed — 2 intent(s), 11 operation(s) | n/a |
| REST compatibility | ✅ No breaks detected | 0 |
| Downstream compatibility | ❌ Issues found | 1 |
| Azure compliance | ❌ failed | 1 |

**Scope:** 2 intent(s), 11 affected operation(s), 2 project(s).<br>
**Changes:** 7 added, 4 modified, 0 removed.<br>
**Highest severity:** high.

## 🎯 Action Required

| Severity | Area | Finding | Why it matters | Code | Guidance |
| --- | --- | --- | --- | --- | --- |
| high | Downstream | Generated SDK method moves between clients | The REST contracts are unchanged, but extending four client-location customizations to Go can move bmsPrepareDataMove, bmsTriggerDataMove, getOperationStatus, and moveRecoveryPoint between generated clients. Existing Go code that constructs the former clients or invokes these methods through them can stop compiling. | [back-compatible.tsp:L67-L112](https://github.com/Azure/azure-rest-api-specs/blob/efe76fb07ac03d9c54e2c64de15ef3ff90fc4030/specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/back-compatible.tsp#L67-L112) | n/a |
| medium | Compliance | Client customizations are not located in client.tsp | The added client-location customizations are in back-compatible.tsp instead of the documented client.tsp file alongside main.tsp. | [back-compatible.tsp:L67-L112](https://github.com/Azure/azure-rest-api-specs/blob/efe76fb07ac03d9c54e2c64de15ef3ff90fc4030/specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/back-compatible.tsp#L67-L112) | [The stable 2026-02-01 promotion follows the retained stable-after-preview guidance by replacing the preview version enum member, but the three added @@clientLocation customizations do not follow the retained client customization location guidance because they are in back-compatible.tsp rather than client.tsp alongside main.tsp.](https://azure.github.io/typespec-azure/docs/howtos/generate-client-libraries/03client/) |

## 🧠 Semantic Understanding

<a id="intent-1-publish-the-inherited-recovery-services-backup-s"></a>
### 1. Publish the inherited Recovery Services Backup surface in stable 2026-02-01

| Change | Aspect | Before | After |
| --- | --- | --- | --- |
| ➕ Added | 2026-02-01 API-version availability | — | Seven existing operations are exposed in the new stable API version without a wire-behavior change. |

**TypeSpec change:** Add the stable Versions.v2026_02_01 member and make it the current version.

```diff
--- a/specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/main.tsp
+++ b/specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/main.tsp
@@ -46,24 +46,29 @@ namespace Microsoft.RecoveryServices;
 enum Versions {
   /**
    * The 2025-02-01 API version.
    */
   v2025_02_01: "2025-02-01",

   /**
    * The 2025-08-01 API version.
    */
   v2025_08_01: "2025-08-01",

   /**
 ... 17 later diff lines omitted; full hunk is in assessment.json ...
```

**Source:** [main.tsp:L62-L71](https://github.com/Azure/azure-rest-api-specs/blob/efe76fb07ac03d9c54e2c64de15ef3ff90fc4030/specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/main.tsp#L62-L71), [main.tsp:L66-L70](https://github.com/Azure/azure-rest-api-specs/blob/efe76fb07ac03d9c54e2c64de15ef3ff90fc4030/specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/main.tsp#L66-L70)

<a id="intent-2-apply-established-client-placement-to-four-go-op"></a>
### 2. Apply established client placement to four Go operations

| Change | Aspect | Before | After |
| --- | --- | --- | --- |
| ✏️ Modified | Go client location | The four methods follow the prior language exclusions and generated Go client placement. | Go is added to all four client-location exclusions, moving the methods to their established generated Go clients. |

**TypeSpec change:** Extend four existing @@clientLocation customizations from !csharp to !csharp,!go.

```diff
--- a/specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/back-compatible.tsp
+++ b/specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/back-compatible.tsp
@@ -67,15 +67,15 @@ using Microsoft.RecoveryServices;

 @@clientLocation(BackupResourceStorageConfigsNonCRR.bmsPrepareDataMove,
   Microsoft.RecoveryServices,
-  "!csharp"
+  "!csharp,!go"
 );
 @@clientLocation(BackupResourceStorageConfigsNonCRR.bmsTriggerDataMove,
   Microsoft.RecoveryServices,
-  "!csharp"
+  "!csharp,!go"
 );
 @@clientLocation(BackupResourceConfigOperationStatuses.getOperationStatus,
 ... 6 later diff lines omitted; full hunk is in assessment.json ...
```

```diff
--- a/specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/back-compatible.tsp
+++ b/specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/back-compatible.tsp
@@ -107,7 +107,7 @@ using Microsoft.RecoveryServices;

 @@clientLocation(RecoveryPoints.moveRecoveryPoint,
   Microsoft.RecoveryServices,
-  "!csharp"
+  "!csharp,!go"
 );

 @@clientLocation(ProtectionContainersOperationGroup.refresh,
```

**Impact:** [Generated SDK method moves between clients](#finding-source-go-client-location-changed), [Client customizations are not located in client.tsp](#finding-client-customizations-outside-client-tsp)<br>
**Source:** [back-compatible.tsp:L67-L112](https://github.com/Azure/azure-rest-api-specs/blob/efe76fb07ac03d9c54e2c64de15ef3ff90fc4030/specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/back-compatible.tsp#L67-L112)

Need the complete REST representation for every affected operation? Use this prompt:

`Using assessment.json for PR #42853, show the complete REST representation for every affected operation, including operation ID, method/path, parameters, request, responses, LRO, paging, and TypeSpec source.`

## 🛡️ Compatibility Assessment

### REST Breaking Changes

None detected.

### Downstream Breaking Changes

<a id="finding-source-go-client-location-changed"></a>
### Generated SDK method moves between clients

- **Severity:** high
- **Confidence:** high
- **Summary:** The REST contracts are unchanged, but extending four client-location customizations to Go can move bmsPrepareDataMove, bmsTriggerDataMove, getOperationStatus, and moveRecoveryPoint between generated clients. Existing Go code that constructs the former clients or invokes these methods through them can stop compiling.
- **Evidence:** All four @@clientLocation rules add Go to the language exclusion.; The four HTTP operation contracts remain unchanged.
- **TypeSpec source:** [back-compatible.tsp:L67-L112](https://github.com/Azure/azure-rest-api-specs/blob/efe76fb07ac03d9c54e2c64de15ef3ff90fc4030/specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/back-compatible.tsp#L67-L112)


## ☁️ Azure Compliance

**Status:** `failed`

### Compliance Findings

<a id="finding-client-customizations-outside-client-tsp"></a>
### Client customizations are not located in client.tsp

**Severity:** medium

**Gap:** The added client-location customizations are in back-compatible.tsp instead of the documented client.tsp file alongside main.tsp.

<details>
<summary><strong>Expected</strong></summary>

The stable 2026-02-01 promotion follows the retained stable-after-preview guidance by replacing the preview version enum member, but the three added @@clientLocation customizations do not follow the retained client customization location guidance because they are in back-compatible.tsp rather than client.tsp alongside main.tsp.

**Guidance:** [Clients — Retained authoritative evidence](https://azure.github.io/typespec-azure/docs/howtos/generate-client-libraries/03client/)

**Documented TypeSpec example**

```tsp
import "@azure-tools/typespec-client-generator-core";
using Azure.ClientGenerator.Core;

@@clientLocation(Feeds.feed, PetStore);
@@clientLocation(Pets.pet, PetStore);
```

</details>

<details>
<summary><strong>Actual</strong></summary>

Expected: client customizations should always be in client.tsp alongside main.tsp. Actual: three @@clientLocation customizations were added to back-compatible.tsp.

**[back-compatible.tsp:L67-L78](https://github.com/Azure/azure-rest-api-specs/blob/efe76fb07ac03d9c54e2c64de15ef3ff90fc4030/specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/back-compatible.tsp#L67-L78)**

```tsp

@@clientLocation(BackupResourceStorageConfigsNonCRR.bmsPrepareDataMove,
  Microsoft.RecoveryServices,
  "!csharp,!go"
);
@@clientLocation(BackupResourceStorageConfigsNonCRR.bmsTriggerDataMove,
  Microsoft.RecoveryServices,
  "!csharp,!go"
);
@@clientLocation(BackupResourceConfigOperationStatuses.getOperationStatus,
  Microsoft.RecoveryServices,
  "!csharp,!go"
```

</details>

## 📎 Appendix

### Assessment Errors

None.

### Code-to-Guidance Evidence

| Result | Document section | Fetched guidance | Observed TypeSpec | Evidence |
| --- | --- | --- | --- | --- |
| Matched | [Adding a Stable Version when the Last Version was Preview - Retained authoritative evidence](https://azure.github.io/typespec-azure/docs/howtos/versioning/03-stable-after-preview/) | For any type with an `@added(p)` decorator, delete the type. Remove the replaced preview version from the version enum. | The stable 2026-02-01 promotion follows the retained stable-after-preview guidance by replacing the preview version enum member, but the three added @@clientLocation customizations do not follow the retained client customization location guidance because they are in back-compatible.tsp rather than client.tsp alongside main.tsp. | [main.tsp:L62-L71](https://github.com/Azure/azure-rest-api-specs/blob/efe76fb07ac03d9c54e2c64de15ef3ff90fc4030/specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/main.tsp#L62-L71), [main.tsp:L66-L70](https://github.com/Azure/azure-rest-api-specs/blob/efe76fb07ac03d9c54e2c64de15ef3ff90fc4030/specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/main.tsp#L66-L70), [back-compatible.tsp:L67-L112](https://github.com/Azure/azure-rest-api-specs/blob/efe76fb07ac03d9c54e2c64de15ef3ff90fc4030/specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/back-compatible.tsp#L67-L112) |
| Mismatch | [Clients - Retained authoritative evidence](https://azure.github.io/typespec-azure/docs/howtos/generate-client-libraries/03client/) | Customizations SHOULD always be made in a file named `client.tsp` alongside `main.tsp`. | Expected: client customizations should always be in client.tsp alongside main.tsp. Actual: three @@clientLocation customizations were added to back-compatible.tsp. | [back-compatible.tsp:L67-L112](https://github.com/Azure/azure-rest-api-specs/blob/efe76fb07ac03d9c54e2c64de15ef3ff90fc4030/specification/recoveryservicesbackup/resource-manager/Microsoft.RecoveryServices/RecoveryServicesBackup/back-compatible.tsp#L67-L112) |

### Tooling Used

- `@azure-tools/typespec-autorest`
- `@azure-tools/typespec-client-generator-core`

### Artifact Evidence

- **autorest:** Preserved base/head emitter runs succeeded for both projects; head adds stable 2026-02-01 OpenAPI artifacts without changing any previously emitted API version.
- **tcgc:** Preserved base/head generic TCGC output adds 2026-02-01 and changes the default API version; independent base/head source comparison confirms four @@clientLocation scopes changed from !csharp to !csharp,!go.
