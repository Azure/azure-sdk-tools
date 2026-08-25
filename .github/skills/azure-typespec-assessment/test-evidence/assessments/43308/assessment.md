# 📋 TypeSpec Assessment

**PR:** [#43308 - Fix TypeSpec LRO operations: remove @extension, use @useFinalStateVia](https://github.com/Azure/azure-rest-api-specs/pull/43308)

**Overall confidence:** 🟢 high<br>
**Overall code safety:** 🔴 Low

**Baseline:** `232dfa71843fd574be49ba91a918ee7d3e7bfec3`<br>
**Head:** `a3b8933eb6ce030faa6abc6d354fc97e30f02e96`; working-tree changes: false<br>
**Total assessment time:** 2m 57s

## 📌 Executive Summary

| Dimension | Result | Findings |
| --- | --- | ---: |
| Semantic understanding | ✅ Assessed — 1 intent(s), 6 operation(s) | n/a |
| REST compatibility | ✅ No breaks detected | 0 |
| Downstream compatibility | ❌ Issues found | 1 |
| Azure compliance | ✅ passed | 0 |

**Scope:** 1 intent(s), 6 affected operation(s), 1 project(s).<br>
**Changes:** 0 added, 6 modified, 0 removed.<br>
**Highest severity:** high.

## 🎯 Action Required

| Severity | Area | Finding | Why it matters | Code | Guidance |
| --- | --- | --- | --- | --- | --- |
| high | Downstream | Generated SDK long-running operation behavior changes | The wire contract is unchanged, but replacing raw long-running-operation metadata and explicit ScenarioRuns.get polling links with TypeSpec Location-based final-state metadata can change whether existing asynchronous methods are exposed as long-running operations. Generated clients can consequently change from an immediate-response method to a poller or operation-handle method, breaking existing call sites, return-type expectations, and completion handling. | [lro-helpers.tsp:L22-L119](https://github.com/Azure/azure-rest-api-specs/blob/a3b8933eb6ce030faa6abc6d354fc97e30f02e96/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/lro-helpers.tsp#L22-L119), [scenarioConfiguration.tsp:L48-L84](https://github.com/Azure/azure-rest-api-specs/blob/a3b8933eb6ce030faa6abc6d354fc97e30f02e96/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/scenarioConfiguration.tsp#L48-L84), +2 more | n/a |

## 🛡️ Compatibility Assessment

### REST Breaking Changes

None detected.

### Downstream Breaking Changes

<a id="finding-source-lro-metadata-representation-changed"></a>
### Generated SDK long-running operation behavior changes

- **Severity:** high
- **Confidence:** high
- **Summary:** The wire contract is unchanged, but replacing raw long-running-operation metadata and explicit ScenarioRuns.get polling links with TypeSpec Location-based final-state metadata can change whether existing asynchronous methods are exposed as long-running operations. Generated clients can consequently change from an immediate-response method to a poller or operation-handle method, breaking existing call sites, return-type expectations, and completion handling.
- **Evidence:** Replacing raw OpenAPI or polling metadata with TypeSpec LRO metadata can change whether generated SDK methods are recognized as long-running.; Changed TypeSpec source: specification/chaos/resource-manager/Microsoft.Chaos/Chaos/lro-helpers.tsp.; Changed TypeSpec source: specification/chaos/resource-manager/Microsoft.Chaos/Chaos/scenarioConfiguration.tsp.; Changed TypeSpec source: specification/chaos/resource-manager/Microsoft.Chaos/Chaos/scenarioRun.tsp.
- **TypeSpec source:** [lro-helpers.tsp:L22-L119](https://github.com/Azure/azure-rest-api-specs/blob/a3b8933eb6ce030faa6abc6d354fc97e30f02e96/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/lro-helpers.tsp#L22-L119), [scenarioConfiguration.tsp:L48-L84](https://github.com/Azure/azure-rest-api-specs/blob/a3b8933eb6ce030faa6abc6d354fc97e30f02e96/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/scenarioConfiguration.tsp#L48-L84), [scenarioRun.tsp:L44-L52](https://github.com/Azure/azure-rest-api-specs/blob/a3b8933eb6ce030faa6abc6d354fc97e30f02e96/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/scenarioRun.tsp#L44-L52), [workspace.tsp:L69-L80](https://github.com/Azure/azure-rest-api-specs/blob/a3b8933eb6ce030faa6abc6d354fc97e30f02e96/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/workspace.tsp#L69-L80)


## ☁️ Azure Compliance

**Status:** `passed`

### Compliance Findings

No compliance mismatches found.

## 🧠 Semantic Understanding

<a id="intent-1-model-existing-location-based-long-running-behav"></a>
### 1. Model existing Location-based long-running behavior with TypeSpec LRO metadata

| Change | Aspect | Before | After |
| --- | --- | --- | --- |
| ✏️ Modified | TypeSpec LRO metadata | Final-state-via Location is represented with raw OpenAPI extensions. | Final-state-via Location is represented with @Azure.Core.useFinalStateVia("location"). |

**TypeSpec change:** Replace OpenAPI-only LRO extensions with TypeSpec LRO decorators and remove the obsolete polling-operation link.

```diff
--- a/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/scenarioConfiguration.tsp
+++ b/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/scenarioConfiguration.tsp
@@ -59,9 +57,7 @@ interface ScenarioConfigurations {
   /**
    * Cancel the currently running scenario execution.
    */
-  #suppress "@azure-tools/typespec-azure-resource-manager/arm-post-operation-response-codes" "LRO POST returns 202 with Location header for polling; final result obtained by polling the Location URL"
   @removed(Microsoft.Chaos.Versions.v2026_05_01_preview)
-  @pollingOperation(ScenarioRuns.get)
   cancel is ArmResourceActionNoContentAsyncWithLocationResult<
     ScenarioConfiguration,
     void,
```

```diff
--- a/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/lro-helpers.tsp
+++ b/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/lro-helpers.tsp
@@ -24,15 +22,10 @@ namespace Microsoft.Chaos;
 #suppress "@azure-tools/typespec-azure-core/documentation-required" "template"
 #suppress "@azure-tools/typespec-azure-resource-manager/arm-resource-operation" "template"
 #suppress "@azure-tools/typespec-azure-resource-manager/arm-post-operation-response-codes" "Custom LRO operation with specific response codes"
-#suppress "@azure-tools/typespec-azure-core/no-openapi" "Required for LRO extensions in OpenAPI output"
 @autoRoute
 @armResourceAction(Resource)
 @post
-@extension("x-ms-long-running-operation", true)
-@extension(
-  "x-ms-long-running-operation-options",
-  #{ `final-state-via`: "location" }
-)
 ... 4 later diff lines omitted; full hunk is in assessment.json ...
```

7 additional TypeSpec hunks omitted; complete diffs are in `assessment.json`.

**Impact:** [Generated SDK long-running operation behavior changes](#finding-source-lro-metadata-representation-changed)<br>
**Source:** [lro-helpers.tsp:L22-L119](https://github.com/Azure/azure-rest-api-specs/blob/a3b8933eb6ce030faa6abc6d354fc97e30f02e96/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/lro-helpers.tsp#L22-L119), [scenarioConfiguration.tsp:L48-L84](https://github.com/Azure/azure-rest-api-specs/blob/a3b8933eb6ce030faa6abc6d354fc97e30f02e96/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/scenarioConfiguration.tsp#L48-L84), [scenarioRun.tsp:L44-L52](https://github.com/Azure/azure-rest-api-specs/blob/a3b8933eb6ce030faa6abc6d354fc97e30f02e96/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/scenarioRun.tsp#L44-L52), [workspace.tsp:L69-L80](https://github.com/Azure/azure-rest-api-specs/blob/a3b8933eb6ce030faa6abc6d354fc97e30f02e96/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/workspace.tsp#L69-L80)

Need the complete REST representation for every affected operation? Use this prompt:

`Using assessment.json for PR #43308, show the complete REST representation for every affected operation, including operation ID, method/path, parameters, request, responses, LRO, paging, and TypeSpec source.`

## 📎 Appendix

### Assessment Errors

None.

### Code-to-Guidance Evidence

| Result | Document section | Fetched guidance | Observed TypeSpec | Evidence |
| --- | --- | --- | --- | --- |
| Matched | Azure.Core long-running operations - Long-Running Operations (LRO) | Deep Dive: Long-running (Asynchronous) Operations \| TypeSpec Azure Skip to content TypeSpec Azure Docs Playground TypeSpec Core Docs Can I Use (Azure Client) Benchmarks Search... Introduction Get started Installation Creating a project Versioning Azure Data Plane Service 1. Writing Your First Service 2. Create the service namespace 3. Defining your first resource 4. Defining standard resource operations 5. Defining long-running | The official Azure.Core guidance distinguishes a polling status-monitor operation, linked with @pollingOperation, from a final resource URL carried by Location, and the ARM guidance documents Location-header LRO response shapes. The changed TypeSpec removes ScenarioRuns.get as a polling status monitor and applies @Azure.Core.useFinalStateVia("location") to the LRO helpers, matching the documented Location-based final-result pattern. The retained 202 response includes Location and Retry-After where shown by the exact operation contract. | lro-helpers.tsp:L22-L119, scenarioConfiguration.tsp:L48-L84, scenarioRun.tsp:L44-L52, +1 more |
| Matched | Azure.Core operation interfaces - Add ARM Resource Operation | 5. Defining long-running resource operations \| TypeSpec Azure Skip to content TypeSpec Azure Docs Playground TypeSpec Core Docs Can I Use (Azure Client) Benchmarks Search... Introduction Get started Installation Creating a project Versioning Azure Data Plane Service 1. Writing Your First Service 2. Create the service namespace 3. Defining your first resource 4. Defining standard resource operations 5. Defining long-running resou | The official Azure.Core guidance distinguishes a polling status-monitor operation, linked with @pollingOperation, from a final resource URL carried by Location, and the ARM guidance documents Location-header LRO response shapes. The changed TypeSpec removes ScenarioRuns.get as a polling status monitor and applies @Azure.Core.useFinalStateVia("location") to the LRO helpers, matching the documented Location-based final-result pattern. The retained 202 response includes Location and Retry-After where shown by the exact operation contract. | lro-helpers.tsp:L22-L119, scenarioConfiguration.tsp:L48-L84, scenarioRun.tsp:L44-L52, +1 more |
| Matched | ARM long-running operations - Long-Running Operations (LRO) | Customizing Long-Running Operations \| TypeSpec Azure Skip to content TypeSpec Azure Docs Playground TypeSpec Core Docs Can I Use (Azure Client) Benchmarks Search... Introduction Get started Installation Creating a project Versioning Azure Data Plane Service 1. Writing Your First Service 2. Create the service namespace 3. Defining your first resource 4. Defining standard resource operations 5. Defining long-running resource opera | The official Azure.Core guidance distinguishes a polling status-monitor operation, linked with @pollingOperation, from a final resource URL carried by Location, and the ARM guidance documents Location-header LRO response shapes. The changed TypeSpec removes ScenarioRuns.get as a polling status monitor and applies @Azure.Core.useFinalStateVia("location") to the LRO helpers, matching the documented Location-based final-result pattern. The retained 202 response includes Location and Retry-After where shown by the exact operation contract. | lro-helpers.tsp:L22-L119, scenarioConfiguration.tsp:L48-L84, scenarioRun.tsp:L44-L52, +1 more |

### Tooling Used

- `@azure-tools/typespec-autorest`
- `@azure-tools/typespec-client-generator-core`

### Artifact Evidence

- **autorest:** Base/head OpenAPI wire contracts are identical for all six operations after excluding documentation text; both retain x-ms-long-running-operation and final-state-via location.
- **tcgc:** Execute, legacy ScenarioConfigurations.cancel, and ScenarioRuns.cancel change from kind: basic without lroMetadata to kind: lro with finalStateVia: location. Validate, fixResourcePermissions, and refreshRecommendations remain kind: lro.
- **canonicalComparison:** Materially consistent overall: both reports identify one emitter-metadata intent, no REST break, one high-severity REST-compatible downstream break, passed compliance with no findings, high confidence, and a rest-compatible-downstream-breaking-change conclusion. The fresh report narrows the downstream scope: preserved TCGC proves only ScenarioConfigurations.execute, legacy ScenarioConfigurations.cancel, and ScenarioRuns.cancel change from basic to lro; validate, fixResourcePermissions, and refreshRecommendations were already lro methods. The canonical semantic wording therefore overstates SDK LRO recognition as newly affecting all six operations.
