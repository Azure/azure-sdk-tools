# APIView v2 Data Model: Problems & Proposed Architecture

---

## Table of Contents

1. [Background](#1-background)
2. [Current Entity Model](#2-current-entity-model)
   - 2.1 [Reviews](#21-reviews)
   - 2.2 [API Revisions](#22-api-revisions)
   - 2.3 [Comments & Comment Threads](#23-comments--comment-threads)
   - 2.4 [Blob Storage](#24-blob-storage)
3. [Problems](#3-problems)
   - 3.1 [Comment Bleed Across Revisions](#31-comment-bleed-across-revisions)
   - 3.2 [Version Ambiguity](#32-version-ambiguity)
   - 3.3 [Opaque Approval Copying](#33-opaque-approval-copying)
   - 3.4 [Duplicate Blob Storage](#34-duplicate-blob-storage)
   - 3.5 [Comment Orphaning on Revision Deletion/Replacement](#35-comment-orphaning-on-revision-deletionreplacement)
   - 3.6 [Daily Alpha/Dev Version Churn](#36-daily-alphadev-version-churn)
4. [Proposed Architecture: Version-Centric Model](#4-proposed-architecture-version-centric-model)
   - 4.1 [New Entity: APIVersion](#41-new-entity-apiversion)
   - 4.2 [Updated Entity Hierarchy](#42-updated-entity-hierarchy)
   - 4.3 [Version Normalization for Rolling Prereleases](#43-version-normalization-for-rolling-prereleases)
   - 4.4 [PR Versions](#44-pr-versions)
   - 4.5 [Comment Scoping](#45-comment-scoping)
   - 4.6 [Explicit Approval Inheritance](#46-explicit-approval-inheritance)
   - 4.7 [O(1) Sameness Checks via Content Hash](#47-o1-sameness-checks-via-content-hash)
   - 4.8 [Retention Policy](#48-retention-policy)
5. [Migration Plan](#5-migration-plan)
   - 5.1 [Phase 1: Add APIVersion Entity (Non-Breaking)](#51-phase-1-add-apiversion-entity-non-breaking)
   - 5.2 [Phase 2: Comment Scoping & Orphan Cleanup](#52-phase-2-comment-scoping--orphan-cleanup)
   - 5.3 [Phase 3: PR Version Migration](#53-phase-3-pr-version-migration)
   - 5.4 [Phase 4: Approval Migration](#54-phase-4-approval-migration)
6. [Summary of Benefits](#6-summary-of-benefits)
7. [Additional Considerations](#7-additional-considerations)
   - 7.1 [Carrying Comments Forward Across Versions](#71-carrying-comments-forward-across-versions)
   - 7.2 [Copilot Automation Strategy](#72-copilot-automation-strategy)
   - 7.3 [Versionless Revisions (JavaScript)](#73-versionless-revisions-javascript)
   - 7.4 [Version List Display](#74-version-list-display)
   - 7.5 [PR Comparison Baseline Selection](#75-pr-comparison-baseline-selection)
   - 7.6 [Notifications](#76-notifications)
   - 7.7 [First Release Approval](#77-first-release-approval)

---

## 1. Background

APIView was originally designed so that a "Review" represented **one version** of a package. When we decided to consolidate all versions of a package into a single Review (the "v2" model), all revisions—across every version bump, PR preview, and manual upload—were crammed under one `ReviewListItemModel`. This was done without fully rethinking how comments, approvals, and artifacts should be scoped. The result is a data model with several structural problems that cause user confusion and storage waste.

---

## 2. Current Entity Model

### 2.1 Reviews

**Container:** `Reviews` (*Cosmos DB, partitioned by `id`)  
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

### 2.2 API Revisions

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

### 2.3 Comments & Comment Threads

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

**`ReviewCommentsModel`** groups ALL review comments by `ElementId` then `ThreadId` with no revision filtering applied at the grouping stage.

### 2.4 Blob Storage

Two Azure Blob Storage containers hold artifacts per revision:

| Container | Path Pattern | Contents |
|---|---|---|
| `codefiles` | `{revisionId}/{fileId}` | Parsed token files (the rendered API surface) |
| `originals` | `{codeFileId}` | Original source artifacts (e.g., .whl, .nupkg, .jar) |

**Key behavior:**
- Blob paths are always **unique per revision** — `codefiles` uses `{revisionId}/{fileId}`, `originals` uses `{codeFileId}`
- Every new revision uploads to **both** containers

---

## 3. Problems

### 3.1 Comment Bleed Across Revisions

**Symptom:** When viewing revision B (e.g., v12.2.0), users see unresolved comments that were originally left on revision A (e.g., v12.1.0), even though the comment may be irrelevant to the newer version.

**Root Cause — The Query Path:**

1. **Controller fetches ALL comments for the review** — the comments query is scoped only to `ReviewId`, not to any specific revision or version.

2. **Filter keeps ALL unresolved comments regardless of revision** — the filter logic shows a comment if it's *unresolved* (from **any** revision) OR if it's *resolved but belongs to the active revision*. This means every unresolved comment from every prior version appears.

3. **Comments are matched to code lines purely by `ElementId`** — no revision check is performed. If the `ElementId` still exists in the new version's code, the comment renders.

**Impact:** Over time, reviews accumulate stale comments from old versions. A review with 50+ revisions may show dozens of irrelevant threads. Users cannot distinguish "this comment is about v12.1.0 and doesn't apply to v12.3.0" from "this is an active concern on the current version."

---

### 3.2 Version Ambiguity

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
- **Cross-version replacement:** Because only one pending automatic revision is retained, a CI upload for one major-minor version can silently overwrite the revision for a different major-minor version ([azure-sdk-tools#5186](https://github.com/Azure/azure-sdk-tools/issues/5186)). This also affects hotfix releases: when a hotfix build (e.g., v1.16.1) creates an automatic revision, it deletes the pending revision for a different version (e.g., v1.17.0), breaking PR API-change detection because all subsequent PRs compare against the hotfix instead of the main-branch revision ([azure-sdk-tools#10105](https://github.com/Azure/azure-sdk-tools/issues/10105)). This is especially problematic for teams that release from feature branches while `main` targets a different version—e.g., the Python ML team releasing from a branch other than `main`.
- **Approval is per-revision, not per-version:** Approving revision X doesn't mean "v12.2.0 is approved"—it means "this API surface is approved." In practice, the auto-copy mechanism ([§3.3](#33-opaque-approval-copying)) propagates approval to any other revision with a byte-identical API surface, regardless of version. This is correct for patch bumps where nothing meaningful changed, but wrong when context matters — e.g., a version that warrants re-review due to new dependencies, changed behavior behind the same surface, or a different release stage. The system offers no way to distinguish these cases or opt out of auto-propagation.
- **Stale phantom versions:** A version is created and an APIView revision is generated, but then the version file (e.g., `version.py`) is bumped after-the-fact. The old fictitious version—which will never be released—remains in APIView while the correct version is never generated ([azure-sdk-tools#5186, comment](https://github.com/Azure/azure-sdk-tools/issues/5186#issuecomment-1713078780)).
- **Version lookup is expensive:** `GetAPIRevisionsAsync(reviewId, packageVersion)` does in-memory filtering across ALL revisions to find matches by major.minor prefix.

---

### 3.3 Opaque Approval Copying

**Symptom:** A new revision for v12.3.0 silently appears as "approved" even though no human reviewed it. The UI presents it identically to a human-approved revision.

**Root Cause — The Approval Copy Flow** (in `AutoReviewService.CreateAutomaticRevisionAsync`):

When a new revision is created, the system:
1. Iterates through **all previous revisions** of the review
2. For each approved revision, calls `AreAPIRevisionsTheSame` which **downloads the blob** and does a full content comparison (tree diff or line-by-line `SequenceEqual`)
3. If the API surfaces match, copies the approval via `ApplyApprovalFrom`

The copy event *is* recorded in `ChangeHistory` — `ApplyApprovalFrom` writes a `Notes` string (`"Approval copied from revision {sourceRevision.Id}"`) and records the original approver's username as `ChangedBy`. However, this audit trail is not actionable:
- **Opaque reference:** The `Notes` field contains a raw revision GUID (e.g., `"Approval copied from revision 8a3f..."`). To understand what that means — which version, which API surface, who originally approved it — a user or developer must look up that revision ID and follow the chain. There is no human-readable context (version label, package version, approver name) in the note itself.
- **Unstructured provenance:** The source revision ID is embedded in free-text, not a dedicated property. Programmatic use (cascading un-approval, querying inheritance chains) requires string parsing.
- **Impersonated actor:** `ChangedBy` is set to the original human approver's username, not a bot or system identity. From the `ChangeHistory` entry alone, the copy is indistinguishable from that human manually approving the revision.

**Sub-problems:**
- **Expensive comparison:** Each `AreAPIRevisionsTheSame` call downloads a blob from Azure Storage and does a full diff. For reviews with many approved revisions, this can mean dozens of blob downloads per CI upload.
- **Approval loops:** If version A was approved, version B gets auto-approved (same API), then version A is un-approved, version B remains approved.
- **Major version bumps skip review ([azure-sdk-tools#9595](https://github.com/Azure/azure-sdk-tools/issues/9595)):** When a new major version (e.g., v2.0.0) has an identical API surface to the previously approved version (e.g., v1.0.0), approval is silently copied. Major version bumps may be driven by branding, behavioral, or dependency changes that warrant architect review even when the public API surface is unchanged. The current system has no concept of "version distance" — any matching API surface triggers auto-approval regardless of how far apart the versions are.

---

### 3.4 Duplicate Blob Storage

**Symptom:** When v12.2.0 and v12.3.0 have identical API surfaces (e.g., only internal/implementation changes), the system stores **complete duplicate blobs** for both versions and then copies approval from one to the other.

**Root Cause — The Upload Flow:**

When `AutoReviewService.CreateAutomaticRevisionAsync` processes a new package version:

1. **Version mismatch forces new revision** — Even if the API surface is byte-for-byte identical, the sameness check returns `false` when `PackageVersion` differs, forcing creation of a new revision.

2. **New revision uploads to BOTH containers** — every new revision uploads to both the `codefiles` and `originals` blob containers.

3. **Blob paths are always unique per revision:**
   - `codefiles` container: `{revisionId}/{fileId}` — new revision = new path
   - `originals` container: `{codeFileId}` — new revision = new FileId = new path

4. **Then approval is copied** — The system notices the API surfaces match and copies approval, but the duplicate blobs are already stored.

This same duplication pattern manifests when a PR is merged into main: the PR revision is retained AND a new Automatic revision is created from the merge commit, producing two blob-identical artifacts ([azure-sdk-tools#8634](https://github.com/Azure/azure-sdk-tools/issues/8634)). The proposed promotion-on-merge flow ([§4.4 PR Versions](#44-pr-versions)) eliminates this entirely for the common case where the API surface is unchanged: the PR version is promoted to `Stable`/`Preview` in-place, reusing its existing blobs.

**Storage Impact Estimate:**  
For a package with N versions where M of those have identical API surfaces, the system stores `2 × M` unnecessary blobs (one in `codefiles`, one in `originals`). Across hundreds of packages each with dozens of versions, this represents significant storage waste.

---

### 3.5 Comment Orphaning on Revision Deletion/Replacement

> **Ref:** [azure-sdk-tools#14187](https://github.com/Azure/azure-sdk-tools/issues/14187)

**Symptom:** Comments reference revisions that no longer exist, creating orphaned data in the `Comments` container.

**Root Cause:** Comments are keyed to `APIRevisionId`, but revisions are routinely deleted or replaced without migrating or cleaning up their associated comments. This happens in two scenarios:

1. **Revision replacement:** When an automatic revision supersedes an older one, the old revision is soft-deleted but its comments' `APIRevisionId` is never updated to the replacement. Revisions with comments are kept as stale "anchors" solely to preserve the linkage.
2. **Direct deletion:** Archive, PR cleanup, manual delete, and purge operations remove revisions without touching their associated comments at all.

The frontend masks this with fallback logic that maps orphaned comments to the active revision, but the underlying data relationship is broken. Over time, orphaned comments accumulate and the comment history becomes unreliable — comments appear attached to revisions they were never written against.

---

### 3.6 Daily Alpha/Dev Version Churn

**Symptom:** Java and C# (and potentially other languages) publish alpha or dev builds from CI on every merge to `main`, producing a new unique prerelease version every day. For example:

| Day | C# version | Java version |
|---|---|---|
| Monday | `1.2.0-alpha.20260323.1` | `1.2.0-alpha.20260323.1` |
| Tuesday | `1.2.0-alpha.20260324.1` | `1.2.0-alpha.20260324.1` |
| Wednesday | `1.2.0-alpha.20260325.1` | `1.2.0-alpha.20260325.1` |

Each of these is a unique version string. In the current model, the deletion heuristic (`SoftDeleteAPIRevisionAsync` on pending non-approved, non-commented, non-matching revisions) provides a partial throttle — previous pending revisions are deleted when a new one arrives. But this only works because the system retains one pending revision at a time; it doesn't address the fundamental waste.

**Root Cause:** The proposed version-centric model assigns one `APIVersion` per unique `VersionIdentifier`. If `VersionIdentifier` is the full semantic version string, each daily alpha build creates a **new `APIVersion` entity** — defeating the goal of reducing entity and storage churn. Over a quarter, a single active package would accumulate ~90 `APIVersion` records for builds that are functionally indistinguishable from each other (same major.minor.patch target, same prerelease channel, only the date stamp differs).

**Impact:**
- **Entity proliferation:** Hundreds of `APIVersion` records per package per year, with no meaningful semantic distinction between most of them.
- **Continued storage waste:** Each daily build with even a minor API change produces its own blobs. Over a quarter, a single active package could accumulate ~90 sets of blob artifacts for builds that are functionally interchangeable.
- **Noisy version list:** The UI would show a long tail of alpha versions that no human ever reviewed or cared about individually.

This problem is specific to **rolling prerelease channels** — automated builds that produce a new version string on every CI run, with no explicit human decision to "cut a new prerelease." It does not affect explicitly versioned prereleases like `1.2.0-beta.1`, `1.2.0-beta.2`, which are intentional milestones.

---

## 4. Proposed Architecture: Version-Centric Model

### 4.1 New Entity: APIVersion

Introduce `APIVersion` as a **first-class entity** that sits between `Review` and `APIRevision`:

```
Review (1 per package+language)
  ├── APIVersion (Kind=Stable/Preview: 1 per unique version label — v12.1.0, v12.2.0-beta.1, etc.)
  │    └── APIRevision (1+ per version: uploads, re-parses)
  ├── APIVersion (Kind=RollingPrerelease: 1 per normalized channel — v1.2.0-alpha, v1.2.0-dev, etc.)
  │    └── APIRevision (1+ per channel: each daily build with a changed API surface)
  └── APIVersion (Kind=PullRequest: 1 per PR, identified by PR number)
       └── APIRevision (1+ per PR: each push to the PR branch)
```

**Manual uploads** are not a separate `VersionKind`. A manual upload follows the same flow as any other upload: the incoming `PackageVersion` is run through `NormalizeVersion()`, the corresponding `APIVersion` is found or created, and a new `APIRevision` is added under it with `APIRevisionType = Manual`. The revision type is retained for provenance (distinguishing human-uploaded artifacts from CI-generated ones in the revision history) but does not affect version identity, comment scoping, or approval inheritance. If the uploaded file has no `PackageVersion` (e.g., a raw `.api.json` without metadata), it falls through to the versionless handling described in [§7.3 Versionless Revisions (JavaScript)](#73-versionless-revisions-javascript).

**Samples** are version-scoped: sample code for v12.1.0 may differ from v12.2.0 when the API surface changes. `SamplesRevisionModel` gains an `APIVersionId` FK; all revisions within a version share the same samples.

**Cosmos DB Container:** `Versions`, partitioned by `ReviewId`.

```csharp
public class APIVersionModel : BaseListitemModel
{
    // Identity
    public string ReviewId { get; set; }               // FK to parent Review
    public string VersionIdentifier { get; set; }      // Normalized version ("12.2.0", "12.2.0-alpha") or PR identifier ("PR#1234")
    public VersionKind Kind { get; set; }               // Stable, Preview, RollingPrerelease, PullRequest

    // Latest raw package version — computed from the most recent revision.
    // Not stored; resolved at read time. For RollingPrerelease this is the full daily stamp
    // (e.g. "1.2.0-alpha.20260324.1"), while VersionIdentifier stays "1.2.0-alpha".
    public string LatestPackageVersion { get; }

    // PR-specific (null for Stable/Preview or Prerelease Alpha)
    public int? PullRequestNumber { get; set; }         // GitHub PR number
    public string SourceBranch { get; set; }            // e.g. "feature/add-widget"
    public PullRequestStatus? PRStatus { get; set; }    // Open, Merged, Closed (null for non-PR)

    // Approval (explicit, auditable)
    public bool IsApproved { get; set; }
    public HashSet<string> Approvers { get; set; } = new();
    public string ApprovalInheritedFromVersionId { get; set; } // null = human-approved
    public DateTime? ApprovalDate { get; set; }

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
    Stable,             // Stable release (12.2.0)
    Preview,            // Explicit prerelease milestone (12.3.0-beta.1, 12.3.0-rc.1) or sub-1.0.0 version (0.5.0, 0.9.1)
    RollingPrerelease,  // Daily CI prerelease channel (12.3.0-alpha — normalized from 12.3.0-alpha.20260323.1)
    PullRequest         // PR preview — one per PR, archived on PR close
}

public enum PullRequestStatus
{
    Open,    // PR is active — revisions are being added
    Merged,  // PR was merged — retain longer, may have historical value
    Closed   // PR was closed without merging — changes rejected, eligible for early cleanup
}
```

### 4.2 Updated Entity Hierarchy

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
│    PullRequestNumber, SourceBranch, PRStatus (PR)    │
└───────────┬─────────────────────────────────────────┘
            │ 1:N
┌───────────▼─────────────────────────────────────────┐
│ Cosmos DB Container: APIRevisions (existing)        │
│ Partition Key: /ReviewId                            │
│                                                     │
│  APIRevisionListItemModel                           │
│    Id, ReviewId, APIVersionId (NEW FK)               │
│    Files[] (includes ContentHash per file),           │
│    APIRevisionType, Label, ...                       │
│    (IsApproved DEPRECATED → lives on APIVersion)     │
└───────────┬─────────────────────────────────────────┘
            │
┌───────────▼─────────────────────────────────────────┐
│ Cosmos DB Container: Comments (existing)            │
│ Partition Key: /ReviewId                            │
│                                                     │
│  CommentItemModel                                   │
│    Id, ReviewId, APIVersionId (NEW FK)               │
│    APIRevisionId (DEPRECATED), ElementId, ThreadId    │
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

### 4.3 Version Normalization for Rolling Prereleases

Daily CI builds for Java and C# produce version strings with embedded date stamps (e.g., `1.2.0-alpha.20260323.1`). If `VersionIdentifier` used the full version string, each daily build would create a new `APIVersion` — producing hundreds of entities per package per year with no meaningful semantic distinction ([§3.6](#36-daily-alphadev-version-churn)).

**Solution:** Normalize rolling prerelease versions to a **channel identifier** that strips date/build-specific suffixes. Daily builds within the same prerelease channel map to the **same `APIVersion`**; each build becomes a new `APIRevision` under that version — exactly like a re-upload of the same version.

**Normalization rules:**

| Raw `PackageVersion` | Normalized `VersionIdentifier` | `VersionKind` | Rationale |
|---|---|---|---|
| `1.2.0` | `1.2.0` | `Stable` | Stable release — no normalization needed |
| `0.5.0` | `0.5.0` | `Preview` | Sub-1.0.0 — treated as prerelease (pre-stable by SemVer convention) |
| `0.9.1` | `0.9.1` | `Preview` | Sub-1.0.0 — treated as prerelease even without a prerelease suffix |
| `1.2.0-beta.1` | `1.2.0-beta.1` | `Preview` | Explicit prerelease milestone — keep as-is |
| `1.2.0-beta.2` | `1.2.0-beta.2` | `Preview` | Different milestone from beta.1 |
| `1.2.0-alpha.20260323.1` | `1.2.0-alpha` | `RollingPrerelease` | Daily alpha — strip date stamp |
| `1.2.0-alpha.20260324.1` | `1.2.0-alpha` | `RollingPrerelease` | Same channel as above → same `APIVersion` |
| `1.2.0-dev.20260323` | `1.2.0-dev` | `RollingPrerelease` | Daily dev build — strip date stamp |

**Normalization algorithm:**

The `NormalizeVersion(packageVersion)` function:
1. Parses the version string as a semantic version. Unparseable strings fall back to `Preview`.
2. Versions with no prerelease suffix are classified as `Stable` (or `Preview` if sub-1.0.0).
3. For prerelease versions, the prerelease label is split into dot-separated identifiers. If the second identifier is a date stamp (8-digit `YYYYMMDD` format), the version is a rolling build — normalize to just the channel prefix (e.g., `1.2.0-alpha`), classified as `RollingPrerelease`.
4. All other prerelease versions (explicit milestones like `beta.1`, `rc.1`) pass through unchanged as `Preview`.

**How rolling prereleases flow through the system:**

1. **Day 1:** CI uploads `1.2.0-alpha.20260323.1`. `NormalizeVersion` returns `("1.2.0-alpha", RollingPrerelease)`. No existing `APIVersion` found for `VersionIdentifier = "1.2.0-alpha"` → create new `APIVersion` + first `APIRevision`.

2. **Day 2:** CI uploads `1.2.0-alpha.20260324.1`. `NormalizeVersion` returns `("1.2.0-alpha", RollingPrerelease)`. Existing `APIVersion` found → create a new `APIRevision` under it. The new revision records the full `PackageVersion` (`1.2.0-alpha.20260324.1`) for traceability. The previous revision can be soft-deleted (same pending-revision cleanup as today) or retained based on the existing retention heuristics.

3. **Day 90:** After 90 daily builds, the `APIVersion` for `1.2.0-alpha` has accumulated revisions over time. The existing pending-revision cleanup heuristics (soft-deleting older pending non-approved, non-commented revisions when a new one arrives) keep the active set small.

**`LatestPackageVersion` traceability:** The `APIVersionModel.LatestPackageVersion` property is computed from the most recent revision's `Files[0].PackageVersion` — it is not a stored field. For the `1.2.0-alpha` channel this resolves to the full daily stamp (e.g., `1.2.0-alpha.20260324.1`), preserving traceability while `VersionIdentifier` (`1.2.0-alpha`) remains stable for grouping.

**What happens when the channel "graduates":** When `1.2.0-alpha` eventually becomes `1.2.0-beta.1` or `1.2.0`, those are distinct `VersionIdentifier` values → new `APIVersion` entities. The approval inheritance policy ([§4.6 Explicit Approval Inheritance](#46-explicit-approval-inheritance)) separately governs whether approval is copied across versions based on the transition kind. The rolling prerelease version (`1.2.0-alpha`) can then be archived or soft-deleted after the stable release ships.

**Language-specific patterns:** The normalization is designed to be language-agnostic — it detects date stamps structurally. Known rolling prerelease patterns covered:

| Language | Example Raw Version | Normalized |
|---|---|---|
| C# (.NET) | `1.2.0-alpha.20260323.1` | `1.2.0-alpha` |
| Java | `1.2.0-alpha.20260323.1` | `1.2.0-alpha` |
| Python | `1.2.0a20260323001` | *(see note)* |
| JS/TS | `1.2.0-alpha.20260323.1` | `1.2.0-alpha` |

> **Note on Python:** Python uses PEP 440 versioning (e.g., `1.2.0a20260323001`) which does not use dot-separated prerelease identifiers. The Python language service should normalize to SemVer before calling `NormalizeVersion`, or a Python-specific rule can be added that detects `aNNNNNNNN` suffixes. This is a language-service concern, not a core model change.

### 4.4 PR Versions

Each pull request is modeled as its own `APIVersion` with `Kind = PullRequest`. This treats PRs as a first-class unit with a clear lifecycle, scoped comments, and clean deletion semantics.

**Identity & Deduplication:**
- `VersionIdentifier` is set to `"PR#1234"` (the GitHub PR number).
- `PullRequestNumber` and `SourceBranch` provide lookup keys.
- `LatestPackageVersion` is computed from the most recent revision's `Files[0].PackageVersion`. This ensures the PR version always shows which package version it would produce (e.g., "PR#1234 — 12.3.0-beta.1").
- When a new push arrives for an existing PR, the system finds the existing `APIVersion` by `ReviewId + PullRequestNumber` and creates a new `APIRevision` under it — no new version is created.

**Lifecycle:**

| Event | Action |
|---|---|
| PR opened / first push | Create `APIVersion` with `Kind = PullRequest`, `PRStatus = Open`; create first `APIRevision` |
| Subsequent push to PR | Create new `APIRevision` under existing PR version |
| PR merged (API unchanged) | **Promote** the PR `APIVersion` in-place: set `Kind` → `Stable`/`Preview` (per `NormalizeVersion`), update `VersionIdentifier` from `"PR#1234"` → `"1.1.0"`, set `PRStatus = Merged`. `PullRequestNumber` and `SourceBranch` are retained for provenance. The `APIVersionId` (which comments are keyed to) does not change, so **all PR review comments survive into the release version**. No new blobs are created — the promoted version reuses the PR's existing artifacts. |
| PR merged (API changed) | Post-merge CI build produces a different `ContentHash` than the PR's latest revision (e.g., merge-conflict resolution, CI delta). Create a new `Stable`/`Preview` `APIVersion` through the normal automatic flow. Set `PRStatus = Merged` on the PR version — it follows the standard merged-PR retention path. |
| PR closed without merge | Set `PRStatus = Closed` — eligible for shorter TTL (changes were rejected) |
| Archive TTL expires | Cascade-delete the `APIVersion`, all its `APIRevision` entries, all its `Comment` entries, and all associated blobs (only applies to non-promoted PR versions) |

**Why this works:**
- **Comment isolation:** PR review comments stay scoped to the PR version. They never leak into release versions or other PRs. When the PR version is deleted, its comments are deleted with it — no orphans.
- **Clean deletion:** Today, PR revision cleanup (`ClosePullRequestAPIRevision`, auto-archive) must carefully handle comments to avoid orphaning. With the version-centric model, deleting a PR version is a single cascade operation: delete version → delete revisions → delete comments → delete blobs.
- **No version guessing:** The system doesn't need to know which release version the PR targets. The PR version is self-contained. When the PR merges and CI builds the merge commit, the system compares ContentHash to decide whether to promote or create a new version.
- **Promotion preserves review context:** When the post-merge API surface matches the PR's latest revision (`ContentHash` match), the PR version is promoted to `Stable`/`Preview` in-place. The `APIVersionId` is unchanged, so all review comments, approval status, and Copilot review results survive into the release version — no data is orphaned or duplicated. This is the common case: most PRs merge cleanly without API-surface changes.
- **Eliminates post-merge blob duplication ([#8634](https://github.com/Azure/azure-sdk-tools/issues/8634)):** Promotion reuses the PR's existing blobs under the same revision IDs. No duplicate upload occurs. Today, both the PR revision and the post-merge revision store identical blobs indefinitely; with promotion, there is only one set of blobs and one `APIVersion` entity.
- **Graceful fallback for API divergence:** When the merge commit produces a different API surface (merge-conflict resolution, CI delta, squash changes), the system creates a new `Stable`/`Preview` version through the normal automatic flow. The PR version is retained under merged-PR retention and eventually cleaned up. This path is uncommon but handled cleanly.
- **UI grouping:** The version list can separate PR versions from release versions, giving users a clean view of both in-flight PRs and the release timeline.

### 4.5 Comment Scoping

Comments are scoped to `APIVersionId` instead of just `ReviewId`.

The comment query is updated to fetch comments by both `ReviewId` and `APIVersionId`. Comments are already scoped to the version, so the filter simplifies to returning all comments matching the active version — no cross-version "unresolved" logic is needed.

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

### 4.6 Explicit Approval Inheritance

Approval lives on `APIVersion`, not `APIRevision`.

#### 4.6.1 Per-Language Approval Inheritance Policy

Different version transitions carry different semantic weight. Since different language ecosystems have different release conventions, the policy is **configurable per language** (in `appsettings.json`). Languages not listed fall back to `Default`.

| Transition | Example | Default | Configurable? | Rationale |
|---|---|---|---|---|
| Prerelease → Prerelease | `1.2.0-alpha.1` → `1.2.0-beta.1`, `0.1.0` → `0.2.0` | **Automatic** | Yes | Prerelease approvals carry low ceremony. The stable boundary is the meaningful gate. |
| Prerelease → Stable | `1.2.0-beta.3` → `1.2.0`, `0.9.0` → `1.0.0` | **Explicit approval** | Yes | A stable release is a milestone — architects should explicitly sign off on the stable readiness decision. |
| Stable → Prerelease | `1.0.0` → `1.1.0-beta.1`, `1.2.0` → `2.0.0-alpha.1` | **Explicit approval** | No | Prerelease versions do not require approval, so inheriting stable approval into a prerelease is meaningless. A new prerelease cycle starts unapproved; approval is only meaningful when it eventually promotes to stable. |
| Stable → Stable (patch) | `1.2.0` → `1.2.1` | **Automatic** | Yes | Patch releases with identical API surfaces are implementation-only fixes. |
| Stable → Stable (minor) | `1.2.0` → `1.3.0` | **Explicit approval** | Yes | A minor bump signals new functionality — warrants a human checkpoint. |
| Stable → Stable (major) | `1.2.0` → `2.0.0` | **Explicit approval** | Yes | Major bumps may reflect branding, behavioral, or dependency shifts ([#9595](https://github.com/Azure/azure-sdk-tools/issues/9595)). |

#### 4.6.2 Approval Flow

| Scenario | `IsApproved` | `ApprovalInheritedFromVersionId` | UI Display |
|---|---|---|---|
| Human approved v12.1.0 | `true` | `null` | ✅ Approved by @reviewer |
| Bot copied approval to v12.1.1 (patch, same API) | `true` | `"12.1.0"` | ✅ Approved (inherited from v12.1.0) |
| v12.2.0-beta.1 → v12.2.0-beta.2 (prerelease→prerelease, same API) | `true` | `"12.2.0-beta.1"` | ✅ Approved (inherited from v12.2.0-beta.1) |
| 0.5.0 → 0.6.0 (sub-1.0.0→sub-1.0.0, same API) | `true` | `"0.5.0"` | ✅ Approved (inherited from 0.5.0) |
| v12.1.0 → v12.2.0-beta.1 (stable→prerelease) | `false` | `null` | ⏳ Pending review |
| PR#1234 approved, merged, promoted to v1.1.0 (same API) | `true` | `null` | ✅ Approved by @reviewer (original PR approval — same entity, promoted) |
| PR#1234 not approved, merged, promoted to v1.1.0 (same API) | `false` | `null` | ⏳ Pending review (PR was not approved before merge) |
| PR#1234 approved, merged, API differs post-merge → new v1.1.0 created | `false` | `null` | ⏳ Pending review (API surface changed; PR approval does not transfer) |
| v12.2.0-beta.1 → v12.2.0 stable (same API, policy blocks) | `false` | `null` | ⏳ Pending review |
| 0.9.0 → 1.0.0 (sub-1.0.0→stable, policy blocks) | `false` | `null` | ⏳ Pending review |
| v1.0.0 → v2.0.0 (same API, policy blocks) | `false` | `null` | ⏳ Pending review |
| New version, no matching API | `false` | `null` | ⏳ Pending review |

**Auto-approval flow (replaces current `CopyApprovalFromAsync`):**

When a new version is created, the system:
1. **PR-merge promotion (preferred):** If the post-merge CI build's `ContentHash` matches a recently merged `PullRequest` version's latest revision, **promote** that PR version to `Stable`/`Preview` instead of creating a new version ([§4.4 PR Versions](#44-pr-versions)). The approval status (whether approved or not) carries over as-is — no inheritance is involved because it is the same entity. If no matching merged PR version exists (or the hashes differ), fall through to step 2.
2. Compares the new version's latest revision's `ContentHash` against existing approved versions' latest revision hashes
3. If a match is found, classifies the version transition using the rules above
4. Checks the per-language `ApprovalInheritancePolicy` to determine if auto-inheritance is allowed for that transition kind
5. If allowed, copies approval and records `ApprovalInheritedFromVersionId` for audit trail
6. If not allowed (e.g., prerelease→stable, stable→prerelease, major bump), leaves the version as pending review — identical API surface but policy requires explicit sign-off

**Benefits:**
- **O(1) comparison** via revision-level `ContentHash` instead of downloading blobs
- **Clear audit trail** — `ApprovalInheritedFromVersionId` makes the provenance visible
- **Revocable inheritance** — Un-approving the source version can cascade to inherited versions if desired
- **UI distinction** — The frontend can show "inherited" approvals differently from human approvals
- **Per-language policy** — Each language ecosystem can tune which transitions auto-inherit approval and which require explicit review, configured in `appsettings.json` without code changes
- **Major version gate ([#9595](https://github.com/Azure/azure-sdk-tools/issues/9595))** — Auto-inheritance is blocked by default across major version boundaries, ensuring architect review of every major bump regardless of API surface changes
- **Stable gate** — Both prerelease-to-stable and stable-to-prerelease transitions always require explicit approval. Prerelease versions do not require approval, so inheriting stable approval into a prerelease is meaningless; approval is only meaningful when a version eventually promotes to stable.
- **Free-flowing prerelease approvals** — Prerelease-to-prerelease transitions (including sub-1.0.0 versions) auto-inherit approval by default, avoiding unnecessary friction for alpha/beta/rc iterations while the stable boundary remains the meaningful checkpoint.
- **PR-merge promotion** — When a PR is merged and the post-merge API surface matches, the PR version is promoted to `Stable`/`Preview` in-place — no new version is created, no inheritance is needed. The approval (if any), comments, and revision history are preserved on the same entity. If the API surface differs post-merge, a new version is created through the normal flow and standard approval inheritance applies.

### 4.7 O(1) Sameness Checks via Content Hash

The current `AreAPIRevisionsTheSame` method is expensive: it downloads the blob from Azure Storage and performs a full content comparison (tree diff or line-by-line `SequenceEqual`).

With revision-level `ContentHash`, most comparisons become a simple string equality check against the stored hash. The hash is computed once at upload time and stored on `APICodeFileModel` (within the revision). To check a version's current API surface, read its latest revision's `ContentHash`. The expensive blob-based comparison becomes a fallback only for hash collisions (extremely rare with SHA-256).

> **Why the hash lives on the revision, not the version:** An `APIVersion` can have multiple revisions with different API surfaces — most notably `RollingPrerelease` versions (where each daily build with API changes creates a new revision) and `PullRequest` versions (where each push to the PR branch can change the API surface — that's the whole point). A version-level hash would need to be updated on every revision add, creating a race-prone denormalized field. Keeping the hash on the revision (where it's immutable once computed) is simpler and correct. The version's "content hash" is always just its latest revision's `ContentHash`.

> **Partially implemented:** [PR #14704](https://github.com/Azure/azure-sdk-tools/pull/14704) (merged, fixes [#14698](https://github.com/Azure/azure-sdk-tools/issues/14698)) added SHA-256 `ContentHash` to `APICodeFileModel` and updated `AreAPIRevisionsTheSame` to use O(1) hash comparison with lazy backfill for legacy revisions. This was scoped to the existing revision-level model (not the proposed `APIVersionModel`) as an immediate performance fix for `UploadAutoReview` timeouts. The carry-forward loop was also tightened to filter candidates by `IsApproved || HasAutoGeneratedComments` and break early. The remaining proposal items (approval audit trail, major-version guard) are not yet implemented and depend on the `APIVersion` entity.

### 4.8 Retention Policy

With the version-centric model, explicit retention rules replace the ad-hoc deletion heuristics scattered across the current codebase. Retention is the primary mechanism for managing storage — rather than deduplicating blobs, the system ensures that superseded versions and revisions are cleaned up on a predictable schedule while preserving the data that matters for audit, approval, and historical comparison.

#### 4.8.1 Current Retention Behavior (Inventory)

The existing system has several independent retention mechanisms with no unified policy:

| Mechanism | Location | What It Does | Retention Period |
|---|---|---|---|
| **Auto-archive (Manual revisions)** | `APIRevisionsManager.AutoArchiveAPIRevisions` | Soft-deletes `Manual` revisions not updated in N months. Preserves the last approved stable and the last preview revision per review. | **4 months** idle (`ArchiveReviewGracePeriodInMonths`) |
| **Auto-purge (hard delete)** | `APIRevisionsManager.AutoPurgeAPIRevisions` | Hard-deletes soft-deleted `Manual` and `PullRequest` revisions (and their blobs) after N months. Does **not** purge `Automatic` revisions. | **6 months** after soft-delete (`PurgeReviewGracePeriodInMonths`) |
| **PR cleanup** | `PullRequestManager.CleanupPullRequestData` | Soft-deletes PR revisions whose GitHub PR has been closed for N days — but only if the revision has **no approvers**. | **30 days** after PR close (`pull-request-review-close-after-days`) |
| **Automatic revision supersession** | `AutoReviewService.CreateAutomaticRevisionAsync` | When a new `Automatic` revision is created, soft-deletes previous automatic revisions — unless they are approved, released, have comments, or have a matching content hash. | Immediate (on new upload) |
| **Background schedule** | `ReviewBackgroundHostedService` | Runs archive and purge loops every **6 hours**. | — |

**Gaps in the current system:**
- `Automatic` revisions are **never hard-deleted** — the auto-purge explicitly excludes them. Over time, soft-deleted automatic revisions accumulate in Cosmos DB.
- The "keep last approved stable + last preview" logic in auto-archive only applies to `Manual` revisions. Automatic revisions rely solely on the supersession heuristic.
- PR revisions with approvers are **never cleaned up**, even long after the PR is closed and merged.
- There is no version-level retention — all decisions are made per-revision with no awareness of which version a revision belongs to.
- Blob cleanup only happens during hard-delete (purge). Soft-deleted revisions retain their blobs indefinitely until purge runs.

#### 4.8.2 Proposed Retention Policy

The version-centric model enables retention rules at **two levels**: version-level (which `APIVersion` entities to keep) and revision-level (which `APIRevision` entries to keep within a version). Both levels contribute to storage management.

**Version-level retention:**

| Version Kind | Condition | Retention | Rationale |
|---|---|---|---|
| **Stable** | Approved or released | **Indefinite** | Stable versions are the canonical API record. In practice, virtually all stable versions are approved or released. |
| **Preview** | Approved or released | **Indefinite** | Approved or released preview milestones are retained for audit and comparison, same as stable. |
| **Preview** | Not approved, not released, superseded by a newer stable version | **3 months** after stable release | Once a stable version ships, earlier unapproved/unreleased previews have diminishing value. Retained briefly for historical diff access. |
| **RollingPrerelease** | Active channel (no stable release yet) | **Indefinite** (but revision-level cleanup manages storage) | The channel itself persists; individual revisions within it are cleaned up aggressively. |
| **RollingPrerelease** | Channel whose major.minor.patch has shipped stable | **30 days** after stable release | Once the stable version ships, the alpha/dev channel is no longer useful. Short grace period for any late references. |
| **PullRequest** | `PRStatus = Open` | **Indefinite** (while PR is open) | Active PRs must retain their version for ongoing review. |
| **PullRequest** | `PRStatus = Merged`, **not promoted** | **60 days** after merge | Only applies when the post-merge API surface differs from the PR's (promotion not possible). Retained longer than closed for historical reference. Promoted PR versions become `Stable`/`Preview` and follow those retention rules instead. |
| **PullRequest** | `PRStatus = Closed` (without merge) | **30 days** after close | Rejected PRs have minimal value. Quick cleanup. |

**Invariant — always retained regardless of age or approval status:**
- The **last stable** version per review (the most recent `Stable` version). This is the baseline for all future comparisons and approval inheritance.
- The **last preview** version per prerelease track (the most recent `Preview` version in each track, e.g., the latest `beta.N`). This is needed so that when a new preview arrives, it can be compared against both the previous preview and the latest stable release. Retained even after a stable version ships for that major.minor.
- Any version that is **released** (`IsReleased = true`). Released versions are permanent records. Note: only `Stable` and `Preview` versions can be released — `RollingPrerelease` and `PullRequest` versions are never marked `IsReleased = true`.

**Revision-level retention (within a version):**

| Scenario | Retention | Rationale |
|---|---|---|
| **Latest revision** per version | **Always retained** | The current API surface for the version. |
| **Approved revision** (the revision that was current when the version was approved) | **Always retained** | Preserves the exact API surface that received approval. |
| **Released revision** (the revision that was current when `IsReleased` was set) | **Always retained** | The exact artifact that shipped. |
| **Superseded revision** (not latest, not approved, not released) | **Soft-delete immediately** on new revision upload; **hard-delete + blob cleanup after 30 days** | Superseded revisions with no anchoring reason are the primary source of storage waste. Aggressive cleanup here is the main storage win. |

**Cascade deletion:** When a version is deleted (retention period expires), all its revisions, comments, and blobs are cascade-deleted in a single operation. This is structurally clean because comments are version-scoped — no orphans.

**Blob cleanup timing:** Unlike the current system where blob cleanup only happens during hard-delete (purge), the proposed model ties blob cleanup to revision hard-delete. When a superseded revision is hard-deleted after its 30-day grace period, its blobs in both `codefiles` and `originals` containers are deleted immediately.

**Configuration:**

All retention periods are configurable in `appsettings.json`:

```jsonc
{
  "RetentionPolicy": {
    "SupersededPreviewMonthsAfterStable": 3,
    "GraduatedRollingPrereleaseDays": 30,
    "MergedPullRequestDays": 60,
    "ClosedPullRequestDays": 30,
    "SupersededRevisionHardDeleteDays": 30
  }
}
```

**How this replaces aliasing/dedup for storage management:**

The original proposal included revision aliasing and blob deduplication as the primary mechanism for controlling storage growth — multiple versions with identical API surfaces would share a single set of blobs via `CanonicalRevisionId`. The retention policy achieves the same storage outcome through a simpler mechanism: instead of sharing blobs, the system **deletes the versions and revisions that no longer need to exist**. This is more effective because:

1. **Aliasing only helped identical surfaces.** Two versions with even a one-line API difference produced full duplicate blobs that aliasing couldn't address. Retention cleanup handles all superseded versions regardless of content similarity.
2. **Simpler data model.** No `CanonicalRevisionId`, no alias promotion on source deletion, no shared-blob reference counting. Each revision owns its own blobs; when the revision is deleted, its blobs are deleted.
3. **Predictable storage profile.** With explicit retention periods, storage usage is bounded and forecastable — the system converges to a steady state where only the retained set (latest + approved + released) persists.
4. **Rolling prereleases benefit most.** A package with 90 daily alpha builds per quarter retains only the latest revision (plus any that were approved or commented). The other 89 sets of blobs are cleaned up within 30 days of supersession — far more effective than aliasing, which would only help if those builds had identical API surfaces.

---

## 5. Migration Plan

### 5.1 Phase 1: Add APIVersion Entity (Non-Breaking)

1. Create `APIVersions` Cosmos DB container
2. Add nullable `APIVersionId` field to `APIRevisionListItemModel` and `SamplesRevisionModel` (backward-compatible — all read paths tolerate `null`). `CommentItemModel` is deferred to Phase 2, where the comment backfill is defined in full.
3. Implement `NormalizeVersion()` on top of the existing `AzureEngSemanticVersion` parser — the parser already extracts `PrereleaseLabel`, `PrereleaseNumber` (8-digit date stamps), and `BuildNumber`, which supplies all the inputs needed for rolling-prerelease detection. `NormalizeVersion` maps a raw `PackageVersion` string to a `(VersionIdentifier, VersionKind)` pair, collapsing daily CI alpha/dev builds to their channel prefix (see [§4.3 Version Normalization for Rolling Prereleases](#43-version-normalization-for-rolling-prereleases)).
4. **Backfill migration — revisions:** For each review, group existing **non-PR** revisions (`APIRevisionType != PullRequest`) by **normalized** `VersionIdentifier` (not raw `PackageVersion`) → create one `APIVersionModel` per unique normalized version per review, and set `APIVersionId` on each revision. Daily alpha builds (e.g., `1.2.0-alpha.20260323.1`, `1.2.0-alpha.20260324.1`) collapse into a single `APIVersion` with `VersionIdentifier = "1.2.0-alpha"` and `Kind = RollingPrerelease`. PR-type revisions are **excluded** from this backfill — they are handled in Phase 3, which groups them by `PullRequestNo` into `Kind = PullRequest` versions. Including PR revisions here would create conflicting version assignments that Phase 3 would need to undo. Revisions with missing or empty `PackageVersion` (common in JavaScript) are handled via content-hash grouping — see [§7.3 Versionless Revisions (JavaScript)](#73-versionless-revisions-javascript).
5. **Backfill migration — samples revisions:** `SamplesRevisionModel` has no existing link to a revision or version — it is purely review-scoped today (`ReviewId` only). For each samples revision, match it to an `APIVersion` by finding the revision within the same review whose `CreatedOn` is closest to (and not after) the samples revision's `CreatedOn`, then adopt that revision's `APIVersionId`. If no plausible match exists (e.g., orphaned samples with no nearby revision), assign to the review's latest stable `APIVersion` as a conservative default.
6. **Wire new-upload path:** Update `AutoReviewService.CreateAutomaticRevisionAsync` (and the manual-upload path) to call `NormalizeVersion()`, find-or-create the `APIVersion`, and set `APIVersionId` on every newly created revision going forward. This ensures all *new* data is version-tagged from this point on, while the backfill (steps 4–5) covers historical data.

> **Note — ContentHash:** `ContentHash` on `APICodeFileModel` is already implemented and deployed ([PR #14704](https://github.com/Azure/azure-sdk-tools/pull/14704)). `AreAPIRevisionsTheSame` uses it as an O(1) fast path with lazy backfill for legacy revisions that lack a hash. No additional work is needed here — Phase 1 depends on `ContentHash` (for versionless-revision grouping and future approval inheritance) but does not need to implement it.

### 5.2 Phase 2: Comment Scoping & Orphan Cleanup

**Prerequisite:** Phase 1 must be complete — all revisions have `APIVersionId`, and the new-upload path sets it on every incoming revision.

1. **Add `APIVersionId` to `CommentItemModel`** (nullable, for backward compat).

2. **Backfill `APIVersionId` on existing comments:** For every comment that has an `APIRevisionId`, look up the revision's `APIVersionId` (populated in Phase 1) and set it on the comment. For comments whose `APIRevisionId` references a deleted/soft-deleted revision, attempt to resolve via the revision's `PackageVersion` → matching `APIVersion`. For comments with a null or unresolvable `APIRevisionId`, assign to the review's latest stable `APIVersion` (same conservative fallback as samples). Delete diagnostic comments whose parent revision no longer exists — diagnostics are machine-generated and revision-specific; orphaned diagnostics have no value.

   > **Why backfill runs first:** Steps 3–6 change the query and filter logic to use `APIVersionId`. If the backfill hasn't run, all existing comments have `APIVersionId = null` and would be invisible. The backfill must complete (or at minimum run as a background job with the query paths tolerating `null` during the transition) before the filter logic switches over.

3. **Update comment creation path:** Update `CommentsController.CreateCommentAsync` and `CommentsManager.AddCommentAsync` to resolve and set `APIVersionId` on every new comment. The frontend already sends `apiRevisionId`; the backend looks up the revision's `APIVersionId` and writes both fields. This ensures all new comments are version-tagged from this point on. `APIRevisionId` continues to be written for backward compatibility and diagnostic traceability.

4. **Add version-scoped query to `CosmosCommentsRepository`:** Add a new `GetCommentsForVersionAsync(reviewId, apiVersionId)` method. The existing `GetCommentsAsync(reviewId)` (review-wide) is **retained** — it is needed for the conversations panel, review-level comment counts, cross-version search, the "show other versions" toggle, and any admin/moderation views. The version-scoped query is the new *default for code-panel rendering*; the review-wide query remains the default for everything else.

5. **Update `ReviewsController.GetReviewContentAsync`:** Replace the current flow (`GetCommentsAsync(reviewId)` → `CommentVisibilityHelper.GetVisibleComments()` → post-filter) with: `GetCommentsForVersionAsync(reviewId, activeApiVersionId)` → pass directly to code panel. The comment-bleed problem ([§3.1](#31-comment-bleed-across-revisions)) is solved by the query itself — no post-filter heuristic needed. The existing `CommentVisibilityHelper.GetVisibleComments()` method on the backend is retired for this path.

6. **Update frontend comment visibility:** Replace the logic in `comment-visibility.helper.ts` (`getVisibleComments`). Today it intentionally shows **all** user and AI comments regardless of revision (only diagnostics are revision-filtered). This is the designed behavior that causes comment bleed — it is not a workaround. Replace it with: show all comments matching the active `apiVersionId`. Diagnostic comments continue to be filtered by `apiRevisionId` within the version (since diagnostics are generated per-upload and reconciled per-revision via `SyncDiagnosticCommentsAsync`). The conversations panel (`conversations.component.ts`) continues to use the review-wide feed for its counts and thread list.

7. **Add "Show comments from other versions" opt-in toggle:** When enabled, the code panel re-fetches using the review-wide `GetCommentsAsync(reviewId)` and renders a visual indicator (e.g., version badge) on comments from non-active versions. This is additive UX on the existing review-wide query path.

8. **Update diagnostic sync:** `SyncDiagnosticCommentsAsync` currently receives the active `APIRevision` and matches diagnostics by `apiRevisionId`. Update it to *also* set `APIVersionId` on newly created diagnostic comments. The reconciliation logic (compare incoming diagnostics against existing, auto-resolve removed, create new) continues to operate per-revision using `DiagnosticsHash` as the short-circuit — but all diagnostic comments are now version-tagged, so the version-scoped query in step 4 picks them up correctly.

9. **Cascade deletion rules:** When an `APIVersion` is deleted, cascade-delete all associated comments (query by `APIVersionId`). When an individual `APIRevision` is replaced within a version, no comment migration is needed — user/AI comments are scoped to the version, and diagnostic comments for the old revision are auto-resolved by the reconciliation in step 8 when the new revision's diagnostics are synced.

10. **Retain `APIRevisionId` on `CommentItemModel`** (do not deprecate yet). It continues to serve three purposes: (a) diagnostic comment reconciliation (`SyncDiagnosticCommentsAsync` matches by revision), (b) traceability for when a comment was created (which specific upload/revision a user was viewing), and (c) backward compatibility with any external consumers reading the Comments container. Deprecation can be revisited once diagnostic sync is fully version-scoped and all read paths have migrated to `APIVersionId`.

### 5.3 Phase 3: PR Version Migration

1. **Backfill:** Group existing PR-type revisions by `PullRequestNo` (or `SourceBranch` where PR number is absent) → create one `APIVersion` per unique PR per review with `Kind = PullRequest`
2. Set `APIVersionId` on each existing PR revision and its associated comments
3. Update `PullRequestManager` to create/find `APIVersion` by PR number on incoming CI pipeline callbacks (via `PullRequestsController.CreateAPIRevisionIfAPIHasChanges`) instead of creating bare `APIRevision` entries
4. **Implement promotion-on-merge:** When the post-merge CI build arrives, compare its `ContentHash` against the merged PR version's latest revision. If they match, promote the PR `APIVersion` in-place (`Kind` → `Stable`/`Preview`, update `VersionIdentifier`, set `PRStatus = Merged`). If they differ, create a new `Stable`/`Preview` version through the normal automatic flow and set `PRStatus = Merged` on the PR version. See [§4.4 PR Versions](#44-pr-versions) for the full promotion semantics.
5. Set `PRStatus = Merged` or `Closed` on non-promoted PR versions based on current GitHub PR state
6. Apply differentiated archive TTLs — shorter for `Closed` (rejected), longer for `Merged` (non-promoted only) — then cascade-delete after retention period

### 5.4 Phase 4: Approval Migration

1. Move `IsApproved` / `Approvers` from `APIRevisionListItemModel` to `APIVersionModel`
2. Set `ApprovalInheritedFromVersionId` for versions that had bot-copied approvals (detectable from `ChangeHistory` notes containing "Approval copied from")
3. Add `ApprovalInheritancePolicy` configuration to `appsettings.json` with default policy and any language-specific overrides
4. Implement `ClassifyTransition()` and wire it into the auto-approval flow, replacing the existing `CarryForwardRevisionDataAsync` / `ApplyApprovalFrom` logic
5. Update `ToggleAPIRevisionApprovalAsync` to operate on `APIVersionModel`
6. Update UI to show inherited vs. human approval, including the transition kind that triggered inheritance (e.g., "inherited from v12.1.0 — patch bump")

---

## 6. Summary of Benefits

| Problem | Current State | Proposed State |
|---|---|---|
| **Comment bleed** | Unresolved comments from all versions appear everywhere | Comments scoped to version; clean per-version view |
| **Version ambiguity** | Flat list of revisions, no version grouping; CI upload for one version can silently delete another ([#5186](https://github.com/Azure/azure-sdk-tools/issues/5186), [#10105](https://github.com/Azure/azure-sdk-tools/issues/10105)) | Explicit `APIVersion` entity; each version owns its own revisions, so cross-version replacement is structurally impossible |
| **Opaque approval** | Silent copy, no UI distinction | `ApprovalInheritedFromVersionId` with clear UI; per-language configurable policy controls which version transitions allow auto-inheritance (prerelease→prerelease, prerelease→stable, patch, minor, major); defaults allow prerelease-to-prerelease and stable patch bumps; stable→prerelease always blocked ([#9595](https://github.com/Azure/azure-sdk-tools/issues/9595)) |
| **Duplicate storage** | Full blob duplication per version | Rolling prereleases normalized to one `APIVersion` per channel; PR-to-release promotion eliminates post-merge blob duplication ([#8634](https://github.com/Azure/azure-sdk-tools/issues/8634)); explicit [retention policy](#48-retention-policy) deletes superseded versions and revisions on a predictable schedule, with blob cleanup on revision hard-delete. O(1) `ContentHash` comparison enables fast sameness checks without blob downloads |
| **Expensive sameness checks** | O(1) `ContentHash` fast path ([PR #14704](https://github.com/Azure/azure-sdk-tools/pull/14704)) with blob-download fallback for legacy revisions lacking a hash | O(1) `ContentHash` comparison universally — legacy fallback eliminated once backfill completes; version-level approval inheritance uses hash directly instead of per-revision carry-forward loop |
| **Comment orphaning** | Comments reference deleted revisions; frontend workarounds mask broken data | Comments scoped to `APIVersion`; revision replacement cannot orphan comments; cascade delete on version removal |
| **PR lifecycle** | PR revisions mixed into flat revision list; cleanup orphans comments | Each PR is its own `APIVersion`; on merge, promoted to `Stable`/`Preview` if API unchanged (preserving comments + approval) or retained under merged-PR TTL if API differs; close → archive → cascade-delete |
| **Version lookup** | In-memory filter across all revisions | Direct query on `APIVersions` container |
| **Daily alpha churn** | Each daily CI build creates a new entity with unique version; deletion heuristics partially mitigate | Rolling prereleases normalized to channel (`1.2.0-alpha`); daily builds become revisions within one `APIVersion`; existing cleanup heuristics continue to manage storage |
| **No unified retention** | Ad-hoc deletion heuristics scattered across archive, purge, PR cleanup, and supersession — with gaps (automatic revisions never hard-deleted, no version-level awareness) | Explicit retention policy at both version and revision level; configurable periods; always-retain invariants for last approved stable, last preview, and released versions; cascade deletion on version expiry |

---

## 7. Additional Considerations

### 7.1 Carrying Comments Forward Across Versions

Today, unresolved comments bleed across all versions implicitly. Once comment scoping is enforced, that implicit behavior goes away — which is the desired outcome for the vast majority of cases. The overwhelming pattern is that old comments are irrelevant to newer versions; version-scoped comments are the correct default.

There is a legitimate (if rare) scenario where a reviewer intentionally wants a comment to persist into future versions — for example, flagging an API surface for re-review at the next major version increase. This can be addressed separately (e.g., a "Defer to next major version" resolution type on comment threads). It is not a blocker for version-scoped comments and is tracked outside this proposal.

### 7.2 Copilot Automation Strategy

The version-centric model enables a clear, well-scoped policy for when Copilot reviews are triggered automatically. The guiding principle is: **one automatic Copilot review per `APIVersion`, and only for stable versions.**

#### 7.2.1 Rules

| Scenario | Automatic Copilot Review? | Rationale |
|---|---|---|
| New `APIVersion` with `Kind = Stable` | **Yes** | Every stable version should receive an automatic Copilot review. This is the primary trigger. |
| PR version (`Kind = PullRequest`) where `PackageVersion` resolves to a stable version | **Yes** | If a PR will produce a stable release, Copilot feedback is valuable before merge. |
| PR version (`Kind = PullRequest`) where a beta `PackageVersion` is bumped to stable (e.g., `1.2.0-beta.3` → `1.2.0`) | **Yes** — triggered on the revision where the version becomes stable | The promotion to stable is the semantic event that warrants Copilot review, regardless of how many prior beta revisions existed. |
| `Kind = Preview` (explicit prerelease milestone: `beta.1`, `rc.1`, etc.) | **No** | Preview versions are iterative milestones; Copilot review is deferred until the version reaches stable. Reviewers can still manually request a Copilot review on any preview version. |
| `Kind = RollingPrerelease` (daily alpha/dev channel) | **Never** | Alpha channels are high-churn, low-ceremony CI artifacts. Automatic Copilot reviews on these would generate noise with no actionable value. |

#### 7.2.2 Enforcement: One Review Per APIVersion

The `IsReviewedByCopilot` field on `APIVersionModel` tracks whether an automatic Copilot review has already been triggered for this version. The flow is:

1. **On new revision upload or version creation:** Check `IsReviewedByCopilot` on the `APIVersion`.
2. **If `false` and the trigger conditions above are met:** Enqueue the Copilot review job. Set `IsReviewedByCopilot = true`.
3. **If `true`:** Skip. The automatic review has already been performed for this version.

This guarantees at most **one** automatic Copilot review per `APIVersion` object, regardless of how many revisions are uploaded within that version. Subsequent re-uploads, re-parses, or PR pushes do not re-trigger the review.

#### 7.2.3 PR Version-Bump Detection

For PR versions, the trigger is not based on the version at PR creation time but on each incoming revision's `PackageVersion`. On each new PR revision upload, the system normalizes the version and checks whether it resolves to `Stable`. If so (and `IsReviewedByCopilot` is still `false`), the Copilot review is enqueued. This handles the case where a PR starts as `1.2.0-beta.3` and is bumped to `1.2.0` mid-flight — the Copilot review triggers on the first revision that resolves to a stable version.

#### 7.2.4 Manual Overrides

Reviewers can always manually request a Copilot review on any `APIVersion` regardless of kind — including preview and rolling prerelease versions. The `IsReviewedByCopilot` flag only governs **automatic** triggering. Manual requests do not set this flag (they are tracked separately via `ReviewRequestIds`), so a manual Copilot review on a preview version does not prevent the automatic review from firing if/when that version is later promoted to stable under a new `APIVersion`.

### 7.3 Versionless Revisions (JavaScript)

Some JavaScript/TypeScript revisions in the existing data have no `PackageVersion` — the field is empty or null. This is a historical artifact of how the JS language service submitted token files. Without a version string, `NormalizeVersion()` cannot produce a `VersionIdentifier`, and the Phase 1 backfill cannot group these revisions by version.

**Scale of the problem:** This primarily affects older JavaScript revisions. Newer JS uploads populate `PackageVersion` correctly, so this is a migration-only concern — not an ongoing pipeline issue.

**Migration strategy:**

1. **Attempt label extraction.** Many versionless revisions have a `Label` field that contains version-like information (e.g., a branch name like `release/storage-blob_12.5.0` or a free-text label like `v12.5.0`). Parse `Label` with a best-effort regex to extract a version string. If successful, normalize it through the standard `NormalizeVersion()` path.

2. **Content-hash grouping.** For revisions where label extraction fails, compute `ContentHash` from the revision's blob (or use the existing `ContentHash` if already populated per PR #14704). Group versionless revisions with identical content hashes into the same `APIVersion`. This leverages API sameness as a proxy for version identity — if two versionless revisions have byte-identical API surfaces, they represent the same logical version.

3. **Group remaining singletons by review.** Versionless revisions that have unique content hashes (no match to any other revision, versioned or not) are each assigned to a synthetic `APIVersion` with:
   - `VersionIdentifier` = `"unknown-{shortHash}"` (first 8 chars of content hash, for human-readability)
   - `Kind` = `Preview` (conservative default — no auto-approval inheritance, no automatic Copilot review)

4. **Merge with versioned content-hash matches.** Before creating synthetic versions, check whether any versionless revision's content hash matches an *existing versioned* `APIVersion` in the same review. If so, assign the versionless revision to that version as an additional `APIRevision` — it represents an upload of the same API surface that simply lacked version metadata. This avoids creating unnecessary synthetic versions.

**Post-migration:** Synthetic `unknown-*` versions appear in the version list with a visual indicator (e.g., "(no version)"). Admins can manually relabel or merge them if the correct version is later identified. No special ongoing code paths are needed — the standard version-centric model handles them like any other version once assigned.

**Going forward:** The JS/TS language service pipeline should be validated to ensure `PackageVersion` is always populated on new uploads. If a new revision arrives with an empty `PackageVersion`, the upload pipeline should reject it or fall back to extracting the version from the token file metadata before creating the `APIVersion` entity.

### 7.4 Version List Display

The UI today presents a single flattened dropdown of all versions and revisions. The version-centric model doesn't change this: **all `APIVersion` entries appear in the list.** Each version is a distinct entity with its own blobs, comments, and approval status.

One refinement worth experimenting with: a display mode that shows only the **latest revision per version** by default. In practice, reviewers almost always want to compare the most recent revision of one version against another released version — not inspect how revision 1 of v12.2.0 differs from revision 3 of the same version. Collapsing to "latest per version" would shorten the dropdown significantly for packages with many re-uploads. Earlier revisions would still be accessible via an expand control or a "Show all revisions" toggle.

This is a UX experiment, not a data model change — the underlying query simply filters to `MAX(CreatedOn)` per `APIVersionId`. It can be shipped (or reverted) independently of any other phase.

### 7.5 PR Comparison Baseline Selection

> **Ref:** [azure-sdk-tools#10105](https://github.com/Azure/azure-sdk-tools/issues/10105)

Today, `PullRequestManager.prHasAPIChanges` compares a PR's code file against **every** automatic revision in the review to decide whether a PR revision should be created. This is the source of #10105: when multiple versions coexist (e.g., a v1.16.1 hotfix and v1.17.0 on `main`), the PR can match against the wrong version and produce a false negative.

The version-centric model makes this straightforward to fix. When a PR arrives, normalize its `PackageVersion` and look up the matching `APIVersion`. Compare only against that version's latest revision. If no matching `APIVersion` exists (first-ever version), there's no baseline — always create the PR revision. The cross-version false-negative edge case goes away because the comparison is naturally scoped to the correct version. This should be updated in Phase 3 alongside the other `PullRequestManager` changes.

### 7.6 Notifications

Notifications are out of scope for this proposal. A separate notification proposal should address version-aware scenarios introduced by this model, including: distinct notification types for "new version uploaded" vs. "new revision within existing version," subscriber filtering by version kind (e.g., stable only, no PRs), and version-level deep-links in emails. `APIVersionModel` provides all the fields needed to support these.

### 7.7 First Release Approval

`ReviewListItemModel.IsApprovedForFirstRelease` is a review-level concept distinct from per-version approval. This field is managed by the **Namespace Approval** system, which is outside the scope of this proposal. The version-centric model does not change or interact with first-release approval — it remains at the `Review` level.
