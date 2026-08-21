# 📋 TypeSpec Assessment

**PR:** [#44882 - Add new stable version 2026-06-01 for New Relic](https://github.com/Azure/azure-rest-api-specs/pull/44882)

**Overall confidence:** 🟡 medium<br>
**Overall code safety:** 🟡 Medium

**Baseline:** `9eb0993333857ef0ffb2863ccc76f8b123cae90b`<br>
**Head:** `22bc88578fb1f253688e8a5bf127ef3a4502745a`; working-tree changes: false<br>
**Total assessment time:** 15m 39s

## 📌 Executive Summary

| Dimension | Result | Findings |
| --- | --- | ---: |
| Semantic understanding | ✅ Assessed — 1 intent(s), 1 operation(s) | n/a |
| REST compatibility | ✅ No breaks detected | 0 |
| Downstream compatibility | ✅ No breaks detected | 0 |
| Azure compliance | ❌ failed | 1 |

**Scope:** 1 intent(s), 1 affected operation(s), 1 project(s).<br>
**Highest severity:** medium.

## 🎯 Action Required

| Severity | Area | Finding | Why it matters | Code | Guidance |
| --- | --- | --- | --- | --- | --- |
| medium | Compliance | The stable version retains the replaced preview version | The enum keeps v2025_05_01_preview beside v2026_06_01 instead of following the stable-after-preview replacement procedure. | [main.tsp:L43-L47](https://github.com/Azure/azure-rest-api-specs/blob/22bc88578fb1f253688e8a5bf127ef3a4502745a/specification/newrelic/NewRelicObservability.Management/main.tsp#L43-L47) | [The stable version replaces the prior preview member.](https://azure.github.io/typespec-azure/docs/howtos/versioning/03-stable-after-preview/) |

## 🧠 Semantic Understanding

### Change Overview

| # | Intent | Operations | API versions | Details |
| ---: | --- | ---: | --- | --- |
| 1 | Add a stable API version while preserving the released Go delete parameter order. | 1 | 2026-06-01 | [details](#intent-1-add-a-stable-api-version-while-preserving-the-re) |

### Operation Details

<a id="intent-1-add-a-stable-api-version-while-preserving-the-re"></a>
### 1. Add a stable API version while preserving the released Go delete parameter order.

**Confidence:** high<br>
**REST summary:** The delete path and userEmail query parameter remain unchanged.

#### `NewRelicMonitors_Delete`

- **HTTP path:** `DELETE /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/NewRelic.Observability/monitors/{monitorName}`
- **API versions:** `2026-06-01`
- **Parameters:** path subscriptionId, resourceGroupName, monitorName: string, required; query userEmail: string, required; query api-version: string, required
- **Request payload:** none
- **Response payloads:** 200 or 204: no body; 202: async headers; default: ErrorResponse
- **Service behavior:** Deletes the New Relic monitor; the Go override preserves historical parameter order.
- **LRO:** arm; via location; Poll the emitted async endpoint after Retry-After until a terminal state.; final result: Use the final response contract described for this operation.
- **Paging:** No.
- **TypeSpec source:** [client.tsp:L321-L354](https://github.com/Azure/azure-rest-api-specs/blob/22bc88578fb1f253688e8a5bf127ef3a4502745a/specification/newrelic/NewRelicObservability.Management/client.tsp#L321-L354)
## 🛡️ Compatibility Assessment

### REST Breaking Changes

None detected.

### Downstream Breaking Changes

None detected.

## ☁️ Azure Compliance

**Status:** `failed`

### Compliance Findings

### The stable version retains the replaced preview version

- **Severity:** medium
- **Summary:** The enum keeps v2025_05_01_preview beside v2026_06_01 instead of following the stable-after-preview replacement procedure.
- **Evidence:** The Go-specific override follows its documentation and is not part of this finding.
- **TypeSpec source:** [main.tsp:L43-L47](https://github.com/Azure/azure-rest-api-specs/blob/22bc88578fb1f253688e8a5bf127ef3a4502745a/specification/newrelic/NewRelicObservability.Management/main.tsp#L43-L47)
- **Guidance:** https://azure.github.io/typespec-azure/docs/howtos/versioning/03-stable-after-preview/

## 📎 Appendix

### Assessment Errors

None.

### Code-to-Guidance Evidence

| Result | Document section | Fetched guidance | Observed TypeSpec | Evidence |
| --- | --- | --- | --- | --- |
| Mismatch | [Adding a Stable Version when the Last Version was Preview - Making Changes to your TypeSpec spec](https://azure.github.io/typespec-azure/docs/howtos/versioning/03-stable-after-preview/) | Remove the replaced preview version from the version enum. | The enum retains v2025_05_01_preview and appends v2026_06_01. | [main.tsp:L43-L47](https://github.com/Azure/azure-rest-api-specs/blob/22bc88578fb1f253688e8a5bf127ef3a4502745a/specification/newrelic/NewRelicObservability.Management/main.tsp#L43-L47) |
| Matched | [TypeSpec Client Generator Core decorators - @Azure.ClientGenerator.Core.override](https://azure.github.io/typespec-azure/docs/libraries/typespec-client-generator-core/reference/decorators/) | Customize a method's signature in the generated client SDK. Currently, only parameter signature customization is supported. | The Go-only @@override is in client.tsp and reorders the delete operation parameters. | [client.tsp:L321-L354](https://github.com/Azure/azure-rest-api-specs/blob/22bc88578fb1f253688e8a5bf127ef3a4502745a/specification/newrelic/NewRelicObservability.Management/client.tsp#L321-L354), [client.tsp:L7-L7](https://github.com/Azure/azure-rest-api-specs/blob/22bc88578fb1f253688e8a5bf127ef3a4502745a/specification/newrelic/NewRelicObservability.Management/client.tsp#L7-L7), [client.tsp:L9-L9](https://github.com/Azure/azure-rest-api-specs/blob/22bc88578fb1f253688e8a5bf127ef3a4502745a/specification/newrelic/NewRelicObservability.Management/client.tsp#L9-L9), +1 more |

### Tooling Used

- `@azure-tools/typespec-autorest`
- `@azure-tools/typespec-client-generator-core`

### Repository Validation

| Project | Tool | Status | Duration | Log |
| --- | --- | --- | ---: | --- |
| `specification/newrelic/NewRelicObservability.Management` | `TypeSpecValidation` | skipped | 0s | `unknown` |

### Artifact Evidence

- **autorest:** base/head succeeded; new stable OpenAPI includes the same path/query serialization
- **tcgc:** base/head succeeded; generic output generated, while Go-only order is confirmed from source

### Changed TypeSpec

- `specification/newrelic/NewRelicObservability.Management/client.tsp`: [client.tsp:L7-L7](https://github.com/Azure/azure-rest-api-specs/blob/22bc88578fb1f253688e8a5bf127ef3a4502745a/specification/newrelic/NewRelicObservability.Management/client.tsp#L7-L7), [client.tsp:L9-L9](https://github.com/Azure/azure-rest-api-specs/blob/22bc88578fb1f253688e8a5bf127ef3a4502745a/specification/newrelic/NewRelicObservability.Management/client.tsp#L9-L9), [client.tsp:L324-L354](https://github.com/Azure/azure-rest-api-specs/blob/22bc88578fb1f253688e8a5bf127ef3a4502745a/specification/newrelic/NewRelicObservability.Management/client.tsp#L324-L354)
- `specification/newrelic/NewRelicObservability.Management/main.tsp`: [main.tsp:L43-L47](https://github.com/Azure/azure-rest-api-specs/blob/22bc88578fb1f253688e8a5bf127ef3a4502745a/specification/newrelic/NewRelicObservability.Management/main.tsp#L43-L47)
