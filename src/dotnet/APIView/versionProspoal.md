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
4. [Proposed Architecture: Version-Centric Model](#proposed-architecture-version-centric-model)
   - [New Entity: APIVersion](#new-entity-apiversion)
   - [Updated Entity Hierarchy](#updated-entity-hierarchy)
   - [Version Aliasing & Storage Deduplication](#version-aliasing--storage-deduplication)
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
- **Approval is per-revision, not per-version:** Approving revision X doesn't mean "v12.2.0 is approved"—it means "this specific upload is approved."
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
- **No audit trail distinction:** `CopyApprovalFromAsync` records `"Approval copied from revision {id}"` in `ChangeHistory`, but the `IsApproved` flag and `Approvers` set look identical to a human approval.
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

## Proposed Architecture: Version-Centric Model

### New Entity: APIVersion

Introduce `APIVersion` as a **first-class entity** that sits between `Review` and `APIRevision`:

```
Review (1 per package+language)
  └── APIVersion (1 per significant version: v12.1.0, v12.2.0-beta.1, etc.)
       └── APIRevision (1+ per version: uploads, re-parses, PR previews)
```

```csharp
public class APIVersionModel : BaseListitemModel
{
    // Identity
    public string ReviewId { get; set; }               // FK to parent Review
    public string SemanticVersion { get; set; }         // e.g. "12.2.0", "12.3.0-beta.1"
    public VersionKind Kind { get; set; }               // Stable, Preview, Development

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

    // Metadata
    public string CreatedBy { get; set; }
    public DateTime CreatedOn { get; set; }
    public List<APIVersionChangeHistoryModel> ChangeHistory { get; set; } = new();
}

public enum VersionKind
{
    Stable,      // GA release (12.2.0)
    Preview,     // Prerelease (12.3.0-beta.1)
    Development  // PR preview, never released
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
│    Id, ReviewId, SemanticVersion, Kind               │
│    IsApproved, ApprovalInheritedFromVersionId        │
│    CanonicalVersionId, APIContentHash                │
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

**Storage savings:** For a package where 75% of version bumps don't change the API surface (common for patch releases and implementation-only changes), this eliminates ~75% of blob storage for that package.

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
2. Add `APIVersionId` field to `APIRevisionListItemModel` and `CommentItemModel` (nullable, for backward compat)
3. **Backfill migration:** Group existing revisions by `PackageVersion` → create one `APIVersionModel` per unique version per review
4. Set `APIVersionId` on each existing revision and comment
5. Compute and store `APIContentHash` for each version from its latest revision's blob

### Phase 2: Version Aliasing & Storage Dedup

1. After backfill, identify versions with identical `APIContentHash` within each review
2. For each group of identical versions, designate the oldest as canonical, set `CanonicalVersionId` on the others
3. Update the upload pipeline (`AutoReviewService.CreateAutomaticRevisionAsync`) to:
   - Compute hash before upload
   - Check for existing canonical version
   - Skip blob upload if alias is appropriate
4. Update retrieval paths to resolve `CanonicalVersionId`

### Phase 3: Comment Scoping

1. Update `ReviewsController` query path to fetch comments by `APIVersionId`
2. Update `CodeFileHelpers.CollectUserCommentsForRow` — no structural change needed since comments are pre-filtered
3. Update SPA `CommentsService` to pass version context
4. Add "Show comments from other versions" opt-in toggle

### Phase 4: Approval Migration

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
| **Version lookup** | In-memory filter across all revisions | Direct query on `APIVersions` container |