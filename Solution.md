# APIView: GitHub-like Submit Review

> **Status:** Draft Proposal  
> **Scope:** APIView review workflow (aligned with APIVersion-centric data model)

---

## Table of Contents

1. [Goal](#goal)
2. [Non-goals](#non-goals)
3. [Proposed Model](#proposed-model)
   - [New Class: `ReviewSubmissionModel`](#new-class-reviewsubmissionmodel)
   - [Existing Class: `CommentItemModel`](#existing-class-commentitemmodel)
4. [Database / Cosmos Changes](#database--cosmos-changes)
5. [API Surface Changes](#api-surface-changes)
6. [Comment Grouping Rules (`CommentIds`)](#comment-grouping-rules-commentids)
7. [Notification Behavior](#notification-behavior)
8. [UI Behavior (Minimal)](#ui-behavior-minimal)
9. [Migration Plan](#migration-plan)
10. [Testing Checklist](#testing-checklist)
11. [Open Questions](#open-questions)
12. [Optional Future Addition: `ReviewRequestModel`](#optional-future-addition-reviewrequestmodel)
13. [Compatibility with v2 Data Model](#compatibility-with-v2-data-model)

---

## Goal

Mimic GitHub submit-review behavior so that:

- Comments can be grouped into a single reviewer submission.
- `Approve` / `Feedback` decisions are explicit state transitions.
- Notifications can be sent as a batch at submit time.
- Current reviewer intent can be distinguished from historical comments.
- Service teams can explicitly trigger `Re-request Review` after addressing feedback.

## Non-goals

- Do **not** rewrite historical comments or thread behavior.

## Prerequisite

- This proposal assumes the APIVersion migration plan is already complete.
- Specifically, existing comments have already been backfilled to include `APIVersionId`.

## Proposed Model

Use submitted review events only, while keeping comments as they are today. In the APIVersion-centric model, submissions are scoped at the version level.

### New Class: `ReviewSubmissionModel`

Represents one submitted review event (`Approve` or `Feedback`).

| Field | Notes |
|---|---|
| `Id` | Unique submission ID |
| `ReviewId` | Parent review |
| `APIVersionId` | Active version at submit time (primary scope) |
| `APIRevisionId` | Optional snapshot of active revision at submit time |
| `ReviewerId` | Submitting reviewer |
| `Decision` | `Approve` or `Feedback` |
| `SubmissionMessage` | Optional message entered in the Submit Review dialog (included in notification email) |
| `CommentIds` | `List<string>` grouped into this submission |
| `SubmittedOn` | Submission timestamp |

### Existing Class: `CommentItemModel`

- No required schema changes.
- Optional optimization: add nullable `ReviewSubmissionId`.
- First iteration groups via `ReviewSubmissionModel.CommentIds`, so comment records remain unchanged.

## Database / Cosmos Changes

Current container already exists: `Comments` (partition key `/ReviewId`).

### Option A (Preferred: minimal risk)

Add container:

- `ReviewSubmissions` (partition key `/ReviewId`)

Why this option:

- Keeps comment read/write path unchanged.
- Adds a single new write path for submission records.
- Keeps timeline and notification queries explicit.
- Matches APIVersion-scoped comments (submission can query by `ReviewId + APIVersionId`).

### Option B (Not preferred)

Store submission metadata only in `ReviewListItemModel.ChangeHistory`.

Why not preferred:

- Timeline queries and analytics become less explicit and harder to evolve.

## API Surface Changes

### Endpoints

#### `POST /reviews/{reviewId}/comments` (existing)

- No behavior change.

#### `POST /reviews/{reviewId}/submit-review`

Input:

- `apiVersionId`
- `apiRevisionId` (optional; captured for traceability/deep link)
- `decision`
- `submissionMessage`
- `commentIds`

Behavior:

1. Validate `commentIds` belong to `reviewId` and `apiVersionId`, and are eligible for the submitting reviewer.
2. Create `ReviewSubmissionModel` with those `commentIds`.
3. Apply decision side effects:
  - `Approve`: set approved state for the active version.
  - `Feedback`: set not-approved state and record that feedback was submitted.
4. Trigger one batch notification to the service team containing message + inline comments since the reviewer’s last submitted review.

#### `POST /reviews/{reviewId}/re-request-review`

Input:

- `apiVersionId`
- `message` (optional)

Behavior:

1. Validate caller permissions for service team/author action.
2. Set review state to re-requested.
3. Trigger notification to reviewers that the API is ready for re-review.

Notes:

- `Re-request Review` does not create a `ReviewSubmissionModel`.
- New inline comments added after re-request are grouped into the reviewer’s next `Submit Review` event.

#### `GET /reviews/{reviewId}/submissions`

- Returns timeline of review submissions.

## Comment Grouping Rules (`CommentIds`)

On submit, the server computes comments where:

- `CreatedBy == reviewer`
- `ReviewId == current review`
- `APIVersionId == active version`
- `CreatedOn > reviewer’s last submission time for this review/version`
- Not already included in a prior submission

Version scoping: submit uses `apiVersionId`, and grouping requires `APIVersionId == active version`. That blocks cross-version bleed.

Important edge case: on a reviewer’s first submission for a version (no prior submission timestamp), historical comments by that reviewer in the same `APIVersionId` can be considered eligible and can be included.

Optional strict mode:

- If desired, additionally require `APIRevisionId == active revision` to submit only comments authored on the latest revision snapshot.

Diagnostic comments are explicitly excluded from submit-review grouping: they remain informational/system-generated comments and are not included in `commentIds` for `ReviewSubmissionModel`.

## Notification Behavior

Batch notification happens on submit-review events only.

- Collect comments via `ReviewSubmissionModel.CommentIds`.
- Send one notification/email payload containing:
  - reviewer,
  - decision,
  - version (`APIVersionId` / version label),
  - submission message,
  - list of comments (or deep links to threads) since the reviewer’s last submitted review.

No batching requirement when comments are added live.

Re-request review triggers a separate notification to reviewers and does not create a `ReviewSubmissionModel` event.

Diagnostic comments also do not create submit-review notification batches.

## UI Behavior (Minimal)

- Current two-state `Approve` / `Revoke Approval` button is replaced with `Submit Review`.
- `Submit Review` opens a pop-up with:
  - Decision: `Feedback` or `Approve`
  - Optional comment field (used in notification email)
- On submit, comments since the reviewer’s last submitted review are grouped into a `ReviewSubmission` event.
- Service team sees either:
  - `Approved` (if last reviewer decision is approve), or
  - `Re-request Review` (if last reviewer decision is feedback)
- Selecting `Re-request Review` sends email to reviewers that the API is ready for re-review.

## Migration Plan

1. Create container for `ReviewSubmissions`.
2. Keep existing comments unchanged and ungrouped (legacy data).
3. Ensure new submission writes include `APIVersionId` (and optional `APIRevisionId` snapshot).
4. Apply grouping only to new submit-review events.

## Testing Checklist

1. Single reviewer submits with multiple `commentIds` → one submission, one batch notification.
2. Single reviewer submits `Approve` → existing approval state updates and submission is recorded.
3. Two reviewers submit independently → two submissions, no cross-contamination.
4. Single reviewer submits `Feedback` → review remains not approved and submission is recorded.
5. Submit includes invalid/foreign `commentIds` → request is rejected.
6. Legacy comments remain visible and queryable.
7. Cross-version isolation: submission for version A never includes comments from version B.
8. `Re-request Review` sends notification to reviewers without creating a new `ReviewSubmissionModel`.

## Open Questions

### Empty submit

Define whether `Approve` / `Feedback` with zero comments is allowed.

- `Approve`: Yes
- `Feedback`: No

### Post-submit edits

Define whether editing/deleting an included comment should change historical submission rendering.

- Proposed: No.
- Rationale: history can remain “reviewer submitted a review,” without replaying mutable comment details.

### Ownership validation

Clarify whether `commentIds` must be created only by the submitting reviewer, or can include bot/other comments.

- AVC can be treated like any other reviewer.
- Since AVC emits comments in batch, submission logging should be simpler (no incremental collection needed).

## Optional Future Addition: `ReviewRequestModel`

> **Optional / Not in first version**: This section describes a possible future enhancement. The current proposal does **not** require this model.

If we need stronger review-cycle tracking later, we can introduce a `ReviewRequestModel` created when the service team requests (or re-requests) review.

| Field | Notes |
|---|---|
| `Id` | Unique request ID |
| `ReviewId` | Parent review |
| `APIVersionId` | Target version |
| `ReviewerId` | Reviewer this request applies to |
| `RequestedBy` | User/service account that requested review |
| `RequestedOn` | Start of review window |
| `Status` | `Open`, `Submitted`, `Canceled` |
| `SubmittedOn` | Optional timestamp when reviewer submits |
| `SubmissionId` | Optional link to `ReviewSubmissionModel` |

How it would be used:

- Initial cycle: when a version/revision first enters review, create one request record per reviewer.
  - Adding a reviewer to a version creates that reviewer’s initial request record (`Status = Open`, `RequestedOn = now`).
- `Re-request Review` creates one request record per reviewer.
- `Submit Review` links to that request record.
  - On successful `Submit Review`, set request `SubmittedOn` to server UTC now and transition request `Status` from `Open` to `Submitted`.
- Comment collection uses `RequestedOn -> SubmittedOn` directly from the request record.

Open-request invariant (important):

- At most one `Open` request exists for a given `ReviewId + APIVersionId + ReviewerId`.
- If `Re-request Review` is triggered while one is already `Open`, reuse/update the existing open request (do not create a second open record).
- Create a new request record only after the previous one is no longer open (`Submitted` or `Canceled`).

Reviewer assignment lifecycle:

- Add reviewer: create request record with `Status = Open`.
- Remove reviewer before submit: set open request to `Canceled` (retain record for audit).
- Remove reviewer after submit: keep submitted history; do not create new open requests unless reviewer is added again.

Why this is optional:

- It provides cleaner auditability and bracketing,
- but adds model/API/storage complexity.
- The first version stays submission-only and can adopt this later if needed.

## Compatibility with v2 Data Model

This proposal is compatible with the APIVersion-centric architecture and can be implemented without changing core v2 boundaries.

| v2 concept | Submit-review alignment |
|---|---|
| `APIVersion` is the primary scope for approval/comments | `ReviewSubmissionModel` is keyed by `APIVersionId` (with optional `APIRevisionId` snapshot) |
| Comments are version-scoped | Submission validation and grouping are constrained to `ReviewId + APIVersionId` |
| Approval is version-level, not revision-level | `Approve` decision applies to version-level approval state |
| Revision churn within a version should not break comment history | Submission references comment IDs; optional revision snapshot is informational only |
| PR versions are first-class `APIVersion` entities | Submit-review works the same for release and PR versions |

### Implementation notes

- No conflict with APIVersion aliasing (`CanonicalVersionId`): submissions remain tied to the logical version under review.
- No dependency on blob-storage strategy: submit-review groups comments/decisions only.
- Migration is additive: legacy comments remain readable; new submit events become version-aware immediately.
