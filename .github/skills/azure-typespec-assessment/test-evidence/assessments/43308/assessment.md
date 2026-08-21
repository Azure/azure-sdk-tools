# 📋 TypeSpec Assessment

**PR:** [#43308 - Fix TypeSpec LRO operations: remove @extension, use @useFinalStateVia](https://github.com/Azure/azure-rest-api-specs/pull/43308)

**Overall confidence:** 🟢 high<br>
**Overall code safety:** 🔴 Low

**Baseline:** `232dfa71843fd574be49ba91a918ee7d3e7bfec3`<br>
**Head:** `a3b8933eb6ce030faa6abc6d354fc97e30f02e96`; working-tree changes: false<br>
**Total assessment time:** 13m 49s

## 📌 Executive Summary

| Dimension | Result | Findings |
| --- | --- | ---: |
| Semantic understanding | ✅ Assessed — 1 intent(s), 6 operation(s) | n/a |
| REST compatibility | ✅ No breaks detected | 0 |
| Downstream compatibility | ❌ Issues found | 1 |
| Azure compliance | ⚠️ not-assessed | 0 |

**Scope:** 1 intent(s), 6 affected operation(s), 1 project(s).<br>
**Highest severity:** high.

## 🎯 Action Required

| Severity | Area | Finding | Why it matters | Code | Guidance |
| --- | --- | --- | --- | --- | --- |
| high | Downstream | Generated SDK methods change from synchronous to long-running | The wire contract is unchanged, but SDKs now expose poller/LRO return types and asynchronous completion semantics. | [lro-helpers.tsp:L22-L119](https://github.com/Azure/azure-rest-api-specs/blob/a3b8933eb6ce030faa6abc6d354fc97e30f02e96/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/lro-helpers.tsp#L22-L119), [scenarioConfiguration.tsp:L48-L84](https://github.com/Azure/azure-rest-api-specs/blob/a3b8933eb6ce030faa6abc6d354fc97e30f02e96/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/scenarioConfiguration.tsp#L48-L84), +2 more | n/a |

## 🧠 Semantic Understanding

### Change Overview

| # | Intent | Operations | API versions | Details |
| ---: | --- | ---: | --- | --- |
| 1 | Make six ARM action operations visible as LROs to both OpenAPI and SDK generators by replacing OpenAPI-only extensions with TypeSpec LRO metadata. | 6 | 2026-05-01-preview, 2024-11-01-preview | [details](#intent-1-make-six-arm-action-operations-visible-as-lros-t) |

### Operation Details

<a id="intent-1-make-six-arm-action-operations-visible-as-lros-t"></a>
### 1. Make six ARM action operations visible as LROs to both OpenAPI and SDK generators by replacing OpenAPI-only extensions with TypeSpec LRO metadata.

**Confidence:** high<br>
**REST summary:** The six POST operations remain 202 ARM LROs using Location and Retry-After with final-state-via location; the change fixes SDK LRO recognition without changing their REST signatures.

#### `ScenarioConfigurations_Execute`

- **HTTP path:** `POST /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Chaos/workspaces/{workspaceName}/scenarios/{scenarioName}/configurations/{scenarioConfigurationName}/execute`
- **API versions:** `2026-05-01-preview`
- **Parameters:** path subscriptionId, resourceGroupName, workspaceName, scenarioName, scenarioConfigurationName: string, required; query api-version: string, required
- **Request payload:** none
- **Response payloads:** 202: no body; Location and Retry-After headers; default: ErrorResponse
- **Service behavior:** Starts a scenario run and returns before execution completes.
- **LRO:** arm; via location; Wait Retry-After, then poll the Location URL until the scenario run reaches a terminal state.; final result: ScenarioRun from the Location endpoint.
- **Paging:** No.
- **TypeSpec source:** [lro-helpers.tsp:L22-L119](https://github.com/Azure/azure-rest-api-specs/blob/a3b8933eb6ce030faa6abc6d354fc97e30f02e96/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/lro-helpers.tsp#L22-L119), [scenarioConfiguration.tsp:L48-L84](https://github.com/Azure/azure-rest-api-specs/blob/a3b8933eb6ce030faa6abc6d354fc97e30f02e96/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/scenarioConfiguration.tsp#L48-L84), [scenarioRun.tsp:L44-L52](https://github.com/Azure/azure-rest-api-specs/blob/a3b8933eb6ce030faa6abc6d354fc97e30f02e96/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/scenarioRun.tsp#L44-L52), [workspace.tsp:L69-L80](https://github.com/Azure/azure-rest-api-specs/blob/a3b8933eb6ce030faa6abc6d354fc97e30f02e96/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/workspace.tsp#L69-L80)

#### `ScenarioConfigurations_Validate`

- **HTTP path:** `POST /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Chaos/workspaces/{workspaceName}/scenarios/{scenarioName}/configurations/{scenarioConfigurationName}/validate`
- **API versions:** `2026-05-01-preview`
- **Parameters:** path subscriptionId, resourceGroupName, workspaceName, scenarioName, scenarioConfigurationName: string, required; query api-version: string, required
- **Request payload:** none
- **Response payloads:** 202: no body; Location and Retry-After headers; default: ErrorResponse
- **Service behavior:** Starts asynchronous validation of a scenario configuration.
- **LRO:** arm; via location; Poll Location after Retry-After until terminal completion.; final result: Validation result from the Location endpoint.
- **Paging:** No.
- **TypeSpec source:** [lro-helpers.tsp:L22-L119](https://github.com/Azure/azure-rest-api-specs/blob/a3b8933eb6ce030faa6abc6d354fc97e30f02e96/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/lro-helpers.tsp#L22-L119), [scenarioConfiguration.tsp:L48-L84](https://github.com/Azure/azure-rest-api-specs/blob/a3b8933eb6ce030faa6abc6d354fc97e30f02e96/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/scenarioConfiguration.tsp#L48-L84), [scenarioRun.tsp:L44-L52](https://github.com/Azure/azure-rest-api-specs/blob/a3b8933eb6ce030faa6abc6d354fc97e30f02e96/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/scenarioRun.tsp#L44-L52), [workspace.tsp:L69-L80](https://github.com/Azure/azure-rest-api-specs/blob/a3b8933eb6ce030faa6abc6d354fc97e30f02e96/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/workspace.tsp#L69-L80)

#### `ScenarioConfigurations_Cancel`

- **HTTP path:** `POST /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Chaos/workspaces/{workspaceName}/scenarios/{scenarioName}/configurations/{scenarioConfigurationName}/cancel`
- **API versions:** `2024-11-01-preview`
- **Parameters:** path subscriptionId, resourceGroupName, workspaceName, scenarioName, scenarioConfigurationName: string, required; query api-version: string, required
- **Request payload:** none
- **Response payloads:** 202: no body; Location and Retry-After headers; default: ErrorResponse
- **Service behavior:** Requests cancellation through the scenario configuration endpoint in versions before its removal.
- **LRO:** arm; via location; Poll Location after Retry-After until Succeeded, Failed, or Canceled.; final result: OperationStatusResult from the Location endpoint.
- **Paging:** No.
- **TypeSpec source:** [lro-helpers.tsp:L22-L119](https://github.com/Azure/azure-rest-api-specs/blob/a3b8933eb6ce030faa6abc6d354fc97e30f02e96/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/lro-helpers.tsp#L22-L119), [scenarioConfiguration.tsp:L48-L84](https://github.com/Azure/azure-rest-api-specs/blob/a3b8933eb6ce030faa6abc6d354fc97e30f02e96/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/scenarioConfiguration.tsp#L48-L84), [scenarioRun.tsp:L44-L52](https://github.com/Azure/azure-rest-api-specs/blob/a3b8933eb6ce030faa6abc6d354fc97e30f02e96/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/scenarioRun.tsp#L44-L52), [workspace.tsp:L69-L80](https://github.com/Azure/azure-rest-api-specs/blob/a3b8933eb6ce030faa6abc6d354fc97e30f02e96/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/workspace.tsp#L69-L80)

#### `ScenarioConfigurations_FixResourcePermissions`

- **HTTP path:** `POST /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Chaos/workspaces/{workspaceName}/scenarios/{scenarioName}/configurations/{scenarioConfigurationName}/fixResourcePermissions`
- **API versions:** `2026-05-01-preview`
- **Parameters:** path subscriptionId, resourceGroupName, workspaceName, scenarioName, scenarioConfigurationName: string, required; query api-version: string, required
- **Request payload:** optional application/json body: FixResourcePermissionsRequest
- **Response payloads:** 202: no body; Location and Retry-After headers; default: ErrorResponse
- **Service behavior:** Starts asynchronous repair of permissions required by the scenario.
- **LRO:** arm; via location; Poll Location after Retry-After until terminal completion.; final result: PermissionsFix result from the Location endpoint.
- **Paging:** No.
- **TypeSpec source:** [lro-helpers.tsp:L22-L119](https://github.com/Azure/azure-rest-api-specs/blob/a3b8933eb6ce030faa6abc6d354fc97e30f02e96/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/lro-helpers.tsp#L22-L119), [scenarioConfiguration.tsp:L48-L84](https://github.com/Azure/azure-rest-api-specs/blob/a3b8933eb6ce030faa6abc6d354fc97e30f02e96/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/scenarioConfiguration.tsp#L48-L84), [scenarioRun.tsp:L44-L52](https://github.com/Azure/azure-rest-api-specs/blob/a3b8933eb6ce030faa6abc6d354fc97e30f02e96/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/scenarioRun.tsp#L44-L52), [workspace.tsp:L69-L80](https://github.com/Azure/azure-rest-api-specs/blob/a3b8933eb6ce030faa6abc6d354fc97e30f02e96/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/workspace.tsp#L69-L80)

#### `ScenarioRuns_Cancel`

- **HTTP path:** `POST /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Chaos/workspaces/{workspaceName}/scenarios/{scenarioName}/runs/{runId}/cancel`
- **API versions:** `2026-05-01-preview`
- **Parameters:** path subscriptionId, resourceGroupName, workspaceName, scenarioName, runId: string, required; query api-version: string, required
- **Request payload:** none
- **Response payloads:** 202: no body; Location and Retry-After headers; default: ErrorResponse
- **Service behavior:** Requests cancellation of an active scenario run.
- **LRO:** arm; via location; Poll Location after Retry-After until Succeeded, Failed, or Canceled.; final result: ScenarioRun from the Location endpoint.
- **Paging:** No.
- **TypeSpec source:** [lro-helpers.tsp:L22-L119](https://github.com/Azure/azure-rest-api-specs/blob/a3b8933eb6ce030faa6abc6d354fc97e30f02e96/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/lro-helpers.tsp#L22-L119), [scenarioConfiguration.tsp:L48-L84](https://github.com/Azure/azure-rest-api-specs/blob/a3b8933eb6ce030faa6abc6d354fc97e30f02e96/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/scenarioConfiguration.tsp#L48-L84), [scenarioRun.tsp:L44-L52](https://github.com/Azure/azure-rest-api-specs/blob/a3b8933eb6ce030faa6abc6d354fc97e30f02e96/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/scenarioRun.tsp#L44-L52), [workspace.tsp:L69-L80](https://github.com/Azure/azure-rest-api-specs/blob/a3b8933eb6ce030faa6abc6d354fc97e30f02e96/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/workspace.tsp#L69-L80)

#### `Workspaces_RefreshRecommendations`

- **HTTP path:** `POST /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Chaos/workspaces/{workspaceName}/refreshRecommendations`
- **API versions:** `2026-05-01-preview`
- **Parameters:** path subscriptionId, resourceGroupName, workspaceName: string, required; query api-version: string, required
- **Request payload:** none
- **Response payloads:** 202: no body; Location and Retry-After headers; default: ErrorResponse
- **Service behavior:** Starts asynchronous regeneration of workspace recommendations.
- **LRO:** arm; via location; Poll Location after Retry-After until terminal completion.; final result: No response body after successful completion.
- **Paging:** No.
- **TypeSpec source:** [lro-helpers.tsp:L22-L119](https://github.com/Azure/azure-rest-api-specs/blob/a3b8933eb6ce030faa6abc6d354fc97e30f02e96/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/lro-helpers.tsp#L22-L119), [scenarioConfiguration.tsp:L48-L84](https://github.com/Azure/azure-rest-api-specs/blob/a3b8933eb6ce030faa6abc6d354fc97e30f02e96/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/scenarioConfiguration.tsp#L48-L84), [scenarioRun.tsp:L44-L52](https://github.com/Azure/azure-rest-api-specs/blob/a3b8933eb6ce030faa6abc6d354fc97e30f02e96/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/scenarioRun.tsp#L44-L52), [workspace.tsp:L69-L80](https://github.com/Azure/azure-rest-api-specs/blob/a3b8933eb6ce030faa6abc6d354fc97e30f02e96/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/workspace.tsp#L69-L80)
## 🛡️ Compatibility Assessment

### REST Breaking Changes

None detected.

### Downstream Breaking Changes

### Generated SDK methods change from synchronous to long-running

- **Severity:** high
- **Confidence:** high
- **Summary:** The wire contract is unchanged, but SDKs now expose poller/LRO return types and asynchronous completion semantics.
- **Evidence:** @useFinalStateVia is visible to both AutoRest and TCGC while @extension was OpenAPI-only.
- **TypeSpec source:** [lro-helpers.tsp:L22-L119](https://github.com/Azure/azure-rest-api-specs/blob/a3b8933eb6ce030faa6abc6d354fc97e30f02e96/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/lro-helpers.tsp#L22-L119), [scenarioConfiguration.tsp:L48-L84](https://github.com/Azure/azure-rest-api-specs/blob/a3b8933eb6ce030faa6abc6d354fc97e30f02e96/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/scenarioConfiguration.tsp#L48-L84), [scenarioRun.tsp:L44-L52](https://github.com/Azure/azure-rest-api-specs/blob/a3b8933eb6ce030faa6abc6d354fc97e30f02e96/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/scenarioRun.tsp#L44-L52), [workspace.tsp:L69-L80](https://github.com/Azure/azure-rest-api-specs/blob/a3b8933eb6ce030faa6abc6d354fc97e30f02e96/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/workspace.tsp#L69-L80)


## ☁️ Azure Compliance

**Status:** `not-assessed`

The ARM LRO page documents Location polling and header templates, but it does not document the @useFinalStateVia customization changed by this PR.

### Compliance Findings

No compliance mismatches found.

## 📎 Appendix

### Assessment Errors

None.

### Code-to-Guidance Evidence

| Result | Document section | Fetched guidance | Observed TypeSpec | Evidence |
| --- | --- | --- | --- | --- |
| Matched | [ARM long-running operations - Action operations](https://azure.github.io/typespec-azure/docs/howtos/arm/long-running-operations/) | Override the `LroHeaders` parameter. Set `FinalResult` to match the response type of the action. | Generated behavior remains Location-polled, but the changed decorator itself is not covered by this page. | [lro-helpers.tsp:L22-L119](https://github.com/Azure/azure-rest-api-specs/blob/a3b8933eb6ce030faa6abc6d354fc97e30f02e96/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/lro-helpers.tsp#L22-L119), [scenarioConfiguration.tsp:L48-L84](https://github.com/Azure/azure-rest-api-specs/blob/a3b8933eb6ce030faa6abc6d354fc97e30f02e96/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/scenarioConfiguration.tsp#L48-L84), [scenarioRun.tsp:L44-L52](https://github.com/Azure/azure-rest-api-specs/blob/a3b8933eb6ce030faa6abc6d354fc97e30f02e96/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/scenarioRun.tsp#L44-L52), +22 more |

### Tooling Used

- `@azure-tools/typespec-autorest`
- `@azure-tools/typespec-client-generator-core`

### Repository Validation

| Project | Tool | Status | Duration | Log |
| --- | --- | --- | ---: | --- |
| `specification/chaos/resource-manager/Microsoft.Chaos/Chaos` | `TypeSpecValidation` | succeeded | 32s | `validation-logs/specification__chaos__resource-manager__Microsoft.Chaos__Chaos-head.log` |

### Artifact Evidence

- **autorest:** base/head OpenAPI both retain x-ms-long-running-operation and final-state-via location
- **tcgc:** the head uses TypeSpec LRO metadata understood by TCGC instead of an OpenAPI-only extension

### Changed TypeSpec

- `specification/chaos/resource-manager/Microsoft.Chaos/Chaos/client.tsp`: [client.tsp:L12-L15](https://github.com/Azure/azure-rest-api-specs/blob/a3b8933eb6ce030faa6abc6d354fc97e30f02e96/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/client.tsp#L12-L15), [client.tsp:L59-L77](https://github.com/Azure/azure-rest-api-specs/blob/a3b8933eb6ce030faa6abc6d354fc97e30f02e96/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/client.tsp#L59-L77)
- `specification/chaos/resource-manager/Microsoft.Chaos/Chaos/lro-helpers.tsp`: [lro-helpers.tsp:L5-L5](https://github.com/Azure/azure-rest-api-specs/blob/232dfa71843fd574be49ba91a918ee7d3e7bfec3/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/lro-helpers.tsp#L5-L5), [lro-helpers.tsp:L11-L11](https://github.com/Azure/azure-rest-api-specs/blob/232dfa71843fd574be49ba91a918ee7d3e7bfec3/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/lro-helpers.tsp#L11-L11), [lro-helpers.tsp:L27-L27](https://github.com/Azure/azure-rest-api-specs/blob/232dfa71843fd574be49ba91a918ee7d3e7bfec3/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/lro-helpers.tsp#L27-L27), [lro-helpers.tsp:L28-L28](https://github.com/Azure/azure-rest-api-specs/blob/a3b8933eb6ce030faa6abc6d354fc97e30f02e96/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/lro-helpers.tsp#L28-L28), [lro-helpers.tsp:L70-L70](https://github.com/Azure/azure-rest-api-specs/blob/232dfa71843fd574be49ba91a918ee7d3e7bfec3/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/lro-helpers.tsp#L70-L70), [lro-helpers.tsp:L66-L66](https://github.com/Azure/azure-rest-api-specs/blob/a3b8933eb6ce030faa6abc6d354fc97e30f02e96/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/lro-helpers.tsp#L66-L66), [lro-helpers.tsp:L110-L110](https://github.com/Azure/azure-rest-api-specs/blob/232dfa71843fd574be49ba91a918ee7d3e7bfec3/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/lro-helpers.tsp#L110-L110), [lro-helpers.tsp:L101-L101](https://github.com/Azure/azure-rest-api-specs/blob/a3b8933eb6ce030faa6abc6d354fc97e30f02e96/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/lro-helpers.tsp#L101-L101)
- `specification/chaos/resource-manager/Microsoft.Chaos/Chaos/scenarioConfiguration.models.tsp`: [scenarioConfiguration.models.tsp:L226-L226](https://github.com/Azure/azure-rest-api-specs/blob/a3b8933eb6ce030faa6abc6d354fc97e30f02e96/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/scenarioConfiguration.models.tsp#L226-L226), [scenarioConfiguration.models.tsp:L278-L278](https://github.com/Azure/azure-rest-api-specs/blob/a3b8933eb6ce030faa6abc6d354fc97e30f02e96/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/scenarioConfiguration.models.tsp#L278-L278), [scenarioConfiguration.models.tsp:L421-L421](https://github.com/Azure/azure-rest-api-specs/blob/a3b8933eb6ce030faa6abc6d354fc97e30f02e96/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/scenarioConfiguration.models.tsp#L421-L421), [scenarioConfiguration.models.tsp:L457-L457](https://github.com/Azure/azure-rest-api-specs/blob/a3b8933eb6ce030faa6abc6d354fc97e30f02e96/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/scenarioConfiguration.models.tsp#L457-L457)
- `specification/chaos/resource-manager/Microsoft.Chaos/Chaos/scenarioConfiguration.tsp`: [scenarioConfiguration.tsp:L51-L52](https://github.com/Azure/azure-rest-api-specs/blob/232dfa71843fd574be49ba91a918ee7d3e7bfec3/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/scenarioConfiguration.tsp#L51-L52), [scenarioConfiguration.tsp:L62-L62](https://github.com/Azure/azure-rest-api-specs/blob/232dfa71843fd574be49ba91a918ee7d3e7bfec3/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/scenarioConfiguration.tsp#L62-L62), [scenarioConfiguration.tsp:L64-L64](https://github.com/Azure/azure-rest-api-specs/blob/232dfa71843fd574be49ba91a918ee7d3e7bfec3/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/scenarioConfiguration.tsp#L64-L64), [scenarioConfiguration.tsp:L74-L74](https://github.com/Azure/azure-rest-api-specs/blob/232dfa71843fd574be49ba91a918ee7d3e7bfec3/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/scenarioConfiguration.tsp#L74-L74), [scenarioConfiguration.tsp:L84-L84](https://github.com/Azure/azure-rest-api-specs/blob/232dfa71843fd574be49ba91a918ee7d3e7bfec3/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/scenarioConfiguration.tsp#L84-L84)
- `specification/chaos/resource-manager/Microsoft.Chaos/Chaos/scenarioRun.models.tsp`: [scenarioRun.models.tsp:L8-L8](https://github.com/Azure/azure-rest-api-specs/blob/a3b8933eb6ce030faa6abc6d354fc97e30f02e96/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/scenarioRun.models.tsp#L8-L8), [scenarioRun.models.tsp:L15-L42](https://github.com/Azure/azure-rest-api-specs/blob/a3b8933eb6ce030faa6abc6d354fc97e30f02e96/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/scenarioRun.models.tsp#L15-L42)
- `specification/chaos/resource-manager/Microsoft.Chaos/Chaos/scenarioRun.tsp`: [scenarioRun.tsp:L5-L5](https://github.com/Azure/azure-rest-api-specs/blob/232dfa71843fd574be49ba91a918ee7d3e7bfec3/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/scenarioRun.tsp#L5-L5), [scenarioRun.tsp:L14-L14](https://github.com/Azure/azure-rest-api-specs/blob/232dfa71843fd574be49ba91a918ee7d3e7bfec3/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/scenarioRun.tsp#L14-L14), [scenarioRun.tsp:L17-L17](https://github.com/Azure/azure-rest-api-specs/blob/a3b8933eb6ce030faa6abc6d354fc97e30f02e96/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/scenarioRun.tsp#L17-L17), [scenarioRun.tsp:L22-L27](https://github.com/Azure/azure-rest-api-specs/blob/a3b8933eb6ce030faa6abc6d354fc97e30f02e96/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/scenarioRun.tsp#L22-L27), [scenarioRun.tsp:L29-L34](https://github.com/Azure/azure-rest-api-specs/blob/a3b8933eb6ce030faa6abc6d354fc97e30f02e96/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/scenarioRun.tsp#L29-L34), [scenarioRun.tsp:L57-L58](https://github.com/Azure/azure-rest-api-specs/blob/232dfa71843fd574be49ba91a918ee7d3e7bfec3/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/scenarioRun.tsp#L57-L58), [scenarioRun.tsp:L60-L60](https://github.com/Azure/azure-rest-api-specs/blob/232dfa71843fd574be49ba91a918ee7d3e7bfec3/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/scenarioRun.tsp#L60-L60)
- `specification/chaos/resource-manager/Microsoft.Chaos/Chaos/workspace.tsp`: [workspace.tsp:L74-L74](https://github.com/Azure/azure-rest-api-specs/blob/232dfa71843fd574be49ba91a918ee7d3e7bfec3/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/workspace.tsp#L74-L74)
- `specification/chaos/resource-manager/Microsoft.Chaos/Chaos/workspaceEvaluation.models.tsp`: [workspaceEvaluation.models.tsp:L63-L63](https://github.com/Azure/azure-rest-api-specs/blob/a3b8933eb6ce030faa6abc6d354fc97e30f02e96/specification/chaos/resource-manager/Microsoft.Chaos/Chaos/workspaceEvaluation.models.tsp#L63-L63)
