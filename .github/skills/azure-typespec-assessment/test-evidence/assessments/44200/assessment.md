# 📋 TypeSpec Assessment

**PR:** [#44200 - Support for Private Frontend on Application Gateway for Containers](https://github.com/Azure/azure-rest-api-specs/pull/44200)

**Overall confidence:** 🟡 medium<br>
**Overall code safety:** 🔴 Low

**Baseline:** `0d3ff673b6b63361a7ba06a355d929902e596dac`<br>
**Head:** `b1582b12f39f1d122fce3c7bbb24b812b0c5c487`; working-tree changes: false<br>
**Total assessment time:** 12m 8s

## 📌 Executive Summary

| Dimension | Result | Findings |
| --- | --- | ---: |
| Semantic understanding | ✅ Assessed — 1 intent(s), 1 operation(s) | n/a |
| REST compatibility | ✅ No breaks detected | 0 |
| Downstream compatibility | ❌ Issues found | 1 |
| Azure compliance | ❌ failed | 4 |

**Scope:** 1 intent(s), 1 affected operation(s), 1 project(s).<br>
**Highest severity:** high.

## 🎯 Action Required

| Severity | Area | Finding | Why it matters | Code | Guidance |
| --- | --- | --- | --- | --- | --- |
| high | Downstream | JavaScript property access changes from flattened to nested | Existing JavaScript construction and member access can break while the intended wire object remains properties. | [main.tsp:L122-L127](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L122-L127) | n/a |
| medium | Compliance | Private endpoint connections bypass the standard templates | PrivateEndpointConnection is manually modeled and uses generic operations instead of PrivateEndpointConnectionResource and PrivateEndpoints. | [main.tsp:L122-L127](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L122-L127), [main.tsp:L30-L35](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L30-L35), +6 more | [Private endpoint connections extend PrivateEndpointConnectionResource and use PrivateEndpoints operations.](https://azure.github.io/typespec-azure/docs/howtos/arm/private-endpoints/) |
| medium | Compliance | Private-link discovery bypasses the standard templates | PrivateLinkResource is manually modeled and uses generic operations instead of PrivateLink and PrivateLinks. | [main.tsp:L122-L127](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L122-L127), [main.tsp:L30-L35](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L30-L35), +6 more | [Private-link discovery uses the standard PrivateLink model and PrivateLinks operation interface.](https://azure.github.io/typespec-azure/docs/howtos/arm/private-links/) |
| medium | Compliance | The stable version retains the replaced preview version | The enum keeps v2025_10_01_preview while appending v2026_03_01, contrary to the stable-after-preview procedure. | [main.tsp:L122-L127](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L122-L127), [main.tsp:L30-L35](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L30-L35), +6 more | [A stable release replacing a preview removes the preview member and retargets promoted changes.](https://azure.github.io/typespec-azure/docs/howtos/versioning/03-stable-after-preview/) |
| low | Compliance | Legacy flattening is applied to newly introduced models | New private endpoint and private link resource properties use a legacy decorator that the reference does not recommend for greenfield types. | [back-compatible.tsp:L30-L39](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/back-compatible.tsp#L30-L39), [client.tsp:L146-L182](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/client.tsp#L146-L182), +1 more | [Do not add legacy flattening to newly introduced API models without an existing compatibility requirement.](https://azure.github.io/typespec-azure/docs/libraries/typespec-client-generator-core/reference/decorators/) |

## 🧠 Semantic Understanding

### Change Overview

| # | Intent | Operations | API versions | Details |
| ---: | --- | ---: | --- | --- |
| 1 | Add private frontend support and stop flattening AssociationUpdate.properties for JavaScript. | 1 | 2025-05-01-preview | [details](#intent-1-add-private-frontend-support-and-stop-flattening) |

### Operation Details

<a id="intent-1-add-private-frontend-support-and-stop-flattening"></a>
### 1. Add private frontend support and stop flattening AssociationUpdate.properties for JavaScript.

**Confidence:** high<br>
**REST summary:** The flattening edit intends to retain the same JSON properties object while changing JavaScript model access.

#### `Associations_Update`

- **HTTP path:** `PATCH /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.ServiceNetworking/trafficControllers/{trafficControllerName}/associations/{associationName}`
- **API versions:** `2025-05-01-preview`
- **Parameters:** path subscriptionId, resourceGroupName, trafficControllerName, associationName: string, required; query api-version: string, required
- **Request payload:** application/json body: AssociationUpdate
- **Response payloads:** 200: Association; 202: async headers; default: ErrorResponse
- **Service behavior:** Updates an association; JavaScript now receives properties as a nested object.
- **LRO:** arm; via azure-async-operation; Poll the emitted async endpoint after Retry-After until a terminal state.; final result: Use the final response contract described for this operation.
- **Paging:** No.
- **TypeSpec source:** [main.tsp:L122-L127](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L122-L127)
## 🛡️ Compatibility Assessment

### REST Breaking Changes

None detected.

### Downstream Breaking Changes

### JavaScript property access changes from flattened to nested

- **Severity:** high
- **Confidence:** high
- **Summary:** Existing JavaScript construction and member access can break while the intended wire object remains properties.
- **Evidence:** flattenProperty changes to flattenProperty("!javascript").
- **TypeSpec source:** [main.tsp:L122-L127](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L122-L127)


## ☁️ Azure Compliance

**Status:** `failed`

### Compliance Findings

### The stable version retains the replaced preview version

- **Severity:** medium
- **Summary:** The enum keeps v2025_10_01_preview while appending v2026_03_01, contrary to the stable-after-preview procedure.
- **Evidence:** The documented workflow removes the replaced preview member and retargets promoted changes to stable.
- **TypeSpec source:** [main.tsp:L122-L127](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L122-L127), [main.tsp:L30-L35](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L30-L35), [main.tsp:L63-L70](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L63-L70), [main.tsp:L80-L105](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L80-L105), [main.tsp:L125-L125](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L125-L125), [main.tsp:L310-L314](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L310-L314), [main.tsp:L357-L459](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L357-L459), [main.tsp:L543-L558](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L543-L558)
- **Guidance:** https://azure.github.io/typespec-azure/docs/howtos/versioning/03-stable-after-preview/

### Private endpoint connections bypass the standard templates

- **Severity:** medium
- **Summary:** PrivateEndpointConnection is manually modeled and uses generic operations instead of PrivateEndpointConnectionResource and PrivateEndpoints.
- **Evidence:** The official private-endpoint guide requires the standard resource base and PrivateEndpoints interface.
- **TypeSpec source:** [main.tsp:L122-L127](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L122-L127), [main.tsp:L30-L35](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L30-L35), [main.tsp:L63-L70](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L63-L70), [main.tsp:L80-L105](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L80-L105), [main.tsp:L125-L125](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L125-L125), [main.tsp:L310-L314](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L310-L314), [main.tsp:L357-L459](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L357-L459), [main.tsp:L543-L558](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L543-L558)
- **Guidance:** https://azure.github.io/typespec-azure/docs/howtos/arm/private-endpoints/

### Private-link discovery bypasses the standard templates

- **Severity:** medium
- **Summary:** PrivateLinkResource is manually modeled and uses generic operations instead of PrivateLink and PrivateLinks.
- **Evidence:** The official private-link guide requires the standard model and PrivateLinks interface.
- **TypeSpec source:** [main.tsp:L122-L127](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L122-L127), [main.tsp:L30-L35](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L30-L35), [main.tsp:L63-L70](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L63-L70), [main.tsp:L80-L105](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L80-L105), [main.tsp:L125-L125](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L125-L125), [main.tsp:L310-L314](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L310-L314), [main.tsp:L357-L459](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L357-L459), [main.tsp:L543-L558](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L543-L558)
- **Guidance:** https://azure.github.io/typespec-azure/docs/howtos/arm/private-links/

### Legacy flattening is applied to newly introduced models

- **Severity:** low
- **Summary:** New private endpoint and private link resource properties use a legacy decorator that the reference does not recommend for greenfield types.
- **Evidence:** The changed types are newly introduced, so they have no prior flattened SDK contract to preserve.
- **TypeSpec source:** [back-compatible.tsp:L30-L39](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/back-compatible.tsp#L30-L39), [client.tsp:L146-L182](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/client.tsp#L146-L182), [client.tsp:L194-L203](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/client.tsp#L194-L203)
- **Guidance:** https://azure.github.io/typespec-azure/docs/libraries/typespec-client-generator-core/reference/decorators/

## 📎 Appendix

### Assessment Errors

None.

### Code-to-Guidance Evidence

| Result | Document section | Fetched guidance | Observed TypeSpec | Evidence |
| --- | --- | --- | --- | --- |
| Mismatch | [Adding a Stable Version when the Last Version was Preview - Making Changes to your TypeSpec spec](https://azure.github.io/typespec-azure/docs/howtos/versioning/03-stable-after-preview/) | Remove the replaced preview version from the version enum. | The enum retains v2025_10_01_preview and appends stable v2026_03_01. | [main.tsp:L122-L127](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L122-L127), [main.tsp:L30-L35](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L30-L35), [main.tsp:L63-L70](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L63-L70), +5 more |
| Mismatch | [Private Endpoints - Defining a Private Endpoint Connection Resource](https://azure.github.io/typespec-azure/docs/howtos/arm/private-endpoints/) | Resource providers that support private endpoint connections must declare a private endpoint connection resource type and use the standard `PrivateEndpoints` interface to expose operations. | The PR manually models PrivateEndpointConnection as ProxyResource and uses generic ArmResource operations. | [main.tsp:L122-L127](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L122-L127), [main.tsp:L30-L35](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L30-L35), [main.tsp:L63-L70](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L63-L70), +5 more |
| Mismatch | [Private Links - Defining a Private Link Resource](https://azure.github.io/typespec-azure/docs/howtos/arm/private-links/) | Resource providers that support private link resources must declare a private link resource type and use the standard `PrivateLinks` interface to expose operations. | The PR manually models PrivateLinkResource as ProxyResource and uses generic read/list templates. | [main.tsp:L122-L127](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L122-L127), [main.tsp:L30-L35](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L30-L35), [main.tsp:L63-L70](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L63-L70), +5 more |
| Mismatch | [TypeSpec Client Generator Core decorators - @Azure.ClientGenerator.Core.Legacy.flattenProperty](https://azure.github.io/typespec-azure/docs/libraries/typespec-client-generator-core/reference/decorators/) | Set whether a model property should be flattened or not. This decorator is not recommended to use for green field services. | The new private endpoint and private link resource properties are flattened for AutoRest and C#. | [back-compatible.tsp:L30-L39](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/back-compatible.tsp#L30-L39), [client.tsp:L146-L182](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/client.tsp#L146-L182), [client.tsp:L194-L203](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/client.tsp#L194-L203) |

### Tooling Used

- `@azure-tools/typespec-autorest`
- `@azure-tools/typespec-client-generator-core`

### Repository Validation

| Project | Tool | Status | Duration | Log |
| --- | --- | --- | ---: | --- |
| `specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking` | `TypeSpecValidation` | skipped | 0s | `unknown` |

### Artifact Evidence

- **autorest:** base succeeded; head failed with duplicate FrontendAssociationUpdate type name
- **tcgc:** base/head succeeded and shows AssociationUpdate model-shape changes

### Changed TypeSpec

- `specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/back-compatible.tsp`: [back-compatible.tsp:L30-L39](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/back-compatible.tsp#L30-L39)
- `specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/client.tsp`: [client.tsp:L146-L182](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/client.tsp#L146-L182), [client.tsp:L194-L203](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/client.tsp#L194-L203)
- `specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp`: [main.tsp:L30-L35](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L30-L35), [main.tsp:L63-L70](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L63-L70), [main.tsp:L80-L105](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L80-L105), [main.tsp:L125-L125](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L125-L125), [main.tsp:L310-L314](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L310-L314), [main.tsp:L357-L459](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L357-L459), [main.tsp:L543-L558](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L543-L558)
