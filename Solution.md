# APIView: GitHub-like Submit Review

> **Status:** Draft Proposal  
> **Scope:** APIView review workflow (aligned with Version-centric data model)

---

## Table of Contents

1. [Goal](#goal)
2. [Prerequisite](#prerequisite)
3. [Proposed Model](#proposed-model)
   - [New Class: `ReviewSubmissionModel`](#new-class-reviewsubmissionmodel)
   - [New Class: `ReviewRequestModel`](#new-class-reviewrequestmodel)
4. [Database / Cosmos Changes](#database--cosmos-changes)
5. [API Surface Changes](#api-surface-changes)
6. [Comment Grouping Rules](#comment-grouping-rules)
7. [Copilot Review Behavior](#copilot-review-behavior)
8. [Notification Behavior](#notification-behavior)
9. [UI Behavior (Minimal)](#ui-behavior-minimal)
10. [Testing Checklist](#testing-checklist)

---

## Goal

Mimic GitHub submit-review behavior so that:

- Comments can be grouped into a single reviewer submission.
- `Approve` / `Feedback` decisions are explicit state transitions.
- Notifications can be sent as a batch at submit time.
- Current reviewer intent can be distinguished from historical comments.
- Service teams can explicitly trigger another review request after addressing feedback.
- Overall approval state is derived from each reviewer's latest submitted decision for the version.

## Prerequisite

- This proposal assumes the Version migration plan is already complete.
- Specifically, existing comments have already been backfilled to include `VersionId`.

## Proposed Model

Reviewer decisions are captured through explicit submission events (`Approve` / `Feedback`) rather than inferred from comments. Comments remain unchanged. In the version-centric model, each submission is scoped to a specific version.

### New Class: `ReviewSubmissionModel`

Represents one submitted review event (`Approve` or `Feedback`).

This is the review outcome record. It captures what the reviewer actually submitted, when they submitted it, which comments were included, and any additional state captured at submit time, such as the approval API hash.

| Field | Notes |
|---|---|
| `Id` | Unique submission ID |
| `ReviewId` | Parent review |
| `VersionId` | Active version at submit time |
| `ReviewerId` | Submitting reviewer |
| `Decision` | `Approve` or `Feedback` |
| `ApiHash` | API hash captured at approval time; null for non-approve submissions |
| `SubmissionMessage` | Optional message entered in the Submit Review dialog (included in notification email) |
| `CommentIds` | `List<string>` grouped into this submission |
| `SubmittedOn` | Submission timestamp |

### New Class: `ReviewRequestModel`

Represents a review window opened for a specific reviewer on a specific version.

This is the review window record. It captures who was asked to review, which version they were asked to review, when that window opened, whether it is still open, and whether it was later submitted or canceled.

Each open `ReviewRequestModel` can produce at most one `ReviewSubmissionModel`.

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

#### `POST /reviews/{reviewId}/{versionId}/reviewers` (Existing Endpoint)

This is an existing API endpoint used for adding/updating reviewers on a version.

In this proposal, we modify this existing add-reviewer endpoint to also drive review-request lifecycle behavior:

- Initial assignment: adds reviewer membership, creates `ReviewRequestModel` (`Status = Open`), and notifies reviewer.
- Re-request: if reviewer is already assigned, the endpoint creates a new `ReviewRequestModel` (`Status = Open`) for that reviewer/version and notifies reviewer (per open-request invariant, does not reuse canceled requests).
- This action does not create a `ReviewSubmissionModel` ever.
- UI trigger: a per-reviewer `Request Review` action (arrow icon, GitHub-style) invokes this same endpoint for already-assigned reviewers (when they do not already have an open request).

##### Reviewer Removal Lifecycle

When a reviewer is removed from a review:

- If the reviewer has an open (not yet submitted) `ReviewRequestModel`, set its `Status = Canceled` and record the `CanceledOn` timestamp.
- Retain the canceled request record for audit trail; do not delete it.
- Removing a reviewer affects who is currently requested, but does not automatically invalidate or dismiss any already-submitted `ReviewSubmissionModel`.
- If the reviewer is re-added later, create a new request record (per open-request invariant, do not reuse a canceled record).

#### `POST /reviews/{reviewId}/submit-review`

Input:

- `versionId`
- `revisionId` (required — needed to capture the API hash)
- `decision`
- `submissionMessage`

Behavior:

1. Resolve reviewer request context for this reviewer/version:
    - This fallback path applies to human submitters only.
    - If an open `ReviewRequestModel` exists for this reviewer/version, use it.
    - If no open request exists, auto-add the submitting user to reviewer membership for this version.
    - In the no-open-request path, create an implicit `ReviewRequestModel` (`Status = Open`) for this reviewer/version with `RequestedBy` set to the submitting user and `RequestedOn` set to:
        - The reviewer's most recent prior `ReviewSubmissionModel.SubmittedOn` on the same version, if one exists, or
        - The version creation timestamp if no prior submission exists.
2. Query for eligible `commentIds` using [comment grouping rules](#comment-grouping-rules).
3. Enforce empty-submit policy:
    - `Feedback` requires at least one grouped comment or a non-empty `submissionMessage`.
    - `Approve` may be submitted with no comments and no message.
4. Preserve existing unresolved-comment approval gating:
  - Any current server-side rule that blocks `Approve` while unresolved comments exist remains unchanged in this proposal.
5. Create `ReviewSubmissionModel` with those `commentIds`.
6. Complete the active request lifecycle for this reviewer/version:
    - Set the corresponding open `ReviewRequestModel.Status` to `Submitted`.
    - Set `ReviewRequestModel.SubmittedOn` to the submit timestamp.
    - Set `ReviewRequestModel.SubmissionId` to the new `ReviewSubmissionModel.Id`.
7. Apply decision side effects:
  - `Approve`: read the API hash from the specified revision (`revisionId`), persist it on the submission (`ApiHash`), and record this reviewer/version latest decision as `Approve`.
  - `Feedback`: record this reviewer/version latest decision as `Feedback`.
  - Recompute version approval state on the server from reviewers' latest submitted decisions for the version, then persist the result to the `APIVersionModel` record:
    - Precedence rule: `Feedback` supersedes `Approve`.
    - A version is `Not Approved` if any reviewer's latest submitted decision for the version is `Feedback`.
    - A version is `Approved` only if there is at least one reviewer whose latest submitted decision for the version is `Approve` and there is no `Feedback` for the version.
    - A reviewer's request/assignment state does not by itself change approval state.
    - Removing a reviewer affects the current requested-reviewer set, but does not automatically invalidate that reviewer's previously submitted approval.
    - If there are no `Approve` submissions for the version, the version is `Not Approved`.
    - Copilot submissions are excluded from approval-state calculation.
8. Trigger one batch notification to the service team containing message + inline comments since the reviewer’s last submitted review.

## Comment Grouping Rules

For human reviewers, on submit the server queries comments within the window defined by the reviewer's active request context and the new `ReviewSubmissionModel`.

Comments are eligible if:

- `CreatedBy == ReviewRequestModel.ReviewerId`
- `VersionId == ReviewRequestModel.VersionId`
- `CreatedOn >= ReviewRequestModel.RequestedOn`
- `CreatedOn < ReviewSubmissionModel.SubmittedOn`
- Not a diagnostic/system-generated comment, and not a Copilot-authored comment when grouping a human reviewer's submission

Because each re-request creates a new `ReviewRequestModel` with a fresh `RequestedOn`, the active request window naturally excludes comments that were part of an earlier submission.

Copilot grouping and request-window semantics are defined in [Copilot Review Behavior](#copilot-review-behavior).

Submission history is immutable: once `ReviewSubmissionModel.CommentIds` is recorded, later edits/deletes to those comments do not rewrite historical submission rendering.

## Copilot Review Behavior

Copilot review is a first-class review flow, not a special case of human review submission.

- Copilot does not participate in reviewer assignment lifecycle and does not use `ReviewRequestModel` records.
- Copilot does not use the human no-open-request fallback and is never auto-added to reviewer membership.
- Copilot comment grouping is evaluated in a Copilot-specific submission window on the same `ReviewId` + `VersionId`.
- Window end is current `ReviewSubmissionModel.SubmittedOn`.
- Window start is the `SubmittedOn` of Copilot's most recent prior submission on that version, or version creation timestamp when none exists.
- Eligible comments must be Copilot-authored, match `VersionId`, fall within the window, and exclude diagnostic/system-generated comments.
- Copilot comments are grouped only into Copilot's own `ReviewSubmissionModel`.
- Copilot comments must never be bundled with an architect's or any other human reviewer's submission.
- Copilot may complete a review and submit a batch of comments as its own review event.
- Copilot submissions are advisory only: they do not approve a version and do not affect approval state.
- For Copilot submissions, `Decision` may be stored for audit/history, but it is ignored for approval-state transitions.

## Notification Behavior

Batch notification happens on submit-review events only.

- Recipients: service team and subscribers only.
- Collect comments via `ReviewSubmissionModel.CommentIds`.
- Send one notification/email payload containing:
  - reviewer,
  - decision,
  - version (`VersionId` / version label),
  - submission message,
  - list of comments (or deep links to threads) since the reviewer’s last submitted review.
- `Approve` and `Feedback` use different email subject/body content.
- For `Approve`, the current approved email is sufficient, with the optional submit-review comment added when present.

Live comment creation does not trigger a notification event; comment notifications are triggered only on submit-review.

Add-reviewer (initial assignment or re-request) triggers a notification to reviewers, using the same email template for both cases.

In the no-open-request submit fallback path, creating the implicit request does not send a reviewer-request notification.

Removing a reviewer does not trigger a notification.

Copilot batched comments also trigger an email notification.

Diagnostic comments also do not create submit-review notification batches.

## UI Behavior (Minimal)

- Current two-state `Approve` / `Revoke Approval` button is replaced with `Submit Review`.
- `Submit Review` opens a pop-up with:
  - Decision: `Feedback` or `Approve`
  - Optional comment field (used in notification email)
- Adding a reviewer uses the existing dropdown/click flow (unchanged).
- Each assigned reviewer in the reviewer list has a small arrow icon next to their name to represent `Re-Request Review`, similar to GitHub, for re-requesting review without removing them.
- Clicking the button creates a new `ReviewRequestModel` (`Status = Open`), opens a request window for that reviewer, and sends a notification.
- The button is disabled and shown as `Already Requested` when that reviewer already has an open request.
- After that reviewer submits and the open request transitions to `Submitted`, the button becomes enabled again so the service team can issue another re-request without removing the reviewer.

## Testing Checklist

1. Single reviewer submits with multiple `commentIds` → one submission, one batch notification.
2. Single reviewer submits `Approve` → existing approval state updates and submission is recorded.
3. Two reviewers submit independently → two submissions, no cross-contamination.
4. Single reviewer submits `Feedback` → review remains not approved and submission is recorded.
5. Legacy comments remain visible and queryable.
6. Cross-version isolation: submission for version A never includes comments from version B.
7. Reviewer already assigned with no open request: clicking `Request Review` button creates a new `ReviewRequestModel` and sends notification without creating a `ReviewSubmissionModel`.
8. Reviewer already assigned with an open request: `Request Review` button is disabled and shows `Already Requested`.
9. Copilot submission creates its own `ReviewSubmissionModel` and never bundles comments with a human reviewer's submission.
10. Copilot submission never sets approved state, even if a `Decision` value is present.
11. `Feedback` submit with no grouped comments and empty `submissionMessage` is rejected.
12. `Approve` submit with no comments and empty `submissionMessage` is accepted.
13. Editing/deleting a comment after submit does not rewrite historical submission rendering.
14. Reviewer A submits `Approve`, then reviewer B submits `Feedback` (blocking): version remains not approved until policy is satisfied.
15. A later `Approve` from reviewer B updates only reviewer B's latest decision; approval state is recomputed from all reviewers' latest decisions.
16. One reviewer's submission does not overwrite another reviewer's latest decision record.
17. Reviewer A submits `Approve` and reviewer B has not submitted yet: version remains approved unless there is blocking `Feedback`.
18. Two assigned reviewers both submit `Approve`: version becomes approved.
19. Removing a reviewer cancels any open request for that reviewer, but does not automatically invalidate that reviewer's previously submitted approval.
20. No reviewers assigned and no `Approve` submissions for the version: version remains not approved.
21. One reviewer submits `Approve`, is later removed, and no other blocking feedback exists: version remains approved.
22. Submit with no open request auto-adds submitter as reviewer, creates an implicit `ReviewRequestModel`, and still succeeds.
23. Submit with no open request uses prior-submission boundary (or version creation time for first submit) as `RequestedOn` anchor for comment grouping.
24. Existing unresolved-comment approval gating behavior is unchanged: if current server rules block `Approve` with unresolved comments, submit is rejected the same way as today.
