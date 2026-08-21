# 📋 TypeSpec Assessment

**PR:** [#45348 - Fix cross-version breaking changes for 2026-11 previews](https://github.com/Azure/azure-rest-api-specs/pull/45348)

**Overall confidence:** 🟢 high<br>
**Overall code safety:** 🟢 High

**Baseline:** `2991970c5377110244bcf9614be03e5aaf32362f`<br>
**Head:** `b209254310b3e569b16210d3c59fc8b8ac3b84b4`; working-tree changes: false<br>
**Assessment time:** 3m 18s

## 📌 Executive Summary

| Dimension | Result | Findings |
| --- | --- | ---: |
| Semantic understanding | ✅ Assessed — 1 intent(s), 2 operation(s) | n/a |
| REST compatibility | ✅ No breaks detected | 0 |
| Downstream compatibility | ✅ No breaks detected | 0 |
| Azure compliance | ✅ passed | 0 |

**Scope:** 1 intent(s), 2 affected operation(s), 1 project(s).<br>
**Highest severity:** none.

## 🎯 Action Required

No action required from the assessed dimensions.

## 🧠 Semantic Understanding

### Change Overview

| # | Intent | Operations | Shape | API versions | Linked findings | Details |
| ---: | --- | ---: | --- | --- | --- | --- |
| 1 | Align the two new Device Registry previews with ARM common-types v5 used by prior versions. | 2 | PUT · 2 LRO · 0 paged | 2026-11-01-preview, 2026-11-15-preview | No linked finding | [details](#intent-1-align-the-two-new-device-registry-previews-with-) |

### Operation Details

<a id="intent-1-align-the-two-new-device-registry-previews-with-"></a>
### 1. Align the two new Device Registry previews with ARM common-types v5 used by prior versions.

**Confidence:** high<br>
**REST summary:** Generated OpenAPI references v5 ARM envelopes instead of v6, removing cross-version identity, SKU, plan, and requiredness drift.

#### `SchemaRegistries_CreateOrReplace`

- **HTTP path:** `PUT /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.DeviceRegistry/schemaRegistries/{schemaRegistryName}`
- **API versions:** `2026-11-01-preview`, `2026-11-15-preview`
- **Parameters:** path subscriptionId, resourceGroupName, schemaRegistryName: string, required; query api-version: string, required
- **Request payload:** application/json body: SchemaRegistry
- **Response payloads:** 200 or 201: SchemaRegistry; default: ErrorResponse
- **Service behavior:** Creates or replaces a schema registry using ARM common-types v5 envelopes.
- **LRO:** arm; via azure-async-operation; Poll the emitted async endpoint after Retry-After until a terminal state.; final result: Use the final response contract described for this operation.
- **Paging:** No.
- **TypeSpec source:** [main.tsp:L75-L84](https://github.com/Azure/azure-rest-api-specs/blob/b209254310b3e569b16210d3c59fc8b8ac3b84b4/specification/deviceregistry/DeviceRegistry.Management/main.tsp#L75-L84)

#### `Namespaces_CreateOrReplace`

- **HTTP path:** `PUT /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.DeviceRegistry/namespaces/{namespaceName}`
- **API versions:** `2026-11-01-preview`, `2026-11-15-preview`
- **Parameters:** path subscriptionId, resourceGroupName, namespaceName: string, required; query api-version: string, required
- **Request payload:** application/json body: Namespace
- **Response payloads:** 200 or 201: Namespace; default: ErrorResponse
- **Service behavior:** Creates or replaces a namespace using ARM common-types v5 envelopes.
- **LRO:** arm; via azure-async-operation; Poll the emitted async endpoint after Retry-After until a terminal state.; final result: Use the final response contract described for this operation.
- **Paging:** No.
- **TypeSpec source:** [main.tsp:L75-L84](https://github.com/Azure/azure-rest-api-specs/blob/b209254310b3e569b16210d3c59fc8b8ac3b84b4/specification/deviceregistry/DeviceRegistry.Management/main.tsp#L75-L84)
## 🛡️ Compatibility Assessment

### REST Breaking Changes

None detected.

### Downstream Breaking Changes

None detected.

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
| Matched | [Evolving APIs - Defining API versions](https://azure.github.io/typespec-azure/docs/howtos/versioning/06-evolving-apis/) | For each version, specify dependencies and common types as needed. | Both new preview versions explicitly select ARM common-types v5, matching their generated contracts. | [main.tsp:L75-L84](https://github.com/Azure/azure-rest-api-specs/blob/b209254310b3e569b16210d3c59fc8b8ac3b84b4/specification/deviceregistry/DeviceRegistry.Management/main.tsp#L75-L84), [main.tsp:L78-L78](specification/deviceregistry/DeviceRegistry.Management/main.tsp#L78-L78), [main.tsp:L83-L83](specification/deviceregistry/DeviceRegistry.Management/main.tsp#L83-L83) |

### Timing

- **Toolchain setup:** 1m 8s
- **Preparation:** 2m 10s
- **Documentation assessment:** 0s
- **Total attributed time:** 3m 18s


### Tooling Used

- `@azure-tools/typespec-autorest`
- `@azure-tools/typespec-client-generator-core`

### Repository Validation

| Project | Tool | Status | Duration | Log |
| --- | --- | --- | ---: | --- |
| `specification/deviceregistry/DeviceRegistry.Management` | `TypeSpecValidation` | succeeded | 54s | `validation-logs/specification__deviceregistry__DeviceRegistry.Management-head.log` |

### Artifact Evidence

- **autorest:** base/head succeeded; both preview OpenAPI files switch common-type references from v6 to v5
- **tcgc:** base/head succeeded; client model reflects aligned ARM envelope types

### Changed TypeSpec

- `specification/deviceregistry/DeviceRegistry.Management/main.tsp`: [main.tsp:L78-L78](specification/deviceregistry/DeviceRegistry.Management/main.tsp#L78-L78), [main.tsp:L83-L83](specification/deviceregistry/DeviceRegistry.Management/main.tsp#L83-L83)
