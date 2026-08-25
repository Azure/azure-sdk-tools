# 📋 TypeSpec Assessment

**PR:** [#45536 - Preserve Java Consumption enum names](https://github.com/Azure/azure-rest-api-specs/pull/45536)

**Overall confidence:** 🟢 high<br>
**Overall code safety:** 🟡 Medium

**Baseline:** `b7170cade07f615426eb153b7035ecf8a1cab4e4`<br>
**Head:** `1395797d6112cb083837b3772083bafe0a91460c`; working-tree changes: false<br>
**Total assessment time:** 12m 48s

## 📌 Executive Summary

| Dimension | Result | Findings |
| --- | --- | ---: |
| Semantic understanding | ✅ Assessed — 1 intent(s), 5 operation(s) | n/a |
| REST compatibility | ✅ No breaks detected | 0 |
| Downstream compatibility | ✅ No breaks detected | 0 |
| Azure compliance | ⚠️ not-assessed | 0 |

**Scope:** 1 intent(s), 5 affected operation(s), 1 project(s).<br>
**Changes:** 0 added, 5 modified, 0 removed.<br>
**Highest severity:** none.

## 🎯 Action Required

No action required from the assessed dimensions.

## 🧠 Semantic Understanding

<a id="intent-1-preserve-released-java-names-for-six-enum-member"></a>
### 1. Preserve released Java names for six enum members

| Change | Aspect | Before | After |
| --- | --- | --- | --- |
| ✏️ Modified | Generated Java enum members | The affected members rely on newly derived Java names. | Explicit Java-only names preserve the six released enum constants. |

**TypeSpec change:** Add six Java-scoped @@clientName directives to existing members of Metrictype, Datagrain, and LookBackPeriod.

```diff
--- a/specification/consumption/resource-manager/Microsoft.Consumption/Consumption/client.tsp
+++ b/specification/consumption/resource-manager/Microsoft.Consumption/Consumption/client.tsp
@@ -532,3 +532,11 @@ model ConsumptionModernReservationRecommendation
   ModernChargeSummary.properties,
   "csharp"
 );
+
+// Preserve Java enum value names from the released SDK.
+@@clientName(Metrictype.ActualCostMetricType, "ACTUALCOST", "java");
+@@clientName(Metrictype.AmortizedCostMetricType, "AMORTIZEDCOST", "java");
+@@clientName(Metrictype.UsageMetricType, "USAGE", "java");
+@@clientName(Datagrain.DailyGrain, "DAILY", "java");
+@@clientName(Datagrain.MonthlyGrain, "MONTHLY", "java");
+@@clientName(LookBackPeriod.Last07Days, "LAST7DAYS", "java");
```

**Source:** [client.tsp:L535-L542](https://github.com/Azure/azure-rest-api-specs/blob/1395797d6112cb083837b3772083bafe0a91460c/specification/consumption/resource-manager/Microsoft.Consumption/Consumption/client.tsp#L535-L542), [client.tsp:L535-L542](https://github.com/Azure/azure-rest-api-specs/blob/1395797d6112cb083837b3772083bafe0a91460c/specification/consumption/resource-manager/Microsoft.Consumption/Consumption/client.tsp#L535-L542)

Need the complete REST representation for every affected operation? Use this prompt:

`Using assessment.json for PR #45536, show the complete REST representation for every affected operation, including operation ID, method/path, parameters, request, responses, LRO, paging, and TypeSpec source.`

## 🛡️ Compatibility Assessment

### REST Breaking Changes

None detected.

### Downstream Breaking Changes

None detected.

## ☁️ Azure Compliance

**Status:** `not-assessed`

The fetched official enum documentation describes enum declarations and the Azure.Core no-enum rule, but the changed source adds only Java-specific @@clientName directives to existing enum members. The fetched material contains no directly applicable guidance for that decorator or for language-specific enum-member naming, so it cannot support a passed or failed compliance judgment.

### Compliance Findings

No compliance mismatches found.

## 📎 Appendix

### Assessment Errors

None.

### Code-to-Guidance Evidence

| Result | Document section | Fetched guidance | Observed TypeSpec | Evidence |
| --- | --- | --- | --- | --- |
| Matched | [Enums - Models and Enums](https://typespec.io/docs/language-basics/enums/) | Enums \| TypeSpec Skip to content TypeSpec Use cases OpenAPI Data Validation Tooling support Docs Videos Playground Blog Community Version 1.15.0 is now available! Search... OpenAPI Data Validation Tooling support Docs Videos Playground Blog Community Getting started Installation Editor VS Code Extension Visual Studio Extension Guides TypeSpec for REST Getting Started with TypeSpec For REST APIs Operations and Respons | The fetched official enum documentation describes enum declarations and the Azure.Core no-enum rule, but the changed source adds only Java-specific @@clientName directives to existing enum members. The fetched material contains no directly applicable guidance for that decorator or for language-specific enum-member naming, so it cannot support a passed or failed compliance judgment. | [client.tsp:L535-L542](https://github.com/Azure/azure-rest-api-specs/blob/1395797d6112cb083837b3772083bafe0a91460c/specification/consumption/resource-manager/Microsoft.Consumption/Consumption/client.tsp#L535-L542), [client.tsp:L535-L542](https://github.com/Azure/azure-rest-api-specs/blob/1395797d6112cb083837b3772083bafe0a91460c/specification/consumption/resource-manager/Microsoft.Consumption/Consumption/client.tsp#L535-L542) |
| Matched | [Azure.Core no-enum rule - Models and Enums](https://azure.github.io/typespec-azure/docs/libraries/azure-core/rules/no-enum/) | no-enum \| TypeSpec Azure Skip to content TypeSpec Azure Docs Playground TypeSpec Core Docs Can I Use (Azure Client) Benchmarks Search... Introduction Get started Installation Creating a project Versioning Azure Data Plane Service 1. Writing Your First Service 2. Create the service namespace 3. Defining your first resource 4. Defining standard resource operations 5. Defining long-running resource operations 6. Defining c | The fetched official enum documentation describes enum declarations and the Azure.Core no-enum rule, but the changed source adds only Java-specific @@clientName directives to existing enum members. The fetched material contains no directly applicable guidance for that decorator or for language-specific enum-member naming, so it cannot support a passed or failed compliance judgment. | [client.tsp:L535-L542](https://github.com/Azure/azure-rest-api-specs/blob/1395797d6112cb083837b3772083bafe0a91460c/specification/consumption/resource-manager/Microsoft.Consumption/Consumption/client.tsp#L535-L542), [client.tsp:L535-L542](https://github.com/Azure/azure-rest-api-specs/blob/1395797d6112cb083837b3772083bafe0a91460c/specification/consumption/resource-manager/Microsoft.Consumption/Consumption/client.tsp#L535-L542) |
| Matched | [Models - Models and Enums](https://typespec.io/docs/language-basics/models/) | figuration Tracing FAQ 📐 Language Basics Overview Built-in types Identifiers Imports Namespaces Decorators Directives Documentation Scalars Models Operations Interfaces Templates Enums Functions Unions Intersections Type Literals Aliases Values Type Relations Access Modifiers Visibility 📘 Standard Library Built-in Decorators Built-in Data types Js api Classes [C] UnserializableValueError [C] UnsupportedScalarConstructorError Enumerations [E] IdentifierKind [E] ListenerFlow [E] ModifierFlags [E | The fetched official enum documentation describes enum declarations and the Azure.Core no-enum rule, but the changed source adds only Java-specific @@clientName directives to existing enum members. The fetched material contains no directly applicable guidance for that decorator or for language-specific enum-member naming, so it cannot support a passed or failed compliance judgment. | [client.tsp:L535-L542](https://github.com/Azure/azure-rest-api-specs/blob/1395797d6112cb083837b3772083bafe0a91460c/specification/consumption/resource-manager/Microsoft.Consumption/Consumption/client.tsp#L535-L542), [client.tsp:L535-L542](https://github.com/Azure/azure-rest-api-specs/blob/1395797d6112cb083837b3772083bafe0a91460c/specification/consumption/resource-manager/Microsoft.Consumption/Consumption/client.tsp#L535-L542) |
| Matched | [Scalars - Models and Enums](https://typespec.io/docs/language-basics/scalars/) | figuration Tracing FAQ 📐 Language Basics Overview Built-in types Identifiers Imports Namespaces Decorators Directives Documentation Scalars Models Operations Interfaces Templates Enums Functions Unions Intersections Type Literals Aliases Values Type Relations Access Modifiers Visibility 📘 Standard Library Built-in Decorators Built-in Data types Js api Classes [C] UnserializableValueError [C] UnsupportedScalarConstructorError Enumerations [E] IdentifierKind [E] ListenerFlow [E] ModifierFlags [E | The fetched official enum documentation describes enum declarations and the Azure.Core no-enum rule, but the changed source adds only Java-specific @@clientName directives to existing enum members. The fetched material contains no directly applicable guidance for that decorator or for language-specific enum-member naming, so it cannot support a passed or failed compliance judgment. | [client.tsp:L535-L542](https://github.com/Azure/azure-rest-api-specs/blob/1395797d6112cb083837b3772083bafe0a91460c/specification/consumption/resource-manager/Microsoft.Consumption/Consumption/client.tsp#L535-L542), [client.tsp:L535-L542](https://github.com/Azure/azure-rest-api-specs/blob/1395797d6112cb083837b3772083bafe0a91460c/specification/consumption/resource-manager/Microsoft.Consumption/Consumption/client.tsp#L535-L542) |

### Tooling Used

- `@azure-tools/typespec-autorest`
- `@azure-tools/typespec-client-generator-core`

### Artifact Evidence

- **autorest:** Preserved base/head AutoRest outputs have no diff for either 2024-08-01 or 2026-06-01.
- **tcgc:** The preserved generic TCGC diff changes only crossLanguageVersion; Java-scoped names are not represented by this generic output.
- **compilation:** No compilation was run during this reassessment; preserved successful base/head artifacts were used.
- **canonicalComparison:** Materially consistent across semantic intent, REST findings, downstream findings, compliance, and overall conclusion. The fresh report corrects the canonical affected-operation inventory from three to five and records validation as skipped per the preserved no-validation evidence.
