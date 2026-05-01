# SCENARIO 1: General Issue

**Title:** [APIView] Diff view freezes on large reviews with 500+ APIs

**Labels:** ['APIView']

---

## Review Link

https://apiview.dev/Assemblies/Review/azure-storage-blob-12.20.0

## Description

When I navigate to the diff view for a review with more than 500 API definitions, the page freezes for 10+ seconds and shows a blank white screen. This happens in Chrome and Edge. The review is azure-storage-blob 12.20.0 vs 12.19.0. Other reviewers on my team also experience this.

## Suggested Next Steps

This looks like a client-side performance problem in the diff rendering path rather than a browser-specific issue, since it reproduces in both Chromium browsers and across multiple users. Investigate whether the review is loading and diffing all API nodes synchronously on initial render, and profile the JavaScript bundle for expensive tree expansion, sorting, or DOM virtualization regressions on large assemblies. It would also be useful to compare this review size against a smaller one to identify the first threshold where the blank screen starts, then check whether the APIView front-end team or the diff viewer component owns the rendering path.

---
*Reported via APIView*

---

# SCENARIO 2: Comment Issue (AVC)

**Title:** [APIView] Copilot comment: incorrect sync recommendation for async list_blobs

**Labels:** ['APIView Copilot']

---

## Review Link

https://apiview.dev/Assemblies/Review/azure-storage-blob-12.20.0

## Description

This AVC suggestion is incorrect. The list_blobs method DOES perform I/O - it makes HTTP calls to Azure Storage to list blobs. Making it synchronous would block the event loop and break all async callers.

## Comment Context
**Source:** copilot
**Language:** python
**Comment:**
> Consider making this method synchronous since it does not perform any I/O operations.
**Code Snippet:**
```
async def list_blobs(self, container: str, prefix: str = None) -> AsyncIterator[BlobProperties]:
```
**Element ID:** `BlobServiceClient.list_blobs`

## Suggested Next Steps

Please verify the Copilot/AVC suggestion against the Python async client design for blob listing. The async `list_blobs` API should remain asynchronous if it issues network requests via the pipeline, and any change to sync/async shape should be checked against the Azure SDK Python guidelines for async methods and iterators. It may also be worth confirming whether this comment is being generated from method signature only rather than inspecting the implementation or related helper calls, since that would indicate an issue in the suggestion model/rules rather than the library code.

---
*Reported via APIView*

---

