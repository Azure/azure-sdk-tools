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
5. [Implementation Plan](#5-implementation-plan)
   - 5.1 [Work Item Index](#51-work-item-index)
   - 5.2 [WI-1: APIVersion Entity & Repository Layer](#52-wi-1-apiversion-entity--repository-layer)
   - 5.3 [WI-2: Wire Upload Paths](#53-wi-2-wire-upload-paths)
   - 5.4 [WI-3: Backfill Revisions & Samples](#54-wi-3-backfill-revisions--samples)
   - 5.5 [WI-4: Retention Configuration & RetainUntil Field](#55-wi-4-retention-configuration--retainuntil-field)
   - 5.6 [WI-5: Retention Event Handlers & Background Purge](#56-wi-5-retention-event-handlers--background-purge)
   - 5.7 [WI-6: Retention Backfill](#57-wi-6-retention-backfill)
   - 5.8 [WI-7: Comment FK & Backfill](#58-wi-7-comment-fk--backfill)
   - 5.9 [WI-8: Comment Query Switch & Frontend Update](#59-wi-8-comment-query-switch--frontend-update)
   - 5.10 [WI-9: Cross-Version Comment Toggle](#510-wi-9-cross-version-comment-toggle)
   - 5.11 [WI-10: PR Version Backfill & Status Tracking](#511-wi-10-pr-version-backfill--status-tracking)
   - 5.12 [WI-11: Version-Aware PullRequestManager](#512-wi-11-version-aware-pullrequestmanager)
   - 5.13 [WI-12: PR Promotion-on-Merge](#513-wi-12-pr-promotion-on-merge)
   - 5.14 [WI-13: PR Cleanup via Retention](#514-wi-13-pr-cleanup-via-retention)
   - 5.15 [WI-14: Approval Inheritance Policy & ClassifyTransition](#515-wi-14-approval-inheritance-policy--classifytransition)
   - 5.16 [WI-15: Version-Level Approval Backend](#516-wi-15-version-level-approval-backend)
   - 5.17 [WI-16: GetReviewStatus & Copilot Trigger](#517-wi-16-getreviewstatus--copilot-trigger)
   - 5.18 [WI-17: Approval SPA Update](#518-wi-17-approval-spa-update)
   - 5.19 [Dependency Map & Stable States](#519-dependency-map--stable-states)
   - 5.20 [Rollback Strategy](#520-rollback-strategy)
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

**Current approval distribution:** Today, API approvals are overwhelmingly given on **automatic** revisions — these are the revisions created by CI pipelines, and they are exclusively the revision type that approval is *copied to*. Of all approval-copy events, 95%+ originate *from* other automatic revisions as well. PR revisions account for a low single-digit percentage of approval sources, and manual revisions are even rarer. In other words, the approval-copying system is almost entirely an automatic-to-automatic pipeline: CI uploads a new revision, the system finds a byte-identical approved automatic revision, and copies the approval forward — with no human in the loop at any point in the chain. This makes the opacity of the mechanism particularly consequential: the dominant path through the approval system is fully automated, yet the audit trail and UI present it as indistinguishable from human review.

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

    // Approval (materialized from ReviewSubmissions for query efficiency)
    // Kept in sync: set by submit-review (Decision=Approve) and by system approval inheritance.
    // Source of truth for "is this version approved now"; ReviewSubmissionModel is the audit trail.
    public bool IsApproved { get; set; }
    public HashSet<string> Approvers { get; set; } = new();
    public string ApprovalInheritedFromVersionId { get; set; } // null = human-approved (via ReviewSubmission)
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

    // Retention
    public DateTime? RetainUntil { get; set; }                  // null = retain indefinitely; set when retention clock starts (e.g. stable ships, PR closes)
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

Different version transitions carry different semantic weight. Since different language ecosystems have different release conventions, the policy is **configurable per language** via JSON blobs stored in **Azure App Configuration**. Each language has its own key; languages without a key fall back to `ApprovalPolicy:Default`.

**App Configuration keys:**

| Key | Value |
|---|---|
| `ApprovalInheritancePolicy:Default` | JSON blob (see schema below) |
| `ApprovalInheritancePolicy:Python` | JSON blob (overrides for Python) |
| `ApprovalInheritancePolicy:Java` | JSON blob (overrides for Java) |
| `ApprovalInheritancePolicy:{Language}` | ... |

The `{Language}` segment matches `LanguageService.Name` (e.g., `C#`, `Python`, `Java`, `JavaScript`, `Go`). Each key's value is a JSON object that deserializes to `ApprovalInheritancePolicyOptions`:

```csharp
public class ApprovalInheritancePolicyOptions
{
    public InheritanceRule PrereleaseToPrerelease { get; set; } = InheritanceRule.Automatic;
    public InheritanceRule PrereleaseToStable { get; set; } = InheritanceRule.Explicit;
    public InheritanceRule StableToPrerelease { get; set; } = InheritanceRule.Explicit;
    public InheritanceRule StablePatch { get; set; } = InheritanceRule.Automatic;
    public InheritanceRule StableMinor { get; set; } = InheritanceRule.Explicit;
    public InheritanceRule StableMajor { get; set; } = InheritanceRule.Explicit;
}

public enum InheritanceRule
{
    Automatic,  // Auto-inherit approval when API surface matches
    Explicit    // Always require explicit human approval
}
```

**Example JSON blob** for the default policy (`ApprovalInheritancePolicy:Default`):

```json
{
  "PrereleaseToPrerelease": "Automatic",
  "PrereleaseToStable": "Explicit",
  "StableToPrerelease": "Explicit",
  "StablePatch": "Automatic",
  "StableMinor": "Explicit",
  "StableMajor": "Explicit"
}
```

A language override only needs to specify the fields it differs on — missing fields fall back to the `Default` policy. For example, if Python wants to auto-inherit on stable minor bumps:

```json
{
  "StableMinor": "Automatic"
}
```

At startup, all `ApprovalInheritancePolicy:*` keys are loaded, deserialized, and **cached in memory** as a `Dictionary<string, ApprovalInheritancePolicyOptions>`. The policy for a language is resolved by overlaying the language-specific blob (if any) onto the `Default` blob. The cache is **only invalidated when the App Configuration sentinel is triggered** — individual requests never read from App Configuration directly. This is consistent with how the existing sentinel-based refresh works: the background refresh checks the sentinel key every 5 minutes, and only when it detects a change does it reload all configuration values and rebuild the cached policy dictionary.

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
- **Per-language policy** — Each language ecosystem can tune which transitions auto-inherit approval and which require explicit review, stored as JSON blobs in Azure App Configuration — runtime-tunable without code changes
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
| **Preview** | Not approved, not released, superseded by a newer stable version | **90 days** after stable release | Once a stable version ships, earlier unapproved/unreleased previews have diminishing value. Retained briefly for historical diff access. |
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

**`RetainUntil` — how retention is enforced:**

Both `APIVersionModel` and `APIRevisionListItemModel` carry a nullable `DateTime? RetainUntil` field. `null` means "retain indefinitely" — the entity is not eligible for deletion. A non-null value means "eligible for hard-delete after this timestamp."

`RetainUntil` is **set at the moment the retention clock starts** — not computed on-the-fly during purge. This makes the purge job trivial:

```
SELECT * FROM c WHERE c.RetainUntil != null AND c.RetainUntil < @now
```

No complex eligibility logic runs during purge — it was already evaluated when `RetainUntil` was written.

**When `RetainUntil` is set on versions:**

| Event | Action |
|---|---|
| Stable version ships for a major.minor.patch | Set `RetainUntil = now + 90d` on superseded `Preview` versions for that track; set `RetainUntil = now + 30d` on the `RollingPrerelease` channel for that major.minor.patch |
| PR merged (not promoted) | Set `RetainUntil = now + 60d` on the PR version |
| PR closed without merge | Set `RetainUntil = now + 30d` on the PR version |
| Version becomes approved or released | Clear `RetainUntil` → `null` (retain indefinitely) |
| Version is the last stable or last preview per track | Clear `RetainUntil` → `null` (invariant override) |

**When `RetainUntil` is set on revisions:**

| Event | Action |
|---|---|
| New revision uploaded within a version | Set `RetainUntil = now + 30d` on superseded revisions (not latest, not approved, not released); soft-delete immediately |
| Revision becomes the approved or released snapshot | Clear `RetainUntil` → `null` |

**Recalculation:** If conditions change (e.g., a version that had `RetainUntil` set is subsequently approved), the write path clears `RetainUntil` back to `null`. Conversely, if the retention period config is shortened, a background sweep can update existing `RetainUntil` values — but this is rare and non-urgent since the purge job simply skips items whose `RetainUntil` is still in the future.

**Cascade deletion:** When a version is deleted (its `RetainUntil` has passed), all its revisions, comments, and blobs are cascade-deleted in a single operation. This is structurally clean because comments are version-scoped — no orphans.

**Blob cleanup timing:** Unlike the current system where blob cleanup only happens during hard-delete (purge), the proposed model ties blob cleanup to revision hard-delete. When a superseded revision is hard-deleted after its 30-day grace period, its blobs in both `codefiles` and `originals` containers are deleted immediately.

**Configuration:**

All retention periods are stored as keys in **Azure App Configuration** (the existing config store used by APIView — see `Program.cs`). This keeps them runtime-tunable without code changes or redeployment, and consistent with how all other operational settings are managed. Keys use the `RetentionPolicy:` prefix:

| Key | Default |
|---|---|
| `RetentionPolicy:SupersededPreviewDaysAfterStable` | `90` |
| `RetentionPolicy:GraduatedRollingPrereleaseDays` | `30` |
| `RetentionPolicy:MergedPullRequestDays` | `60` |
| `RetentionPolicy:ClosedPullRequestDays` | `30` |
| `RetentionPolicy:SupersededRevisionHardDeleteDays` | `30` |

All values are in **days**.

Defaults are hardcoded in a `RetentionPolicyOptions` class and bound via `IOptions<RetentionPolicyOptions>`. If a key is absent from App Configuration, the hardcoded default applies. The existing 5-minute sentinel-based refresh ensures changes propagate without restart.

**How this replaces aliasing/dedup for storage management:**

The original proposal included revision aliasing and blob deduplication as the primary mechanism for controlling storage growth — multiple versions with identical API surfaces would share a single set of blobs via `CanonicalRevisionId`. The retention policy achieves the same storage outcome through a simpler mechanism: instead of sharing blobs, the system **deletes the versions and revisions that no longer need to exist**. This is more effective because:

1. **Aliasing only helped identical surfaces.** Two versions with even a one-line API difference produced full duplicate blobs that aliasing couldn't address. Retention cleanup handles all superseded versions regardless of content similarity.
2. **Simpler data model.** No `CanonicalRevisionId`, no alias promotion on source deletion, no shared-blob reference counting. Each revision owns its own blobs; when the revision is deleted, its blobs are deleted.
3. **Predictable storage profile.** With explicit retention periods, storage usage is bounded and forecastable — the system converges to a steady state where only the retained set (latest + approved + released) persists.
4. **Rolling prereleases benefit most.** A package with 90 daily alpha builds per quarter retains only the latest revision (plus any that were approved or commented). The other 89 sets of blobs are cleaned up within 30 days of supersession — far more effective than aliasing, which would only help if those builds had identical API surfaces.

---

## 5. Implementation Plan

The plan is structured as **17 independent work items** grouped into 6 logical areas. Each work item is individually deployable behind a feature flag and leaves the system in a stable state when completed. Where two or more work items must land together to form a meaningful behavior change, this is called out explicitly as a **stable-state gate**.

> **Prerequisite — ContentHash:** `ContentHash` on `APICodeFileModel` is already implemented and deployed ([PR #14704](https://github.com/Azure/azure-sdk-tools/pull/14704)). `AreAPIRevisionsTheSame` uses it as an O(1) fast path with lazy backfill for legacy revisions that lack a hash. No additional work is needed — every work item that references `ContentHash` (versionless-revision grouping, approval inheritance) depends on this but does not need to implement it.

> **Feature flag convention:** Each work item (or group of related items) is gated by an Azure App Configuration key under the `FeatureManagement:` prefix (e.g., `FeatureManagement:APIVersionEntity`). The existing 5-minute sentinel-based refresh propagates flag changes without restart. Guards are placed at the write path entry points so that the flag can be flipped on once the work is verified.

> **Separability principle:** Every work item below can be deployed with an arbitrary delay before the next one. New data continues to be tagged with new fields as they're added; old code paths continue to run until their replacement is explicitly switched on via feature flag. There is no accumulating tech debt or data drift during gaps — new FK fields are always populated going forward, and old query paths work fine reading documents that happen to have extra fields.

---

### 5.1 Work Item Index

| WI | Name | Goal | Area | Layer | Depends On | Stable Alone? |
|----|------|------|------|-------|------------|---------------|
| 1 | APIVersion Entity & Repository Layer | New `APIVersionModel`, Cosmos container, repo, manager, and `NormalizeVersion()` — no existing code touched | Foundation | Backend | — | ✅ Yes |
| 2 | Wire Upload Paths | Every new revision/sample gets an `APIVersionId` FK from this point forward | Foundation | Backend | WI-1 | ✅ Yes |
| 3 | Backfill Revisions & Samples | Backfill `APIVersionId` on all historical revisions and samples so the FK is universal | Foundation | Backend | WI-1, WI-2 | ✅ Yes |
| 4 | Retention Configuration & RetainUntil Field | Add config keys and `RetainUntil` field — groundwork for unified retention | Retention | Backend | — † | ✅ Yes |
| 5 | Retention Event Handlers & Background Purge | Replace ad-hoc archive/purge with policy-driven `RetainUntil` lifecycle + unified purge loop | Retention | Backend | WI-3, WI-4 | ✅ Yes |
| 6 | Retention Backfill | Apply retention policy to historical data so old superseded versions get cleaned up | Retention | Backend | WI-5 | ✅ Yes |
| 7 | Comment FK & Backfill | Add `APIVersionId` FK to comments and backfill all historical comments | Comments | Backend | WI-3 | ✅ Yes |
| 8 | Comment Query Switch & Frontend Update | Switch code panel from review-wide to version-scoped comments, eliminating comment bleed | Comments | Backend + Frontend | WI-7 | ⚠️ See note |
| 9 | Cross-Version Comment Toggle | Opt-in UI toggle to see comments from other versions in the code panel | Comments | Frontend | WI-8 | ✅ Yes |
| 10 | PR Version Backfill & Status Tracking | Backfill `APIVersionId` on historical PR revisions and populate `PRStatus` | PR Lifecycle | Backend | WI-3 | ✅ Yes |
| 11 | Version-Aware PullRequestManager | Refactor PR creation to be version-aware; fix baseline comparison bug | PR Lifecycle | Backend | WI-10 | ✅ Yes |
| 12 | PR Promotion-on-Merge | Promote merged PR version to Stable/Preview in-place, preserving comments and blobs | PR Lifecycle | Backend | WI-8, WI-11 | ✅ Yes |
| 13 | PR Cleanup via Retention | Replace ad-hoc `CleanupPullRequestData` with `RetainUntil`-based lifecycle | PR Lifecycle | Backend | WI-5, WI-10 | ✅ Yes |
| 14 | Approval Inheritance Policy & ClassifyTransition | Define per-language policy matrix and version-transition classifier | Approvals | Backend | — † | ✅ Yes |
| 15 | Version-Level Approval Backend | Move approval from per-revision to per-version; replace opaque carry-forward with policy-driven inheritance | Approvals | Backend | WI-3, WI-14 | ⚠️ Gate with WI-17 |
| 16 | GetReviewStatus & Copilot Trigger | Make release-gating check and Copilot automation version-aware | Approvals | Backend (CI-facing) | WI-3 | ✅ Yes |
| 17 | Approval SPA Update | Update Angular SPA to operate on version-level approvals and show inheritance provenance | Approvals | Frontend | WI-15 | ⚠️ Gate with WI-15 |

> **† Parallel with WI-1:** WI-4 and WI-14 are pure config/schema/pure-function work with no runtime dependency on the `APIVersions` container, repository, or manager. They can be developed and merged in parallel with WI-1. The only touch point is adding `RetainUntil` to the `APIVersionModel` type (WI-4) and using `VersionKind` in `ClassifyTransition` (WI-14) — both trivial once the model file lands, which is the first artifact in WI-1.

**Stable-state gates** (must ship together when the feature flag is flipped):
- **WI-8 (Comment Query Switch):** The backfill in WI-7 must be verified complete before flipping the flag. WI-7 and WI-8 can be *developed and deployed* independently, but the WI-8 flag flip requires WI-7's backfill to have finished. If you flip WI-8 before WI-7 completes, comments without `APIVersionId` would be invisible. In practice: deploy both, run backfill, verify, flip.
- **WI-15 + WI-17 (Approval Backend + SPA):** The backend (WI-15) starts expecting version-level approval calls when its flag is on, and the SPA (WI-17) must be sending them. Deploy both behind the same flag; flip once. You can *develop and merge* them independently — the flag gates runtime behavior, not deployment.

---

### 5.2 WI-1: APIVersion Entity & Repository Layer

**Area:** Foundation · **Depends on:** Nothing · **Stable alone:** ✅ Yes — creates new infrastructure, touches nothing existing

**Goal:** Introduce the `APIVersionModel` entity, its Cosmos DB container, repository, and manager — without modifying any existing entity or read/write path.

#### New model

Create `APIViewWeb/LeanModels/APIVersionModel.cs` containing the `APIVersionModel` class, `VersionKind` enum, and `PullRequestStatus` enum as defined in [§4.1](#41-new-entity-apiversion). The model inherits from `BaseListitemModel` (which provides `Id`, `PackageName`, `Language`). Add `APIVersionChangeHistoryModel` extending `ChangeHistoryModel` with a new `APIVersionChangeAction` enum (`Created`, `Approved`, `ApprovalReverted`, `Deleted`, `UnDeleted`, `Promoted`, `RetentionSet`), following the same pattern as `APIRevisionChangeHistoryModel` in `LeanModels/ChangeHistory.cs`.

#### New Cosmos container

Create the `APIVersions` container with partition key `/ReviewId`. This mirrors the `APIRevisions` container's partitioning strategy — all versions for a review are co-located, enabling efficient cross-version queries within a review.

Register the container in `Startup.cs` alongside the existing 8 container registrations (`ICosmosReviewRepository`, `ICosmosAPIRevisionsRepository`, etc.) as `ICosmosVersionsRepository` → `CosmosVersionsRepository`, following the identical `AddSingleton` pattern.

#### New repository

Create `APIViewWeb/Repositories/Interfaces/ICosmosVersionsRepository.cs` and `APIViewWeb/Repositories/CosmosVersionsRepository.cs`. Core query methods:

| Method | Purpose |
|---|---|
| `GetVersionAsync(reviewId, versionId)` | Single version by ID |
| `GetVersionsAsync(reviewId)` | All non-deleted versions for a review |
| `GetVersionAsync(reviewId, versionIdentifier)` | Lookup by normalized version string |
| `GetVersionByPullRequestAsync(reviewId, pullRequestNumber)` | PR version lookup |
| `GetVersionsAsync(reviewId, versionKind)` | Filter by kind (Stable, Preview, etc.) |
| `GetVersionsEligibleForRetentionAsync(now)` | Cross-partition query: `WHERE RetainUntil != null AND RetainUntil < @now` |
| `UpsertVersionAsync(version)` | Create or update |
| `DeleteVersionAsync(versionId, reviewId)` | Hard delete |

#### New manager

Create `APIViewWeb/Managers/APIVersionsManager.cs` with `IAPIVersionsManager` interface. Initial methods:

| Method | Purpose |
|---|---|
| `GetOrCreateVersionAsync(reviewId, packageVersion, apiRevisionType, pullRequestNo?)` | Core find-or-create: calls `NormalizeVersion()`, looks up existing by `(ReviewId, VersionIdentifier)`, creates if absent. Sets `Kind`, `VersionIdentifier`, `PullRequestNumber`, `SourceBranch` as appropriate. |
| `GetVersionsForReviewAsync(reviewId)` | Passthrough to repository with caching for the active request |
| `GetVersionByIdAsync(reviewId, versionId)` | Single version lookup |
| `SoftDeleteVersionAsync(versionId, reviewId)` | Soft delete with change history |

#### Implement `NormalizeVersion()`

Add a static method `NormalizeVersion(string packageVersion)` to a new helper class `APIViewWeb/Helpers/VersionNormalizationHelper.cs`. The method returns a `(string VersionIdentifier, VersionKind Kind)` tuple.

**Implementation** builds on top of `AzureEngSemanticVersion` which already parses `Major`, `Minor`, `Patch`, `PrereleaseLabel`, `PrereleaseNumber`, and `BuildNumber`:

1. If `AzureEngSemanticVersion.TryCreate()` fails → return `(rawVersion, VersionKind.Preview)` as fallback.
2. If no prerelease suffix:
   - Major ≥ 1 → `(major.minor.patch, Stable)`
   - Major == 0 → `(major.minor.patch, Preview)`
3. If prerelease suffix present:
   - Split prerelease identifiers by `.`. If the second identifier matches `^\d{8}$` (8-digit YYYYMMDD date stamp) → `(major.minor.patch-label, RollingPrerelease)` where `label` is the first identifier only (e.g., `alpha`, `dev`).
   - Otherwise → `(major.minor.patch-fullPrerelease, Preview)`. Examples: `1.2.0-beta.1` → `Preview`, `1.2.0-rc.1` → `Preview`.
   - Sub-1.0.0 with prerelease (e.g., `0.5.0-beta.1`) → treat as `Preview` (not `RollingPrerelease` unless the date-stamp rule fires).

> **Python PEP 440 note:** Python versions like `1.2.0a20260323001` do not use dot-separated prerelease identifiers. `AzureEngSemanticVersion` has a Python-specific branch (`IsSemVerFormat == false`) that extracts `PrereleaseLabel = "a"` and `PrereleaseNumber` as the numeric portion. `NormalizeVersion` detects this pattern (numeric PrereleaseNumber with 8+ digits) and normalizes to `major.minor.patch-a` as a `RollingPrerelease`.

#### Tests

| Test file | Coverage |
|---|---|
| `VersionNormalizationTests.cs` | All normalization rules from [§4.3](#43-version-normalization-for-rolling-prereleases): stable, sub-1.0.0, explicit prerelease milestones, daily alpha/dev builds for C#/Java/Python/JS, unparseable fallback |
| `CosmosVersionsRepositoryTests.cs` | CRUD operations, query-by-kind, query-by-PR-number, retention-eligible query |
| `APIVersionsManagerTests.cs` | `GetOrCreateVersionAsync` — find existing, create new, rolling prerelease collapsing, PR version creation |

#### Documentation

Update [docs/overview.md](docs/overview.md) §3 (Data Model Hierarchy) to include the `APIVersion` layer between Review and APIRevision. The current hierarchy is `Review → APIRevision → CodeFile`; it becomes `Review → APIVersion → APIRevision → CodeFile`.

---

### 5.3 WI-2: Wire Upload Paths

**Area:** Foundation · **Depends on:** WI-1 · **Stable alone:** ✅ Yes — new uploads get tagged, nothing reads the tag yet, old paths unchanged

**Goal:** Make every new revision and sample carry an `APIVersionId` from this point forward. Historical data is not yet backfilled (that's WI-3).

#### Add `APIVersionId` FK to existing models

Add a nullable `string? APIVersionId` property to:
- `APIRevisionListItemModel` in `LeanModels/ReviewListModels.cs`
- `SamplesRevisionModel` in `LeanModels/ReviewListModels.cs`

Both are backward-compatible — Cosmos DB documents without this field deserialize with `null`. All existing read paths tolerate `null` without changes.

#### Wire the automatic-upload path

Update `AutoReviewService.CreateAutomaticRevisionAsync` (the primary entry point for CI-uploaded revisions):

1. After the `CodeFile` is parsed and `PackageVersion` is known, call `_apiVersionsManager.GetOrCreateVersionAsync(reviewId, packageVersion, APIRevisionType.Automatic)`.
2. Set `APIVersionId` on the new `APIRevisionListItemModel` before `UpsertAPIRevisionAsync`.
3. The existing pending-revision cleanup logic (soft-deleting prior non-approved, non-commented automatic revisions) continues to operate unchanged — it now operates within the scope of the version's revisions.

**Critical change to supersession logic:** Today, `CreateAutomaticRevisionAsync` iterates all revisions for the review and soft-deletes pending ones. With the version-centric model, this logic must be scoped to the **same `APIVersionId`** — a new revision for v12.2.0 must not delete a pending revision for v12.3.0. Filter the candidate list to `revision.APIVersionId == newVersion.Id` before applying the delete heuristic. This directly resolves [#5186](https://github.com/Azure/azure-sdk-tools/issues/5186) and [#10105](https://github.com/Azure/azure-sdk-tools/issues/10105).

#### Wire the manual-upload path

The manual upload flows through `APIRevisionsController` → `APIRevisionsManager.CreateAPIRevisionAsync`. Add the same `GetOrCreateVersionAsync` call and `APIVersionId` assignment. If the uploaded file has no `PackageVersion`, fall through to the versionless handling in [§7.3](#73-versionless-revisions-javascript).

#### Wire the PR-upload path (forward-only tagging)

PR revisions are fully handled in WI-11, but to avoid accumulating untagged data in the interim, add a lightweight forward-only tagging step to `PullRequestManager.CreateAPIRevisionIfAPIHasChanges`:

1. Call `GetOrCreateVersionAsync(reviewId, packageVersion, APIRevisionType.PullRequest, pullRequestNo)` using `VersionKind.PullRequest` and `VersionIdentifier = "PR#{pullRequestNo}"`.
2. Set `APIVersionId` on the PR revision.

This is the minimal change — the full PR lifecycle (promotion, status tracking, retention) is deferred to WI-10 through WI-13.

#### Wire the samples-upload path

Update `SamplesRevisionsManager.UpsertSamplesRevisionsAsync` to accept an `apiVersionId` parameter. The frontend must pass the active version ID when uploading samples. For backward compatibility, if `apiVersionId` is null, fall back to the review's latest stable version.

#### Tests

| Test file | Coverage |
|---|---|
| `AutoReviewServiceTests.cs` | New revision gets `APIVersionId`; rolling prerelease builds collapse into same version; supersession scoped to same version (v12.2.0 doesn't delete v12.3.0's pending revision) |
| `APIRevisionsManagerTests.cs` | Manual upload gets `APIVersionId`; versionless revision fallback |
| `PullRequestManagerTests.cs` | PR revision gets forward-tagged `APIVersionId` |

---

### 5.4 WI-3: Backfill Revisions & Samples

**Area:** Foundation · **Depends on:** WI-1, WI-2 · **Stable alone:** ✅ Yes — backfills FK on historical data; nothing reads it yet

**Goal:** Every `APIRevisionListItemModel` and `SamplesRevisionModel` in the database has a non-null `APIVersionId`.

#### Backfill revisions

Run a one-time migration (implemented as a console command or a background job triggered by an admin endpoint). For each review in the `Reviews` container:

1. Fetch all non-deleted revisions where `APIVersionId == null` and `APIRevisionType != PullRequest`.
2. For each revision, compute `NormalizeVersion(revision.Files[0].PackageVersion)` → `(versionIdentifier, kind)`.
3. Group revisions by `(reviewId, versionIdentifier)`.
4. For each group:
   a. Call `GetOrCreateVersionAsync` to find or create the `APIVersionModel`.
   b. Set `APIVersionId` on each revision in the group.
   c. Copy approval state from the group's revisions to the version: if any revision in the group is approved, set `IsApproved = true` on the version and populate `Approvers` from the approved revision's `Approvers`. If multiple revisions are approved, use the most recently approved one as the source.
   d. Copy release state: if any revision is released (`IsReleased = true`), set `IsReleased = true` and `ReleasedOn` on the version.
   e. Batch-upsert the revisions (Cosmos DB supports transactional batch within a partition key, and all revisions for a review share the `ReviewId` partition).
5. **Versionless revisions** (empty or null `PackageVersion`, common in JavaScript): Apply the strategy from [§7.3](#73-versionless-revisions-javascript) — attempt label extraction, then content-hash grouping, then synthetic version assignment. PR revisions with no `PackageVersion` are skipped (handled in WI-10).

**Idempotency:** The migration skips revisions where `APIVersionId` is already set. It can be re-run safely.

**Progress tracking:** Log progress by review ID. The migration can be paused and resumed by filtering to reviews not yet processed (e.g., reviews whose revisions still have `APIVersionId == null`).

#### Backfill samples revisions

For each `SamplesRevisionModel` where `APIVersionId == null`:

1. Find the revision within the same review whose `CreatedOn` is closest to (and not after) the samples revision's `CreatedOn`.
2. Adopt that revision's `APIVersionId`.
3. If no plausible match (orphaned sample), assign to the review's latest stable `APIVersion`.

#### Validation gate

Before subsequent work items that read `APIVersionId` are flag-flipped (WI-5, WI-7, WI-8, etc.), verify:
- Query: `SELECT COUNT(1) FROM c WHERE c.APIVersionId = null AND c.IsDeleted != true` returns 0 for both `APIRevisions` and `SamplesRevisions` containers (excluding PR revisions, handled in WI-10).
- Every `APIVersion` in the `APIVersions` container has at least one linked revision.

#### Tests

| Test file | Coverage |
|---|---|
| `BackfillMigrationTests.cs` (new) | Revisions grouped by normalized version; approval/release state copied to version; versionless revision gets synthetic version; idempotent re-run |
| `SamplesRevisionsManagerTests.cs` | Backfill assigns correct version; orphaned sample falls back to latest stable |

#### Documentation

Update [docs/overview.md](docs/overview.md) §4c (Key Managers) to add `APIVersionsManager` with its responsibility description. Update [docs/release_approval.md](docs/release_approval.md) §5 (Automatic Approval Carry-Forward) to note that carry-forward is being migrated to version-level in WI-15.

---

### 5.5 WI-4: Retention Configuration & RetainUntil Field

**Area:** Retention · **Depends on:** Nothing (parallel with WI-1 †) · **Stable alone:** ✅ Yes — adds config keys and a nullable field; nothing reads them yet

**Goal:** Lay the config and schema groundwork for the retention system.

#### Configuration

Add the following keys to Azure App Configuration under the `RetentionPolicy:` prefix, with hardcoded defaults in a new `RetentionPolicyOptions` class bound via `IOptions<RetentionPolicyOptions>`:

| Key | Default | Purpose |
|---|---|---|
| `RetentionPolicy:SupersededPreviewDaysAfterStable` | `90` | Unapproved/unreleased preview cleanup after stable ships |
| `RetentionPolicy:GraduatedRollingPrereleaseDays` | `30` | Rolling prerelease channel cleanup after stable ships |
| `RetentionPolicy:MergedPullRequestDays` | `60` | Non-promoted merged PR version cleanup |
| `RetentionPolicy:ClosedPullRequestDays` | `30` | Closed-without-merge PR version cleanup |
| `RetentionPolicy:SupersededRevisionHardDeleteDays` | `30` | Hard-delete for superseded revisions within a version |

Register `RetentionPolicyOptions` in `Startup.cs` with `services.Configure<RetentionPolicyOptions>(configuration.GetSection("RetentionPolicy"))`. The existing sentinel-based refresh ensures changes propagate without restart.

#### `RetainUntil` field

Add `DateTime? RetainUntil` to both `APIVersionModel` and `APIRevisionListItemModel`. Default is `null` (retain indefinitely). The field is set at the moment the retention clock starts, not computed during purge.

#### Tests

| Test file | Coverage |
|---|---|
| `RetentionPolicyOptionsTests.cs` (new) | Defaults are correct; App Config overrides work; missing keys fall back to defaults |

---

### 5.6 WI-5: Retention Event Handlers & Background Purge

**Area:** Retention · **Depends on:** WI-3 (all revisions have `APIVersionId`), WI-4 (config + field exist) · **Stable alone:** ✅ Yes — replaces old archive/purge with new unified system

**Goal:** Replace the ad-hoc deletion heuristics (`AutoArchiveAPIRevisions`, `AutoPurgeAPIRevisions`, `CleanupPullRequestData`) with the unified retention policy from [§4.8](#48-retention-policy).

#### Event handlers

Add methods to `APIVersionsManager`:

| Method | Trigger | Action |
|---|---|---|
| `OnStableVersionReleased(reviewId, versionId)` | A `Stable` version's `IsReleased` is set to `true` | Set `RetainUntil` on superseded `Preview` versions (90d) and graduated `RollingPrerelease` channels (30d) for that major.minor.patch. Enforce always-retain invariants (last stable, last preview per track, any released version). |
| `OnRevisionSuperseded(revision)` | A new revision is created within a version, replacing a prior one | Set `RetainUntil = now + 30d` on the superseded revision if it is not the latest, approved, or released snapshot. Soft-delete immediately (existing behavior). |
| `OnPRClosed(reviewId, versionId, merged)` | PR status transitions to `Merged` or `Closed` | Set `RetainUntil` based on merged (60d) vs. closed (30d). For promoted PRs, clear `RetainUntil` (they become `Stable`/`Preview` and follow those rules). |
| `OnVersionApprovedOrReleased(versionId)` | Version becomes approved or released | Clear `RetainUntil → null` (retain indefinitely). |

#### Background purge job

Replace the retention-related logic in `ReviewBackgroundHostedService` (which currently calls `AutoArchiveAPIRevisions` and `AutoPurgeAPIRevisions` every 6 hours) with a new unified purge loop:

1. **Version-level purge:** Query `CosmosVersionsRepository.GetVersionsEligibleForRetentionAsync(DateTime.UtcNow)` — returns versions where `RetainUntil < now`. For each, cascade-delete: all revisions, all comments (by `APIVersionId`), all blobs (`codefiles` and `originals` containers), then the version itself.
2. **Revision-level purge:** Query revisions where `RetainUntil < now` (hard-delete candidates). For each, delete the revision's blobs, then hard-delete the Cosmos document.
3. **Rate limiting:** Retain the existing 500ms inter-deletion delay from `AutoPurgeAPIRevisions` to avoid overwhelming Cosmos throughput.

The existing `AutoArchiveAPIRevisions` and `AutoPurgeAPIRevisions` are **retired** once this WI is fully deployed. During the transition, both old and new paths can coexist behind the feature flag — the old paths continue to run for revisions without `RetainUntil`, and the new path handles revisions with it.

#### Tests

| Test file | Coverage |
|---|---|
| `RetentionPolicyTests.cs` (new) | `OnStableVersionReleased` sets correct `RetainUntil` on previews and rolling prereleases; invariants (last stable, last preview, released) are never given a `RetainUntil`; `OnVersionApprovedOrReleased` clears `RetainUntil`; cascade deletion removes version + revisions + comments; `OnRevisionSuperseded` sets `RetainUntil` on non-latest, non-approved revision |
| `ReviewBackgroundHostedServiceTests.cs` | New purge loop runs on schedule; respects rate limiting; handles empty result sets |

#### Documentation

Update [docs/overview.md](docs/overview.md) §4e (Background Services) to describe the new unified retention purge replacing the old archive/purge split.

---

### 5.7 WI-6: Retention Backfill

**Area:** Retention · **Depends on:** WI-5 · **Stable alone:** ✅ Yes — sets `RetainUntil` on historical data; purge loop picks it up on next cycle

**Goal:** Apply retention policy to existing data so that historical superseded versions and revisions are cleaned up.

#### Migration

Run a one-time migration:

1. For each `RollingPrerelease` version whose major.minor.patch has a shipped `Stable` version → set `RetainUntil = stableReleasedOn + 30d` (may already be in the past — purge will clean up on next cycle).
2. For each `Preview` version that is not approved, not released, and has a newer shipped `Stable` version → set `RetainUntil = stableReleasedOn + 90d`.
3. For each superseded revision (not latest, not approved, not released) within a version → set `RetainUntil = now + 30d` and soft-delete.

---

### 5.8 WI-7: Comment FK & Backfill

**Area:** Comments · **Depends on:** WI-3 (all revisions have `APIVersionId`) · **Stable alone:** ✅ Yes — adds a nullable field and populates it; nothing reads it yet

**Goal:** Every comment in the database has an `APIVersionId`, preparing for the query switch in WI-8.

#### Add `APIVersionId` FK to comments

Add a nullable `string? APIVersionId` property to `CommentItemModel` in `LeanModels/CommentItemModel.cs`. Backward-compatible — existing documents deserialize with `null`.

#### Backfill

Run a one-time migration:

1. For every comment where `APIVersionId == null` and `APIRevisionId != null`:
   a. Look up the revision (including soft-deleted revisions — query with `IsDeleted` filter disabled).
   b. If found and the revision has `APIVersionId` → set it on the comment.
   c. If the revision is not found (deleted/purged), attempt to resolve by parsing the comment's context: look up the review's versions and find the `APIVersion` whose `CreatedOn` range brackets the comment's `CreatedOn`.
   d. If still unresolvable → assign to the review's latest stable `APIVersion` (conservative fallback).
2. For comments where `APIRevisionId == null` (older comments before revision-scoping was added) → apply the same timestamp-bracketing logic.
3. **Orphaned diagnostic comments** (whose parent revision no longer exists): hard-delete. Diagnostics are machine-generated and revision-specific; they have no value once their revision is gone.

#### Update comment creation paths (forward-write)

Ensure every *new* comment gets `APIVersionId` from this point on, even before the query switch:

1. **`CommentsManager.AddCommentAsync`:** Look up the revision's `APIVersionId` and write both fields.
2. **`CommentsManager.SyncDiagnosticCommentsAsync`:** Set `APIVersionId` from the revision's `APIVersionId` on new diagnostic comments.
3. **`CommentsManager.CommentsBatchOperationAsync`:** New reply comments inherit `APIVersionId` from the thread's root comment.

#### Tests

| Test file | Coverage |
|---|---|
| `CommentsManagerTests.cs` | New comment gets `APIVersionId`; diagnostic sync sets `APIVersionId`; batch replies inherit `APIVersionId` from thread root |
| `CommentBackfillTests.cs` (new) | Revision lookup populates `APIVersionId`; deleted revision falls back to timestamp bracketing; orphaned diagnostics are deleted |

---

### 5.9 WI-8: Comment Query Switch & Frontend Update

**Area:** Comments · **Depends on:** WI-7 · **Stable alone:** ⚠️ The backfill in WI-7 must be *verified complete* before this WI's feature flag is flipped. You can develop, merge, and deploy WI-8 at any time — but the flag that activates it requires WI-7's data to be in place. If flipped prematurely, comments without `APIVersionId` would be invisible in the code panel.

**Goal:** Switch the code panel from review-wide comment loading to version-scoped loading, eliminating cross-version comment bleed ([§3.1](#31-comment-bleed-across-revisions)).

#### Add version-scoped query

Add `GetCommentsForVersionAsync(string reviewId, string apiVersionId)` to `ICosmosCommentsRepository` / `CosmosCommentsRepository`. The query:

```sql
SELECT * FROM c WHERE c.ReviewId = @reviewId AND c.APIVersionId = @apiVersionId AND c.IsDeleted != true
```

The existing `GetCommentsAsync(reviewId)` (review-wide) is **retained** — it serves: the conversations panel, review-level comment counts, cross-version search, the "show other versions" toggle, and admin/moderation views.

#### Update backend rendering path

Update `ReviewsController.GetReviewContentAsync` (`LeanControllers/ReviewsController.cs`):

1. Resolve the active revision's `APIVersionId` (already available on the revision after WI-2/WI-3).
2. Replace the current flow:
   - **Before:** `GetCommentsAsync(reviewId)` → `CommentVisibilityHelper.GetVisibleComments(allComments, activeApiRevisionId)` → pass to code panel.
   - **After:** `GetCommentsForVersionAsync(reviewId, apiVersionId)` → pass to code panel. The comment-bleed problem is solved by the query itself — no heuristic post-filter needed.
3. The backend `CommentVisibilityHelper.GetVisibleComments()` static method is retired for this path. It remains available for any legacy callers until fully removed.

#### Update frontend comment visibility

Replace the logic in `ClientSPA/src/app/_helpers/comment-visibility.helper.ts` (`getVisibleComments`):

- **Before:** Shows all user and AI comments regardless of revision; only diagnostics are filtered by `apiRevisionId`.
- **After:** The backend already returns only version-scoped comments. The frontend helper simplifies to: show all comments from the response; filter diagnostics to `apiRevisionId` within the version. The `VisibleCommentsResult` structure is unchanged — `userComments`, `aiGeneratedComments`, `diagnosticCommentsForRevision` — but the input is now pre-scoped.

The conversations panel (`conversations.component.ts`) continues to use the review-wide feed for its counts and thread list.

#### Cascade deletion rules

When an `APIVersion` is deleted (via retention purge or manual deletion):
- Cascade-delete all comments where `APIVersionId` matches — a single partition-scoped query + batch delete.
- No orphans possible because comments are keyed to `APIVersionId`, not `APIRevisionId`.

When an individual `APIRevision` is replaced within a version:
- User and AI comments are unaffected (version-scoped).
- Diagnostic comments for the old revision are auto-resolved by `SyncDiagnosticCommentsAsync` when the new revision's diagnostics are synced.

#### Retain `APIRevisionId` on `CommentItemModel`

Do not deprecate. It continues to serve: (a) diagnostic reconciliation, (b) traceability, (c) backward compatibility with external consumers.

#### Tests

| Test file | Coverage |
|---|---|
| `CosmosCommentsRepositoryTests.cs` | `GetCommentsForVersionAsync` returns only version-scoped comments; review-wide query still returns all |
| `ReviewsControllerTests.cs` | `GetReviewContentAsync` uses version-scoped query; response contains no cross-version comments |
| `CommentVisibilityHelperTests.cs` (frontend) | Helper passes through pre-scoped backend response; diagnostics still filtered by revision |

#### Documentation

Update [docs/overview.md](docs/overview.md) §5b (Key Components) to note that the code panel now receives version-scoped comments. Update [docs/overview.md](docs/overview.md) §4c to describe the updated `CommentsManager` query behavior.

---

### 5.10 WI-9: Cross-Version Comment Toggle

**Area:** Comments · **Depends on:** WI-8 · **Stable alone:** ✅ Yes — additive UX feature, can ship or revert independently

**Goal:** Let users opt in to seeing comments from other versions in the code panel.

Add an opt-in toggle to the code panel (`review-page-options` component). When enabled:

1. The code panel re-fetches using the review-wide `GetCommentsAsync(reviewId)`.
2. Comments from non-active versions are rendered with a visual badge indicating their source version (e.g., "v12.1.0").
3. These cross-version comments are read-only — replies and resolution actions are scoped to the active version only.

#### Tests

Frontend unit test: toggle on shows cross-version comments with badge; toggle off reverts to version-scoped.

---

### 5.11 WI-10: PR Version Backfill & Status Tracking

**Area:** PR Lifecycle · **Depends on:** WI-3 (backfill infrastructure exists) · **Stable alone:** ✅ Yes — backfills FK on historical PR revisions; nothing reads it via new paths yet

**Goal:** Every PR revision in the database has an `APIVersionId`, and PR versions have `PRStatus`.

#### Backfill existing PR versions

WI-2 already forward-tagged new PR revisions. This backfill covers pre-WI-2 PR revisions:

1. Query PR revisions where `APIVersionId == null`.
2. Group by `PullRequestNo` (or `SourceBranch` where PR number is absent) per review.
3. For each group, create an `APIVersionModel` with `Kind = PullRequest`, `VersionIdentifier = "PR#{pullRequestNo}"`.
4. Set `APIVersionId` on each revision and its associated comments (using the same revision-to-comment linkage as WI-7 backfill).

#### Populate `PRStatus`

- Query the GitHub API (via the existing Octokit integration in `PullRequestManager`) for the current state of each PR.
- Set `PRStatus = Open`, `Merged`, or `Closed` accordingly.

For new PRs going forward, `PullRequestManager.CreateAPIRevisionIfAPIHasChanges` sets `PRStatus = Open` on version creation.

#### Tests

| Test file | Coverage |
|---|---|
| `PRBackfillTests.cs` (new) | PR revisions grouped by PR number; PRStatus populated from GitHub state; comments linked to PR version |

---

### 5.12 WI-11: Version-Aware PullRequestManager

**Area:** PR Lifecycle · **Depends on:** WI-10 · **Stable alone:** ✅ Yes — changes how new PR revisions are created; old PR cleanup paths still work

**Goal:** Refactor `PullRequestManager.CreateAPIRevisionIfAPIHasChanges` to be version-aware, fixing the baseline comparison issue ([#10105](https://github.com/Azure/azure-sdk-tools/issues/10105)).

#### Changes to `CreateAPIRevisionIfAPIHasChanges`

1. **Find or create PR version:** Call `_apiVersionsManager.GetOrCreateVersionAsync(reviewId, packageVersion, APIRevisionType.PullRequest, pullRequestNo)` (replaces the current bare-revision creation).
2. **Commit dedup:** Check if the commit SHA is already recorded on any revision under this version (same logic as today, but scoped to the version's revisions).
3. **API change detection:** Compare the incoming `ContentHash` against the version's latest revision's `ContentHash` (replaces the current `prHasAPIChanges` which compares against all automatic revisions). If the API surface is unchanged from the previous push to this PR, skip revision creation.
4. **Baseline selection:** For PR comparison (determining whether the PR introduces API changes vs. the release baseline), compare against the matching `Stable` or `Preview` version's latest revision — found by normalizing the PR's `PackageVersion` and looking up the corresponding non-PR version. This replaces the current approach of comparing against every automatic revision.
5. **Create revision:** Create a new `APIRevision` under the PR version with the incoming `ContentHash` and full `PackageVersion`.

#### Tests

| Test file | Coverage |
|---|---|
| `PullRequestManagerTests.cs` | PR creates version with `Kind = PullRequest`; subsequent pushes create revisions under same version; baseline comparison scoped to correct version; commit dedup works |

---

### 5.13 WI-12: PR Promotion-on-Merge

**Area:** PR Lifecycle · **Depends on:** WI-8 (comments are version-scoped — required so promoted PR comments don't bleed), WI-11 (PR creation is version-aware) · **Stable alone:** ✅ Yes — adds a new code path in `AutoReviewService`; the non-promotion fallback (create new version) still works

**Goal:** When a PR is merged and the post-merge API surface matches the PR's latest revision, promote the PR version to `Stable`/`Preview` in-place, preserving comments and avoiding blob duplication ([#8634](https://github.com/Azure/azure-sdk-tools/issues/8634)).

#### Implement `PromotePRVersionAsync`

Add `PromotePRVersionAsync(reviewId, prVersionId, targetVersionIdentifier, targetKind)` to `APIVersionsManager`:

1. **Trigger:** When `AutoReviewService.CreateAutomaticRevisionAsync` processes a post-merge CI build, before creating a new `Stable`/`Preview` version, check for a recently merged PR version in the same review whose latest revision's `ContentHash` matches the incoming hash.
2. **Match found (API unchanged):** Promote the PR version in-place:
   - Set `Kind` → `targetKind` (`Stable` or `Preview` per `NormalizeVersion`).
   - Update `VersionIdentifier` from `"PR#1234"` → the normalized version string (e.g., `"1.1.0"`).
   - Set `PRStatus = Merged`.
   - Retain `PullRequestNumber` and `SourceBranch` for provenance.
   - **Do not change the `Id`** — `APIVersionId` on comments and revisions remains valid. All PR review comments survive into the release version.
   - **Do not upload new blobs** — the promoted version reuses the PR's existing artifacts.
   - Record the promotion in `ChangeHistory` with action `Promoted`.
   - Skip the normal `CreateAutomaticRevisionAsync` flow (no new revision or version needed).
3. **No match (API changed post-merge):** Create a new `Stable`/`Preview` version through the normal automatic flow. Set `PRStatus = Merged` on the PR version. The PR version follows its merged-PR retention schedule.

#### Tests

| Test file | Coverage |
|---|---|
| `APIVersionsManagerTests.cs` | `PromotePRVersionAsync` — promotes matching PR, preserves `Id`/comments/blobs, updates `Kind`/`VersionIdentifier`; non-matching PR creates new version |
| `AutoReviewServiceTests.cs` | Post-merge build triggers promotion check; promotion skips revision creation; non-matching hash falls through to normal flow |

#### Documentation

Update [docs/overview.md](docs/overview.md) §4c (PullRequestManager) to describe the promotion-on-merge flow. Update [docs/release_approval.md](docs/release_approval.md) §7 (Marking a Revision as Released) to note that promoted PR versions carry their release state forward.

---

### 5.14 WI-13: PR Cleanup via Retention

**Area:** PR Lifecycle · **Depends on:** WI-5 (retention system exists), WI-10 (PR versions have `PRStatus`) · **Stable alone:** ✅ Yes — replaces old `CleanupPullRequestData` with `RetainUntil`-based cleanup

**Goal:** Use the unified retention system for PR version lifecycle instead of the ad-hoc `CleanupPullRequestData` logic.

#### Changes

Replace `PullRequestManager.CleanupPullRequestData` (which today soft-deletes PR revisions 30 days after PR close, only if unapproved) with version-level retention:

1. When a PR transitions to `Merged` (non-promoted): set `RetainUntil = now + MergedPullRequestDays`.
2. When a PR transitions to `Closed`: set `RetainUntil = now + ClosedPullRequestDays`.
3. The WI-5 purge loop handles the actual deletion.

The existing `PullRequestBackgroundHostedService` (which posts PR comments asynchronously) is unchanged.

#### Tests

| Test file | Coverage |
|---|---|
| `PullRequestCleanupTests.cs` (new) | Merged non-promoted gets 60d retention; closed gets 30d; promoted PR has `RetainUntil` cleared |

---

### 5.15 WI-14: Approval Inheritance Policy & ClassifyTransition

**Area:** Approvals · **Depends on:** Nothing (parallel with WI-1 †) · **Stable alone:** ✅ Yes — pure infrastructure, no behavior change

**Goal:** Define the per-language approval inheritance policy and the transition classifier.

#### Configuration

Add the following keys to Azure App Configuration:

| Key | Value |
|---|---|
| `ApprovalInheritancePolicy:Default` | JSON blob per `ApprovalInheritancePolicyOptions` schema in [§4.6.1](#461-per-language-approval-inheritance-policy) |
| `ApprovalInheritancePolicy:{Language}` | Language-specific overrides (optional) |

Create `APIViewWeb/Models/ApprovalInheritancePolicyOptions.cs` with the `InheritanceRule` enum and the six transition fields. Register as `IOptionsSnapshot<Dictionary<string, ApprovalInheritancePolicyOptions>>` bound to the `ApprovalInheritancePolicy` section. Cache in memory; refresh on sentinel change.

#### Implement `ClassifyTransition()`

Add a static method `ClassifyTransition(APIVersionModel source, APIVersionModel target)` to `VersionNormalizationHelper.cs`. Returns the applicable `InheritanceRule` from the policy matrix:

1. Parse both versions' `VersionIdentifier` via `AzureEngSemanticVersion`.
2. Determine the transition kind: `PrereleaseToPrerelease`, `PrereleaseToStable`, `StableToPrerelease`, `StablePatch`, `StableMinor`, `StableMajor`.
3. Look up the rule in the per-language policy (falling back to `Default`).

#### Tests

| Test file | Coverage |
|---|---|
| `VersionNormalizationTests.cs` | `ClassifyTransition` for all 6 transition kinds; language-specific policy overrides; fallback to default |

---

### 5.16 WI-15: Version-Level Approval Backend

**Area:** Approvals · **Depends on:** WI-3 (versions have approval state), WI-14 (policy exists) · **Stable alone:** ⚠️ Must ship with WI-17 (SPA update) — the feature flag must be flipped together. Both can be developed and deployed independently, but runtime activation requires both to be in place.

**Goal:** Move approval from per-revision to per-version, and replace the opaque carry-forward with policy-driven inheritance.

#### Replace `CarryForwardRevisionDataAsync` with version-level inheritance

Update `AutoReviewService.CreateAutomaticRevisionAsync`:

1. After creating/finding the `APIVersion` for the incoming revision, if the version is not already approved:
2. **PR-merge promotion (preferred):** Check if this is a promotion from a PR version (WI-12). If so, approval status carries over as-is — no inheritance needed.
3. Compare the new version's latest revision's `ContentHash` against all approved versions in the same review (queried from `CosmosVersionsRepository`).
4. For each matching approved version, call `ClassifyTransition(source, target)` to get the applicable `InheritanceRule`.
5. If the rule is `Automatic`: set `IsApproved = true`, `Approvers = source.Approvers`, `ApprovalInheritedFromVersionId = source.Id`, `ApprovalDate = now` on the target version. Record in `ChangeHistory` with action `Approved` and notes `"Approval inherited from {source.VersionIdentifier} ({transitionKind})"`.
6. If the rule is `Explicit`: leave the version as pending.
7. **Break early** after the first matching approved version (ordered by `CreatedOn DESC`).

The existing `CarryForwardRevisionDataAsync` and `ApplyApprovalFrom` are **retired** once this WI's feature flag is active. During transition, the flag gates which path runs.

#### Implement `ToggleVersionApprovalAsync`

Add to `APIVersionsManager`:

1. Operates on `APIVersionModel` instead of `APIRevisionListItemModel`.
2. Same binary toggle logic via `ChangeHistoryHelpers.UpdateBinaryChangeAction()` with `APIVersionChangeAction.Approved`.
3. Clears `ApprovalInheritedFromVersionId` when a human explicitly approves.
4. On approval, clears `RetainUntil → null`.
5. Broadcasts `ReceiveApproval` via SignalR (same channel, updated payload to include `apiVersionId`).
6. **Cascade consideration:** When the source of an inheritance chain is un-approved, optionally cascade un-approval. Configurable (default: no cascade).

The legacy `ToggleAPIRevisionApprovalAsync` on `APIRevisionsManager` is retained behind the feature flag during transition.

#### Deprecate revision-level approval fields

Once the feature flag is on and stable:

1. `IsApproved` and `Approvers` on `APIRevisionListItemModel`: stop writing. Existing values remain as vestigial data.
2. `HasAutoGeneratedComments` copy in `CarryForwardRevisionDataAsync`: retired (Copilot tracking moved to version-level `IsReviewedByCopilot`).
3. `ApplyApprovalFrom` and `CarryForwardRevisionDataAsync`: deleted.

Do **not** strip fields from existing Cosmos documents — harmless, useful for auditing.

#### Tests

| Test file | Coverage |
|---|---|
| `APIVersionsManagerTests.cs` | Version-level approval toggle; inheritance from matching approved version; `Explicit` rule blocks inheritance; PR promotion preserves approval; cascade un-approval (if enabled) |
| `AutoReviewServiceTests.cs` | New version inherits approval from patch-bump source; major-bump blocks inheritance; prerelease-to-stable blocks; ContentHash mismatch → no inheritance |

---

### 5.17 WI-16: GetReviewStatus & Copilot Trigger

**Area:** Approvals · **Depends on:** WI-3 (versions exist with approval state) · **Stable alone:** ✅ Yes — additive improvements, backward-compatible

**Goal:** Make `GetReviewStatus` version-aware and implement version-level Copilot automation triggers.

#### Update `GetReviewStatus` (release gating)

Update `AutoReviewController.GetReviewStatus` in `APIViewWeb/Controllers/AutoReviewController.cs`:

- **Before:** Iterates revisions to find one with matching `PackageVersion` and checks `IsApproved`.
- **After:** Call `NormalizeVersion(packageVersion)` → look up `APIVersion` by `(reviewId, versionIdentifier)` → return status from `version.IsApproved`.
- Retain backward compatibility: if no matching `APIVersion` exists (pre-backfill edge case), fall through to the existing revision-level check.

#### Update Copilot automation trigger

Implement the rules from [§7.2](#72-copilot-automation-strategy):

1. On version creation or new revision upload, check `APIVersionModel.IsReviewedByCopilot`.
2. If `false` and the version is `Kind = Stable` (or a PR version whose `PackageVersion` resolves to stable): enqueue Copilot review, set `IsReviewedByCopilot = true`.
3. `Kind = Preview` and `Kind = RollingPrerelease`: skip automatic trigger.
4. Manual Copilot requests are tracked via `ReviewRequestIds` and do not set `IsReviewedByCopilot`.

#### Tests

| Test file | Coverage |
|---|---|
| `ReviewsControllerTests.cs` | `GetReviewStatus` returns approval from version; backward-compatible fallback |
| `CopilotTriggerTests.cs` (new) | Stable version triggers Copilot; preview does not; rolling prerelease does not; PR-with-stable-version triggers; `IsReviewedByCopilot` prevents duplicate |

#### Documentation

Update [docs/release_approval.md](docs/release_approval.md) §6: `GetReviewStatus` resolves by version.

---

### 5.18 WI-17: Approval SPA Update

**Area:** Approvals · **Depends on:** WI-15 · **Stable alone:** ⚠️ Must activate together with WI-15 — when the approval feature flag is flipped, the backend expects version-level calls and the SPA must be sending them. Deploy independently; flip flag together.

**Goal:** Update the Angular SPA to operate on version-level approvals and display inheritance provenance.

#### Frontend changes

| Component/Service | Change |
|---|---|
| `review-page-options.component.ts` | Approval button operates on `apiVersionId` instead of `apiRevisionId`. Display "Approved (inherited from v12.1.0)" when `ApprovalInheritedFromVersionId` is set. |
| `revisions-list.component.ts` | Version list shows version-level approval status with badge distinguishing inherited vs. human-approved. |
| `ReviewContextService` | Add `activeApiVersionId$` BehaviorSubject alongside existing `reviewId$` and `language$`. |
| `APIRevisionsController` API model | Add `apiVersionId` field to the revision response DTO so the frontend can derive the active version. |
| Approval prerequisites (`shouldDisableApproval`) | Evaluate guards against the version: missing version check, unresolved must-fix comments (version-scoped), Copilot review status (`IsReviewedByCopilot` on version). |

#### Tests

Frontend unit tests: approval button sends `apiVersionId`; inherited approval shows provenance badge; `shouldDisableApproval` evaluates version-level guards.

#### Documentation

Update [docs/release_approval.md](docs/release_approval.md):
- §1a: Approval is now per-version, not per-revision.
- §4: Toggle flow operates on `APIVersionModel`.
- §5: Carry-forward replaced by version-level inheritance with per-language policy.

Update [docs/overview.md](docs/overview.md) §4c: `APIRevisionsManager` no longer owns approval; `APIVersionsManager` does.

---

### 5.19 Dependency Map & Stable States

```
WI-1  APIVersion entity, repository, NormalizeVersion()
 │
 ├── WI-2  Wire upload paths (forward-tag new data)
 │    │
 │    └── WI-3  Backfill revisions & samples
 │         │
 │         ├── WI-5  Retention event handlers + purge (needs WI-3 + WI-4)
 │         │    │
 │         │    └── WI-6  Retention backfill
 │         │
 │         ├── WI-7  Comment FK & backfill
 │         │    │
 │         │    └── WI-8  Comment query switch ⚠️ (flip after WI-7 verified)
 │         │         │
 │         │         ├── WI-9   Cross-version comment toggle
 │         │         │
 │         │         └── WI-12  PR promotion-on-merge (needs WI-8 + WI-11)
 │         │
 │         ├── WI-10  PR version backfill & status
 │         │    │
 │         │    ├── WI-11  Version-aware PullRequestManager
 │         │    │
 │         │    └── WI-13  PR cleanup via retention (needs WI-5 + WI-10)
 │         │
 │         ├── WI-15  Approval backend (needs WI-3 + WI-14)
 │         │    │
 │         │    └── WI-15 + WI-17  Approval backend + SPA ⚠️ (flip together)
 │         │
 │         └── WI-16  GetReviewStatus & Copilot trigger
 │
WI-4  Retention config & RetainUntil field  ←── parallel with WI-1 †
WI-14 Approval policy & ClassifyTransition  ←── parallel with WI-1 †
```

#### Key stable-state gates

| Gate | Work Items | Why |
|------|-----------|-----|
| **Comment query activation** | WI-7 backfill verified → then flip WI-8 | Comments without `APIVersionId` would be invisible if WI-8 activates before WI-7 completes |
| **Approval activation** | WI-15 + WI-17 flag flip together | Backend expects version-level approval calls; SPA must be sending them |

Everything else is independently deployable and stable at rest. You can park at any point — between any two work items, for any duration — without system instability.

### 5.20 Rollback Strategy

Each work item (or gate group) is independently reversible by flipping its feature flag off:

| WI | Rollback |
|---|---|
| **1** | Drop the `APIVersions` container. No other entity references it yet. |
| **2** | Flip flag off. Uploads revert to not setting `APIVersionId`. The field remains on documents as a no-op. |
| **3** | No flag — data migration. To rollback: leave `APIVersionId` on documents (harmless). To fully undo: null out `APIVersionId` via background sweep (non-urgent). |
| **4** | No flag — config + field. Remove App Config keys; `RetainUntil` field ignored. |
| **5** | Flip retention flag off. Re-enable old `AutoArchiveAPIRevisions` / `AutoPurgeAPIRevisions`. `RetainUntil` fields ignored. |
| **6** | No flag — data migration. `RetainUntil` values sit inert until WI-5 flag is active. |
| **7** | No flag — data migration + forward-write. To rollback forward-write: flip WI-7 flag off; new comments stop getting `APIVersionId`. Backfilled values sit inert. |
| **8** | Flip comment-scoping flag off. `GetReviewContentAsync` reverts to `GetCommentsAsync(reviewId)` + `CommentVisibilityHelper`. |
| **9** | Remove toggle from UI. Falls back to version-scoped-only view. |
| **10** | No flag — data migration. Backfilled PR versions sit inert. |
| **11** | Flip PR-lifecycle flag off. `PullRequestManager` reverts to old path. |
| **12** | Flip promotion flag off. Post-merge builds always create new versions (old behavior). |
| **13** | Flip PR-retention flag off. Re-enable old `CleanupPullRequestData`. |
| **14** | No flag — config + code. Remove App Config keys; `ClassifyTransition` unused. |
| **15 + 17** | Flip approval flag off. `ToggleAPIRevisionApprovalAsync` re-enabled. `CarryForwardRevisionDataAsync` resumes. SPA reverts to revision-level approval. |
| **16** | Flip flag off. `GetReviewStatus` reverts to revision-level lookup. Copilot trigger reverts to revision-level. |

In all cases, rollback does not require data deletion — new fields and container become inert. A full rollback to pre-WI-1 state requires dropping the `APIVersions` container and clearing FK fields (a background sweep, not urgent).

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
