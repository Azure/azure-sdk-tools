# 📋 TypeSpec Assessment

**PR:** [#44742 - Removed NFSv2 from STG103/104 TypeSpec & Swagger](https://github.com/Azure/azure-rest-api-specs/pull/44742)

**Overall confidence:** 🟢 high<br>
**Overall code safety:** 🔴 Low

**Baseline:** `af067c9fc987b837f27a34b86e3ac591507711c9 (e75a07cd7ea1b2207190a362305db02b639715ec)`<br>
**Head:** `656ca8b0741973760f8a60ee0fd293ba9c31e708`; working-tree changes: false<br>
**Total assessment time:** 3m 47s

## 📌 Executive Summary

| Dimension | Result | Findings |
| --- | --- | ---: |
| Semantic understanding | ✅ Assessed — 2 intent(s), 4 operation(s) | n/a |
| REST compatibility | ❌ Issues found | 3 |
| Downstream compatibility | ❌ Issues found | 1 |
| Azure compliance | ✅ passed | 0 |

**Scope:** 2 intent(s), 4 affected operation(s), 1 project(s).<br>
**Changes:** 0 added, 2 modified, 0 removed.<br>
**Highest severity:** high.

## 🎯 Action Required

| Severity | Area | Finding | Why it matters | Code | Guidance |
| --- | --- | --- | --- | --- | --- |
| high | Downstream | REST contract changes require generated-client updates | The approved REST contract changes also alter generated client request or response handling and can break callers compiled against the previous contract. | [models.tsp:L405-L426](https://github.com/Azure/azure-rest-api-specs/blob/e75a07cd7ea1b2207190a362305db02b639715ec/specification/storage/data-plane/FileStorage/models.tsp#L405-L426), [models.tsp:L557-L592](https://github.com/Azure/azure-rest-api-specs/blob/e75a07cd7ea1b2207190a362305db02b639715ec/specification/storage/data-plane/FileStorage/models.tsp#L557-L592), +2 more | n/a |
| high | REST | Operation parameter contract changes | The parameter remains an optional array of strings, but the exact source removes accepted include enum values for the 2026-12-06 special-file entries. Requests using those values are no longer described as valid, so the existing query contract narrows. | [models.tsp:L405-L426](https://github.com/Azure/azure-rest-api-specs/blob/e75a07cd7ea1b2207190a362305db02b639715ec/specification/storage/data-plane/FileStorage/models.tsp#L405-L426), [models.tsp:L557-L592](https://github.com/Azure/azure-rest-api-specs/blob/e75a07cd7ea1b2207190a362305db02b639715ec/specification/storage/data-plane/FileStorage/models.tsp#L557-L592), +1 more | n/a |
| high | REST | Operation parameter contract changes | The header remains an optional string, but the exact source removes the explicitly serialized Fifo member from NfsFileType. Existing 2026-12-06 requests using Fifo for x-ms-file-file-type are no longer valid, so the accepted wire-value set narrows. | [models.tsp:L284-L305](https://github.com/Azure/azure-rest-api-specs/blob/e75a07cd7ea1b2207190a362305db02b639715ec/specification/storage/data-plane/FileStorage/models.tsp#L284-L305) | n/a |
| high | REST | Operation response contract changes | The 200 response keeps its top-level schema name, but the exact source removes the XML-serialized SymLink, BlockDevice, CharDevice, Fifo, and Socket collections and their item models. Removing response properties and schemas from the existing 2026-12-06 contract is breaking. | [models.tsp:L405-L426](https://github.com/Azure/azure-rest-api-specs/blob/e75a07cd7ea1b2207190a362305db02b639715ec/specification/storage/data-plane/FileStorage/models.tsp#L405-L426), [models.tsp:L557-L592](https://github.com/Azure/azure-rest-api-specs/blob/e75a07cd7ea1b2207190a362305db02b639715ec/specification/storage/data-plane/FileStorage/models.tsp#L557-L592), +1 more | n/a |

## ☁️ Azure Compliance

**Status:** `passed`

### Compliance Findings

No compliance mismatches found.

## 🧠 Semantic Understanding

<a id="intent-1-remove-2026-12-06-special-file-listing-options-a"></a>
### 1. Remove 2026-12-06 special-file listing options and response shapes

| Change | Aspect | Before | After |
| --- | --- | --- | --- |
| ✏️ Modified | include accepted values | included the removed 2026-12-06 special-file values | special-file values removed |
| ✏️ Modified | 200 response special-file collections | SymLink, BlockDevice, CharDevice, Fifo, and Socket collections with item models | collections and item models removed |

**TypeSpec change:** Directory_ListFilesAndDirectoriesSegment no longer accepts the removed special-file include values and its 200 response no longer exposes the SymLink, BlockDevice, CharDevice, Fifo, or Socket collections or their item models. This narrows both the existing 2026-12-06 query and response contracts.

```diff
--- a/specification/storage/data-plane/FileStorage/models.tsp
+++ b/specification/storage/data-plane/FileStorage/models.tsp
@@ -405,22 +389,6 @@ enum ListFilesIncludeType {

   /** PermissionKey */
   PermissionKey,
-
-  /** Permissions */
-  @added(Versions.v2026_12_06)
-  Permissions,
-
-  /** LinkCount */
-  @added(Versions.v2026_12_06)
-  LinkCount,
-
 ... 10 later diff lines omitted; full hunk is in assessment.json ...
```

```diff
--- a/specification/storage/data-plane/FileStorage/models.tsp
+++ b/specification/storage/data-plane/FileStorage/models.tsp
@@ -557,36 +525,6 @@ model FilesAndDirectoriesListSegment {
 ... 5 earlier diff lines omitted; full hunk is in assessment.json ...
-  @added(Versions.v2026_12_06)
-  @Xml.unwrapped
-  @Xml.name("SymLink")
-  symLinkItems?: SymLinkItem[];
-
-  /** The block device items. */
-  @added(Versions.v2026_12_06)
-  @Xml.unwrapped
-  @Xml.name("BlockDevice")
-  blockDeviceItems?: BlockDeviceItem[];
-
-  /** The character device items. */
 ... 19 later diff lines omitted; full hunk is in assessment.json ...
```

1 additional TypeSpec hunk omitted; complete diffs are in `assessment.json`.

**Impact:** [Operation parameter contract changes](#finding-rest-1-parameter-contract-changed-2026-12-06-get-restype-directory-comp-list), [Operation response contract changes](#finding-rest-2-response-contract-changed-2026-12-06-get-restype-directory-comp-list), [REST contract changes require generated-client updates](#finding-derived-rest-contract-sdk-impact)<br>
**Source:** [models.tsp:L405-L426](https://github.com/Azure/azure-rest-api-specs/blob/e75a07cd7ea1b2207190a362305db02b639715ec/specification/storage/data-plane/FileStorage/models.tsp#L405-L426), [models.tsp:L557-L592](https://github.com/Azure/azure-rest-api-specs/blob/e75a07cd7ea1b2207190a362305db02b639715ec/specification/storage/data-plane/FileStorage/models.tsp#L557-L592), [models.tsp:L691-L806](https://github.com/Azure/azure-rest-api-specs/blob/e75a07cd7ea1b2207190a362305db02b639715ec/specification/storage/data-plane/FileStorage/models.tsp#L691-L806)

<a id="intent-2-remove-fifo-from-the-2026-12-06-file-creation-ty"></a>
### 2. Remove Fifo from the 2026-12-06 file creation type

| Change | Aspect | Before | After |
| --- | --- | --- | --- |
| ✏️ Modified | x-ms-file-file-type accepted values | Fifo accepted | Fifo removed |

**TypeSpec change:** File_Create no longer accepts the Fifo wire value through the optional x-ms-file-file-type header because that member is removed from NfsFileType. This narrows the existing 2026-12-06 request contract.

```diff
--- a/specification/storage/data-plane/FileStorage/models.tsp
+++ b/specification/storage/data-plane/FileStorage/models.tsp
@@ -284,22 +284,6 @@ union NfsFileType {

   /** SymLink */
   SymLink: "SymLink",
-
-  /** BlockDevice */
-  @added(Versions.v2026_12_06)
-  BlockDevice: "BlockDevice",
-
-  /** CharacterDevice */
-  @added(Versions.v2026_12_06)
-  CharacterDevice: "CharacterDevice",
-
 ... 10 later diff lines omitted; full hunk is in assessment.json ...
```

**Impact:** [Operation parameter contract changes](#finding-rest-3-parameter-contract-changed-2026-12-06-put), [REST contract changes require generated-client updates](#finding-derived-rest-contract-sdk-impact)<br>
**Source:** [models.tsp:L284-L305](https://github.com/Azure/azure-rest-api-specs/blob/e75a07cd7ea1b2207190a362305db02b639715ec/specification/storage/data-plane/FileStorage/models.tsp#L284-L305)

Need the complete REST representation for every affected operation? Use this prompt:

`Using assessment.json for PR #44742, show the complete REST representation for every affected operation, including operation ID, method/path, parameters, request, responses, LRO, paging, and TypeSpec source.`

## 🛡️ Compatibility Assessment

### REST Breaking Changes

<a id="finding-rest-1-parameter-contract-changed-2026-12-06-get-restype-directory-comp-list"></a>
### Operation parameter contract changes

- **Severity:** high
- **Confidence:** high
- **Summary:** The parameter remains an optional array of strings, but the exact source removes accepted include enum values for the 2026-12-06 special-file entries. Requests using those values are no longer described as valid, so the existing query contract narrows.
- **Evidence:** Directory_ListFilesAndDirectoriesSegment changed 1 parameter contract(s).; Compared REST operation: 2026-12-06:GET:?restype=directory&comp=list.
- **TypeSpec source:** [models.tsp:L405-L426](https://github.com/Azure/azure-rest-api-specs/blob/e75a07cd7ea1b2207190a362305db02b639715ec/specification/storage/data-plane/FileStorage/models.tsp#L405-L426), [models.tsp:L557-L592](https://github.com/Azure/azure-rest-api-specs/blob/e75a07cd7ea1b2207190a362305db02b639715ec/specification/storage/data-plane/FileStorage/models.tsp#L557-L592), [models.tsp:L691-L806](https://github.com/Azure/azure-rest-api-specs/blob/e75a07cd7ea1b2207190a362305db02b639715ec/specification/storage/data-plane/FileStorage/models.tsp#L691-L806)


<a id="finding-rest-2-response-contract-changed-2026-12-06-get-restype-directory-comp-list"></a>
### Operation response contract changes

- **Severity:** high
- **Confidence:** high
- **Summary:** The 200 response keeps its top-level schema name, but the exact source removes the XML-serialized SymLink, BlockDevice, CharDevice, Fifo, and Socket collections and their item models. Removing response properties and schemas from the existing 2026-12-06 contract is breaking.
- **Evidence:** Directory_ListFilesAndDirectoriesSegment changed an existing response contract.; Compared REST operation: 2026-12-06:GET:?restype=directory&comp=list.
- **TypeSpec source:** [models.tsp:L405-L426](https://github.com/Azure/azure-rest-api-specs/blob/e75a07cd7ea1b2207190a362305db02b639715ec/specification/storage/data-plane/FileStorage/models.tsp#L405-L426), [models.tsp:L557-L592](https://github.com/Azure/azure-rest-api-specs/blob/e75a07cd7ea1b2207190a362305db02b639715ec/specification/storage/data-plane/FileStorage/models.tsp#L557-L592), [models.tsp:L691-L806](https://github.com/Azure/azure-rest-api-specs/blob/e75a07cd7ea1b2207190a362305db02b639715ec/specification/storage/data-plane/FileStorage/models.tsp#L691-L806)


<a id="finding-rest-3-parameter-contract-changed-2026-12-06-put"></a>
### Operation parameter contract changes

- **Severity:** high
- **Confidence:** high
- **Summary:** The header remains an optional string, but the exact source removes the explicitly serialized Fifo member from NfsFileType. Existing 2026-12-06 requests using Fifo for x-ms-file-file-type are no longer valid, so the accepted wire-value set narrows.
- **Evidence:** File_Create changed 1 parameter contract(s).; Compared REST operation: 2026-12-06:PUT:/.
- **TypeSpec source:** [models.tsp:L284-L305](https://github.com/Azure/azure-rest-api-specs/blob/e75a07cd7ea1b2207190a362305db02b639715ec/specification/storage/data-plane/FileStorage/models.tsp#L284-L305)


### Downstream Breaking Changes

<a id="finding-derived-rest-contract-sdk-impact"></a>
### REST contract changes require generated-client updates

- **Severity:** high
- **Confidence:** high
- **Summary:** The approved REST contract changes also alter generated client request or response handling and can break callers compiled against the previous contract.
- **Evidence:** Approved REST finding: rest-1-parameter-contract-changed-2026-12-06-get-restype-directory-comp-list.; Approved REST finding: rest-2-response-contract-changed-2026-12-06-get-restype-directory-comp-list.; Approved REST finding: rest-3-parameter-contract-changed-2026-12-06-put.
- **TypeSpec source:** [models.tsp:L405-L426](https://github.com/Azure/azure-rest-api-specs/blob/e75a07cd7ea1b2207190a362305db02b639715ec/specification/storage/data-plane/FileStorage/models.tsp#L405-L426), [models.tsp:L557-L592](https://github.com/Azure/azure-rest-api-specs/blob/e75a07cd7ea1b2207190a362305db02b639715ec/specification/storage/data-plane/FileStorage/models.tsp#L557-L592), [models.tsp:L691-L806](https://github.com/Azure/azure-rest-api-specs/blob/e75a07cd7ea1b2207190a362305db02b639715ec/specification/storage/data-plane/FileStorage/models.tsp#L691-L806), [models.tsp:L284-L305](https://github.com/Azure/azure-rest-api-specs/blob/e75a07cd7ea1b2207190a362305db02b639715ec/specification/storage/data-plane/FileStorage/models.tsp#L284-L305)


## 📎 Appendix

### Assessment Errors

None.

### Code-to-Guidance Evidence

| Result | Document section | Fetched guidance | Observed TypeSpec | Evidence |
| --- | --- | --- | --- | --- |
| Matched | Versioning overview - API Versioning | on-over-inheritance documentation-required friendly-name key-visibility-required known-encoding long-running-polling-operation-required no-case-mismatch no-closed-literal-union no-enum no-error-status-codes no-explicit-routes-resource-ops no-format no-generic-numeric no-header-explode no-legacy-usage no-multiple-discriminator no-nullable no-offsetdatetime no-openapi-client-extensions no-openapi no-private-usage no-query-explode no-response-body no-rest-library-interfaces no-route-parameter-name- | The official Azure TypeSpec evolving-APIs guidance shows @added applied to declarations introduced in a later version, and the exact source removes the affected declarations together with their @added annotations rather than leaving newly introduced declarations unversioned. The TypeSpec enum guidance shows explicit serialized values, and the remaining enum members shown in the changed source continue to use explicit string values. The ARM common-types document is not applicable to this data-plane model change. | models.tsp:L405-L426, models.tsp:L557-L592, models.tsp:L691-L806, +1 more |
| Matched | Evolving APIs - API Versioning | on-over-inheritance documentation-required friendly-name key-visibility-required known-encoding long-running-polling-operation-required no-case-mismatch no-closed-literal-union no-enum no-error-status-codes no-explicit-routes-resource-ops no-format no-generic-numeric no-header-explode no-legacy-usage no-multiple-discriminator no-nullable no-offsetdatetime no-openapi-client-extensions no-openapi no-private-usage no-query-explode no-response-body no-rest-library-interfaces no-route-parameter-name- | The official Azure TypeSpec evolving-APIs guidance shows @added applied to declarations introduced in a later version, and the exact source removes the affected declarations together with their @added annotations rather than leaving newly introduced declarations unversioned. The TypeSpec enum guidance shows explicit serialized values, and the remaining enum members shown in the changed source continue to use explicit string values. The ARM common-types document is not applicable to this data-plane model change. | models.tsp:L405-L426, models.tsp:L557-L592, models.tsp:L691-L806, +1 more |
| Matched | Enums - Models and Enums | Enums \| TypeSpec Skip to content TypeSpec Use cases OpenAPI Data Validation Tooling support Docs Videos Playground Blog Community Version 1.15.0 is now available! Search... OpenAPI Data Validation Tooling support Docs Videos Playground Blog Community Getting started Installation Editor VS Code Extension Visual Studio Extension Guides TypeSpec for REST Getting Started with TypeSpec For REST APIs Operations and Respons | The official Azure TypeSpec evolving-APIs guidance shows @added applied to declarations introduced in a later version, and the exact source removes the affected declarations together with their @added annotations rather than leaving newly introduced declarations unversioned. The TypeSpec enum guidance shows explicit serialized values, and the remaining enum members shown in the changed source continue to use explicit string values. The ARM common-types document is not applicable to this data-plane model change. | models.tsp:L405-L426, models.tsp:L557-L592, models.tsp:L691-L806, +1 more |

### Tooling Used

- `@azure-tools/typespec-autorest`
- `@azure-tools/typespec-client-generator-core`

### Artifact Evidence

- **autorest:** base/head succeeded; OpenAPI removes enum values, properties, and schemas
- **tcgc:** base/head succeeded; client models and properties are removed
