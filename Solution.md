# APIView: GitHub-like Submit Review

> **Status:** Draft Proposal  
> **Scope:** APIView review workflow (aligned with Version-centric data model)

---

## Table of Contents

1. [Goal](#goal)
2. [Proposed Model](#proposed-model)
   - [New Class: `ReviewSubmissionModel`](#new-class-reviewsubmissionmodel)
   - [New Class: `ReviewRequestModel`](#new-class-reviewrequestmodel)
3. [Database / Cosmos Changes](#database--cosmos-changes)
4. [API Surface Changes](#api-surface-changes)
5. [Comment Grouping Rules (`CommentIds`)](#comment-grouping-rules-commentids)
6. [Copilot Review Behavior](#copilot-review-behavior)
7. [Notification Behavior](#notification-behavior)
8. [UI Behavior (Minimal)](#ui-behavior-minimal)
9. [Testing Checklist](#testing-checklist)
10. [Resolved Questions](#resolved-questions)

---

## Goal

Mimic GitHub submit-review behavior so that:

- Comments can be grouped into a single reviewer submission.
- `Approve` / `Feedback` decisions are explicit state transitions.
- Notifications can be sent as a batch at submit time.
- Current reviewer intent can be distinguished from historical comments.
- Service teams can explicitly trigger another review request after addressing feedback.

## Prerequisite

- This proposal assumes the Version migration plan is already complete.
- Specifically, existing comments have already been backfilled to include `VersionId`.

## Proposed Model

Use submitted review events only, while keeping comments as they are today. In the Version-centric model, submissions are scoped at the version level.

### New Class: `ReviewSubmissionModel`

Represents one submitted review event (`Approve` or `Feedback`).

| Field | Notes |
|---|---|
| `Id` | Unique submission ID |
| `ReviewId` | Parent review |
| `VersionId` | Active version at submit time (primary scope) |
| `ReviewerId` | Submitting reviewer |
| `Decision` | `Approve` or `Feedback` |
| `ApiHash` | API hash captured at approval time; null for non-approve submissions |
| `SubmissionMessage` | Optional message entered in the Submit Review dialog (included in notification email) |
| `CommentIds` | `List<string>` grouped into this submission |
| `SubmittedOn` | Submission timestamp |

### New Class: `ReviewRequestModel`

Represents a review window opened for a specific reviewer on a specific version.

| Field | Notes |
|---|---|
| `Id` | Unique request ID |
| `ReviewId` | Parent review |
| `VersionId` | Target version |
| `ReviewerId` | Reviewer this request applies to |
| `RequestedBy` | User/service account that requested review |
| `RequestedOn` | Start of review window |
| `Status` | `Open`, `Submitted`, `Canceled` |
| `SubmittedOn` | Optional timestamp when reviewer submits |
| `CanceledOn` | Optional timestamp when request is canceled (reviewer removal) |
| `SubmissionId` | Optional link to `ReviewSubmissionModel` |

## Database / Cosmos Changes

### Proposed Approach

Add containers:

- `ReviewSubmissions` (partition key `/ReviewId`)
- `ReviewRequests` (partition key `/ReviewId`)

Why this approach:

- Keeps comment read/write path unchanged.
- Adds a single new write path for submission records.
- Keeps timeline and notification queries explicit.
- Matches Version-scoped comments (submission can query by `ReviewId + VersionId`).

## API Surface Changes

### Endpoints

#### `POST /reviews/{reviewId}/submit-review`

Input:

- `versionId`
- `revisionId` (required — needed to capture the API hash)
- `decision`
- `submissionMessage`

Behavior:

1. Query for eligible `commentIds` using comment grouping rules (reviewer, active version, time window, not previously submitted).
2. Create `ReviewSubmissionModel` with those `commentIds`.
3. Apply decision side effects:
  - `Approve`: read the API hash from the specified revision (`revisionId`), persist it on the submission (`ApiHash`), and set approved state for the active version.
  - `Feedback`: set not-approved state and record that feedback was submitted.
4. Trigger one batch notification to the service team containing message + inline comments since the reviewer’s last submitted review.


The existing add-reviewer endpoint is the single API action used to request review:

- Initial assignment: adds reviewer membership, creates `ReviewRequestModel` (`Status = Open`), and notifies reviewer.
- Re-request: if reviewer is already assigned, the endpoint creates a new request window and notifies reviewer (per open-request invariant, does not reuse canceled requests).
- This action does not create a `ReviewSubmissionModel`.
- UI trigger: a per-reviewer `Request Review` action (arrow icon, GitHub-style) invokes this same endpoint for already-assigned reviewers (when they do not already have an open request).

##### Reviewer Removal Lifecycle

When a reviewer is removed from a review:

- If the reviewer has an open (not yet submitted) `ReviewRequestModel`, set its `Status = Canceled` and record the `CanceledOn` timestamp.
- Retain the canceled request record for audit trail; do not delete it.
- If the reviewer is re-added later, create a new request record (per open-request invariant, do not reuse a canceled record).

#### `GET /reviews/{reviewId}/versions/{versionId}/submissions`

- Returns timeline of review submissions for the specified version.

## Comment Grouping Rules (`CommentIds`)

On submit, the server queries comments between the review window opened by the request record and the submission time.

Comments are eligible if:

- `CreatedBy == reviewer`
- `VersionId == active version`
- `CreatedOn >= reviewer's RequestedOn` (per the open request record)
- `CreatedOn < SubmittedOn` (on submit)
- Not already included in a prior submission

Review submissions are scoped by version: each `ReviewSubmissionModel` is tied to a specific `VersionId`, so submissions never cross version boundaries.

Diagnostic comments are explicitly excluded from submit-review grouping: they remain informational/system-generated comments and are not included in `commentIds` for `ReviewSubmissionModel`.

## Copilot Review Behavior

Copilot review is a first-class review flow, not a special case of human review submission.

- Copilot comments are grouped only into Copilot's own `ReviewSubmissionModel`.
- Copilot comments must never be bundled with an architect's or any other human reviewer's submission.
- Copilot may complete a review and submit a batch of comments as its own review event.
- Copilot submissions are advisory only: they do not approve a version and do not affect approval state.
- For Copilot submissions, `Decision` may be stored for audit/history, but it is ignored for approval-state transitions.

## Notification Behavior

Batch notification happens on submit-review events only.

- Collect comments via `ReviewSubmissionModel.CommentIds`.
- Send one notification/email payload containing:
  - reviewer,
  - decision,
  - version (`VersionId` / version label),
  - submission message,
  - list of comments (or deep links to threads) since the reviewer’s last submitted review.

No batching requirement when comments are added live.

Add-reviewer (initial assignment or re-request) triggers a notification to reviewers and does not create a `ReviewSubmissionModel` event.

Diagnostic comments also do not create submit-review notification batches.

## UI Behavior (Minimal)

- Current two-state `Approve` / `Revoke Approval` button is replaced with `Submit Review`.
- `Submit Review` opens a pop-up with:
  - Decision: `Feedback` or `Approve`
  - Optional comment field (used in notification email)
- Service team sees either:
  - `Approved` (if last reviewer decision is approve), or
  - `Request Review` (if last reviewer decision is feedback)
- Adding a reviewer uses the existing dropdown/click flow (unchanged).
- Each assigned reviewer in the reviewer list has a small `Request Review` button (arrow icon) next to their name for re-requesting review without removing them.
- Clicking the button creates a new `ReviewRequestModel` (`Status = Open`), opens a request window for that reviewer, and sends a notification.
- The button is disabled and shown as `Already Requested` when that reviewer already has an open request.

## Testing Checklist

1. Single reviewer submits with multiple `commentIds` → one submission, one batch notification.
2. Single reviewer submits `Approve` → existing approval state updates and submission is recorded.
3. Two reviewers submit independently → two submissions, no cross-contamination.
4. Single reviewer submits `Feedback` → review remains not approved and submission is recorded.
5. Submit includes invalid/foreign `commentIds` → request is rejected.
6. Legacy comments remain visible and queryable.
7. Cross-version isolation: submission for version A never includes comments from version B.
8. Reviewer already assigned with no open request: clicking `Request Review` button creates a new `ReviewRequestModel` and sends notification without creating a `ReviewSubmissionModel`.
9. Reviewer already assigned with an open request: `Request Review` button is disabled and shows `Already Requested`.
10. Copilot submission creates its own `ReviewSubmissionModel` and never bundles comments with a human reviewer's submission.
11. Copilot submission never sets approved state, even if a `Decision` value is present.

## Resolved Questions

### Empty submit

Define whether a reviewer can submit with zero comments or messages.

**For `Feedback` (submitting a review):** Not allowed — feedback requires either comments or a message. You cannot submit an empty review.

**For `Approve`:** Allowed — approval can be submitted without any comments or message.

### Post-submit edits

Define whether editing/deleting an included comment should change historical submission rendering.

- Proposed: No.
- Rationale: history can remain “reviewer submitted a review,” without replaying mutable comment details.

