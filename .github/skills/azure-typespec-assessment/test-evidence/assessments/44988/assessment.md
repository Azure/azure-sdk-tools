# 📋 TypeSpec Assessment

**PR:** [#44988 - Release Network API Version 2025-09-01](https://github.com/Azure/azure-rest-api-specs/pull/44988)

**Overall confidence:** 🟢 high<br>
**Overall code safety:** 🔴 Low

**Baseline:** `9f0ad696cc186c2d16cb522abc0fbd4aa3854ca5`<br>
**Head:** `780a61ace56c22ce10dd01caa8ab95ca4514ac2e`; working-tree changes: false<br>
**Total assessment time:** 11m 20s

## 📌 Executive Summary

| Dimension | Result | Findings |
| --- | --- | ---: |
| Semantic understanding | ✅ Assessed — 11 intent(s), 127 operation(s) | n/a |
| REST compatibility | ✅ No breaks detected | 0 |
| Downstream compatibility | ❌ Issues found | 1 |
| Azure compliance | ❌ failed | 4 |

**Scope:** 11 intent(s), 127 affected operation(s), 2 project(s).<br>
**Changes:** 124 added, 3 modified, 0 removed.<br>
**Highest severity:** high.

## 🎯 Action Required

| Severity | Area | Finding | Why it matters | Code | Guidance |
| --- | --- | --- | --- | --- | --- |
| high | Downstream | Generated service gateway update methods can change from pollers to synchronous calls | The older REST API versions remain compatible, but generated SDKs that move to 2025-09-01 can expose ServiceGateways_UpdateAddressLocations and ServiceGateways_UpdateServices as synchronous methods instead of long-running poller or operation-handle methods. Existing call sites can stop compiling because the return type and completion-handling pattern change, and runtime code that waits for polling completion must instead consume the immediate 200 response. | [ServiceGateway.tsp:L49-L77](https://github.com/Azure/azure-rest-api-specs/blob/780a61ace56c22ce10dd01caa8ab95ca4514ac2e/specification/network/resource-manager/Microsoft.Network/Network/Network/ServiceGateway.tsp#L49-L77), [ServiceGateway.tsp:L126-L135](https://github.com/Azure/azure-rest-api-specs/blob/780a61ace56c22ce10dd01caa8ab95ca4514ac2e/specification/network/resource-manager/Microsoft.Network/Network/Network/ServiceGateway.tsp#L126-L135), +3 more | n/a |
| medium | Compliance | AddressPrefixSet does not use the documented child-resource model template | The documented child-resource pattern uses ProxyResource<TProperties> and spreads ResourceNameParameter<T>. AddressPrefixSet instead extends the local non-generic ProxyResource, declares properties separately, and manually declares its path/key/segment name. | [AddressPrefixSet.tsp:L1-L100](https://github.com/Azure/azure-rest-api-specs/blob/780a61ace56c22ce10dd01caa8ab95ca4514ac2e/specification/network/resource-manager/Microsoft.Network/Network/Network/AddressPrefixSet.tsp#L1-L100) | [The retained ARM guidance applies to the changed declarations identified below. AddressPrefixSet and FirewallPolicyKubeSelectorGroup explicitly use nonstandard child-resource base types, while ConnectionAnalyzer explicitly uses a legacy custom resource model and Legacy.RoutedOperations with manual CRUDL routes. No finding is inferred merely because a compact excerpt omits operation declarations, and no separate provisioning-state consequence is counted.](https://azure.github.io/typespec-azure/docs/howtos/arm/resource-type/) |
| medium | Compliance | ConnectionAnalyzer does not use the documented child-resource model template | The documented child-resource pattern declares a parent resource, uses ProxyResource<TProperties>, and spreads ResourceNameParameter<T>. ConnectionAnalyzer is instead a plain legacy custom Azure resource model with only an optional read-only name, while its changed routes place it beneath NetworkWatcher. | [models.tsp:L27735-L27791](https://github.com/Azure/azure-rest-api-specs/blob/780a61ace56c22ce10dd01caa8ab95ca4514ac2e/specification/network/resource-manager/Microsoft.Network/Network/Network/models.tsp#L27735-L27791) | [The retained ARM guidance applies to the changed declarations identified below. AddressPrefixSet and FirewallPolicyKubeSelectorGroup explicitly use nonstandard child-resource base types, while ConnectionAnalyzer explicitly uses a legacy custom resource model and Legacy.RoutedOperations with manual CRUDL routes. No finding is inferred merely because a compact excerpt omits operation declarations, and no separate provisioning-state consequence is counted.](https://azure.github.io/typespec-azure/docs/howtos/arm/resource-type/) |
| medium | Compliance | ConnectionAnalyzer lifecycle operations do not use the documented ARM templates | The documented CRUDL pattern declares resource operations with the ArmResource templates and reserves custom actions for behavior outside CRUDL. The changed ConnectionAnalyzer lifecycle uses a Legacy.RoutedOperations alias plus explicit HTTP verbs and routes for create, delete, update, get, and list. | [NetworkWatcher.tsp:L41-L487](https://github.com/Azure/azure-rest-api-specs/blob/780a61ace56c22ce10dd01caa8ab95ca4514ac2e/specification/network/resource-manager/Microsoft.Network/Network/Network/NetworkWatcher.tsp#L41-L487), [NetworkWatcher.tsp:L470-L552](https://github.com/Azure/azure-rest-api-specs/blob/780a61ace56c22ce10dd01caa8ab95ca4514ac2e/specification/network/resource-manager/Microsoft.Network/Network/Network/NetworkWatcher.tsp#L470-L552) | [The retained ARM guidance applies to the changed declarations identified below. AddressPrefixSet and FirewallPolicyKubeSelectorGroup explicitly use nonstandard child-resource base types, while ConnectionAnalyzer explicitly uses a legacy custom resource model and Legacy.RoutedOperations with manual CRUDL routes. No finding is inferred merely because a compact excerpt omits operation declarations, and no separate provisioning-state consequence is counted.](https://azure.github.io/typespec-azure/docs/howtos/arm/resource-operations/) |
| medium | Compliance | FirewallPolicyKubeSelectorGroup does not use the documented child-resource model template | The documented child-resource pattern uses ProxyResource<TProperties> and spreads ResourceNameParameter<T>. FirewallPolicyKubeSelectorGroup instead extends SubResourceModel, declares properties separately, and manually declares its path/key/segment name. | [FirewallPolicyKubeSelectorGroup.tsp:L1-L97](https://github.com/Azure/azure-rest-api-specs/blob/780a61ace56c22ce10dd01caa8ab95ca4514ac2e/specification/network/resource-manager/Microsoft.Network/Network/Network/FirewallPolicyKubeSelectorGroup.tsp#L1-L97) | [The retained ARM guidance applies to the changed declarations identified below. AddressPrefixSet and FirewallPolicyKubeSelectorGroup explicitly use nonstandard child-resource base types, while ConnectionAnalyzer explicitly uses a legacy custom resource model and Legacy.RoutedOperations with manual CRUDL routes. No finding is inferred merely because a compact excerpt omits operation declarations, and no separate provisioning-state consequence is counted.](https://azure.github.io/typespec-azure/docs/howtos/arm/resource-type/) |

## ☁️ Azure Compliance

**Status:** `failed`

### Compliance Findings

<a id="finding-address-prefix-set-child-resource-template"></a>
### AddressPrefixSet does not use the documented child-resource model template

**Severity:** medium

**Gap:** The documented child-resource pattern uses ProxyResource<TProperties> and spreads ResourceNameParameter<T>. AddressPrefixSet instead extends the local non-generic ProxyResource, declares properties separately, and manually declares its path/key/segment name.

**TypeSpec source:** [AddressPrefixSet.tsp:L1-L100](https://github.com/Azure/azure-rest-api-specs/blob/780a61ace56c22ce10dd01caa8ab95ca4514ac2e/specification/network/resource-manager/Microsoft.Network/Network/Network/AddressPrefixSet.tsp#L1-L100)

<details>
<summary><strong>Expected</strong></summary>

The retained ARM guidance applies to the changed declarations identified below. AddressPrefixSet and FirewallPolicyKubeSelectorGroup explicitly use nonstandard child-resource base types, while ConnectionAnalyzer explicitly uses a legacy custom resource model and Legacy.RoutedOperations with manual CRUDL routes. No finding is inferred merely because a compact excerpt omits operation declarations, and no separate provisioning-state consequence is counted.

**Guidance:** [ARM resource types and modeling — Add ARM Resource Type](https://azure.github.io/typespec-azure/docs/howtos/arm/resource-type/)

**Documented TypeSpec example**

```tsp
@parentResource(Employee)
model Job is ProxyResource<JobProperties> {
  ...ResourceNameParameter<Job>;
}
```

</details>

<details>
<summary><strong>Actual</strong></summary>

The changed declaration is @parentResource(ApplicationSecurityGroup) model AddressPrefixSet extends ProxyResource, followed by a separate properties property and a manually decorated name property; its suppression explicitly identifies ProxyResource as the local Network RP base type. The changed declaration is @parentResource(FirewallPolicy) model FirewallPolicyKubeSelectorGroup extends SubResourceModel, followed by a separate properties property and a manually decorated name property. The changed model uses @Azure.ResourceManager.Legacy.customAzureResource and declares model ConnectionAnalyzer with only @visibility(Lifecycle.Read) name?: string; the changed NetworkWatcher routes address /networkWatchers/{networkWatcherName}/connectionAnalyzers/{connectionAnalyzerName}.

**[AddressPrefixSet.tsp:L25-L36](https://github.com/Azure/azure-rest-api-specs/blob/780a61ace56c22ce10dd01caa8ab95ca4514ac2e/specification/network/resource-manager/Microsoft.Network/Network/Network/AddressPrefixSet.tsp#L25-L36)**

```tsp
@Azure.ResourceManager.Legacy.feature(Features.virtualNetwork)
@parentResource(ApplicationSecurityGroup)
@Http.Private.includeInapplicableMetadataInPayload(false)
model AddressPrefixSet extends ProxyResource {
  /** Properties of the address prefix set. */
  properties?: AddressPrefixSetPropertiesFormat;

  /** The name of the address prefix set. */
  @visibility(Lifecycle.Read)
  @path
  @key("addressPrefixSetName")
  @segment("addressPrefixSets")
```

</details>

<a id="finding-firewall-policy-kube-selector-group-child-resource-template"></a>
### FirewallPolicyKubeSelectorGroup does not use the documented child-resource model template

**Severity:** medium

**Gap:** The documented child-resource pattern uses ProxyResource<TProperties> and spreads ResourceNameParameter<T>. FirewallPolicyKubeSelectorGroup instead extends SubResourceModel, declares properties separately, and manually declares its path/key/segment name.

**TypeSpec source:** [FirewallPolicyKubeSelectorGroup.tsp:L1-L97](https://github.com/Azure/azure-rest-api-specs/blob/780a61ace56c22ce10dd01caa8ab95ca4514ac2e/specification/network/resource-manager/Microsoft.Network/Network/Network/FirewallPolicyKubeSelectorGroup.tsp#L1-L97)

<details>
<summary><strong>Expected</strong></summary>

The retained ARM guidance applies to the changed declarations identified below. AddressPrefixSet and FirewallPolicyKubeSelectorGroup explicitly use nonstandard child-resource base types, while ConnectionAnalyzer explicitly uses a legacy custom resource model and Legacy.RoutedOperations with manual CRUDL routes. No finding is inferred merely because a compact excerpt omits operation declarations, and no separate provisioning-state consequence is counted.

**Guidance:** [ARM resource types and modeling — Add ARM Resource Type](https://azure.github.io/typespec-azure/docs/howtos/arm/resource-type/)

**Documented TypeSpec example**

```tsp
@parentResource(Employee)
model Job is ProxyResource<JobProperties> {
  ...ResourceNameParameter<Job>;
}
```

</details>

<details>
<summary><strong>Actual</strong></summary>

The changed declaration is @parentResource(ApplicationSecurityGroup) model AddressPrefixSet extends ProxyResource, followed by a separate properties property and a manually decorated name property; its suppression explicitly identifies ProxyResource as the local Network RP base type. The changed declaration is @parentResource(FirewallPolicy) model FirewallPolicyKubeSelectorGroup extends SubResourceModel, followed by a separate properties property and a manually decorated name property. The changed model uses @Azure.ResourceManager.Legacy.customAzureResource and declares model ConnectionAnalyzer with only @visibility(Lifecycle.Read) name?: string; the changed NetworkWatcher routes address /networkWatchers/{networkWatcherName}/connectionAnalyzers/{connectionAnalyzerName}.

**[FirewallPolicyKubeSelectorGroup.tsp:L25-L36](https://github.com/Azure/azure-rest-api-specs/blob/780a61ace56c22ce10dd01caa8ab95ca4514ac2e/specification/network/resource-manager/Microsoft.Network/Network/Network/FirewallPolicyKubeSelectorGroup.tsp#L25-L36)**

```tsp
@parentResource(FirewallPolicy)
@Http.Private.includeInapplicableMetadataInPayload(false)
@added(Versions.v2025_09_01)
model FirewallPolicyKubeSelectorGroup extends SubResourceModel {
  properties?: FirewallPolicyKubeSelectorGroupProperties;

  /**
   * The name of the resource that is unique within a resource group. This name can be used to access the resource.
   */
  @visibility(Lifecycle.Read)
  @path
  @key("kubeSelectorGroupName")
```

</details>

<a id="finding-connection-analyzer-child-resource-template"></a>
### ConnectionAnalyzer does not use the documented child-resource model template

**Severity:** medium

**Gap:** The documented child-resource pattern declares a parent resource, uses ProxyResource<TProperties>, and spreads ResourceNameParameter<T>. ConnectionAnalyzer is instead a plain legacy custom Azure resource model with only an optional read-only name, while its changed routes place it beneath NetworkWatcher.

**TypeSpec source:** [models.tsp:L27735-L27791](https://github.com/Azure/azure-rest-api-specs/blob/780a61ace56c22ce10dd01caa8ab95ca4514ac2e/specification/network/resource-manager/Microsoft.Network/Network/Network/models.tsp#L27735-L27791)

<details>
<summary><strong>Expected</strong></summary>

The retained ARM guidance applies to the changed declarations identified below. AddressPrefixSet and FirewallPolicyKubeSelectorGroup explicitly use nonstandard child-resource base types, while ConnectionAnalyzer explicitly uses a legacy custom resource model and Legacy.RoutedOperations with manual CRUDL routes. No finding is inferred merely because a compact excerpt omits operation declarations, and no separate provisioning-state consequence is counted.

**Guidance:** [ARM resource types and modeling — Add ARM Resource Type](https://azure.github.io/typespec-azure/docs/howtos/arm/resource-type/)

**Documented TypeSpec example**

```tsp
@parentResource(Employee)
model Job is ProxyResource<JobProperties> {
  ...ResourceNameParameter<Job>;
}
```

</details>

<details>
<summary><strong>Actual</strong></summary>

The changed declaration is @parentResource(ApplicationSecurityGroup) model AddressPrefixSet extends ProxyResource, followed by a separate properties property and a manually decorated name property; its suppression explicitly identifies ProxyResource as the local Network RP base type. The changed declaration is @parentResource(FirewallPolicy) model FirewallPolicyKubeSelectorGroup extends SubResourceModel, followed by a separate properties property and a manually decorated name property. The changed model uses @Azure.ResourceManager.Legacy.customAzureResource and declares model ConnectionAnalyzer with only @visibility(Lifecycle.Read) name?: string; the changed NetworkWatcher routes address /networkWatchers/{networkWatcherName}/connectionAnalyzers/{connectionAnalyzerName}.

**[models.tsp:L27738-L27749](https://github.com/Azure/azure-rest-api-specs/blob/780a61ace56c22ce10dd01caa8ab95ca4514ac2e/specification/network/resource-manager/Microsoft.Network/Network/Network/models.tsp#L27738-L27749)**

```tsp
#suppress "@azure-tools/typespec-azure-core/no-legacy-usage" "Required to attach Connection Analyzer child models to the existing NetworkWatcher resource which is modeled with the legacy ARM resource pattern via Azure.ResourceManager.Legacy.feature."
#suppress "@azure-tools/typespec-azure-resource-manager/arm-custom-resource-no-key" "ConnectionAnalyzer is exposed via the legacy NetworkWatcher action pattern; its key is supplied by the routed operation, not modeled on the resource."
#suppress "@azure-tools/typespec-azure-resource-manager/arm-custom-resource-usage-discourage" "Connection Analyzer follows the existing legacy custom-resource pattern used by sibling NetworkWatcher child resources in this project."
@added(Versions.v2025_09_01)
@Azure.ResourceManager.Legacy.feature(Features.networkWatcher)
@Azure.ResourceManager.Legacy.customAzureResource(#{ isAzureResource: true })
model ConnectionAnalyzer {
  /**
   * Name of the connection analyzer.
   */
  @visibility(Lifecycle.Read)
  name?: string;
```

</details>

<a id="finding-connection-analyzer-lifecycle-operation-templates"></a>
### ConnectionAnalyzer lifecycle operations do not use the documented ARM templates

**Severity:** medium

**Gap:** The documented CRUDL pattern declares resource operations with the ArmResource templates and reserves custom actions for behavior outside CRUDL. The changed ConnectionAnalyzer lifecycle uses a Legacy.RoutedOperations alias plus explicit HTTP verbs and routes for create, delete, update, get, and list.

**TypeSpec source:** [NetworkWatcher.tsp:L41-L487](https://github.com/Azure/azure-rest-api-specs/blob/780a61ace56c22ce10dd01caa8ab95ca4514ac2e/specification/network/resource-manager/Microsoft.Network/Network/Network/NetworkWatcher.tsp#L41-L487), [NetworkWatcher.tsp:L470-L552](https://github.com/Azure/azure-rest-api-specs/blob/780a61ace56c22ce10dd01caa8ab95ca4514ac2e/specification/network/resource-manager/Microsoft.Network/Network/Network/NetworkWatcher.tsp#L470-L552)

<details>
<summary><strong>Expected</strong></summary>

The retained ARM guidance applies to the changed declarations identified below. AddressPrefixSet and FirewallPolicyKubeSelectorGroup explicitly use nonstandard child-resource base types, while ConnectionAnalyzer explicitly uses a legacy custom resource model and Legacy.RoutedOperations with manual CRUDL routes. No finding is inferred merely because a compact excerpt omits operation declarations, and no separate provisioning-state consequence is counted.

**Guidance:** [ARM resource operations — Add ARM Resource Operation](https://azure.github.io/typespec-azure/docs/howtos/arm/resource-operations/)

**Documented TypeSpec example**

```tsp
get is ArmResourceRead<Resource>;
createOrUpdate is ArmResourceCreateOrReplaceAsync<Resource>;
update is ArmCustomPatchSync<Resource, PatchRequest>;
delete is ArmResourceDeleteSync<Resource>;
listByParent is ArmResourceListByParent<Resource>;
```

</details>

<details>
<summary><strong>Actual</strong></summary>

ConnectionAnalyzerOps aliases Azure.ResourceManager.Legacy.RoutedOperations, and the changed NetworkWatchers declarations apply @put, @delete, @patch, @get, and @list directly to explicit /connectionAnalyzers routes rather than using the documented CRUDL templates.

**[NetworkWatcher.tsp:L41-L46](https://github.com/Azure/azure-rest-api-specs/blob/780a61ace56c22ce10dd01caa8ab95ca4514ac2e/specification/network/resource-manager/Microsoft.Network/Network/Network/NetworkWatcher.tsp#L41-L46)**

```tsp
alias ConnectionAnalyzerOps = Azure.ResourceManager.Legacy.RoutedOperations<
  {
    ...ApiVersionParameter;
    ...SubscriptionIdParameter;
    ...ResourceGroupParameter;

```

**[NetworkWatcher.tsp:L476-L487](https://github.com/Azure/azure-rest-api-specs/blob/780a61ace56c22ce10dd01caa8ab95ca4514ac2e/specification/network/resource-manager/Microsoft.Network/Network/Network/NetworkWatcher.tsp#L476-L487)**

```tsp
  @put
  @route("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Network/networkWatchers/{networkWatcherName}/connectionAnalyzers/{connectionAnalyzerName}")
  connectionAnalyzersCreate is ConnectionAnalyzerOps.ActionAsync<
    NetworkWatcher,
    ConnectionAnalyzer,
    ConnectionAnalyzer,
    Response =
      | ArmResourceUpdatedResponse<ConnectionAnalyzer>
      | ArmResourceCreatedResponse<
          ConnectionAnalyzer,
          LroHeaders = ArmAsyncOperationHeader<FinalResult = ConnectionAnalyzer> &
            Azure.Core.Foundations.RetryAfterHeader
```

</details>

## 🧠 Semantic Understanding

<a id="intent-1-introduce-the-2025-09-01-network-api-version-wit"></a>
### 1. Introduce the 2025-09-01 Network API version with additive fields on existing resources

| Change | Aspect | Before | After |
| --- | --- | --- | --- |
| ➕ Added | 2025-09-01 API-version surface | — | 87 existing operations are carried into the new API version; application gateways and public IP addresses also receive additive fields. |

**TypeSpec change:** Add Versions.v2025_09_01 and version-scoped fields disableDefaultServerHeaderInResponse and upgradedToV2.

```diff
--- a/specification/network/resource-manager/Microsoft.Network/Network/Common/main.tsp
+++ b/specification/network/resource-manager/Microsoft.Network/Network/Common/main.tsp
@@ -1417,6 +1445,13 @@ model PublicIPAddressPropertiesFormat {
 ... 1 earlier diff lines omitted; full hunk is in assessment.json ...
    */
   deleteOption?: DeleteOptions;
+
+  /**
+   * Whether the public IP address SKU has been upgraded from Standard to StandardV2.
+   */
+  @added(Versions.v2025_09_01)
+  @visibility(Lifecycle.Read)
+  upgradedToV2?: boolean;
 }

 /**
```

```diff
--- a/specification/network/resource-manager/Microsoft.Network/Network/Network/models.tsp
+++ b/specification/network/resource-manager/Microsoft.Network/Network/Network/models.tsp
@@ -8585,6 +8674,12 @@ model ApplicationGatewayGlobalConfiguration {
    * Enable response buffering.
    */
   enableResponseBuffering?: boolean;
+
+  /**
+   * Disable default server header in response.
+   */
+  @added(Versions.v2025_09_01)
+  disableDefaultServerHeaderInResponse?: boolean;
 }

 /**
```

1 additional TypeSpec hunk omitted; complete diffs are in `assessment.json`.

**Source:** [main.tsp:L55-L65](https://github.com/Azure/azure-rest-api-specs/blob/780a61ace56c22ce10dd01caa8ab95ca4514ac2e/specification/network/resource-manager/Microsoft.Network/Network/Common/main.tsp#L55-L65), [main.tsp:L1445-L1457](https://github.com/Azure/azure-rest-api-specs/blob/780a61ace56c22ce10dd01caa8ab95ca4514ac2e/specification/network/resource-manager/Microsoft.Network/Network/Common/main.tsp#L1445-L1457), [main.tsp:L1519-L1534](https://github.com/Azure/azure-rest-api-specs/blob/780a61ace56c22ce10dd01caa8ab95ca4514ac2e/specification/network/resource-manager/Microsoft.Network/Network/Common/main.tsp#L1519-L1534), [models.tsp:L8674-L8685](https://github.com/Azure/azure-rest-api-specs/blob/780a61ace56c22ce10dd01caa8ab95ca4514ac2e/specification/network/resource-manager/Microsoft.Network/Network/Network/models.tsp#L8674-L8685)

<a id="intent-2-add-address-prefix-sets-beneath-application-secu"></a>
### 2. Add address prefix sets beneath application security groups

| Change | Aspect | Before | After |
| --- | --- | --- | --- |
| ➕ Added | Operation family | — | 4 REST operations added. |

**TypeSpec change:** The 2025-09-01 API adds an AddressPrefixSet child resource with a required non-empty addressPrefixes collection and read-only provisioning state, together with create-or-update, delete, get, and list operations under its application security group parent.

```diff
--- /dev/null
+++ b/specification/network/resource-manager/Microsoft.Network/Network/Network/AddressPrefixSet.tsp
@@ -0,0 +1,100 @@
 ... 25 earlier diff lines omitted; full hunk is in assessment.json ...
+@parentResource(ApplicationSecurityGroup)
+@Http.Private.includeInapplicableMetadataInPayload(false)
+model AddressPrefixSet extends ProxyResource {
+  /** Properties of the address prefix set. */
+  properties?: AddressPrefixSetPropertiesFormat;
+
+  /** The name of the address prefix set. */
+  @visibility(Lifecycle.Read)
+  @path
+  @key("addressPrefixSetName")
+  @segment("addressPrefixSets")
+  @pattern("^[a-zA-Z0-9]([a-zA-Z0-9_.-]{0,78}[a-zA-Z0-9_])?$")
 ... 63 later diff lines omitted; full hunk is in assessment.json ...
```

```diff
--- /dev/null
+++ b/specification/network/resource-manager/Microsoft.Network/Network/Network/AddressPrefixSet.tsp
@@ -0,0 +1,100 @@
 ... 44 earlier diff lines omitted; full hunk is in assessment.json ...
+@added(Versions.v2025_09_01)
+@Azure.ResourceManager.Legacy.feature(Features.virtualNetwork)
+model AddressPrefixSetPropertiesFormat {
+  /** The list of address prefixes in CIDR notation. Supports both IPv4 and IPv6 CIDR notation (e.g. '10.0.0.0/16', '2001:db8::/32'). */
+  @minItems(1)
+  addressPrefixes: string[];
+
+  /** The provisioning state of the address prefix set resource. */
+  @visibility(Lifecycle.Read)
+  provisioningState?: ProvisioningState;
+}
+
 ... 44 later diff lines omitted; full hunk is in assessment.json ...
```

**Impact:** [AddressPrefixSet does not use the documented child-resource model template](#finding-address-prefix-set-child-resource-template)<br>
**Source:** [AddressPrefixSet.tsp:L1-L100](https://github.com/Azure/azure-rest-api-specs/blob/780a61ace56c22ce10dd01caa8ab95ca4514ac2e/specification/network/resource-manager/Microsoft.Network/Network/Network/AddressPrefixSet.tsp#L1-L100)

<a id="intent-3-add-expressroute-lag-resources-and-link-member-o"></a>
### 3. Add ExpressRoute LAG resources and link/member operations

| Change | Aspect | Before | After |
| --- | --- | --- | --- |
| ➕ Added | Operation family | — | 11 REST operations added. |

**TypeSpec change:** The 2025-09-01 API introduces ExpressRoute LAG resource lifecycle operations, tag updates, LOA generation, and routed read/list operations for links and members. List operations carry paging metadata, while the resource identity and all nested route parameters remain represented in their ARM paths.

```diff
--- /dev/null
+++ b/specification/network/resource-manager/Microsoft.Network/Network/Network/ExpressRouteLag.tsp
@@ -0,0 +1,302 @@
 ... 24 earlier diff lines omitted; full hunk is in assessment.json ...
+@Http.Private.includeInapplicableMetadataInPayload(false)
+@added(Versions.v2025_09_01)
+model ExpressRouteLag extends Common.Resource {
+  /** ExpressRouteLag properties. */
+  properties?: ExpressRouteLagPropertiesFormat;
+
+  /** The unique identifier of the resource. */
+  @visibility(Lifecycle.Read)
+  id?: string;
+
+  /** The type of the resource. */
+  @visibility(Lifecycle.Read)
 ... 266 later diff lines omitted; full hunk is in assessment.json ...
```

```diff
--- /dev/null
+++ b/specification/network/resource-manager/Microsoft.Network/Network/Network/ExpressRouteLag.tsp
@@ -0,0 +1,302 @@
 ... 131 earlier diff lines omitted; full hunk is in assessment.json ...
+@armResourceOperations(#{ allowStaticRoutes: true, omitTags: true })
+@added(Versions.v2025_09_01)
+interface ExpressRouteLags {
+  /**
+   * Retrieves the requested ExpressRouteLag resource.
+   */
+  @tag("ExpressRouteLags")
+  @autoRoute
+  get is ArmResourceRead<ExpressRouteLag, Error = CloudError>;
+
+  /**
+   * Creates or updates the specified ExpressRouteLag resource.
 ... 159 later diff lines omitted; full hunk is in assessment.json ...
```

1 additional TypeSpec hunk omitted; complete diffs are in `assessment.json`.

**Source:** [ExpressRouteLag.tsp:L1-L302](https://github.com/Azure/azure-rest-api-specs/blob/780a61ace56c22ce10dd01caa8ab95ca4514ac2e/specification/network/resource-manager/Microsoft.Network/Network/Network/ExpressRouteLag.tsp#L1-L302)

<a id="intent-4-add-kubernetes-selector-groups-under-firewall-po"></a>
### 4. Add Kubernetes selector groups under firewall policies

| Change | Aspect | Before | After |
| --- | --- | --- | --- |
| ➕ Added | Operation family | — | 4 REST operations added. |

**TypeSpec change:** Add the versioned FirewallPolicyKubeSelectorGroup child model and its ARM resource-operation interface.

```diff
--- /dev/null
+++ b/specification/network/resource-manager/Microsoft.Network/Network/Network/FirewallPolicyKubeSelectorGroup.tsp
@@ -0,0 +1,97 @@
 ... 25 earlier diff lines omitted; full hunk is in assessment.json ...
+@Http.Private.includeInapplicableMetadataInPayload(false)
+@added(Versions.v2025_09_01)
+model FirewallPolicyKubeSelectorGroup extends SubResourceModel {
+  properties?: FirewallPolicyKubeSelectorGroupProperties;
+
+  /**
+   * The name of the resource that is unique within a resource group. This name can be used to access the resource.
+   */
+  @visibility(Lifecycle.Read)
+  @path
+  @key("kubeSelectorGroupName")
+  @segment("kubeSelectorGroups")
 ... 60 later diff lines omitted; full hunk is in assessment.json ...
```

```diff
--- /dev/null
+++ b/specification/network/resource-manager/Microsoft.Network/Network/Network/FirewallPolicyKubeSelectorGroup.tsp
@@ -0,0 +1,97 @@
 ... 48 earlier diff lines omitted; full hunk is in assessment.json ...
+@added(Versions.v2025_09_01)
+@armResourceOperations
+interface FirewallPolicyKubeSelectorGroups {
+  /**
+   * Gets the specified FirewallPolicyKubeSelectorGroup.
+   */
+  get is ArmResourceRead<FirewallPolicyKubeSelectorGroup, Error = CloudError>;
+
+  /**
+   * Creates or updates the specified FirewallPolicyKubeSelectorGroup.
+   */
+  createOrUpdate is ArmResourceCreateOrReplaceAsync<
 ... 37 later diff lines omitted; full hunk is in assessment.json ...
```

**Impact:** [FirewallPolicyKubeSelectorGroup does not use the documented child-resource model template](#finding-firewall-policy-kube-selector-group-child-resource-template)<br>
**Source:** [FirewallPolicyKubeSelectorGroup.tsp:L1-L97](https://github.com/Azure/azure-rest-api-specs/blob/780a61ace56c22ce10dd01caa8ab95ca4514ac2e/specification/network/resource-manager/Microsoft.Network/Network/Network/FirewallPolicyKubeSelectorGroup.tsp#L1-L97)

<a id="intent-5-add-first-party-service-tag-resources"></a>
### 5. Add first-party service tag resources

| Change | Aspect | Before | After |
| --- | --- | --- | --- |
| ➕ Added | Operation family | — | 6 REST operations added. |

**TypeSpec change:** The 2025-09-01 API introduces first-party service tag create-or-update, delete, get, resource-group list, subscription list, and tag-update operations. The related optional firstPartyServiceTagId reference on IP tags links existing tag data to the new resource type without making an existing request property required.

```diff
--- a/specification/network/resource-manager/Microsoft.Network/Network/Common/main.tsp
+++ b/specification/network/resource-manager/Microsoft.Network/Network/Common/main.tsp
@@ -1484,6 +1519,16 @@ model IpTag {
 ... 4 earlier diff lines omitted; full hunk is in assessment.json ...
+  /**
+   * The resource ID of the first party service tag associated with the IP tag.
+   */
+  @added(Versions.v2025_09_01)
+  firstPartyServiceTagId?: Azure.Core.armResourceIdentifier<[
+    {
+      type: "Microsoft.Network/firstPartyServiceTags";
+    }
+  ]>;
 }

 /**
```

```diff
--- /dev/null
+++ b/specification/network/resource-manager/Microsoft.Network/Network/Network/FirstPartyServiceTag.tsp
@@ -0,0 +1,166 @@
 ... 22 earlier diff lines omitted; full hunk is in assessment.json ...
+#suppress "@azure-tools/typespec-azure-core/composition-over-inheritance" "Back compatibility with the previous Network AutoRest C# hierarchy"
+#suppress "@azure-tools/typespec-azure-core/no-legacy-usage" "Legacy.feature decorator is required for swagger file splitting in this converted project"
+@added(Versions.v2025_09_01)
+@Azure.ResourceManager.Legacy.feature(Features.firstPartyServiceTag)
+@Http.Private.includeInapplicableMetadataInPayload(false)
+model FirstPartyServiceTag extends Common.Resource {
+  /**
+   * Properties of the first party service tag.
+   */
+  properties?: FirstPartyServiceTagPropertiesFormat;
+
+  /**
 ... 132 later diff lines omitted; full hunk is in assessment.json ...
```

**Source:** [main.tsp:L1519-L1534](https://github.com/Azure/azure-rest-api-specs/blob/780a61ace56c22ce10dd01caa8ab95ca4514ac2e/specification/network/resource-manager/Microsoft.Network/Network/Common/main.tsp#L1519-L1534), [FirstPartyServiceTag.tsp:L1-L166](https://github.com/Azure/azure-rest-api-specs/blob/780a61ace56c22ce10dd01caa8ab95ca4514ac2e/specification/network/resource-manager/Microsoft.Network/Network/Network/FirstPartyServiceTag.tsp#L1-L166)

<a id="intent-6-add-network-virtual-appliance-migration-actions"></a>
### 6. Add network virtual appliance migration actions

| Change | Aspect | Before | After |
| --- | --- | --- | --- |
| ➕ Added | Operation family | — | 4 REST operations added. |

**TypeSpec change:** The 2025-09-01 API adds prepare, execute, commit, and abort migration actions for network virtual appliances. Existing create, get, list, and tag-update operations retain their prior wire signatures as the resource family is carried into the new version.

```diff
--- a/specification/network/resource-manager/Microsoft.Network/Network/Network/NetworkVirtualAppliance.tsp
+++ b/specification/network/resource-manager/Microsoft.Network/Network/Network/NetworkVirtualAppliance.tsp
@@ -231,6 +232,62 @@ interface NetworkVirtualAppliances {
 ... 5 earlier diff lines omitted; full hunk is in assessment.json ...
+   * Prepares the migration of the specified Network Virtual Appliance. This is the first step of a migration workflow, such as migrating to a new OS version or to the new internal load balancer architecture.
+   */
+  @added(Versions.v2025_09_01)
+  prepareMigration is ArmResourceActionAsyncBase<
+    NetworkVirtualAppliance,
+    Request = NetworkVirtualAppliancePrepareMigrationRequest,
+    Response =
+      | ArmAcceptedLroResponse
+      | ArmNoContentResponse<"Action completed successfully.">,
+    BaseParameters = Azure.ResourceManager.Foundations.DefaultBaseParameters<NetworkVirtualAppliance>,
+    Error = CloudError
+  >;
 ... 45 later diff lines omitted; full hunk is in assessment.json ...
```

**Source:** [NetworkVirtualAppliance.tsp:L232-L293](https://github.com/Azure/azure-rest-api-specs/blob/780a61ace56c22ce10dd01caa8ab95ca4514ac2e/specification/network/resource-manager/Microsoft.Network/Network/Network/NetworkVirtualAppliance.tsp#L232-L293)

<a id="intent-7-add-connection-analyzer-lifecycle-and-query-oper"></a>
### 7. Add Connection Analyzer lifecycle and query operations under Network Watcher

| Change | Aspect | Before | After |
| --- | --- | --- | --- |
| ➕ Added | Operation family | — | 6 REST operations added. |

**TypeSpec change:** The 2025-09-01 API adds create, delete, get, list, tag-update, and query operations beneath a Network Watcher. The query operation is long-running and polls through the Azure-AsyncOperation header; list receives paging semantics, and the resource name is carried by the routed connectionAnalyzerName path parameter.

```diff
--- a/specification/network/resource-manager/Microsoft.Network/Network/Network/NetworkWatcher.tsp
+++ b/specification/network/resource-manager/Microsoft.Network/Network/Network/NetworkWatcher.tsp
@@ -439,6 +466,113 @@ interface NetworkWatchers {
 ... 5 earlier diff lines omitted; full hunk is in assessment.json ...
+   * Creates or updates a connection analyzer in the specified network watcher.
+   */
+  @added(Versions.v2025_09_01)
+  @tag("ConnectionAnalyzers")
+  @Azure.Core.useFinalStateVia("azure-async-operation")
+  @put
+  @route("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Network/networkWatchers/{networkWatcherName}/connectionAnalyzers/{connectionAnalyzerName}")
+  connectionAnalyzersCreate is ConnectionAnalyzerOps.ActionAsync<
+    NetworkWatcher,
+    ConnectionAnalyzer,
+    ConnectionAnalyzer,
+    Response =
 ... 96 later diff lines omitted; full hunk is in assessment.json ...
```

**Impact:** [ConnectionAnalyzer does not use the documented child-resource model template](#finding-connection-analyzer-child-resource-template), [ConnectionAnalyzer lifecycle operations do not use the documented ARM templates](#finding-connection-analyzer-lifecycle-operation-templates)<br>
**Source:** [models.tsp:L27062-L27900](https://github.com/Azure/azure-rest-api-specs/blob/780a61ace56c22ce10dd01caa8ab95ca4514ac2e/specification/network/resource-manager/Microsoft.Network/Network/Network/models.tsp#L27062-L27900), [NetworkWatcher.tsp:L1-L86](https://github.com/Azure/azure-rest-api-specs/blob/780a61ace56c22ce10dd01caa8ab95ca4514ac2e/specification/network/resource-manager/Microsoft.Network/Network/Network/NetworkWatcher.tsp#L1-L86), [NetworkWatcher.tsp:L466-L578](https://github.com/Azure/azure-rest-api-specs/blob/780a61ace56c22ce10dd01caa8ab95ca4514ac2e/specification/network/resource-manager/Microsoft.Network/Network/Network/NetworkWatcher.tsp#L466-L578)

<a id="intent-8-add-long-running-virtual-network-ip-configuratio"></a>
### 8. Add long-running virtual network IP configuration moves

| Change | Aspect | Before | After |
| --- | --- | --- | --- |
| ➕ Added | Operation family | — | 1 REST operation added. |

**TypeSpec change:** The 2025-09-01 API adds MoveIpConfigurations as a virtual network action. It accepts an explicit empty body and uses Azure-AsyncOperation polling; the existing virtual network lifecycle operations otherwise retain their wire contracts in the new version lineage.

```diff
--- a/specification/network/resource-manager/Microsoft.Network/Network/Network/VirtualNetwork.tsp
+++ b/specification/network/resource-manager/Microsoft.Network/Network/Network/VirtualNetwork.tsp
@@ -137,6 +139,26 @@ interface VirtualNetworks {
 ... 6 earlier diff lines omitted; full hunk is in assessment.json ...
+   */
+  #suppress "@azure-tools/typespec-azure-core/invalid-final-state" "azure-async-operation used for LRO polling"
+  @added(Versions.v2025_09_01)
+  @tag("VirtualNetworks")
+  @action("moveIpConfigurations")
+  @Azure.Core.useFinalStateVia("azure-async-operation")
+  moveIpConfigurations is ArmResourceActionAsync<
+    VirtualNetwork,
+    MoveIpConfigurationsRequest,
+    Response = {
+      @body body: void;
+    },
 ... 8 later diff lines omitted; full hunk is in assessment.json ...
```

**Source:** [VirtualNetwork.tsp:L139-L164](https://github.com/Azure/azure-rest-api-specs/blob/780a61ace56c22ce10dd01caa8ab95ca4514ac2e/specification/network/resource-manager/Microsoft.Network/Network/Network/VirtualNetwork.tsp#L139-L164)

<a id="intent-9-add-effective-route-retrieval-for-virtual-networ"></a>
### 9. Add effective-route retrieval for virtual network gateways

| Change | Aspect | Before | After |
| --- | --- | --- | --- |
| ➕ Added | Operation family | — | 1 REST operation added. |

**TypeSpec change:** The 2025-09-01 API exposes GetEffectiveRoutes on virtual network gateways. This is an additive operation in the new version and does not remove or alter an existing path.

```diff
--- a/specification/network/resource-manager/Microsoft.Network/Network/Network/VirtualNetworkGateway.tsp
+++ b/specification/network/resource-manager/Microsoft.Network/Network/Network/VirtualNetworkGateway.tsp
@@ -271,6 +272,18 @@ interface VirtualNetworkGateways {
 ... 4 earlier diff lines omitted; full hunk is in assessment.json ...
+   * This operation retrieves a list of effective routes for the virtual network gateway.
+   */
+  @added(Versions.v2025_09_01)
+  @tag("VirtualNetworkGateways")
+  getEffectiveRoutes is ArmResourceActionAsync<
+    VirtualNetworkGateway,
+    void,
+    GatewayEffectiveRouteListResult,
+    Error = CloudError
+  >;
+
   /**
 ... 2 later diff lines omitted; full hunk is in assessment.json ...
```

**Source:** [VirtualNetworkGateway.tsp:L272-L289](https://github.com/Azure/azure-rest-api-specs/blob/780a61ace56c22ce10dd01caa8ab95ca4514ac2e/specification/network/resource-manager/Microsoft.Network/Network/Network/VirtualNetworkGateway.tsp#L272-L289)

<a id="intent-10-make-service-gateway-update-actions-synchronous-"></a>
### 10. Make service gateway update actions synchronous in 2025-09-01

| Change | Aspect | Before | After |
| --- | --- | --- | --- |
| ✏️ Modified | Responses | Status: 202; Schemas: ; Headers: Location; Retry-After; Status: 204; Schemas: ; Headers: | Status: 200; Schemas: ServiceGatewayActionOkResponseBody; Headers: |
| ✏️ Modified | Lro | Is Long Running: true; Final State Via: location; Final Result: operation response | Is Long Running: false |

**TypeSpec change:** For the new 2025-09-01 version, UpdateAddressLocations and UpdateServices replace the prior asynchronous 202/204 flow and Location polling with a synchronous 200 response containing an optional status value. This is a real REST behavior change relative to the preceding version lineage, but it is intentionally versioned and does not rewrite an already released API version.

```diff
--- a/specification/network/resource-manager/Microsoft.Network/Network/Network/ServiceGateway.tsp
+++ b/specification/network/resource-manager/Microsoft.Network/Network/Network/ServiceGateway.tsp
@@ -48,11 +49,29 @@ model ServiceGateway extends SecurityPerimeterTrackedResource {
   /** A list of availability zones denoting the zone in which service gateway should be deployed.
    *
    * - The zone values must be provided as strings representing numeric identifiers like "1", "2", "3" etc. */
-  #suppress "@azure-tools/typespec-azure-resource-manager/arm-resource-invalid-envelope-property" "FIXME: Update justification, follow aka.ms/tsp/conversion-fix for details"
+  #suppress "@azure-tools/typespec-azure-resource-manager/arm-resource-invalid-envelope-property" "The zones property is a top-level envelope field required for availability zone deployment configuration. It is consistent with the existing pattern used by other zonal resources in this namespace (e.g. NatGateway, PublicIPAddress) and must remain in the envelope for backward compatibility."
   zones?: string[];
 }

-#suppress "@azure-tools/typespec-azure-core/no-legacy-usage" "FIXME: Update justification, follow aka.ms/tsp/conversion-fix for details"
+@doc("Empty success response.")
+model ServiceGatewayActionOkResponseBody {
+  /** The status of the operation. */
 ... 19 later diff lines omitted; full hunk is in assessment.json ...
```

```diff
--- a/specification/network/resource-manager/Microsoft.Network/Network/Network/ServiceGateway.tsp
+++ b/specification/network/resource-manager/Microsoft.Network/Network/Network/ServiceGateway.tsp
@@ -107,7 +126,10 @@ interface ServiceGateways {
    *
    * For address-level partial updates, if no services are provided, the existing services will be considered for deletion.
    */
-  updateAddressLocations is ArmResourceActionAsync<
+  @removed(Versions.v2025_09_01)
+  @action("updateAddressLocations")
+  @sharedRoute
+  updateAddressLocationsLro is ArmResourceActionAsync<
     ServiceGateway,
     ServiceGatewayUpdateAddressLocationsRequest,
     NoContentResponse,
```

3 additional TypeSpec hunks omitted; complete diffs are in `assessment.json`.

**Impact:** [Generated service gateway update methods can change from pollers to synchronous calls](#finding-source-service-gateway-actions-change-from-lro-to-synchronous)<br>
**Source:** [ServiceGateway.tsp:L49-L77](https://github.com/Azure/azure-rest-api-specs/blob/780a61ace56c22ce10dd01caa8ab95ca4514ac2e/specification/network/resource-manager/Microsoft.Network/Network/Network/ServiceGateway.tsp#L49-L77), [ServiceGateway.tsp:L126-L135](https://github.com/Azure/azure-rest-api-specs/blob/780a61ace56c22ce10dd01caa8ab95ca4514ac2e/specification/network/resource-manager/Microsoft.Network/Network/Network/ServiceGateway.tsp#L126-L135), [ServiceGateway.tsp:L138-L165](https://github.com/Azure/azure-rest-api-specs/blob/780a61ace56c22ce10dd01caa8ab95ca4514ac2e/specification/network/resource-manager/Microsoft.Network/Network/Network/ServiceGateway.tsp#L138-L165), [ServiceGateway.tsp:L167-L176](https://github.com/Azure/azure-rest-api-specs/blob/780a61ace56c22ce10dd01caa8ab95ca4514ac2e/specification/network/resource-manager/Microsoft.Network/Network/Network/ServiceGateway.tsp#L167-L176), [ServiceGateway.tsp:L179-L200](https://github.com/Azure/azure-rest-api-specs/blob/780a61ace56c22ce10dd01caa8ab95ca4514ac2e/specification/network/resource-manager/Microsoft.Network/Network/Network/ServiceGateway.tsp#L179-L200)

<a id="intent-11-add-opt-in-afc-managed-firewall-policy-writes"></a>
### 11. Add opt-in AFC-managed firewall policy writes

| Change | Aspect | Before | After |
| --- | --- | --- | --- |
| ✏️ Modified | Parameters | — | In: query; Name: afcManagedSync; Required: false; Type: boolean |

**TypeSpec change:** Add the optional afcManagedSync query parameter to FirewallPolicies_CreateOrUpdate in the 2025-09-01 version.

```diff
--- a/specification/network/resource-manager/Microsoft.Network/Network/Network/FirewallPolicy.tsp
+++ b/specification/network/resource-manager/Microsoft.Network/Network/Network/FirewallPolicy.tsp
@@ -68,6 +70,14 @@ interface FirewallPolicies {
 ... 1 earlier diff lines omitted; full hunk is in assessment.json ...
   createOrUpdate is ArmResourceCreateOrReplaceAsync<
     FirewallPolicy,
+    Parameters = {
+      /**
+       * Indicates that the write originates from AFC (Azure Firewall for Containers) and is permitted to modify an AFC-managed Firewall Policy.
+       */
+      @added(Versions.v2025_09_01)
+      @query("afcManagedSync")
+      afcManagedSync?: boolean;
+    },
     Error = CloudError
   >;
```

**Source:** [FirewallPolicy.tsp:L70-L82](https://github.com/Azure/azure-rest-api-specs/blob/780a61ace56c22ce10dd01caa8ab95ca4514ac2e/specification/network/resource-manager/Microsoft.Network/Network/Network/FirewallPolicy.tsp#L70-L82)

Need the complete REST representation for every affected operation? Use this prompt:

`Using assessment.json for PR #44988, show the complete REST representation for every affected operation, including operation ID, method/path, parameters, request, responses, LRO, paging, and TypeSpec source.`

## 🛡️ Compatibility Assessment

### REST Breaking Changes

None detected.

### Downstream Breaking Changes

<a id="finding-source-service-gateway-actions-change-from-lro-to-synchronous"></a>
### Generated service gateway update methods can change from pollers to synchronous calls

- **Severity:** high
- **Confidence:** high
- **Summary:** The older REST API versions remain compatible, but generated SDKs that move to 2025-09-01 can expose ServiceGateways_UpdateAddressLocations and ServiceGateways_UpdateServices as synchronous methods instead of long-running poller or operation-handle methods. Existing call sites can stop compiling because the return type and completion-handling pattern change, and runtime code that waits for polling completion must instead consume the immediate 200 response.
- **Evidence:** Both existing operations change from 202/204 responses with Location polling to a synchronous 200 response in 2025-09-01.; The TypeSpec replaces ArmResourceActionAsync with version-gated ArmResourceActionSync operations.
- **TypeSpec source:** [ServiceGateway.tsp:L49-L77](https://github.com/Azure/azure-rest-api-specs/blob/780a61ace56c22ce10dd01caa8ab95ca4514ac2e/specification/network/resource-manager/Microsoft.Network/Network/Network/ServiceGateway.tsp#L49-L77), [ServiceGateway.tsp:L126-L135](https://github.com/Azure/azure-rest-api-specs/blob/780a61ace56c22ce10dd01caa8ab95ca4514ac2e/specification/network/resource-manager/Microsoft.Network/Network/Network/ServiceGateway.tsp#L126-L135), [ServiceGateway.tsp:L138-L165](https://github.com/Azure/azure-rest-api-specs/blob/780a61ace56c22ce10dd01caa8ab95ca4514ac2e/specification/network/resource-manager/Microsoft.Network/Network/Network/ServiceGateway.tsp#L138-L165), [ServiceGateway.tsp:L167-L176](https://github.com/Azure/azure-rest-api-specs/blob/780a61ace56c22ce10dd01caa8ab95ca4514ac2e/specification/network/resource-manager/Microsoft.Network/Network/Network/ServiceGateway.tsp#L167-L176), [ServiceGateway.tsp:L179-L200](https://github.com/Azure/azure-rest-api-specs/blob/780a61ace56c22ce10dd01caa8ab95ca4514ac2e/specification/network/resource-manager/Microsoft.Network/Network/Network/ServiceGateway.tsp#L179-L200)


## 📎 Appendix

### Assessment Errors

None.

### Code-to-Guidance Evidence

| Result | Document section | Fetched guidance | Observed TypeSpec | Evidence |
| --- | --- | --- | --- | --- |
| Mismatch | ARM resource types and modeling - Add ARM Resource Type | Resources are modeled in TypeSpec by choosing a base resource type, defining rp-specific properties, and optionally mixing in standard envelope properties. Child resources usually use the `ProxyResource<TProperties>` as their base resource type. | The changed declaration is @parentResource(ApplicationSecurityGroup) model AddressPrefixSet extends ProxyResource, followed by a separate properties property and a manually decorated name property; its suppression explicitly identifies ProxyResource as the local Network RP base type. The changed declaration is @parentResource(FirewallPolicy) model FirewallPolicyKubeSelectorGroup extends SubResourceModel, followed by a separate properties property and a manually decorated name property. The changed model uses @Azure.ResourceManager.Legacy.customAzureResource and declares model ConnectionAnalyzer with only @visibility(Lifecycle.Read) name?: string; the changed NetworkWatcher routes address /networkWatchers/{networkWatcherName}/connectionAnalyzers/{connectionAnalyzerName}. | AddressPrefixSet.tsp:L1-L100, FirewallPolicyKubeSelectorGroup.tsp:L1-L97, NetworkWatcher.tsp:L12-L12, +988 more |
| Mismatch | ARM resource operations - Add ARM Resource Operation | Custom actions define any operations over resources outside the simple CRUDL (Create, Read, Update, Delete, List) or lifecycle operations described above. | ConnectionAnalyzerOps aliases Azure.ResourceManager.Legacy.RoutedOperations, and the changed NetworkWatchers declarations apply @put, @delete, @patch, @get, and @list directly to explicit /connectionAnalyzers routes rather than using the documented CRUDL templates. | NetworkWatcher.tsp:L12-L12, NetworkWatcher.tsp:L41-L66, NetworkWatcher.tsp:L469-L575, +3 more |

### Tooling Used

- `@azure-tools/typespec-autorest`
- `@azure-tools/typespec-client-generator-core`

### Artifact Evidence

- **autorest:** Preserved successful base/head AutoRest artifacts were reused without recompilation. They contain 37 additive 2025-09-01 operations, no removed operation, unchanged older-version wire shapes apart from documentation text, and additive shared Vmss model fields.
- **tcgc:** Preserved successful base/head generic TCGC artifacts were reused without recompilation. The service gateway update actions create a downstream compatibility risk when generated SDKs adopt 2025-09-01 because their methods can change from long-running pollers to synchronous calls.
- **compilation:** Compilation was explicitly skipped; preserved successful base/head AutoRest and TCGC artifacts from the designated historical evidence set were reused.
- **canonicalComparison:** Materially consistent with the corrected PR #44988 report: 10 semantic intents, no REST breaking finding, one downstream SDK finding for the service gateway LRO-to-synchronous transition, failed compliance status with the same two medium findings, high confidence, and a Medium code-safety conclusion.
