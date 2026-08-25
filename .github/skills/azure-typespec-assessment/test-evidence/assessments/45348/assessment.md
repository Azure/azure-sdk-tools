# 📋 TypeSpec Assessment

**PR:** [#45348 - Fix cross-version breaking changes for 2026-11 previews](https://github.com/Azure/azure-rest-api-specs/pull/45348)

**Overall confidence:** 🟢 high<br>
**Overall code safety:** 🟢 High

**Baseline:** `2991970c5377110244bcf9614be03e5aaf32362f`<br>
**Head:** `b209254310b3e569b16210d3c59fc8b8ac3b84b4`; working-tree changes: false<br>
**Total assessment time:** 2m 14s

## 📌 Executive Summary

| Dimension | Result | Findings |
| --- | --- | ---: |
| Semantic understanding | ✅ Assessed — 1 intent(s), 2 operation(s) | n/a |
| REST compatibility | ✅ No breaks detected | 0 |
| Downstream compatibility | ✅ No breaks detected | 0 |
| Azure compliance | ✅ passed | 0 |

**Scope:** 1 intent(s), 2 affected operation(s), 1 project(s).<br>
**Changes:** 0 added, 2 modified, 0 removed.<br>
**Highest severity:** none.

## 🎯 Action Required

No action required from the assessed dimensions.

## ☁️ Azure Compliance

**Status:** `passed`

### Compliance Findings

No compliance mismatches found.

## 🧠 Semantic Understanding

<a id="intent-1-align-the-new-device-registry-previews-with-arm-"></a>
### 1. Align the new Device Registry previews with ARM common-types v5

| Change | Aspect | Before | After |
| --- | --- | --- | --- |
| ✏️ Modified | ARM common-types dependency | The two preview versions reference common-types v6. | Both preview versions reference common-types v5. |

**TypeSpec change:** Change @armCommonTypesVersion from v6 to v5 on the 2026-11-01-preview and 2026-11-02-preview version members.

```diff
--- a/specification/deviceregistry/DeviceRegistry.Management/main.tsp
+++ b/specification/deviceregistry/DeviceRegistry.Management/main.tsp
@@ -75,12 +75,12 @@ enum Versions {
   v2026_04_01: "2026-04-01",

   @doc("Microsoft.DeviceRegistry Resource Provider management API version 2026-11-01-preview.")
-  @armCommonTypesVersion(Azure.ResourceManager.CommonTypes.Versions.v6)
+  @armCommonTypesVersion(Azure.ResourceManager.CommonTypes.Versions.v5)
   v2026_11_01_preview: "2026-11-01-preview",

   @doc("Microsoft.DeviceRegistry Resource Provider management API version 2026-11-02-preview.")
   @Azure.Core.previewVersion
-  @armCommonTypesVersion(Azure.ResourceManager.CommonTypes.Versions.v6)
+  @armCommonTypesVersion(Azure.ResourceManager.CommonTypes.Versions.v5)
   v2026_11_02_preview: "2026-11-02-preview",
 ... 2 later diff lines omitted; full hunk is in assessment.json ...
```

**Source:** [main.tsp:L75-L84](https://github.com/Azure/azure-rest-api-specs/blob/b209254310b3e569b16210d3c59fc8b8ac3b84b4/specification/deviceregistry/DeviceRegistry.Management/main.tsp#L75-L84)

Need the complete REST representation for every affected operation? Use this prompt:

`Using assessment.json for PR #45348, show the complete REST representation for every affected operation, including operation ID, method/path, parameters, request, responses, LRO, paging, and TypeSpec source.`

## 🛡️ Compatibility Assessment

### REST Breaking Changes

None detected.

### Downstream Breaking Changes

None detected.

## 📎 Appendix

### Assessment Errors

None.

### Code-to-Guidance Evidence

| Result | Document section | Fetched guidance | Observed TypeSpec | Evidence |
| --- | --- | --- | --- | --- |
| Matched | ARM common types version rule - Models and Enums | . Introduction Get started Installation Creating a project Versioning Azure Data Plane Service 1. Writing Your First Service 2. Create the service namespace 3. Defining your first resource 4. Defining standard resource operations 5. Defining long-running resource operations 6. Defining child resources 7. Defining custom resource actions 8. Customizing operations with traits 9. Versioning 10. Complete Example 11. Advanced Topics ARM Service 1. Installing Tools 2. Defining the Service 3. Defining | The official ARM common types version rule documents @armCommonTypesVersion on an ARM provider namespace and explicitly shows Azure.ResourceManager.CommonTypes.Versions.v5 as a valid selection. The exact changed declaration uses that documented decorator and enum member, so the changed source follows the applicable documented pattern. | main.tsp:L75-L84 |

### Tooling Used

- `@azure-tools/typespec-autorest`
- `@azure-tools/typespec-client-generator-core`

### Artifact Evidence

- **autorest:** base/head succeeded; both preview OpenAPI files switch common-type references from v6 to v5
- **tcgc:** base/head succeeded; client model reflects aligned ARM envelope types
