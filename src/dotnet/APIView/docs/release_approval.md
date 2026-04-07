# API Approval & Release Gating

This document describes how API reviews are approved in APIView and how that approval status integrates with CI/CD pipelines to gate package releases.

---

## 1. Approval Levels

APIView tracks approval at three levels. Each serves a different purpose in the release lifecycle.

### a. API Revision Approval

The primary approval mechanism. Each `APIRevisionListItemModel` carries:

- **`IsApproved`** (bool) — whether the revision's API surface has been approved.
- **`Approvers`** (HashSet\<string\>) — the set of users who have approved.
- **`ChangeHistory`** — an append-only audit trail of approval/revert events.

A revision is considered approved when `Approvers` is non-empty. Multiple reviewers can each toggle their approval independently; removing all approvers reverts the revision to unapproved.

**Source:** [`APIViewWeb/LeanModels/ReviewListModels.cs`](../APIViewWeb/LeanModels/ReviewListModels.cs) (fields on `APIRevisionListItemModel`), [`APIViewWeb/Managers/APIRevisionsManager.cs`](../APIViewWeb/Managers/APIRevisionsManager.cs) (`ToggleAPIRevisionApprovalAsync`).

### b. Review-Level (First-Release) Approval

A separate, review-scoped flag on `ReviewListItemModel.IsApproved`. This covers first-release approval of a package name/namespace and is largely a legacy concept being phased out in favour of namespace approval (below).

**Source:** [`APIViewWeb/Managers/ReviewManager.cs`](../APIViewWeb/Managers/ReviewManager.cs) (`ToggleReviewApprovalAsync`).

### c. Namespace Approval

For TypeSpec-based packages, APIView tracks whether the namespace has been approved across all SDK languages in a review group. When every SDK language's revision for a TypeSpec project is approved, the namespace is auto-approved. This approval is tracked per-language in a `ProjectNamespaceInfo` record.

**Source:** [`APIViewWeb/Managers/NamespaceManager.cs`](../APIViewWeb/Managers/NamespaceManager.cs) (`IsNamespaceApprovedAsync`), [`APIViewWeb/Managers/ReviewManager.cs`](../APIViewWeb/Managers/ReviewManager.cs) (`RequestNamespaceReviewAsync`).

---

## 2. GA vs Preview Version Classification

APIView classifies every revision as either **GA (stable)** or **preview (prerelease)** based on its package version string. The classification is performed by `AzureEngSemanticVersion`, which has matching implementations in both C# and TypeScript.

### Rules

A version is **preview** if either condition is true:

1. **The version has a prerelease label** — any suffix after a `-` separator (e.g., `-beta.1`, `-alpha.20210901.1`, `-rc.1`).
2. **The major version is `0`** — `0.x.y` versions are always treated as prerelease, even without a label.

Otherwise the version is **GA**.

### Sort Order

Versions sort with GA above preview at the same major.minor.patch. For example (descending):

```
2.0.0             ← GA
2.0.0-beta.10     ← preview
2.0.0-beta.2      ← preview
1.0.0             ← GA
1.0.0-beta.1      ← preview
```

### Where the Distinction Matters

| Area | GA behaviour | Preview behaviour |
|------|-------------|-------------------|
| **Copilot review gate** | Must have Copilot review before human approval (for supported languages). | Exempt — approval is allowed without Copilot review. |
| **Auto-archive** | The last approved GA revision is always preserved. | The most recent preview revision is always preserved (regardless of approval status). |

**Source:** [`APIViewWeb/Helpers/AzureEngSemanticVersion.cs`](../APIViewWeb/Helpers/AzureEngSemanticVersion.cs), [`ClientSPA/src/app/_models/azureEngSemanticVersion.ts`](../ClientSPA/src/app/_models/azureEngSemanticVersion.ts), [`APIViewWeb/Managers/APIRevisionsManager.cs`](../APIViewWeb/Managers/APIRevisionsManager.cs) (`IsPrerelease`, `AutoArchiveAPIRevisions`).

---

## 3. Approval Prerequisites

Before a user can approve a revision in the Angular SPA, the UI enforces several guards (evaluated in priority order):

| # | Guard | Effect |
|---|-------|--------|
| 1 | **Missing package version** | Blocks approval — a version must be present on the code file. |
| 2 | **Unresolved "Must Fix" comments** | Blocks approval — all must-fix comments (human, AI, or diagnostic) must be resolved first. |
| 3 | **Copilot review required** | For supported languages, the revision must have been reviewed by Copilot before human approval (preview versions are exempt). |

On the backend, the controller additionally verifies that the requesting user has the **approver** role via `ManagerHelpers.AssertApprover()`.

**Source:** [`ClientSPA/src/app/_components/review-page-options/review-page-options.component.ts`](../ClientSPA/src/app/_components/review-page-options/review-page-options.component.ts) (`shouldDisableApproval`).

---

## 4. Approval Toggle Flow

When a reviewer clicks **Approve** (or **Revert API Approval**):

