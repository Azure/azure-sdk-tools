# 📋 TypeSpec Assessment

**PR:** [#45536 - Preserve Java Consumption enum names](https://github.com/Azure/azure-rest-api-specs/pull/45536)

**Overall confidence:** 🟢 high<br>
**Overall code safety:** 🟡 Medium

**Baseline:** `b7170cade07f615426eb153b7035ecf8a1cab4e4`<br>
**Head:** `1395797d6112cb083837b3772083bafe0a91460c`; working-tree changes: false<br>
**Assessment time:** 2m 34s

## 📌 Executive Summary

| Dimension | Result | Findings |
| --- | --- | ---: |
| Semantic understanding | ✅ Assessed — 1 intent(s), 3 operation(s) | n/a |
| REST compatibility | ✅ No breaks detected | 0 |
| Downstream compatibility | ✅ No breaks detected | 0 |
| Azure compliance | ⚠️ not-assessed | 0 |

**Scope:** 1 intent(s), 3 affected operation(s), 1 project(s).<br>
**Highest severity:** none.

## 🎯 Action Required

No action required from the assessed dimensions.

## 🧠 Semantic Understanding

### Change Overview

| # | Intent | Operations | Shape | API versions | Linked findings | Details |
| ---: | --- | ---: | --- | --- | --- | --- |
| 1 | Preserve released Java enum constants while retaining existing serialized values. | 3 | GET · 0 LRO · 2 paged | 2024-08-01, 2026-06-01 | No linked finding | [details](#intent-1-preserve-released-java-enum-constants-while-reta) |

### Operation Details

<a id="intent-1-preserve-released-java-enum-constants-while-reta"></a>
### 1. Preserve released Java enum constants while retaining existing serialized values.

**Confidence:** high<br>
**REST summary:** Serialized enum values and OpenAPI schemas remain unchanged.

#### `ReservationRecommendationDetails_Get`

- **HTTP path:** `GET /{resourceScope}/providers/Microsoft.Consumption/reservationRecommendationDetails`
- **API versions:** `2024-08-01`, `2026-06-01`
- **Parameters:** path resourceScope: string, required; query scope, region, term, lookBackPeriod, product, api-version: required
- **Request payload:** none
- **Response payloads:** 200: ReservationRecommendationDetails; default: ErrorResponse
- **Service behavior:** Returns reservation recommendation details using the selected look-back period.
- **LRO:** No.
- **Paging:** No.
- **TypeSpec source:** [client.tsp:L532-L543](https://github.com/Azure/azure-rest-api-specs/blob/1395797d6112cb083837b3772083bafe0a91460c/specification/consumption/resource-manager/Microsoft.Consumption/Consumption/client.tsp#L532-L543)

#### `ReservationsSummaries_List`

- **HTTP path:** `GET /{resourceScope}/providers/Microsoft.Consumption/reservationSummaries`
- **API versions:** `2024-08-01`, `2026-06-01`
- **Parameters:** path resourceScope: string, required; query grain: Datagrain, required; query filter and api-version
- **Request payload:** none
- **Response payloads:** 200: ReservationSummariesListResult; default: ErrorResponse
- **Service behavior:** Lists daily or monthly reservation summaries.
- **LRO:** No.
- **Paging:** ReservationSummary; nextLink; GET the opaque nextLink until it is absent.
- **TypeSpec source:** [client.tsp:L532-L543](https://github.com/Azure/azure-rest-api-specs/blob/1395797d6112cb083837b3772083bafe0a91460c/specification/consumption/resource-manager/Microsoft.Consumption/Consumption/client.tsp#L532-L543)

#### `UsageDetails_List`

- **HTTP path:** `GET /{scope}/providers/Microsoft.Consumption/usageDetails`
- **API versions:** `2024-08-01`, `2026-06-01`
- **Parameters:** path scope: string, required; query metric: Metrictype, optional; query $expand, $filter, $skiptoken, $top, api-version: optional/required as emitted
- **Request payload:** none
- **Response payloads:** 200: UsageDetailsListResult; default: ErrorResponse
- **Service behavior:** Lists cost or usage records using the selected metric.
- **LRO:** No.
- **Paging:** UsageDetail; nextLink; GET the opaque nextLink, which carries $skiptoken, until absent.
- **TypeSpec source:** [client.tsp:L532-L543](https://github.com/Azure/azure-rest-api-specs/blob/1395797d6112cb083837b3772083bafe0a91460c/specification/consumption/resource-manager/Microsoft.Consumption/Consumption/client.tsp#L532-L543)
## 🛡️ Compatibility Assessment

### REST Breaking Changes

None detected.

### Downstream Breaking Changes

None detected.

## ☁️ Azure Compliance

**Status:** `not-assessed`

The enum wire values follow the fetched enum syntax guidance, but the shared catalog has no authoritative document for Java-scoped @@clientName compatibility aliases.

### Compliance Findings

No compliance mismatches found.

## 📎 Appendix

### Assessment Errors

None.

### Code-to-Guidance Evidence

| Result | Document section | Fetched guidance | Observed TypeSpec | Evidence |
| --- | --- | --- | --- | --- |
| Matched | [Enums - Custom enum values](https://typespec.io/docs/language-basics/enums/) | You can assign custom values to enum members using the `:` operator. | The six existing serialized enum values remain unchanged. | [models.tsp:L31-L50](https://github.com/Azure/azure-rest-api-specs/blob/1395797d6112cb083837b3772083bafe0a91460c/specification/consumption/resource-manager/Microsoft.Consumption/Consumption/models.tsp#L31-L50), [models.tsp:L211-L224](https://github.com/Azure/azure-rest-api-specs/blob/1395797d6112cb083837b3772083bafe0a91460c/specification/consumption/resource-manager/Microsoft.Consumption/Consumption/models.tsp#L211-L224), [models.tsp:L272-L290](https://github.com/Azure/azure-rest-api-specs/blob/1395797d6112cb083837b3772083bafe0a91460c/specification/consumption/resource-manager/Microsoft.Consumption/Consumption/models.tsp#L272-L290) |

### Timing

- **Toolchain setup:** 1m 6s
- **Preparation:** 1m 28s
- **Documentation assessment:** 0s
- **Total attributed time:** 2m 34s


### Tooling Used

- `@azure-tools/typespec-autorest`
- `@azure-tools/typespec-client-generator-core`

### Repository Validation

| Project | Tool | Status | Duration | Log |
| --- | --- | --- | ---: | --- |
| `specification/consumption/resource-manager/Microsoft.Consumption/Consumption` | `TypeSpecValidation` | succeeded | 31s | `validation-logs/specification__consumption__resource-manager__Microsoft.Consumption__Consumption-head.log` |

### Artifact Evidence

- **autorest:** base/head succeeded with no wire artifact diff
- **tcgc:** base/head succeeded; generic output does not apply Java-only names, so source decorators are primary evidence

### Changed TypeSpec

- `specification/consumption/resource-manager/Microsoft.Consumption/Consumption/client.tsp`: [client.tsp:L535-L542](specification/consumption/resource-manager/Microsoft.Consumption/Consumption/client.tsp#L535-L542)
