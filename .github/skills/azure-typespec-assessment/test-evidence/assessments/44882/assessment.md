# 📋 TypeSpec Assessment

**PR:** [#44882 - Add new stable version 2026-06-01 for New Relic](https://github.com/Azure/azure-rest-api-specs/pull/44882)

**Overall confidence:** 🟢 high<br>
**Overall code safety:** 🟡 Medium

**Baseline:** `9eb0993333857ef0ffb2863ccc76f8b123cae90b`<br>
**Head:** `22bc88578fb1f253688e8a5bf127ef3a4502745a`; working-tree changes: false<br>
**Total assessment time:** 4m 22s

## 📌 Executive Summary

| Dimension | Result | Findings |
| --- | --- | ---: |
| Semantic understanding | ✅ Assessed — 2 intent(s), 36 operation(s) | n/a |
| REST compatibility | ✅ No breaks detected | 0 |
| Downstream compatibility | ✅ No breaks detected | 0 |
| Azure compliance | ❌ failed | 1 |

**Scope:** 2 intent(s), 36 affected operation(s), 1 project(s).<br>
**Changes:** 35 added, 1 modified, 0 removed.<br>
**Highest severity:** medium.

## 🎯 Action Required

| Severity | Area | Finding | Why it matters | Code | Guidance |
| --- | --- | --- | --- | --- | --- |
| medium | Compliance | Replaced preview version remains in the version enum | The stable 2026-06-01 version is added without removing the replaced 2025-05-01-preview enum member, contrary to the documented stable-after-preview procedure. | [main.tsp:L38-L48](https://github.com/Azure/azure-rest-api-specs/blob/22bc88578fb1f253688e8a5bf127ef3a4502745a/specification/newrelic/NewRelicObservability.Management/main.tsp#L38-L48) | [The stable-after-preview guidance requires removing the replaced preview version from the version enum, but the changed Versions enum retains v2025_05_01_preview while adding v2026_06_01. The Go customization is compliant: @@override targets parameter-signature customization and the "go" scope limits that customization to the Go emitter, matching the client-generator-core decorator guidance.](https://azure.github.io/typespec-azure/docs/howtos/versioning/03-stable-after-preview/) |

## 🛡️ Compatibility Assessment

### REST Breaking Changes

None detected.

### Downstream Breaking Changes

None detected.

## ☁️ Azure Compliance

**Status:** `failed`

### Compliance Findings

<a id="finding-compliance-remove-replaced-preview-version"></a>
### Replaced preview version remains in the version enum

**Severity:** medium

**Gap:** The stable 2026-06-01 version is added without removing the replaced 2025-05-01-preview enum member, contrary to the documented stable-after-preview procedure.

**TypeSpec source:** [main.tsp:L38-L48](https://github.com/Azure/azure-rest-api-specs/blob/22bc88578fb1f253688e8a5bf127ef3a4502745a/specification/newrelic/NewRelicObservability.Management/main.tsp#L38-L48)

<details>
<summary><strong>Expected</strong></summary>

The stable-after-preview guidance requires removing the replaced preview version from the version enum, but the changed Versions enum retains v2025_05_01_preview while adding v2026_06_01. The Go customization is compliant: @@override targets parameter-signature customization and the "go" scope limits that customization to the Go emitter, matching the client-generator-core decorator guidance.

**Guidance:** [Adding a Stable Version when the Last Version was Preview — Retained authoritative evidence](https://azure.github.io/typespec-azure/docs/howtos/versioning/03-stable-after-preview/)

_The bounded official document evidence did not contain an example block._

</details>

<details>
<summary><strong>Actual</strong></summary>

Expected: "Remove the replaced preview version from the version enum." Actual: Versions contains both v2025_05_01_preview: "2025-05-01-preview" and v2026_06_01: "2026-06-01".

**[main.tsp:L38-L48](https://github.com/Azure/azure-rest-api-specs/blob/22bc88578fb1f253688e8a5bf127ef3a4502745a/specification/newrelic/NewRelicObservability.Management/main.tsp#L38-L48)**

```tsp
enum Versions {
  /**
   * The 2025-05-01-preview API version.
   */
  v2025_05_01_preview: "2025-05-01-preview",

  /**
   * The 2026-06-01 API version.
   */
  v2026_06_01: "2026-06-01",
}
```

</details>

## 🧠 Semantic Understanding

<a id="intent-1-preserve-the-released-go-delete-parameter-order"></a>
### 1. Preserve the released Go delete parameter order

| Change | Aspect | Before | After |
| --- | --- | --- | --- |
| ✏️ Modified | Go method parameter order | The default generated order places monitorName before userEmail. | The Go-only override preserves userEmail before monitorName. |

**TypeSpec change:** Add a Go-only @@override with the released delete parameter order.

```diff
--- a/specification/newrelic/NewRelicObservability.Management/client.tsp
+++ b/specification/newrelic/NewRelicObservability.Management/client.tsp
@@ -319,3 +321,34 @@ namespace Azure.ResourceManager.Models {
 @@usage(AccountProperties, Usage.input | Usage.output, "csharp");
 @@usage(OrganizationProperties, Usage.input | Usage.output, "csharp");
 @@usage(PlanDataProperties, Usage.input | Usage.output, "csharp");
+
+// ===== Go delete parameter reorder =====
+// Reorder the delete method parameters for the Go SDK to keep backward compatibility.
+/**
+ * Deletes an existing New Relic monitor resource from your Azure subscription, removing the integration and stopping the observability of your Azure resources through New Relic
+ */
+#suppress "@azure-tools/typespec-azure-resource-manager/arm-resource-operation" "Override to reorder parameters for the Go SDK"
+op newRelicMonitorResourceDelete(
+  ...ApiVersionParameter,
 ... 22 later diff lines omitted; full hunk is in assessment.json ...
```

**Source:** [client.tsp:L321-L354](https://github.com/Azure/azure-rest-api-specs/blob/22bc88578fb1f253688e8a5bf127ef3a4502745a/specification/newrelic/NewRelicObservability.Management/client.tsp#L321-L354)

<a id="intent-2-publish-the-inherited-new-relic-management-surfa"></a>
### 2. Publish the inherited New Relic management surface in stable 2026-06-01

| Change | Aspect | Before | After |
| --- | --- | --- | --- |
| ➕ Added | 2026-06-01 API-version availability | — | 35 existing operations are exposed in the new stable API version without a wire-behavior change. |

**TypeSpec change:** Add the stable Versions.v2026_06_01 member.

```diff
--- a/specification/newrelic/NewRelicObservability.Management/main.tsp
+++ b/specification/newrelic/NewRelicObservability.Management/main.tsp
@@ -40,6 +40,11 @@ enum Versions {
   /**
    * The 2025-05-01-preview API version.
    */
   v2025_05_01_preview: "2025-05-01-preview",
+
+  /**
+   * The 2026-06-01 API version.
+   */
+  v2026_06_01: "2026-06-01",
 }
```

**Impact:** [Replaced preview version remains in the version enum](#finding-compliance-remove-replaced-preview-version)<br>
**Source:** [main.tsp:L38-L48](https://github.com/Azure/azure-rest-api-specs/blob/22bc88578fb1f253688e8a5bf127ef3a4502745a/specification/newrelic/NewRelicObservability.Management/main.tsp#L38-L48)

Need the complete REST representation for every affected operation? Use this prompt:

`Using assessment.json for PR #44882, show the complete REST representation for every affected operation, including operation ID, method/path, parameters, request, responses, LRO, paging, and TypeSpec source.`

## 📎 Appendix

### Assessment Errors

None.

### Code-to-Guidance Evidence

| Result | Document section | Fetched guidance | Observed TypeSpec | Evidence |
| --- | --- | --- | --- | --- |
| Mismatch | Adding a Stable Version when the Last Version was Preview - Retained authoritative evidence | Remove the replaced preview version from the version enum. | Expected: "Remove the replaced preview version from the version enum." Actual: Versions contains both v2025_05_01_preview: "2025-05-01-preview" and v2026_06_01: "2026-06-01". | main.tsp:L38-L48 |
| Matched | TypeSpec Client Generator Core decorators - Retained authoritative evidence | Customize a method's signature in the generated client SDK. Currently, only parameter signature customization is supported. | The stable-after-preview guidance requires removing the replaced preview version from the version enum, but the changed Versions enum retains v2025_05_01_preview while adding v2026_06_01. The Go customization is compliant: @@override targets parameter-signature customization and the "go" scope limits that customization to the Go emitter, matching the client-generator-core decorator guidance. | main.tsp:L38-L48, client.tsp:L321-L354, main.tsp:L43-L47 |

### Tooling Used

- `@azure-tools/typespec-autorest`
- `@azure-tools/typespec-client-generator-core`

### Artifact Evidence

- **autorest:** Cached base/head compilation succeeded. Head adds the complete stable 2026-06-01 OpenAPI surface; comparison with the prior preview shows no incompatible wire-contract changes.
- **tcgc:** Cached base/head compilation succeeded. The generic client surface adds 2026-06-01 and changes every client API-version default from 2025-05-01-preview to 2026-06-01; the Go-only override is verified from changed TypeSpec source.
