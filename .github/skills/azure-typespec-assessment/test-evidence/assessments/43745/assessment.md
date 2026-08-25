# 📋 TypeSpec Assessment

**PR:** [#43745 - Updated CloudHsmClusterSkuName from closed enum to extensible enum](https://github.com/Azure/azure-rest-api-specs/pull/43745)

**Overall confidence:** 🟢 high<br>
**Overall code safety:** 🔴 Low

**Baseline:** `a6887d2260f26285d4b1f5fba97da370be9200b4`<br>
**Head:** `71ce7d0c524c4bea7bc737836684acd53f343147`; working-tree changes: false<br>
**Total assessment time:** 2m 32s

## 📌 Executive Summary

| Dimension | Result | Findings |
| --- | --- | ---: |
| Semantic understanding | ✅ Assessed — 1 intent(s), 5 operation(s) | n/a |
| REST compatibility | ✅ No breaks detected | 0 |
| Downstream compatibility | ❌ Issues found | 1 |
| Azure compliance | ✅ passed | 0 |

**Scope:** 1 intent(s), 5 affected operation(s), 1 project(s).<br>
**Changes:** 0 added, 5 modified, 0 removed.<br>
**Highest severity:** high.

## 🎯 Action Required

| Severity | Area | Finding | Why it matters | Code | Guidance |
| --- | --- | --- | --- | --- | --- |
| high | Downstream | Generated SDK enum shape changes | CloudHsmClusterSkuName changes from an enum to a named string-literal union. Although its two wire values are unchanged and the union has no open string arm, the declaration-kind change can alter the generated SDK public type shape and enum-member identities, breaking source code that references the prior enum surface without requiring any REST operation change. | [models.tsp:L286-L293](https://github.com/Azure/azure-rest-api-specs/blob/71ce7d0c524c4bea7bc737836684acd53f343147/specification/hardwaresecuritymodules/resource-manager/Microsoft.HardwareSecurityModules/HardwareSecurityModules/models.tsp#L286-L293), [models.tsp:L286-L293](https://github.com/Azure/azure-rest-api-specs/blob/71ce7d0c524c4bea7bc737836684acd53f343147/specification/hardwaresecuritymodules/resource-manager/Microsoft.HardwareSecurityModules/HardwareSecurityModules/models.tsp#L286-L293) | n/a |

## ☁️ Azure Compliance

**Status:** `passed`

### Compliance Findings

No compliance mismatches found.

## 🧠 Semantic Understanding

<a id="intent-1-make-cloud-hsm-cluster-sku-names-forward-compati"></a>
### 1. Make Cloud HSM cluster SKU names forward-compatible with an open string union

| Change | Aspect | Before | After |
| --- | --- | --- | --- |
| ✏️ Modified | `sku.name` accepted values | The closed enum accepts only Standard_B1 and Standard B10. | The open string union preserves both known values and accepts future string values. |

**TypeSpec change:** Replace the suppressed enum with a union that includes string and preserves both named values.

```diff
--- a/specification/hardwaresecuritymodules/resource-manager/Microsoft.HardwareSecurityModules/HardwareSecurityModules/models.tsp
+++ b/specification/hardwaresecuritymodules/resource-manager/Microsoft.HardwareSecurityModules/HardwareSecurityModules/models.tsp
@@ -283,10 +283,14 @@ union IdentityType {
 /**
  * Sku name of the Cloud HSM Cluster
  */
-#suppress "@azure-tools/typespec-azure-core/no-enum" "FIXME: Update justification, follow aka.ms/tsp/conversion-fix for details"
-enum CloudHsmClusterSkuName {
-  Standard_B1,
-  `Standard B10`,
+union CloudHsmClusterSkuName {
+  string,
+
+  /** Standard_B1 SKU */
+  Standard_B1: "Standard_B1",
 ... 6 later diff lines omitted; full hunk is in assessment.json ...
```

**Impact:** [Generated SDK enum shape changes](#finding-source-enum-replaced-by-open-union)<br>
**Source:** [models.tsp:L286-L293](https://github.com/Azure/azure-rest-api-specs/blob/71ce7d0c524c4bea7bc737836684acd53f343147/specification/hardwaresecuritymodules/resource-manager/Microsoft.HardwareSecurityModules/HardwareSecurityModules/models.tsp#L286-L293), [models.tsp:L286-L293](https://github.com/Azure/azure-rest-api-specs/blob/71ce7d0c524c4bea7bc737836684acd53f343147/specification/hardwaresecuritymodules/resource-manager/Microsoft.HardwareSecurityModules/HardwareSecurityModules/models.tsp#L286-L293)

Need the complete REST representation for every affected operation? Use this prompt:

`Using assessment.json for PR #43745, show the complete REST representation for every affected operation, including operation ID, method/path, parameters, request, responses, LRO, paging, and TypeSpec source.`

## 🛡️ Compatibility Assessment

### REST Breaking Changes

None detected.

### Downstream Breaking Changes

<a id="finding-source-enum-replaced-by-open-union"></a>
### Generated SDK enum shape changes

- **Severity:** high
- **Confidence:** high
- **Summary:** CloudHsmClusterSkuName changes from an enum to a named string-literal union. Although its two wire values are unchanged and the union has no open string arm, the declaration-kind change can alter the generated SDK public type shape and enum-member identities, breaking source code that references the prior enum surface without requiring any REST operation change.
- **Evidence:** Replacing an enum with a string-backed union can change generated enum shape and member identities while preserving wire values.; Changed TypeSpec source: specification/hardwaresecuritymodules/resource-manager/Microsoft.HardwareSecurityModules/HardwareSecurityModules/models.tsp.
- **TypeSpec source:** [models.tsp:L286-L293](https://github.com/Azure/azure-rest-api-specs/blob/71ce7d0c524c4bea7bc737836684acd53f343147/specification/hardwaresecuritymodules/resource-manager/Microsoft.HardwareSecurityModules/HardwareSecurityModules/models.tsp#L286-L293), [models.tsp:L286-L293](https://github.com/Azure/azure-rest-api-specs/blob/71ce7d0c524c4bea7bc737836684acd53f343147/specification/hardwaresecuritymodules/resource-manager/Microsoft.HardwareSecurityModules/HardwareSecurityModules/models.tsp#L286-L293)


## 📎 Appendix

### Assessment Errors

None.

### Code-to-Guidance Evidence

| Result | Document section | Fetched guidance | Observed TypeSpec | Evidence |
| --- | --- | --- | --- | --- |
| Matched | Azure.Core no-enum rule - Models and Enums | no-enum \| TypeSpec Azure Skip to content TypeSpec Azure Docs Playground TypeSpec Core Docs Can I Use (Azure Client) Benchmarks Search... Introduction Get started Installation Creating a project Versioning Azure Data Plane Service 1. Writing Your First Service 2. Create the service namespace 3. Defining your first resource 4. Defining standard resource operations 5. Defining long-running resource operations 6. Defining c | The applicable official Azure.Core no-enum guidance identifies enum declarations as the disallowed pattern. The changed TypeSpec directly replaces CloudHsmClusterSkuName's enum declaration with a union and removes the no-enum suppression, so the changed declaration follows that guidance. The other fetched documents describe general data types, TypeSpec enum syntax, or operation interfaces and impose no additional requirement on this declaration. | models.tsp:L286-L293, models.tsp:L286-L293 |

### Tooling Used

- `@azure-tools/typespec-autorest`
- `@azure-tools/typespec-client-generator-core`

### Artifact Evidence

- **autorest:** Preserved baseline/head artifacts succeeded; the only OpenAPI diff changes CloudHsmClusterSkuName x-ms-enum modelAsString from false to true and adds explicit metadata for the same two wire values.
- **tcgc:** Preserved baseline/head artifacts succeeded; CloudHsmClusterSkuName changes from fixed enum metadata to union-as-enum metadata and the Standard B10 member identity changes to StandardB10.