1. **UI** sends `POST /api/APIRevisions/{reviewId}/{apiRevisionId}` with body `{ "approve": true/false }` to `APIRevisionsController`.
2. **Backend** checks authorization, then calls `ToggleAPIRevisionApprovalAsync`.
3. **`ChangeHistoryHelpers.UpdateBinaryChangeAction()`** computes the toggle:
   - If the user has more "Approved" entries than "ApprovalReverted" entries → emit `ApprovalReverted`, set `IsApproved = false`, remove from `Approvers`.
   - Otherwise → emit `Approved`, set `IsApproved = true`, add to `Approvers`.
4. The change history entry is appended (records `ChangedBy`, `ChangedOn`, `Notes`).
5. The updated revision is persisted to Cosmos DB via `UpsertAPIRevisionAsync`.
6. A **SignalR** broadcast (`ReceiveApproval`) notifies all connected clients in real-time.

---

## 5. Automatic Approval Carry-Forward

When a new automatic revision is created (e.g., from a CI build), its API surface is compared against existing approved revisions using `AreAPIRevisionsTheSame()`.

- **Fast path:** If both revisions have a `ContentHash` (SHA-256 of the API surface), comparison is O(1).
- **Slow path:** If no hash is stored, the token file is downloaded from Blob Storage and compared structurally. The hash is then back-filled for future comparisons.

If the surfaces match and the source revision is approved, `CarryForwardRevisionDataAsync` copies the approval to the new revision. The change history records `"Approval copied from revision {sourceId}"` with the original approver's identity.

**Source:** [`APIViewWeb/Managers/APIRevisionsManager.cs`](../APIViewWeb/Managers/APIRevisionsManager.cs) (`CarryForwardRevisionDataAsync`, `ApplyApprovalFrom`, `AreAPIRevisionsTheSame`).

---

## 6. Release Gating (CI/CD Integration)

APIView does **not** enforce release gates directly. Instead, it reports approval status via HTTP status codes that CI/CD pipelines query before proceeding with a release.

### Endpoint

```
GET /AutoReview/GetReviewStatus?language={lang}&packageName={pkg}
    [&packageVersion={ver}][&firstReleaseStatusOnly={bool}]
```

**Source:** [`APIViewWeb/Controllers/AutoReviewController.cs`](../APIViewWeb/Controllers/AutoReviewController.cs) (`GetReviewStatus`).

### Response Codes

| HTTP Status | Meaning | Pipeline Action |
|-------------|---------|-----------------|
| **200 OK** | API revision is approved | Proceed to release |
| **201 Created** | Namespace / first-release approved (revision itself may not be) | Proceed (with conditions — depends on pipeline policy) |
| **202 Accepted** | Approval pending | Block release, wait for approval |
| **404 Not Found** | No review exists for this package | Fail — package not registered in APIView |

### Resolution Logic

1. If a `packageVersion` is provided, the endpoint looks for an automatic revision matching that version (exact or same major.minor). Falls back to the latest automatic revision otherwise.
2. If `firstReleaseStatusOnly` is not `true` and the matching revision has `IsApproved == true` → **200**.
3. If the parent review has `IsApproved == true` or `IsNamespaceApprovedAsync()` returns true → **201**.
4. Otherwise → **202**.

### Callers

This endpoint is used by:

- **Prepare-release scripts** in SDK repositories — verifying approval before cutting a release.
- **CI/CD release pipelines** — automated gate checks before publishing a package.

The actual block/allow decision lives in the pipeline. APIView only reports status; it does not create GitHub checks or commit statuses.

---

## 7. Marking a Revision as Released

When a release pipeline publishes a package, it calls back to APIView to stamp the revision:

```
POST /api/auto-reviews/upload   (or /api/auto-reviews/create)
    setReleaseTag=true
    compareAllRevisions=true
    packageVersion={released-version}
```

This triggers:

1. `AutoReviewService.CreateAutomaticRevisionAsync()` — finds the existing approved revision with a matching API surface.
2. `APIRevisionsManager.UpdateRevisionMetadataAsync()` — sets `IsReleased = true` and `ReleasedOn = DateTime.UtcNow`.
3. Once marked released, the revision's metadata is frozen (subsequent calls with `setReleaseTag` are no-ops).

The revision then displays as **"Shipped"** in the UI.

**Source:** [`APIViewWeb/LeanControllers/AutoReviewController.cs`](../APIViewWeb/LeanControllers/AutoReviewController.cs), [`APIViewWeb/Managers/APIRevisionsManager.cs`](../APIViewWeb/Managers/APIRevisionsManager.cs) (`UpdateRevisionMetadataAsync`).

---

## 8. End-to-End Lifecycle

```
CI Build Pipeline
  │  POST /autoreview/upload (artifact)
  ▼
APIView creates/matches APIRevision
  │  Carry-forward approval if surface unchanged
  ▼
Reviewers approve in Angular SPA
  │  Guards: version present, no must-fix, Copilot reviewed
  ▼
Release Pipeline
  │  GET /AutoReview/GetReviewStatus → 200 OK
  ▼
Package published
  │  POST /autoreview/upload (setReleaseTag=true)
  ▼
Revision marked "Shipped" (IsReleased, ReleasedOn)
```
