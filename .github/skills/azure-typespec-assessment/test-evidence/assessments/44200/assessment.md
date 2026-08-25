# 📋 TypeSpec Assessment

**PR:** [#44200 - Support for Private Frontend on Application Gateway for Containers](https://github.com/Azure/azure-rest-api-specs/pull/44200)

**Overall confidence:** 🟢 high<br>
**Overall code safety:** 🔴 Low

**Baseline:** `c3011918b7318f44dcc15e92d4ffb307aa50a475 (0d3ff673b6b63361a7ba06a355d929902e596dac)`<br>
**Head:** `b1582b12f39f1d122fce3c7bbb24b812b0c5c487`; working-tree changes: false<br>
**Total assessment time:** 11m 12s

## 📌 Executive Summary

| Dimension | Result | Findings |
| --- | --- | ---: |
| Semantic understanding | ✅ Assessed — 2 intent(s), 13 operation(s) | n/a |
| REST compatibility | ✅ No breaks detected | 0 |
| Downstream compatibility | ❌ Issues found | 1 |
| Azure compliance | ❌ failed | 4 |

**Scope:** 2 intent(s), 13 affected operation(s), 1 project(s).<br>
**Changes:** 6 added, 1 modified, 0 removed.<br>
**Highest severity:** high.

## 🎯 Action Required

| Severity | Area | Finding | Why it matters | Code | Guidance |
| --- | --- | --- | --- | --- | --- |
| high | Downstream | Generated SDK model property shape changes | Excluding JavaScript from an existing property-flattening rule preserves the serialized JSON but can replace flattened constructor arguments and direct property access with nested model access. Existing JavaScript callers can therefore stop compiling or require source changes even though their HTTP requests and responses remain compatible. | [main.tsp:L357-L459](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L357-L459), [main.tsp:L543-L558](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L543-L558), +13 more | n/a |
| medium | Compliance | Private endpoint resource and operations do not use the standard pattern | The private endpoint guidance requires PrivateEndpointConnectionResource with the standard PrivateEndpoints interface, but the change adds a custom parented PrivateEndpointConnection model and a custom @armResourceOperations PrivateEndpointConnectionsInterface. | [main.tsp:L357-L459](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L357-L459), [main.tsp:L543-L558](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L543-L558), +2 more | [The changed source retains the preview version replaced by the new stable version, implements private endpoint and private link resources with custom resource and operation patterns instead of the documented standard patterns, and applies the legacy flattenProperty decorator to newly introduced models despite guidance against its use for green-field services.](https://azure.github.io/typespec-azure/docs/howtos/arm/private-endpoints/) |
| medium | Compliance | Private link resource and operations do not use the standard pattern | The private link guidance requires the PrivateLink resource type with the standard PrivateLinks interface, but the change adds PrivateLinkResource as ProxyResource<PrivateLinkResourceProperties> and exposes it through a custom @armResourceOperations PrivateLinkResourcesInterface. | [main.tsp:L357-L459](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L357-L459), [main.tsp:L543-L558](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L543-L558), +2 more | [The changed source retains the preview version replaced by the new stable version, implements private endpoint and private link resources with custom resource and operation patterns instead of the documented standard patterns, and applies the legacy flattenProperty decorator to newly introduced models despite guidance against its use for green-field services.](https://azure.github.io/typespec-azure/docs/howtos/arm/private-links/) |
| medium | Compliance | The replaced preview version is retained when adding the stable version | The stable-after-preview guidance says to remove the replaced preview version, but the changed version enum retains v2025_10_01_preview alongside v2026_03_01. | [main.tsp:L30-L35](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L30-L35), [main.tsp:L63-L70](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L63-L70), +13 more | [The changed source retains the preview version replaced by the new stable version, implements private endpoint and private link resources with custom resource and operation patterns instead of the documented standard patterns, and applies the legacy flattenProperty decorator to newly introduced models despite guidance against its use for green-field services.](https://azure.github.io/typespec-azure/docs/howtos/versioning/03-stable-after-preview/) |
| low | Compliance | New models use the legacy property-flattening decorator | The decorator guidance does not recommend Legacy.flattenProperty for green-field services, but the changed client customization applies it to properties of the newly introduced private endpoint and private link models. | [client.tsp:L146-L182](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/client.tsp#L146-L182), [main.tsp:L357-L459](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L357-L459), +2 more | [The changed source retains the preview version replaced by the new stable version, implements private endpoint and private link resources with custom resource and operation patterns instead of the documented standard patterns, and applies the legacy flattenProperty decorator to newly introduced models despite guidance against its use for green-field services.](https://azure.github.io/typespec-azure/docs/libraries/typespec-client-generator-core/reference/decorators/) |

## 🧠 Semantic Understanding

<a id="intent-1-add-private-endpoint-connection-and-private-link"></a>
### 1. Add private endpoint connection and private link resource management

| Change | Aspect | Before | After |
| --- | --- | --- | --- |
| ➕ Added | Operation family | — | 6 REST operations added. |

**TypeSpec change:** The change introduces the 2025-10-01-preview API version, adds PrivateEndpointConnection and PrivateLinkResource as children of TrafficController, and adds resource-operation interfaces for them. The preview and subsequent 2026-03-01 version expose get, list, update, and delete operations for private endpoint connections and get and list operations for private link resources.

```diff
--- a/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp
+++ b/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp
@@ -392,4 +540,20 @@ interface TrafficControllerInterface {
 ... 2 earlier diff lines omitted; full hunk is in assessment.json ...
+@added(Versions.v2025_10_01_preview)
+@armResourceOperations
+interface PrivateEndpointConnectionsInterface {
+  get is ArmResourceRead<PrivateEndpointConnection>;
+  update is ArmResourceCreateOrReplaceSync<PrivateEndpointConnection>;
+  delete is ArmResourceDeleteWithoutOkAsync<PrivateEndpointConnection>;
+  listByTrafficController is ArmResourceListByParent<PrivateEndpointConnection>;
+}
+
+@added(Versions.v2025_10_01_preview)
+@armResourceOperations
+interface PrivateLinkResourcesInterface {
 ... 5 later diff lines omitted; full hunk is in assessment.json ...
```

```diff
--- a/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp
+++ b/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp
@@ -392,4 +540,20 @@ interface TrafficControllerInterface {
 ... 7 earlier diff lines omitted; full hunk is in assessment.json ...
+  delete is ArmResourceDeleteWithoutOkAsync<PrivateEndpointConnection>;
+  listByTrafficController is ArmResourceListByParent<PrivateEndpointConnection>;
+}
+
+@added(Versions.v2025_10_01_preview)
+@armResourceOperations
+interface PrivateLinkResourcesInterface {
+  get is ArmResourceRead<PrivateLinkResource>;
+  listByTrafficController is ArmResourceListByParent<PrivateLinkResource>;
+}
+
 interface Operations extends Azure.ResourceManager.Operations {}
```

**Impact:** [The replaced preview version is retained when adding the stable version](#finding-compliance-stable-version-retains-replaced-preview), [Private endpoint resource and operations do not use the standard pattern](#finding-compliance-private-endpoint-standard-pattern), [Private link resource and operations do not use the standard pattern](#finding-compliance-private-link-standard-pattern), [New models use the legacy property-flattening decorator](#finding-compliance-legacy-flattening-on-new-models)<br>
**Source:** [main.tsp:L357-L459](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L357-L459), [main.tsp:L543-L558](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L543-L558), [main.tsp:L357-L459](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L357-L459), [main.tsp:L543-L558](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L543-L558), [main.tsp:L30-L35](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L30-L35), [main.tsp:L63-L70](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L63-L70), [main.tsp:L80-L105](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L80-L105), [main.tsp:L125-L125](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L125-L125), [main.tsp:L310-L314](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L310-L314)

<a id="intent-2-keep-associationupdate-properties-nested-in-gene"></a>
### 2. Keep AssociationUpdate.properties nested in generated JavaScript

| Change | Aspect | Before | After |
| --- | --- | --- | --- |
| ✏️ Modified | JavaScript AssociationUpdate model shape | properties is flattened into AssociationUpdate. | properties remains a nested object in JavaScript. |

**TypeSpec change:** Change the AssociationUpdate.properties flattenProperty scope from all languages to !javascript.

```diff
--- a/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp
+++ b/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp
@@ -82,7 +122,7 @@ model AssociationUpdate {

   /** The resource-specific properties for this resource. */
   #suppress "@azure-tools/typespec-azure-core/no-legacy-usage"
-  @Azure.ClientGenerator.Core.Legacy.flattenProperty
+  @Azure.ClientGenerator.Core.Legacy.flattenProperty("!javascript")
   properties?: AssociationUpdateProperties;
 }

```

**Impact:** [Generated SDK model property shape changes](#finding-source-javascript-flattening-scope-changed)<br>
**Source:** [client.tsp:L146-L182](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/client.tsp#L146-L182), [main.tsp:L80-L105](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L80-L105), [client.tsp:L146-L182](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/client.tsp#L146-L182), [main.tsp:L80-L105](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L80-L105), [main.tsp:L122-L127](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L122-L127), [back-compatible.tsp:L30-L39](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/back-compatible.tsp#L30-L39), [client.tsp:L194-L203](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/client.tsp#L194-L203), [main.tsp:L30-L35](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L30-L35), [main.tsp:L63-L70](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L63-L70), [main.tsp:L125-L125](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L125-L125), [main.tsp:L310-L314](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L310-L314), [main.tsp:L357-L459](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L357-L459), [main.tsp:L543-L558](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L543-L558)

Need the complete REST representation for every affected operation? Use this prompt:

`Using assessment.json for PR #44200, show the complete REST representation for every affected operation, including operation ID, method/path, parameters, request, responses, LRO, paging, and TypeSpec source.`

## 🛡️ Compatibility Assessment

### REST Breaking Changes

None detected.

### Downstream Breaking Changes

<a id="finding-source-javascript-flattening-scope-changed"></a>
### Generated SDK model property shape changes

- **Severity:** high
- **Confidence:** high
- **Summary:** Excluding JavaScript from an existing property-flattening rule preserves the serialized JSON but can replace flattened constructor arguments and direct property access with nested model access. Existing JavaScript callers can therefore stop compiling or require source changes even though their HTTP requests and responses remain compatible.
- **Evidence:** Changing flattenProperty to exclude JavaScript can change generated JavaScript construction and property access from flattened to nested.; Changed TypeSpec source: specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/back-compatible.tsp.; Changed TypeSpec source: specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/client.tsp.; Changed TypeSpec source: specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp.
- **TypeSpec source:** [main.tsp:L357-L459](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L357-L459), [main.tsp:L543-L558](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L543-L558), [main.tsp:L357-L459](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L357-L459), [main.tsp:L543-L558](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L543-L558), [main.tsp:L30-L35](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L30-L35), [main.tsp:L63-L70](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L63-L70), [main.tsp:L80-L105](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L80-L105), [main.tsp:L125-L125](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L125-L125), [main.tsp:L310-L314](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L310-L314), [client.tsp:L146-L182](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/client.tsp#L146-L182), [client.tsp:L146-L182](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/client.tsp#L146-L182), [main.tsp:L80-L105](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L80-L105), [main.tsp:L122-L127](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L122-L127), [back-compatible.tsp:L30-L39](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/back-compatible.tsp#L30-L39), [client.tsp:L194-L203](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/client.tsp#L194-L203)


## ☁️ Azure Compliance

**Status:** `failed`

### Compliance Findings

<a id="finding-compliance-stable-version-retains-replaced-preview"></a>
### The replaced preview version is retained when adding the stable version

**Severity:** medium

**Gap:** The stable-after-preview guidance says to remove the replaced preview version, but the changed version enum retains v2025_10_01_preview alongside v2026_03_01.

<details>
<summary><strong>Expected</strong></summary>

The changed source retains the preview version replaced by the new stable version, implements private endpoint and private link resources with custom resource and operation patterns instead of the documented standard patterns, and applies the legacy flattenProperty decorator to newly introduced models despite guidance against its use for green-field services.

**Guidance:** [Adding a Stable Version when the Last Version was Preview — Retained authoritative evidence](https://azure.github.io/typespec-azure/docs/howtos/versioning/03-stable-after-preview/)

_The bounded official document evidence did not contain an example block._

</details>

<details>
<summary><strong>Actual</strong></summary>

Guidance: "Remove the replaced preview version from the version enum." Changed source at main.tsp:28 contains both v2025_10_01_preview: "2025-10-01-preview" and v2026_03_01: "2026-03-01".

**[main.tsp:L30-L35](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L30-L35)**

```tsp

  /** 2025-10-01 preview version */
  v2025_10_01_preview: "2025-10-01-preview",

  /** 2026-03-01 stable version */
  v2026_03_01: "2026-03-01",
```

</details>

<a id="finding-compliance-private-endpoint-standard-pattern"></a>
### Private endpoint resource and operations do not use the standard pattern

**Severity:** medium

**Gap:** The private endpoint guidance requires PrivateEndpointConnectionResource with the standard PrivateEndpoints interface, but the change adds a custom parented PrivateEndpointConnection model and a custom @armResourceOperations PrivateEndpointConnectionsInterface.

<details>
<summary><strong>Expected</strong></summary>

The changed source retains the preview version replaced by the new stable version, implements private endpoint and private link resources with custom resource and operation patterns instead of the documented standard patterns, and applies the legacy flattenProperty decorator to newly introduced models despite guidance against its use for green-field services.

**Guidance:** [Private Endpoints — Retained authoritative evidence](https://azure.github.io/typespec-azure/docs/howtos/arm/private-endpoints/)

**Documented TypeSpec example**

```tsp
model PrivateEndpointConnection is PrivateEndpointConnectionResource;
alias PrivateEndpointOperations = PrivateEndpoints<PrivateEndpointConnection>;

getPrivateEndpointConnection is PrivateEndpointOperations.Read<Employee>;
createOrUpdatePrivateEndpointConnection is PrivateEndpointOperations.CreateOrUpdateAsync<Employee>;
deletePrivateEndpointConnection is PrivateEndpointOperations.DeleteAsync<Employee>;
listPrivateEndpointConnections is PrivateEndpointOperations.ListByParent<Employee>;
```

</details>

<details>
<summary><strong>Actual</strong></summary>

Guidance: private endpoint providers must declare a private endpoint connection resource type and use the standard PrivateEndpoints interface. Changed source adds @parentResource(TrafficController) model PrivateEndpointConnection and @armResourceOperations interface PrivateEndpointConnectionsInterface rather than PrivateEndpointConnectionResource and PrivateEndpoints<PrivateEndpointConnection>.

**[main.tsp:L357-L368](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L357-L368)**

```tsp
//----------------------- PrivateEndpointConnection -----------------------
/** Private Endpoint Connection resource of Traffic Controller. */
@added(Versions.v2025_10_01_preview)
@parentResource(TrafficController)
model PrivateEndpointConnection
  is ProxyResource<PrivateEndpointConnectionProperties> {
  /** Private Endpoint Connection */
  @key("privateEndpointConnectionName")
  @visibility(Lifecycle.Read)
  @path
  @segment("privateEndpointConnections")
  @pattern("^[A-Za-z0-9]([A-Za-z0-9-_.]{0,62}[A-Za-z0-9])?$")
```

</details>

<a id="finding-compliance-private-link-standard-pattern"></a>
### Private link resource and operations do not use the standard pattern

**Severity:** medium

**Gap:** The private link guidance requires the PrivateLink resource type with the standard PrivateLinks interface, but the change adds PrivateLinkResource as ProxyResource<PrivateLinkResourceProperties> and exposes it through a custom @armResourceOperations PrivateLinkResourcesInterface.

<details>
<summary><strong>Expected</strong></summary>

The changed source retains the preview version replaced by the new stable version, implements private endpoint and private link resources with custom resource and operation patterns instead of the documented standard patterns, and applies the legacy flattenProperty decorator to newly introduced models despite guidance against its use for green-field services.

**Guidance:** [Private Links — Retained authoritative evidence](https://azure.github.io/typespec-azure/docs/howtos/arm/private-links/)

**Documented TypeSpec example**

```tsp
model MyPrivateLinkResource is PrivateLink;
alias PrivateLinkOperations = PrivateLinks<MyPrivateLinkResource>;

getPrivateLink is PrivateLinkOperations.Read<Employee>;
listPrivateLinks is PrivateLinkOperations.ListByParent<Employee>;
```

</details>

<details>
<summary><strong>Actual</strong></summary>

Guidance: private link providers must declare a private link resource type and use the standard PrivateLinks interface. Changed source adds model PrivateLinkResource is ProxyResource<PrivateLinkResourceProperties> and @armResourceOperations interface PrivateLinkResourcesInterface rather than PrivateLink and PrivateLinks<PrivateLinkResource>.

**[main.tsp:L357-L368](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L357-L368)**

```tsp
//----------------------- PrivateEndpointConnection -----------------------
/** Private Endpoint Connection resource of Traffic Controller. */
@added(Versions.v2025_10_01_preview)
@parentResource(TrafficController)
model PrivateEndpointConnection
  is ProxyResource<PrivateEndpointConnectionProperties> {
  /** Private Endpoint Connection */
  @key("privateEndpointConnectionName")
  @visibility(Lifecycle.Read)
  @path
  @segment("privateEndpointConnections")
  @pattern("^[A-Za-z0-9]([A-Za-z0-9-_.]{0,62}[A-Za-z0-9])?$")
```

</details>

<a id="finding-compliance-legacy-flattening-on-new-models"></a>
### New models use the legacy property-flattening decorator

**Severity:** low

**Gap:** The decorator guidance does not recommend Legacy.flattenProperty for green-field services, but the changed client customization applies it to properties of the newly introduced private endpoint and private link models.

<details>
<summary><strong>Expected</strong></summary>

The changed source retains the preview version replaced by the new stable version, implements private endpoint and private link resources with custom resource and operation patterns instead of the documented standard patterns, and applies the legacy flattenProperty decorator to newly introduced models despite guidance against its use for green-field services.

**Guidance:** [TypeSpec Client Generator Core decorators — Retained authoritative evidence](https://azure.github.io/typespec-azure/docs/libraries/typespec-client-generator-core/reference/decorators/)

_The bounded official document evidence did not contain an example block._

</details>

<details>
<summary><strong>Actual</strong></summary>

Guidance: "This decorator is not recommended to use for green field services." Changed source adds @@Azure.ClientGenerator.Core.Legacy.flattenProperty calls in client.tsp for models introduced by the same change as PrivateEndpointConnection and PrivateLinkResource.

**[client.tsp:L146-L157](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/client.tsp#L146-L157)**

```tsp
@@alternateType(
  TrafficControllerProperties.privateEndpointConnections,
  Azure.ResourceManager.Models.SubResource[],
  "csharp"
);
@@alternateType(
  FrontendAssociation.id,
  Azure.Core.armResourceIdentifier,
  "csharp"
);
@@alternateType(
  FrontendAssociationUpdate.id,
```

</details>

## 📎 Appendix

### Assessment Errors

None.

### Code-to-Guidance Evidence

| Result | Document section | Fetched guidance | Observed TypeSpec | Evidence |
| --- | --- | --- | --- | --- |
| Mismatch | [Adding a Stable Version when the Last Version was Preview - Retained authoritative evidence](https://azure.github.io/typespec-azure/docs/howtos/versioning/03-stable-after-preview/) | Remove the replaced preview version from the version enum. | Guidance: "Remove the replaced preview version from the version enum." Changed source at main.tsp:28 contains both v2025_10_01_preview: "2025-10-01-preview" and v2026_03_01: "2026-03-01". | [main.tsp:L30-L35](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L30-L35), [main.tsp:L63-L70](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L63-L70), [main.tsp:L80-L105](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L80-L105), +12 more |
| Mismatch | [Private Endpoints - Retained authoritative evidence](https://azure.github.io/typespec-azure/docs/howtos/arm/private-endpoints/) | Resource providers that support private endpoint connections must declare a private endpoint connection resource type and use the standard `PrivateEndpoints` interface to expose operations. | Guidance: private endpoint providers must declare a private endpoint connection resource type and use the standard PrivateEndpoints interface. Changed source adds @parentResource(TrafficController) model PrivateEndpointConnection and @armResourceOperations interface PrivateEndpointConnectionsInterface rather than PrivateEndpointConnectionResource and PrivateEndpoints<PrivateEndpointConnection>. | [main.tsp:L357-L459](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L357-L459), [main.tsp:L543-L558](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L543-L558), [main.tsp:L357-L459](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L357-L459), +1 more |
| Mismatch | [Private Links - Retained authoritative evidence](https://azure.github.io/typespec-azure/docs/howtos/arm/private-links/) | Resource providers that support private link resources must declare a private link resource type and use the standard `PrivateLinks` interface to expose operations. | Guidance: private link providers must declare a private link resource type and use the standard PrivateLinks interface. Changed source adds model PrivateLinkResource is ProxyResource<PrivateLinkResourceProperties> and @armResourceOperations interface PrivateLinkResourcesInterface rather than PrivateLink and PrivateLinks<PrivateLinkResource>. | [main.tsp:L357-L459](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L357-L459), [main.tsp:L543-L558](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L543-L558), [main.tsp:L357-L459](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L357-L459), +1 more |
| Mismatch | [TypeSpec Client Generator Core decorators - Retained authoritative evidence](https://azure.github.io/typespec-azure/docs/libraries/typespec-client-generator-core/reference/decorators/) | Set whether a model property should be flattened or not. This decorator is not recommended to use for green field services. | Guidance: "This decorator is not recommended to use for green field services." Changed source adds @@Azure.ClientGenerator.Core.Legacy.flattenProperty calls in client.tsp for models introduced by the same change as PrivateEndpointConnection and PrivateLinkResource. | [client.tsp:L146-L182](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/client.tsp#L146-L182), [main.tsp:L357-L459](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/main.tsp#L357-L459), [client.tsp:L146-L182](https://github.com/Azure/azure-rest-api-specs/blob/b1582b12f39f1d122fce3c7bbb24b812b0c5c487/specification/servicenetworking/resource-manager/Microsoft.ServiceNetworking/ServiceNetworking/client.tsp#L146-L182), +1 more |

### Tooling Used

- `@azure-tools/typespec-autorest`
- `@azure-tools/typespec-client-generator-core`

### Artifact Evidence

- **autorest:** base/head succeeded; added preview 2025-10-01-preview and stable 2026-03-01 contracts were compared
- **tcgc:** base/head succeeded; AssociationUpdate client-shape evidence requires JavaScript-specific review
