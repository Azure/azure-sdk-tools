# 📋 TypeSpec Assessment

**PR:** [#44742 - Removed NFSv2 from STG103/104 TypeSpec & Swagger](https://github.com/Azure/azure-rest-api-specs/pull/44742)

**Overall confidence:** 🟢 high<br>
**Overall code safety:** 🔴 Low

**Baseline:** `af067c9fc987b837f27a34b86e3ac591507711c9 (e75a07cd7ea1b2207190a362305db02b639715ec)`<br>
**Head:** `656ca8b0741973760f8a60ee0fd293ba9c31e708`; working-tree changes: false<br>
**Total assessment time:** 12m 46s

## 📌 Executive Summary

| Dimension | Result | Findings |
| --- | --- | ---: |
| Semantic understanding | ✅ Assessed — 1 intent(s), 1 operation(s) | n/a |
| REST compatibility | ❌ Issues found | 1 |
| Downstream compatibility | ✅ No breaks detected | 0 |
| Azure compliance | ✅ passed | 0 |

**Scope:** 1 intent(s), 1 affected operation(s), 1 project(s).<br>
**Highest severity:** high.

## 🎯 Action Required

| Severity | Area | Finding | Why it matters | Code | Guidance |
| --- | --- | --- | --- | --- | --- |
| high | REST | Stable API response and enum surface is removed | Existing 2026-12-06 clients can no longer request or deserialize the removed NFSv2 shapes. | [models.tsp:L284-L806](https://github.com/Azure/azure-rest-api-specs/blob/e75a07cd7ea1b2207190a362305db02b639715ec/specification/storage/data-plane/FileStorage/models.tsp#L284-L806) | n/a |

## 🧠 Semantic Understanding

### Change Overview

| # | Intent | Operations | API versions | Details |
| ---: | --- | ---: | --- | --- |
| 1 | Remove NFSv2 file types, include values, response fields, and item models from the 2026-12-06 File Storage API. | 1 | 2026-12-06 | [details](#intent-1-remove-nfsv2-file-types-include-values-response-) |

### Operation Details

<a id="intent-1-remove-nfsv2-file-types-include-values-response-"></a>
### 1. Remove NFSv2 file types, include values, response fields, and item models from the 2026-12-06 File Storage API.

**Confidence:** high<br>
**REST summary:** The stable 2026-12-06 OpenAPI removes accepted include values and response shapes such as BlockDeviceItem, CharDeviceItem, FIFO, Socket, and NFS metadata.

#### `Directory_ListFilesAndDirectories`

- **HTTP path:** `GET /{filesystem}/{directoryPath}`
- **API versions:** `2026-12-06`
- **Parameters:** path filesystem and directoryPath: string, required; query restype=directory and comp=list: constant, required; query include, marker, maxresults, prefix, timeout: optional
- **Request payload:** none
- **Response payloads:** 200: application/xml ListFilesAndDirectoriesSegmentResponse; default: StorageError
- **Service behavior:** Lists directory entries, including NFS-specific item shapes when requested.
- **LRO:** No.
- **Paging:** File or Directory entry; NextMarker; Send the returned marker in the next marker query parameter; stop when absent.
- **TypeSpec source:** [models.tsp:L284-L806](https://github.com/Azure/azure-rest-api-specs/blob/e75a07cd7ea1b2207190a362305db02b639715ec/specification/storage/data-plane/FileStorage/models.tsp#L284-L806)
## 🛡️ Compatibility Assessment

### REST Breaking Changes

### Stable API response and enum surface is removed

- **Severity:** high
- **Confidence:** high
- **Summary:** Existing 2026-12-06 clients can no longer request or deserialize the removed NFSv2 shapes.
- **Evidence:** AutoRest removes enum values and response schemas from a stable API version.
- **TypeSpec source:** [models.tsp:L284-L806](https://github.com/Azure/azure-rest-api-specs/blob/e75a07cd7ea1b2207190a362305db02b639715ec/specification/storage/data-plane/FileStorage/models.tsp#L284-L806)


### Downstream Breaking Changes

None detected.

## ☁️ Azure Compliance

**Status:** `passed`

### Compliance Findings

No compliance mismatches found.

## 📎 Appendix

### Assessment Errors

None.

### Code-to-Guidance Evidence

| Result | Document section | Fetched guidance | Observed TypeSpec | Evidence |
| --- | --- | --- | --- | --- |
| Matched | [Evolving APIs - Removing API elements](https://azure.github.io/typespec-azure/docs/howtos/versioning/06-evolving-apis/) | To remove models, properties, or operations starting from a specific version, use the `@removed` decorator. | The NFSv2 enum members, properties, and models are explicitly excluded from stable 2026-12-06 while earlier versions remain represented. | [models.tsp:L284-L806](https://github.com/Azure/azure-rest-api-specs/blob/e75a07cd7ea1b2207190a362305db02b639715ec/specification/storage/data-plane/FileStorage/models.tsp#L284-L806), [models.tsp:L287-L302](https://github.com/Azure/azure-rest-api-specs/blob/e75a07cd7ea1b2207190a362305db02b639715ec/specification/storage/data-plane/FileStorage/models.tsp#L287-L302), [models.tsp:L408-L423](https://github.com/Azure/azure-rest-api-specs/blob/e75a07cd7ea1b2207190a362305db02b639715ec/specification/storage/data-plane/FileStorage/models.tsp#L408-L423), +4 more |

### Tooling Used

- `@azure-tools/typespec-autorest`
- `@azure-tools/typespec-client-generator-core`

### Repository Validation

| Project | Tool | Status | Duration | Log |
| --- | --- | --- | ---: | --- |
| `specification/storage/data-plane/FileStorage` | `TypeSpecValidation` | succeeded | 28s | `validation-logs/specification__storage__data-plane__FileStorage-head.log` |

### Artifact Evidence

- **autorest:** base/head succeeded; OpenAPI removes enum values, properties, and schemas
- **tcgc:** base/head succeeded; client models and properties are removed

### Changed TypeSpec

- `specification/storage/data-plane/FileStorage/models.tsp`: [models.tsp:L287-L302](https://github.com/Azure/azure-rest-api-specs/blob/e75a07cd7ea1b2207190a362305db02b639715ec/specification/storage/data-plane/FileStorage/models.tsp#L287-L302), [models.tsp:L408-L423](https://github.com/Azure/azure-rest-api-specs/blob/e75a07cd7ea1b2207190a362305db02b639715ec/specification/storage/data-plane/FileStorage/models.tsp#L408-L423), [models.tsp:L560-L589](https://github.com/Azure/azure-rest-api-specs/blob/e75a07cd7ea1b2207190a362305db02b639715ec/specification/storage/data-plane/FileStorage/models.tsp#L560-L589), [models.tsp:L609-L613](https://github.com/Azure/azure-rest-api-specs/blob/e75a07cd7ea1b2207190a362305db02b639715ec/specification/storage/data-plane/FileStorage/models.tsp#L609-L613), [models.tsp:L660-L674](https://github.com/Azure/azure-rest-api-specs/blob/e75a07cd7ea1b2207190a362305db02b639715ec/specification/storage/data-plane/FileStorage/models.tsp#L660-L674), [models.tsp:L694-L803](https://github.com/Azure/azure-rest-api-specs/blob/e75a07cd7ea1b2207190a362305db02b639715ec/specification/storage/data-plane/FileStorage/models.tsp#L694-L803)
