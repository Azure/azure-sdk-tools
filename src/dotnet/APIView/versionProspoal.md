# APIView v2 Data Model: Problems & Proposed Architecture

> **Status:** RFC / Design Proposal  
> **Author:** (generated from codebase investigation)  
> **Date:** February 2026

---

## Table of Contents

1. [Background](#background)
2. [Current Entity Model](#current-entity-model)
   - [Reviews](#reviews)
   - [API Revisions](#api-revisions)
   - [Comments & Comment Threads](#comments--comment-threads)
   - [Blob Storage](#blob-storage)
3. [Problems](#problems)
   - [Problem 1: Comment Bleed Across Revisions](#problem-1-comment-bleed-across-revisions)
   - [Problem 2: Version Ambiguity](#problem-2-version-ambiguity)
   - [Problem 3: Opaque Approval Copying](#problem-3-opaque-approval-copying)
   - [Problem 4: Duplicate Blob Storage](#problem-4-duplicate-blob-storage)
   - [Problem 5: Comment Orphaning on Revision Deletion/Replacement](#problem-5-comment-orphaning-on-revision-deletionreplacement)
4. [Proposed Architecture: Version-Centric Model](#proposed-architecture-version-centric-model)
   - [New Entity: APIVersion](#new-entity-apiversion)
   - [Updated Entity Hierarchy](#updated-entity-hierarchy)
   - [Version Aliasing & Storage Deduplication](#version-aliasing--storage-deduplication)
   - [PR Versions](#pr-versions)
   - [Comment Scoping](#comment-scoping)
   - [Explicit Approval Inheritance](#explicit-approval-inheritance)
   - [O(1) Sameness Checks via Content Hash](#o1-sameness-checks-via-content-hash)
5. [Migration Plan](#migration-plan)
6. [Summary of Benefits](#summary-of-benefits)

---

## Background

APIView was originally designed so that a "Review" represented **one version** of a package. When we decided to consolidate all versions of a package into a single Review (the "v2" model), all revisions—across every version bump, PR preview, and manual upload—were crammed under one `ReviewListItemModel`. This was done without fully rethinking how comments, approvals, and artifacts should be scoped. The result is a data model with several structural problems that cause user confusion and storage waste.

---

## Current Entity Model

### Reviews

**Container:** `Reviews` (Cosmos DB, partitioned by `id`)  
**Model:** `ReviewListItemModel` (in `APIViewWeb/LeanModels/ReviewListModels.cs`)

There is **one Review per package + language combination**. Key fields:

| Field | Purpose |
|---|---|
| `Id` | Unique review identifier (also partition key) |
| `PackageName` | e.g., `azure-storage-blob` |
| `Language` | e.g., `Python`, `.NET` |
| `IsApproved` | Whether the review is approved |
| `Subscribers` | Users subscribed to notifications |
| `IsClosed` | Soft-close flag |
| `CreatedBy` / `CreatedOn` | Creation metadata |

The Review is the **root entity**. A single review for `azure-storage-blob` in Python holds ALL revisions: v12.1.0, v12.2.0-beta.1, PR previews, manual uploads, etc.

### API Revisions

**Container:** `APIRevisions` (Cosmos DB, partitioned by `ReviewId`)  
**Model:** `APIRevisionListItemModel` (in `APIViewWeb/LeanModels/ReviewListModels.cs`)

Each revision belongs to exactly one Review via `ReviewId`. Key fields:

| Field | Purpose |
|---|---|
| `Id` | Unique revision identifier |
| `ReviewId` | FK to parent Review |
| `APIRevisionType` | `Manual`, `Automatic`, `PullRequest`, or `All` |
| `Files` | `List<APICodeFileModel>` — typically one entry per revision |
| `Label` | Free-text label, sometimes encodes source branch |
| `PackageVersion` | Computed from `Files.First().PackageVersion` |
| `IsApproved` / `Approvers` | Approval state |
| `IsReleased` / `ReleasedOn` | Release tracking |
| `PullRequestNo` | PR number for PR-type revisions |
| `SourceBranch` | Source branch (extracted from `Label` if not set directly) |
| `HeadingsOfSectionsWithDiff` | Precomputed diff data for collapsible Swagger sections |
| `DiagnosticsHash` | Hash for diagnostic comment sync |

**Critical observation:** `APIRevisionType` is the **only** structural distinction between a version bump (Automatic), a PR preview (PullRequest), and a manual upload (Manual). There is no explicit "version" concept—v12.1.0 and v12.2.0 are just different revisions under the same review, distinguished only by the `PackageVersion` field buried inside `Files[0]`.

### Comments & Comment Threads

**Container:** `Comments` (Cosmos DB, partitioned by `ReviewId`)  
**Model:** `CommentItemModel` (in `APIViewWeb/LeanModels/CommentItemModel.cs`)

| Field | Purpose |
|---|---|
| `Id` | Unique comment identifier |
| `ReviewId` | FK to Review (partition key) |
| `APIRevisionId` | FK to specific revision (optionally set) |
| `ElementId` | The code line identifier this comment is attached to |
| `ThreadId` | Groups replies into a thread |
| `IsResolved` | Whether the thread is resolved |
| `CommentSource` | `UserGenerated`, `AIGenerated`, or `Diagnostic` |
| `Severity` | `Question`, `Suggestion`, `ShouldFix`, `MustFix` |

**`CommentThreadModel`** (in `APIViewWeb/Models/CommentThreadModel.cs`) groups comments by `ThreadId` for display. It has `ReviewId` and `LineId` but **no version/revision scoping**.

**`ReviewCommentsModel`** groups ALL review comments by `ElementId` then `ThreadId`:
```csharp
_threads = comments.GroupBy(c => c.ElementId).ToDictionary(...)
```
No revision filtering is applied at the grouping stage.

### Blob Storage

Two Azure Blob Storage containers hold artifacts per revision:

| Container | Path Pattern | Contents |
|---|---|---|
| `codefiles` | `{revisionId}/{fileId}` | Parsed token files (the rendered API surface) |
| `originals` | `{codeFileId}` | Original source artifacts (e.g., .whl, .nupkg, .jar) |

**Key code:**
- `BlobCodeFileRepository.GetBlobClient()` builds path: `key = revisionId + "/" + codeFileId` → always **unique per revision**
- `BlobOriginalsRepository` stores at path `{codeFileId}` → also unique per revision since each revision gets a new `FileId`
- `CodeFileManager.CreateReviewCodeFileModel()` uploads to **both** containers for every new revision

---

## Problems

### Problem 1: Comment Bleed Across Revisions

**Symptom:** When viewing revision B (e.g., v12.2.0), users see unresolved comments that were originally left on revision A (e.g., v12.1.0), even though the comment may be irrelevant to the newer version.

**Root Cause — The Query Path:**

1. **Controller fetches ALL comments for the review** (`ReviewsController.cs`, line 260):
   ```csharp
   IEnumerable<CommentItemModel> allCommentsFromDb = 
       await _commentsManager.GetCommentsAsync(reviewId, commentType: CommentType.APIRevision);
   ```

2. **Filter keeps ALL unresolved comments regardless of revision** (line 270):
   ```csharp
   List<CommentItemModel> filteredComments = allComments
       .Where(c => !c.IsResolved || c.APIRevisionId == activeApiRevisionId)
       .ToList();
   ```
   The logic is: show a comment if it's *unresolved* (from **any** revision) OR if it's *resolved but belongs to the active revision*. This means every unresolved comment from every prior version appears.

3. **Comments are matched to code lines purely by `ElementId`** (`CodeFileHelpers.cs`, `CollectUserCommentsForRow`):
   ```csharp
   commentsForRow = codePanelRawData.Comments
       .Where(c => nodeId == c.ElementId)
       .ToList();
   ```
   No revision check. If the `ElementId` still exists in the new version's code, the comment renders.

**Impact:** Over time, reviews accumulate stale comments from old versions. A review with 50+ revisions may show dozens of irrelevant threads. Users cannot distinguish "this comment is about v12.1.0 and doesn't apply to v12.3.0" from "this is an active concern on the current version."

---

### Problem 2: Version Ambiguity

**Symptom:** The revision list for a review is a flat, undifferentiated timeline mixing fundamentally different things.

**Root Cause:** `APIRevisionListItemModel` serves as **four different concepts** distinguished only by an enum and free-text:

| Concept | How it's encoded |
|---|---|
| A new package version (e.g., v12.2.0) | `APIRevisionType = Automatic`, version in `Files[0].PackageVersion` |
| A re-upload of the *same* version | `APIRevisionType = Automatic`, same `PackageVersion` |
| A PR preview for an in-progress change | `APIRevisionType = PullRequest`, label has source branch |
| A manual upload for ad-hoc review | `APIRevisionType = Manual`, label is whatever the user typed |

There is no explicit "version" entity. The concept of "v12.2.0" exists implicitly as "whichever revisions happen to have `Files[0].PackageVersion == "12.2.0"`. This means:

- **No clean version timeline:** The UI shows one big list of revisions with no grouping.
- **Approval is per-revision, not per-version:** Approving revision X doesn't mean "v12.2.0 is approved"—it means "this API surface is approved." In practice, the auto-copy mechanism ([Problem 3](#problem-3-opaque-approval-copying)) propagates approval to any other revision with a byte-identical API surface, regardless of version. This is correct for patch bumps where nothing meaningful changed, but wrong when context matters — e.g., a version that warrants re-review due to new dependencies, changed behavior behind the same surface, or a different release stage. The system offers no way to distinguish these cases or opt out of auto-propagation.
- **Version lookup is expensive:** `GetAPIRevisionsAsync(reviewId, packageVersion)` does in-memory filtering across ALL revisions to find matches by major.minor prefix.

---

### Problem 3: Opaque Approval Copying

**Symptom:** A new revision for v12.3.0 silently appears as "approved" even though no human reviewed it. Users cannot tell whether an approval was a deliberate human review or an automated copy.

**Root Cause — The Approval Copy Flow** (in `AutoReviewService.CreateAutomaticRevisionAsync`):

```csharp
if (!apiRevision.IsApproved && apiRevisions.Any())
{
    foreach (var apiRev in apiRevisions)
    {
        if (apiRev.IsApproved && 
            await _apiRevisionsManager.AreAPIRevisionsTheSame(apiRev, renderedCodeFile))
        {
            await _apiRevisionsManager.CopyApprovalFromAsync(
                targetRevision: apiRevision, sourceRevision: apiRev);
            break;
        }    
    }
}
```

This loop:
1. Iterates through **all previous revisions** of the review
2. For each approved revision, calls `AreAPIRevisionsTheSame` which **downloads the blob** and does a full content comparison (tree diff or line-by-line `SequenceEqual`)
3. If the API surfaces match, copies the approval silently

**Sub-problems:**
- **Expensive comparison:** Each `AreAPIRevisionsTheSame` call downloads a blob from Azure Storage and does a full diff. For reviews with many approved revisions, this can mean dozens of blob downloads per CI upload.
- **Approval loops:** If version A was approved, version B gets auto-approved (same API), then version A is un-approved, version B remains approved.

---

### Problem 4: Duplicate Blob Storage

**Symptom:** When v12.2.0 and v12.3.0 have identical API surfaces (e.g., only internal/implementation changes), the system stores **complete duplicate blobs** for both versions and then copies approval from one to the other.

**Root Cause — The Upload Flow:**

When `AutoReviewService.CreateAutomaticRevisionAsync` processes a new package version:

1. **Version mismatch forces new revision** — Even if the API surface is byte-for-byte identical, `AreAPIRevisionsTheSame` with `considerPackageVersion = true` returns `false` when `PackageVersion` differs:
   ```csharp
   bool considerPackageVersion = !String.IsNullOrWhiteSpace(codeFile.PackageVersion);
   // ...
   if (await _apiRevisionsManager.AreAPIRevisionsTheSame(
       latestAutomaticAPIRevision, renderedCodeFile, considerPackageVersion))
   ```

2. **New revision uploads to BOTH containers** — `CreateReviewCodeFileModel` always uploads:
   ```csharp
   // Upload original artifact
   await _originalsRepository.UploadOriginalAsync(reviewCodeFileModel.FileId, memoryStream);
   // Upload parsed token file
   await _codeFileRepository.UpsertCodeFileAsync(apiRevisionId, reviewCodeFileModel.FileId, codeFile);
   ```

3. **Blob paths are always unique per revision:**
   - `codefiles` container: `{revisionId}/{fileId}` — new revision = new path
   - `originals` container: `{codeFileId}` — new revision = new FileId = new path

4. **Then approval is copied** — The system notices the API surfaces match and copies approval, but the duplicate blobs are already stored.

**Storage Impact Estimate:**  
For a package with N versions where M of those have identical API surfaces, the system stores `2 × M` unnecessary blobs (one in `codefiles`, one in `originals`). Across hundreds of packages each with dozens of versions, this represents significant storage waste.

---

### Problem 5: Comment Orphaning on Revision Deletion/Replacement

> **Ref:** [azure-sdk-tools#14187](https://github.com/Azure/azure-sdk-tools/issues/14187)

**Symptom:** Comments reference revisions that no longer exist, creating orphaned data in the `Comments` container.

**Root Cause:** Comments are keyed to `APIRevisionId`, but revisions are routinely deleted or replaced without migrating or cleaning up their associated comments. This happens in two scenarios:

1. **Revision replacement:** When an automatic revision supersedes an older one, the old revision is soft-deleted but its comments' `APIRevisionId` is never updated to the replacement. Revisions with comments are kept as stale "anchors" solely to preserve the linkage.
2. **Direct deletion:** Archive, PR cleanup, manual delete, and purge operations remove revisions without touching their associated comments at all.

The frontend masks this with fallback logic that maps orphaned comments to the active revision, but the underlying data relationship is broken. Over time, orphaned comments accumulate and the comment history becomes unreliable — comments appear attached to revisions they were never written against.

---

## Proposed Architecture: Version-Centric Model

### New Entity: APIVersion

Introduce `APIVersion` as a **first-class entity** that sits between `Review` and `APIRevision`:

```
Review (1 per package+language)
  ├── APIVersion (Kind=Stable/Preview: 1 per unique version label — v12.1.0, v12.2.0-beta.1, etc.)
  │    ├── APIRevision (1+ per version: uploads, re-parses)
  │    └── SamplesRevision (0+ per version: code samples for this API surface)
  └── APIVersion (Kind=PullRequest: 1 per PR, identified by PR number)
       └── APIRevision (1+ per PR: each push to the PR branch)
```

Samples revisions are version-scoped: sample code for v12.1.0 may differ from v12.2.0 when the API surface changes. `SamplesRevisionModel` gains an `APIVersionId` FK. For aliased versions (identical API surface), samples from the canonical version are shared via the same alias resolution used for code file blobs.

```csharp
public class APIVersionModel : BaseListitemModel
{
    // Identity
    public string ReviewId { get; set; }               // FK to parent Review
    public string VersionIdentifier { get; set; }      // Semantic version ("12.2.0") or PR identifier ("PR#1234")
    public VersionKind Kind { get; set; }               // Stable, Preview, PullRequest

    // Package version (derived from latest revision's Files[0].PackageVersion)
    public string PackageVersion { get; set; }          // e.g. "12.2.0-beta.1" — updated on each new revision

    // PR-specific (null for Stable/Preview)
    public int? PullRequestNumber { get; set; }         // GitHub PR number
    public string SourceBranch { get; set; }            // e.g. "feature/add-widget"
    public PullRequestStatus? PRStatus { get; set; }    // Open, Merged, Closed (null for non-PR)

    // Approval (explicit, auditable)
    public bool IsApproved { get; set; }
    public HashSet<string> Approvers { get; set; } = new();
    public string ApprovalInheritedFromVersionId { get; set; } // null = human-approved
    public DateTime? ApprovalDate { get; set; }

    // Version Aliasing (storage dedup)
    public string CanonicalVersionId { get; set; }      // null = this version owns its blobs
    public string APIContentHash { get; set; }          // SHA-256 of normalized API surface

    // Release Tracking
    public bool IsReleased { get; set; }
    public DateTime? ReleasedOn { get; set; }

    // Review Requests
    public List<string> ReviewRequestIds { get; set; } = new(); // IDs of review-request records (reviewer assignments, approvals)
    public bool IsReviewedByCopilot { get; set; }              // true if any ReviewRequest includes a completed Copilot review

    // Metadata
    public string CreatedBy { get; set; }
    public DateTime CreatedOn { get; set; }
    public DateTime LastUpdated { get; set; }                   // Updated on any revision add, approval change, or metadata edit
    public List<APIVersionChangeHistoryModel> ChangeHistory { get; set; } = new();
}

public enum VersionKind
{
    Stable,       // GA release (12.2.0)
    Preview,      // Prerelease (12.3.0-beta.1)
    PullRequest   // PR preview — one per PR, archived on PR close
}

public enum PullRequestStatus
{
    Open,    // PR is active — revisions are being added
    Merged,  // PR was merged — retain longer, may have historical value
    Closed   // PR was closed without merging — changes rejected, eligible for early cleanup
}
```

**Cosmos DB Container:** `APIVersions`, partitioned by `ReviewId`.

### Updated Entity Hierarchy

```
┌─────────────────────────────────────────────────────┐
│ Cosmos DB Container: Reviews                        │
│ Partition Key: /id                                  │
│                                                     │
│  ReviewListItemModel                                │
│    Id, PackageName, Language, IsApproved, ...        │
└───────────┬─────────────────────────────────────────┘
            │ 1:N
┌───────────▼─────────────────────────────────────────┐
│ Cosmos DB Container: APIVersions (NEW)              │
│ Partition Key: /ReviewId                            │
│                                                     │
│  APIVersionModel                                    │
│    Id, ReviewId, VersionIdentifier, Kind             │
│    IsApproved, ApprovalInheritedFromVersionId        │
│    CanonicalVersionId, APIContentHash                │
│    PullRequestNumber, SourceBranch, PRStatus (PR)    │
└───────────┬─────────────────────────────────────────┘
            │ 1:N
┌───────────▼─────────────────────────────────────────┐
│ Cosmos DB Container: APIRevisions (existing)        │
│ Partition Key: /ReviewId                            │
│                                                     │
│  APIRevisionListItemModel                           │
│    Id, ReviewId, APIVersionId (NEW FK)               │
│    Files[], APIRevisionType, Label, ...              │
│    (IsApproved DEPRECATED → lives on APIVersion)     │
└───────────┬─────────────────────────────────────────┘
            │
┌───────────▼─────────────────────────────────────────┐
│ Cosmos DB Container: Comments (existing)            │
│ Partition Key: /ReviewId                            │
│                                                     │
│  CommentItemModel                                   │
│    Id, ReviewId, APIVersionId (NEW FK)               │
│    APIRevisionId, ElementId, ThreadId, ...           │
└─────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────┐
│ Cosmos DB Container: SamplesRevisions (existing)    │
│ Partition Key: /ReviewId                            │
│                                                     │
│  SamplesRevisionModel                               │
│    Id, ReviewId, APIVersionId (NEW FK)               │
│    OriginalFileId, Title, ...                        │
└─────────────────────────────────────────────────────┘
```

### Version Aliasing & Storage Deduplication

When a new package version arrives with an identical API surface:

```
┌──────────────────┐         ┌──────────────────┐
│  APIVersion      │         │  APIVersion      │
│  v12.2.0         │◄────────│  v12.3.0         │
│                  │  alias  │                  │
│  CanonicalId=null│         │  CanonicalId=    │
│  ContentHash=abc │         │   "v12.2.0-id"   │
│                  │         │  ContentHash=abc │
│  Owns blobs:     │         │  Owns blobs: NONE│
│   codefiles/...  │         │                  │
│   originals/...  │         │                  │
└──────────────────┘         └──────────────────┘
```

**How the alias works:**

1. **On upload:** Compute `APIContentHash` (SHA-256 over normalized API surface — the tree token structure or rendered text lines). This is a fast, in-memory operation on the already-parsed `CodeFile`.

2. **Check for match:** Query existing APIVersions for the same `ReviewId` where `APIContentHash` matches:
   ```csharp
   var existingVersion = versions.FirstOrDefault(v => 
       v.APIContentHash == newHash && v.CanonicalVersionId == null);
   ```

3. **If match found — create alias:**
   - Create the `APIVersionModel` with `CanonicalVersionId = existingVersion.Id`
   - Create the `APIRevisionListItemModel` with `APIVersionId = newVersion.Id`
   - **Skip blob upload entirely** — no `codefiles` blob, no `originals` blob
   - Copy approval via `ApprovalInheritedFromVersionId`

4. **On retrieval — resolve alias:**
   ```csharp
   public async Task<RenderedCodeFile> GetCodeFileForVersion(APIVersionModel version)
   {
       var resolvedVersionId = version.CanonicalVersionId ?? version.Id;
       var canonicalRevision = await GetLatestRevisionForVersion(resolvedVersionId);
       return await _codeFileRepository.GetCodeFileAsync(canonicalRevision, false);
   }
   ```

5. **If alias target changes (rare):** If the canonical version's blobs are deleted or updated, the alias can be re-pointed or the aliased version can be "materialized" by uploading its own blobs and clearing `CanonicalVersionId`.

6. **Canonical version soft-deletion:** Admins should never hard-delete a canonical version that has aliases. If a canonical version is soft-deleted:
   - The **first alias** (by `CreatedOn`) is promoted: it copies the canonical version's blobs to its own storage paths, clears its `CanonicalVersionId` (becoming a new canonical), and becomes the live version.
   - All **other aliases** update their `CanonicalVersionId` to point to the newly promoted version.
   - This ensures that when the soft-deleted canonical version is eventually garbage-collected, no artifacts are lost.
   - The promotion is performed as part of the soft-delete operation, not deferred to garbage collection, to prevent a window where aliases point to a version whose blobs may be reclaimed.

**Storage savings:** For a package where 75% of version bumps don't change the API surface (common for patch releases and implementation-only changes), this eliminates ~75% of blob storage for that package.

### PR Versions

Each pull request is modeled as its own `APIVersion` with `Kind = PullRequest`. This treats PRs as a first-class unit with a clear lifecycle, scoped comments, and clean deletion semantics.

**Identity & Deduplication:**
- `VersionIdentifier` is set to `"PR#1234"` (the GitHub PR number).
- `PullRequestNumber` and `SourceBranch` provide lookup keys.
- `PackageVersion` is updated to reflect the most recent revision's `Files[0].PackageVersion` on each new push. This ensures the PR version always shows which package version it would produce (e.g., "PR#1234 — 12.3.0-beta.1").
- When a new push arrives for an existing PR, the system finds the existing `APIVersion` by `ReviewId + PullRequestNumber` and creates a new `APIRevision` under it — no new version is created.

**Lifecycle:**

| Event | Action |
|---|---|
| PR opened / first push | Create `APIVersion` with `Kind = PullRequest`, `PRStatus = Open`, `PackageVersion` from revision; create first `APIRevision` |
| Subsequent push to PR | Create new `APIRevision` under existing PR version; update `PackageVersion` to latest revision's value |
| PR merged | Set `PRStatus = Merged` — retain for longer TTL (changes were accepted, may have historical value) |
| PR closed without merge | Set `PRStatus = Closed` — eligible for shorter TTL (changes were rejected) |
| Archive TTL expires | Cascade-delete the `APIVersion`, all its `APIRevision` entries, all its `Comment` entries, and all associated blobs |

**Why this works:**
- **Comment isolation:** PR review comments stay scoped to the PR version. They never leak into release versions or other PRs. When the PR version is deleted, its comments are deleted with it — no orphans.
- **Clean deletion:** Today, PR revision cleanup (`ClosePullRequestAPIRevision`, auto-archive) must carefully handle comments to avoid orphaning. With the version-centric model, deleting a PR version is a single cascade operation: delete version → delete revisions → delete comments → delete blobs.
- **No version guessing:** The system doesn't need to know which release version the PR targets. The PR version is self-contained. If the PR merges and triggers a CI build, that build creates a new Stable/Preview version through the normal automatic revision flow.
- **UI grouping:** The version list can separate PR versions from release versions, giving users a clean view of both in-flight PRs and the release timeline.

### Comment Scoping

Comments are scoped to `APIVersionId` instead of just `ReviewId`:

**New query path:**
```csharp
// Instead of: GetCommentsAsync(reviewId)
// Now:
var comments = await _commentsManager.GetCommentsAsync(reviewId, apiVersionId);
```

**New filter logic:**
```csharp
// Instead of:
//   allComments.Where(c => !c.IsResolved || c.APIRevisionId == activeApiRevisionId)
// Now: comments are already scoped to the version. Show all of them.
var filteredComments = allComments.Where(c => 
    c.APIVersionId == activeVersion.Id).ToList();
```

**Benefits:**
- Comments on v12.1.0 stay on v12.1.0 — they never leak into v12.2.0
- Within a version, all revisions share comments (correct behavior — re-uploads of the same version should see the same feedback)
- The `ElementId` matching in `CollectUserCommentsForRow` continues to work but now only against version-scoped comments
- **Eliminates comment orphaning (Problem 5):** Since comments are scoped to `APIVersion` rather than `APIRevision`, replacing or deleting an individual revision within a version does not orphan its comments. The comment's `APIVersionId` remains valid regardless of which revision is the "current" one for that version. For version-level deletion (rare), comments are cascade-deleted with the version entity, maintaining referential integrity.

**Diagnostic comments** follow the same version-level scoping as user comments — they are keyed to `APIVersionId`, not `APIRevisionId`. Although diagnostics are generated per-upload by language-specific analyzers, scoping them to the version is consistent with the model and avoids the complexity of mixed scoping rules. When a new revision is uploaded within a version, diagnostics are reconciled as follows:

1. **On new revision upload:** Compare the new revision's diagnostics against the existing diagnostic comments for that `APIVersionId`.
2. **Diagnostics present in both:** No action needed — the existing diagnostic comment remains valid and visible.
3. **Diagnostics present only in the old set:** Auto-resolve the diagnostic comment (set `IsResolved = true`). The issue no longer exists in the latest code.
4. **Diagnostics present only in the new revision:** Create a new diagnostic comment with `APIVersionId` scoping.

The existing `DiagnosticsHash` on `APIRevisionListItemModel` continues to serve as a short-circuit — if the hash matches the previous revision's, no diagnostic diff is needed.

**Cross-version comment visibility (optional UX):**  
If a user *wants* to see what was said on a prior version, the UI can offer an opt-in "Show comments from other versions" toggle. This is additive UX, not a data model concern.

### Explicit Approval Inheritance

Approval lives on `APIVersion`, not `APIRevision`:

| Scenario | `IsApproved` | `ApprovalInheritedFromVersionId` | UI Display |
|---|---|---|---|
| Human approved v12.1.0 | `true` | `null` | ✅ Approved by @reviewer |
| Bot copied approval to v12.2.0 (same API) | `true` | `"v12.1.0-id"` | ✅ Approved (inherited from v12.1.0) |
| New version, no matching API | `false` | `null` | ⏳ Pending review |

**Auto-approval flow (replaces current `CopyApprovalFromAsync`):**

```csharp
if (existingVersion != null && existingVersion.APIContentHash == newVersion.APIContentHash)
{
    if (existingVersion.IsApproved)
    {
        newVersion.IsApproved = true;
        newVersion.Approvers = new HashSet<string>(existingVersion.Approvers);
        newVersion.ApprovalInheritedFromVersionId = existingVersion.Id;
        newVersion.ApprovalDate = DateTime.UtcNow;
    }
}
```

**Benefits:**
- **O(1) comparison** via `APIContentHash` instead of downloading blobs
- **Clear audit trail** — `ApprovalInheritedFromVersionId` makes the provenance visible
- **Revocable inheritance** — Un-approving the source version can cascade to inherited versions if desired
- **UI distinction** — The frontend can show "inherited" approvals differently from human approvals

### O(1) Sameness Checks via Content Hash

The current `AreAPIRevisionsTheSame` method is expensive:

```csharp
// Current: downloads blob, does full comparison
var lastRevisionFile = await _codeFileRepository.GetCodeFileAsync(revision, false);
var result = _codeFileManager.AreAPICodeFilesTheSame(codeFileA: lastRevisionFile, codeFileB: renderedCodeFile);
```

With `APIContentHash`, most comparisons become a simple string equality check:

```csharp
// Proposed: O(1) hash comparison
bool isSame = existingVersion.APIContentHash == ComputeContentHash(renderedCodeFile);
```

The hash is computed once at upload time and stored on `APIVersionModel`. The expensive blob-based comparison becomes a fallback only for hash collisions (extremely rare with SHA-256).

---

## Migration Plan

### Phase 1: Add APIVersion Entity (Non-Breaking)

1. Create `APIVersions` Cosmos DB container
2. Add `APIVersionId` field to `APIRevisionListItemModel`, `CommentItemModel`, and `SamplesRevisionModel` (nullable, for backward compat)
3. **Backfill migration:** Group existing revisions by `PackageVersion` → create one `APIVersionModel` per unique version per review
4. Set `APIVersionId` on each existing revision, comment, and samples revision
5. Compute and store `APIContentHash` for each version from its latest revision's blob

### Phase 2: Version Aliasing & Storage Dedup

1. After backfill, identify versions with identical `APIContentHash` within each review
2. For each group of identical versions, designate the oldest as canonical, set `CanonicalVersionId` on the others
3. Update the upload pipeline (`AutoReviewService.CreateAutomaticRevisionAsync`) to:
   - Compute hash before upload
   - Check for existing canonical version
   - Skip blob upload if alias is appropriate
4. Update retrieval paths to resolve `CanonicalVersionId`

### Phase 3: Comment Scoping & Orphan Cleanup

1. Update `ReviewsController` query path to fetch comments by `APIVersionId`
2. Update `CodeFileHelpers.CollectUserCommentsForRow` — no structural change needed since comments are pre-filtered
3. Update SPA `CommentsService` to pass version context
4. Add "Show comments from other versions" opt-in toggle
5. **Orphan cleanup ([#14187](https://github.com/Azure/azure-sdk-tools/issues/14187)):** During migration, identify comments whose `APIRevisionId` references a deleted/soft-deleted revision. Re-associate these to the correct `APIVersionId` based on the revision's original `PackageVersion`. Delete diagnostic comments whose parent revision no longer exists (they are revision-specific by nature).
6. **Cascade deletion rules:** When an `APIVersion` is deleted, cascade-delete all associated comments. When an individual `APIRevision` is replaced within a version, no comment migration is needed — comments are already scoped to the version, not the revision.

### Phase 4: PR Version Migration

1. **Backfill:** Group existing PR-type revisions by `PullRequestNo` (or `SourceBranch` where PR number is absent) → create one `APIVersion` per unique PR per review with `Kind = PullRequest`
2. Set `APIVersionId` on each existing PR revision and its associated comments
3. Update `PullRequestManager` to create/find `APIVersion` by PR number on incoming webhooks instead of creating bare `APIRevision` entries
4. Set `PRStatus = Merged` or `Closed` on PR versions based on current GitHub PR state
5. Apply differentiated archive TTLs — shorter for `Closed` (rejected), longer for `Merged` — then cascade-delete after retention period

### Phase 5: Approval Migration

1. Move `IsApproved` / `Approvers` from `APIRevisionListItemModel` to `APIVersionModel`
2. Set `ApprovalInheritedFromVersionId` for versions that had bot-copied approvals (detectable from `ChangeHistory` notes containing "Approval copied from")
3. Update `ToggleAPIRevisionApprovalAsync` to operate on `APIVersionModel`
4. Update UI to show inherited vs. human approval

---

## Summary of Benefits

| Problem | Current State | Proposed State |
|---|---|---|
| **Comment bleed** | Unresolved comments from all versions appear everywhere | Comments scoped to version; clean per-version view |
| **Version ambiguity** | Flat list of revisions, no version grouping | Explicit `APIVersion` entity; clear hierarchy |
| **Opaque approval** | Silent copy, no UI distinction | `ApprovalInheritedFromVersionId` with clear UI |
| **Duplicate storage** | Full blob duplication per version | Version aliasing; ~75% blob reduction for stable packages |
| **Expensive sameness checks** | Blob download + full diff per comparison | O(1) `APIContentHash` comparison |
| **Comment orphaning** | Comments reference deleted revisions; frontend workarounds mask broken data | Comments scoped to `APIVersion`; revision replacement cannot orphan comments; cascade delete on version removal |
| **PR lifecycle** | PR revisions mixed into flat revision list; cleanup orphans comments | Each PR is its own `APIVersion`; close → archive → cascade-delete version + revisions + comments + blobs |
| **Version lookup** | In-memory filter across all revisions | Direct query on `APIVersions` container |

---

## Additional Considerations

### Carrying Comments Forward Across Versions

Today, unresolved comments bleed across all versions implicitly. Once comment scoping is enforced, that implicit behavior goes away — which is the desired outcome for the vast majority of cases. However, there is a legitimate (if rare) scenario where a reviewer intentionally wants a comment to remain visible in future versions: for example, flagging an API surface for re-review at the next breaking change release.

The current proposal's "Show comments from other versions" toggle (in [Comment Scoping](#comment-scoping)) addresses the *read* side — users can opt in to viewing historical threads. But it doesn't cover the *write* side: a reviewer explicitly marking a thread as "revisit this later."

**Options to explore (not yet committed):**

1. **Thread-level carry-forward flag** — A boolean on a comment thread that makes it appear in future versions until explicitly resolved. Simple, but adds UI/filter complexity.
2. **Severity-based carry-forward** — Threads marked `MustFix` or `ShouldFix` could automatically carry forward to the next version until resolved. Leverages the existing `Severity` field without new data model changes.
3. **External tracking** — Rather than building this into APIView, link out to a GitHub issue for long-lived action items. APIView threads stay version-scoped; the issue tracks the cross-version concern.
4. **Do nothing initially** — Ship version-scoped comments with the "show other versions" toggle. If the carry-forward need proves common in practice, add the mechanism later. The data model supports it (adding a flag to `CommentItemModel` is non-breaking).

### Version List Display

The UI today presents a single flattened dropdown of all versions and revisions. The version-centric model doesn't change this: **all `APIVersion` entries — including aliased versions — appear in the list.** An aliased version (one whose `CanonicalVersionId` points elsewhere) is still a real version with its own label; it just shares blobs and approval with the canonical version. Users should see it in the list so they can confirm it exists and verify its approval status.

One refinement worth experimenting with: a display mode that shows only the **latest revision per version** by default. In practice, reviewers almost always want to compare the most recent revision of one version against another released version — not inspect how revision 1 of v12.2.0 differs from revision 3 of the same version. Collapsing to "latest per version" would shorten the dropdown significantly for packages with many re-uploads. Earlier revisions would still be accessible via an expand control or a "Show all revisions" toggle.

This is a UX experiment, not a data model change — the underlying query simply filters to `MAX(CreatedOn)` per `APIVersionId`. It can be shipped (or reverted) independently of any other phase.

### Notifications

The current notification system (`NotificationManager`) sends emails referencing `reviewId` + `apiRevisionId`. The version-centric model introduces new notification considerations:

- Whether "new version uploaded" and "new revision within existing version" should be distinct notification types
- Whether subscribers can filter by version kind (e.g., stable only, no PRs)
- How email deep-links should be structured (version-level vs. revision-level)

These decisions are deferred to a **separate notification proposal**. The data model changes in this proposal are compatible with any notification granularity — `APIVersionModel` provides all the fields needed for version-aware notifications when that work is undertaken.

### First Release Approval

`ReviewListItemModel.IsApprovedForFirstRelease` is a review-level concept distinct from per-version approval. This field is managed by the **Namespace Approval** system, which is outside the scope of this proposal. The version-centric model does not change or interact with first-release approval — it remains at the `Review` level.

### Samples Revisions

Samples revisions (`SamplesRevisionModel`) move from review-level to version-level scoping. Sample code for v12.1.0 may differ from v12.2.0 when the API surface changes, so samples are associated with `APIVersionId`. For aliased versions sharing an identical API surface, samples from the canonical version are resolved through the same alias indirection used for code file blobs. Migration of existing samples revisions is included in Phase 1 (backfill assigns each existing `SamplesRevision` to the `APIVersion` matching its associated revision's `PackageVersion`).
