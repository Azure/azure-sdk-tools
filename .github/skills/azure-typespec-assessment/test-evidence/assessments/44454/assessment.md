# 📋 TypeSpec Assessment

**PR:** [#44454 - Mitigate operation group casing breaking changes for azure-mgmt-elastic](https://github.com/Azure/azure-rest-api-specs/pull/44454)

**Overall confidence:** 🟢 high<br>
**Overall code safety:** 🟡 Medium

**Baseline:** `e8420e45fdaf12e2c72417d379c454f1fc8ce1e6`<br>
**Head:** `b63b8bfc79e274c916a91c5a2ff8b403cb00c3f2`; working-tree changes: false<br>
**Total assessment time:** 3m 39s

## 📌 Executive Summary

| Dimension | Result | Findings |
| --- | --- | ---: |
| Semantic understanding | ✅ Assessed — 1 intent(s), 3 operation(s) | n/a |
| REST compatibility | ✅ No breaks detected | 0 |
| Downstream compatibility | ✅ No breaks detected | 0 |
| Azure compliance | ❌ failed | 1 |

**Scope:** 1 intent(s), 3 affected operation(s), 1 project(s).<br>
**Changes:** 0 added, 3 modified, 0 removed.<br>
**Highest severity:** medium.

## 🎯 Action Required

| Severity | Area | Finding | Why it matters | Code | Guidance |
| --- | --- | --- | --- | --- | --- |
| medium | Compliance | @@clientLocation customizations are outside client.tsp | Expected: the Clients documentation says customizations should always be made in client.tsp alongside main.tsp. Actual: five @@clientLocation decorators, which the decorator reference defines as changing operation location in the client, are added in back-compatible.tsp. Gap: these client customizations use a nonstandard customization file instead of client.tsp. | [back-compatible.tsp:L108-L113](https://github.com/Azure/azure-rest-api-specs/blob/b63b8bfc79e274c916a91c5a2ff8b403cb00c3f2/specification/elastic/Elastic.Management/back-compatible.tsp#L108-L113), [back-compatible.tsp:L108-L113](https://github.com/Azure/azure-rest-api-specs/blob/b63b8bfc79e274c916a91c5a2ff8b403cb00c3f2/specification/elastic/Elastic.Management/back-compatible.tsp#L108-L113), +1 more | [The retained decorator reference confirms that @@clientLocation is a client customization used to change an operation's client location. The retained Clients guidance says customizations should always be made in client.tsp alongside main.tsp, but the changed source adds five @@clientLocation decorators in specification/elastic/Elastic.Management/back-compatible.tsp. This exact decorator-to-path comparison establishes a documented file-placement mismatch.](https://azure.github.io/typespec-azure/docs/howtos/generate-client-libraries/03client/) |

## ☁️ Azure Compliance

**Status:** `failed`

### Compliance Findings

<a id="finding-compliance-client-customization-file"></a>
### @@clientLocation customizations are outside client.tsp

**Severity:** medium

**Gap:** Expected: the Clients documentation says customizations should always be made in client.tsp alongside main.tsp. Actual: five @@clientLocation decorators, which the decorator reference defines as changing operation location in the client, are added in back-compatible.tsp. Gap: these client customizations use a nonstandard customization file instead of client.tsp.

**TypeSpec source:** [back-compatible.tsp:L108-L113](https://github.com/Azure/azure-rest-api-specs/blob/b63b8bfc79e274c916a91c5a2ff8b403cb00c3f2/specification/elastic/Elastic.Management/back-compatible.tsp#L108-L113), [back-compatible.tsp:L108-L113](https://github.com/Azure/azure-rest-api-specs/blob/b63b8bfc79e274c916a91c5a2ff8b403cb00c3f2/specification/elastic/Elastic.Management/back-compatible.tsp#L108-L113), [back-compatible.tsp:L94-L131](https://github.com/Azure/azure-rest-api-specs/blob/b63b8bfc79e274c916a91c5a2ff8b403cb00c3f2/specification/elastic/Elastic.Management/back-compatible.tsp#L94-L131)

<details>
<summary><strong>Expected</strong></summary>

The retained decorator reference confirms that @@clientLocation is a client customization used to change an operation's client location. The retained Clients guidance says customizations should always be made in client.tsp alongside main.tsp, but the changed source adds five @@clientLocation decorators in specification/elastic/Elastic.Management/back-compatible.tsp. This exact decorator-to-path comparison establishes a documented file-placement mismatch.

**Guidance:** [Clients — Retained authoritative evidence](https://azure.github.io/typespec-azure/docs/howtos/generate-client-libraries/03client/)

**Documented TypeSpec example**

```tsp
import "@azure-tools/typespec-client-generator-core";
using Azure.ClientGenerator.Core;

@@clientLocation(Feeds.feed, PetStore);
@@clientLocation(Pets.pet, PetStore);
```

</details>

<details>
<summary><strong>Actual</strong></summary>

Clients, Customizations: "Customizations SHOULD always be made in a file named `client.tsp` alongside `main.tsp`." @Azure.ClientGenerator.Core.clientLocation: "Change the operation location in the client. If the target client is not defined, use `string` to indicate a new client name." The changed source records five added @@clientLocation decorators in specification/elastic/Elastic.Management/back-compatible.tsp.

**[back-compatible.tsp:L108-L113](https://github.com/Azure/azure-rest-api-specs/blob/b63b8bfc79e274c916a91c5a2ff8b403cb00c3f2/specification/elastic/Elastic.Management/back-compatible.tsp#L108-L113)**

```tsp
  "!csharp,!java,!python"
);
@@clientLocation(
  ElasticMonitorResources.createAndAssociateIPFilterCreate,
  "CreateAndAssociateIPFilter",
  "python"
```

</details>

## 🧠 Semantic Understanding

<a id="intent-1-restore-released-python-pascalcase-operation-gro"></a>
### 1. Restore released Python PascalCase operation-group names for three operations

| Change | Aspect | Before | After |
| --- | --- | --- | --- |
| ✏️ Modified | Python operation-group location | The operations use shared camelCase client locations in generated Python. | Python uses the released PascalCase client locations while other languages retain their existing locations. |

**TypeSpec change:** Exclude Python from three shared @@clientLocation rules and add three Python-only PascalCase rules.

```diff
--- a/specification/elastic/Elastic.Management/back-compatible.tsp
+++ b/specification/elastic/Elastic.Management/back-compatible.tsp
@@ -94,13 +94,23 @@ using Microsoft.Elastic;
 @@clientLocation(
   ElasticMonitorResources.listAssociatedTrafficFiltersList,
   "listAssociatedTrafficFilters",
-  "!csharp"
+  "!csharp,!python"
+);
+@@clientLocation(
+  ElasticMonitorResources.listAssociatedTrafficFiltersList,
+  "ListAssociatedTrafficFilters",
+  "python"
 );
 @@clientName(ElasticMonitorResources.listAssociatedTrafficFiltersList, "list");
 ... 13 later diff lines omitted; full hunk is in assessment.json ...
```

```diff
--- a/specification/elastic/Elastic.Management/back-compatible.tsp
+++ b/specification/elastic/Elastic.Management/back-compatible.tsp
@@ -109,7 +119,12 @@ using Microsoft.Elastic;
 @@clientLocation(
   ElasticMonitorResources.createAndAssociatePLFilterCreate,
   "createAndAssociatePLFilter",
-  "!csharp"
+  "!csharp,!python"
+);
+@@clientLocation(
+  ElasticMonitorResources.createAndAssociatePLFilterCreate,
+  "CreateAndAssociatePLFilter",
+  "python"
 );
 @@clientName(
 ... 1 later diff lines omitted; full hunk is in assessment.json ...
```

**Impact:** [@@clientLocation customizations are outside client.tsp](#finding-compliance-client-customization-file)<br>
**Source:** [back-compatible.tsp:L108-L113](https://github.com/Azure/azure-rest-api-specs/blob/b63b8bfc79e274c916a91c5a2ff8b403cb00c3f2/specification/elastic/Elastic.Management/back-compatible.tsp#L108-L113), [back-compatible.tsp:L108-L113](https://github.com/Azure/azure-rest-api-specs/blob/b63b8bfc79e274c916a91c5a2ff8b403cb00c3f2/specification/elastic/Elastic.Management/back-compatible.tsp#L108-L113), [back-compatible.tsp:L94-L131](https://github.com/Azure/azure-rest-api-specs/blob/b63b8bfc79e274c916a91c5a2ff8b403cb00c3f2/specification/elastic/Elastic.Management/back-compatible.tsp#L94-L131)

Need the complete REST representation for every affected operation? Use this prompt:

`Using assessment.json for PR #44454, show the complete REST representation for every affected operation, including operation ID, method/path, parameters, request, responses, LRO, paging, and TypeSpec source.`

## 🛡️ Compatibility Assessment

### REST Breaking Changes

None detected.

### Downstream Breaking Changes

None detected.

## 📎 Appendix

### Assessment Errors

None.

### Code-to-Guidance Evidence

| Result | Document section | Fetched guidance | Observed TypeSpec | Evidence |
| --- | --- | --- | --- | --- |
| Matched | TypeSpec Client Generator Core decorators - Decorators | Change the operation location in the client. If the target client is not defined, use `string` to indicate a new client name. | The retained decorator reference confirms that @@clientLocation is a client customization used to change an operation's client location. The retained Clients guidance says customizations should always be made in client.tsp alongside main.tsp, but the changed source adds five @@clientLocation decorators in specification/elastic/Elastic.Management/back-compatible.tsp. This exact decorator-to-path comparison establishes a documented file-placement mismatch. | back-compatible.tsp:L108-L113, back-compatible.tsp:L108-L113, back-compatible.tsp:L94-L131 |
| Mismatch | Clients - Retained authoritative evidence | Customizations SHOULD always be made in a file named `client.tsp` alongside `main.tsp`. | Clients, Customizations: "Customizations SHOULD always be made in a file named `client.tsp` alongside `main.tsp`." @Azure.ClientGenerator.Core.clientLocation: "Change the operation location in the client. If the target client is not defined, use `string` to indicate a new client name." The changed source records five added @@clientLocation decorators in specification/elastic/Elastic.Management/back-compatible.tsp. | back-compatible.tsp:L108-L113, back-compatible.tsp:L108-L113, back-compatible.tsp:L94-L131 |

### Tooling Used

- `@azure-tools/typespec-autorest`
- `@azure-tools/typespec-client-generator-core`

### Artifact Evidence

- **autorest:** base/head succeeded with no wire artifact diff
- **tcgc:** base/head succeeded; generic output does not apply Python-only naming, so source decorators are primary evidence
