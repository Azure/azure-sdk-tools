# 📋 TypeSpec Assessment

**PR:** [#43745 - Updated CloudHsmClusterSkuName from closed enum to extensible enum](https://github.com/Azure/azure-rest-api-specs/pull/43745)

**Overall confidence:** 🟡 medium<br>
**Overall code safety:** 🔴 Low

**Baseline:** `a6887d2260f26285d4b1f5fba97da370be9200b4`<br>
**Head:** `71ce7d0c524c4bea7bc737836684acd53f343147`; working-tree changes: false<br>
**Total assessment time:** 7m 52s

## 📌 Executive Summary

| Dimension | Result | Findings |
| --- | --- | ---: |
| Semantic understanding | ✅ Assessed — 1 intent(s), 3 operation(s) | n/a |
| REST compatibility | ✅ No breaks detected | 0 |
| Downstream compatibility | ❌ Issues found | 1 |
| Azure compliance | ✅ passed | 0 |

**Scope:** 1 intent(s), 3 affected operation(s), 1 project(s).<br>
**Highest severity:** high.

## 🎯 Action Required

| Severity | Area | Finding | Why it matters | Code | Guidance |
| --- | --- | --- | --- | --- | --- |
| high | Downstream | Generated closed enum becomes an extensible string-backed shape | REST becomes more permissive, but generated enum typing, construction, comparison, and exhaustive handling can change. | [models.tsp:L283-L296](https://github.com/Azure/azure-rest-api-specs/blob/71ce7d0c524c4bea7bc737836684acd53f343147/specification/hardwaresecuritymodules/resource-manager/Microsoft.HardwareSecurityModules/HardwareSecurityModules/models.tsp#L283-L296) | n/a |

## 🧠 Semantic Understanding

### Change Overview

| # | Intent | Operations | API versions | Details |
| ---: | --- | ---: | --- | --- |
| 1 | Make CloudHsmClusterSkuName forward-compatible by changing it from a closed enum to an extensible string union. | 3 | 2025-03-31 | [details](#intent-1-make-cloudhsmclusterskuname-forward-compatible-b) |

### Operation Details

<a id="intent-1-make-cloudhsmclusterskuname-forward-compatible-b"></a>
### 1. Make CloudHsmClusterSkuName forward-compatible by changing it from a closed enum to an extensible string union.

**Confidence:** high<br>
**REST summary:** The same known strings remain valid and unknown strings become allowed; OpenAPI x-ms-enum modelAsString changes from false to true.

#### `CloudHsmClusters_CreateOrUpdate`

- **HTTP path:** `PUT /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.HardwareSecurityModules/cloudHsmClusters/{cloudHsmClusterName}`
- **API versions:** `2025-03-31`
- **Parameters:** path subscriptionId, resourceGroupName, cloudHsmClusterName: string, required; query api-version: string, required
- **Request payload:** application/json body: CloudHsmCluster with sku.name
- **Response payloads:** 200 or 201: CloudHsmCluster; default: ErrorResponse
- **Service behavior:** Creates or replaces a Cloud HSM cluster using the requested SKU.
- **LRO:** arm; via azure-async-operation; Poll the emitted async endpoint after Retry-After until a terminal state.; final result: Use the final response contract described for this operation.
- **Paging:** No.
- **TypeSpec source:** [models.tsp:L283-L296](https://github.com/Azure/azure-rest-api-specs/blob/71ce7d0c524c4bea7bc737836684acd53f343147/specification/hardwaresecuritymodules/resource-manager/Microsoft.HardwareSecurityModules/HardwareSecurityModules/models.tsp#L283-L296)

#### `CloudHsmClusters_Get`

- **HTTP path:** `GET /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.HardwareSecurityModules/cloudHsmClusters/{cloudHsmClusterName}`
- **API versions:** `2025-03-31`
- **Parameters:** path subscriptionId, resourceGroupName, cloudHsmClusterName: string, required; query api-version: string, required
- **Request payload:** none
- **Response payloads:** 200: CloudHsmCluster; default: ErrorResponse
- **Service behavior:** Returns a Cloud HSM cluster and its SKU.
- **LRO:** No.
- **Paging:** No.
- **TypeSpec source:** [models.tsp:L283-L296](https://github.com/Azure/azure-rest-api-specs/blob/71ce7d0c524c4bea7bc737836684acd53f343147/specification/hardwaresecuritymodules/resource-manager/Microsoft.HardwareSecurityModules/HardwareSecurityModules/models.tsp#L283-L296)

#### `CloudHsmClusters_ListByResourceGroup`

- **HTTP path:** `GET /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.HardwareSecurityModules/cloudHsmClusters`
- **API versions:** `2025-03-31`
- **Parameters:** path subscriptionId and resourceGroupName: string, required; query api-version: string, required
- **Request payload:** none
- **Response payloads:** 200: CloudHsmClusterListResult; default: ErrorResponse
- **Service behavior:** Lists Cloud HSM clusters and their SKU values in one resource group.
- **LRO:** No.
- **Paging:** CloudHsmCluster; nextLink; GET the opaque nextLink until it is absent.
- **TypeSpec source:** [models.tsp:L283-L296](https://github.com/Azure/azure-rest-api-specs/blob/71ce7d0c524c4bea7bc737836684acd53f343147/specification/hardwaresecuritymodules/resource-manager/Microsoft.HardwareSecurityModules/HardwareSecurityModules/models.tsp#L283-L296)
## 🛡️ Compatibility Assessment

### REST Breaking Changes

None detected.

### Downstream Breaking Changes

### Generated closed enum becomes an extensible string-backed shape

- **Severity:** high
- **Confidence:** high
- **Summary:** REST becomes more permissive, but generated enum typing, construction, comparison, and exhaustive handling can change.
- **Evidence:** AutoRest sets modelAsString to true.; TCGC changes enum member metadata.
- **TypeSpec source:** [models.tsp:L283-L296](https://github.com/Azure/azure-rest-api-specs/blob/71ce7d0c524c4bea7bc737836684acd53f343147/specification/hardwaresecuritymodules/resource-manager/Microsoft.HardwareSecurityModules/HardwareSecurityModules/models.tsp#L283-L296)


## ☁️ Azure Compliance

**Status:** `passed`

### Compliance Findings

No compliance mismatches found.

## 📎 Appendix

### Assessment Errors

None.

### Code-to-Guidance Evidence

| Result | Document section | Fetched guidance | Observed TypeSpec | Evidence |
| --- | --- | --- | --- | --- |
| Matched | [Azure Core no-enum rule - Rule and remediation](https://azure.github.io/typespec-azure/docs/libraries/azure-core/rules/no-enum/) | Using a union with the base scalar (`string`, `int32`, `int64`, etc.) as a variant instead of an enum makes it extensible. | CloudHsmClusterSkuName changes from a suppressed enum to a union containing string and two named variants with unchanged wire values. | [models.tsp:L283-L296](https://github.com/Azure/azure-rest-api-specs/blob/71ce7d0c524c4bea7bc737836684acd53f343147/specification/hardwaresecuritymodules/resource-manager/Microsoft.HardwareSecurityModules/HardwareSecurityModules/models.tsp#L283-L296), [models.tsp:L286-L293](https://github.com/Azure/azure-rest-api-specs/blob/71ce7d0c524c4bea7bc737836684acd53f343147/specification/hardwaresecuritymodules/resource-manager/Microsoft.HardwareSecurityModules/HardwareSecurityModules/models.tsp#L286-L293) |

### Tooling Used

- `@azure-tools/typespec-autorest`
- `@azure-tools/typespec-client-generator-core`

### Repository Validation

| Project | Tool | Status | Duration | Log |
| --- | --- | --- | ---: | --- |
| `specification/hardwaresecuritymodules/resource-manager/Microsoft.HardwareSecurityModules/HardwareSecurityModules` | `TypeSpecValidation` | skipped | 0s | `unknown` |

### Artifact Evidence

- **autorest:** base/head succeeded; modelAsString changes false to true
- **tcgc:** base/head succeeded; enum metadata and StandardB10 definition identity change

### Changed TypeSpec

- `specification/hardwaresecuritymodules/resource-manager/Microsoft.HardwareSecurityModules/HardwareSecurityModules/models.tsp`: [models.tsp:L286-L293](https://github.com/Azure/azure-rest-api-specs/blob/71ce7d0c524c4bea7bc737836684acd53f343147/specification/hardwaresecuritymodules/resource-manager/Microsoft.HardwareSecurityModules/HardwareSecurityModules/models.tsp#L286-L293)
