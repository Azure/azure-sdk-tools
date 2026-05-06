# APIView: GitHub-like Submit Review

> **Scope:** APIView review workflow (aligned with Version-centric data model)

---

## Table of Contents

1. [Goal](#goal)
2. [Prerequisite](#prerequisite)
3. [Proposed Model](#proposed-model)
   - [New Class: `ReviewerState`](#new-class-reviewerstate)
   - [Change History Shape](#change-history-shape)
4. [Database / Cosmos Changes](#database--cosmos-changes)
5. [API Surface Changes](#api-surface-changes)
6. [Comment Grouping Rules](#comment-grouping-rules)
7. [Copilot Review Behavior](#copilot-review-behavior)
8. [Notification Behavior](#notification-behavior)
9. [UI Behavior (Minimal)](#ui-behavior-minimal)
10. [Testing Checklist](#testing-checklist)
11. [Open Scenario](#open-scenario)

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

Reviewer decisions are captured through explicit submission events (`Approve` / `Feedback`) rather than inferred from comments. Comments remain unchanged. In the version-centric model, each reviewer has exactly one `ReviewerState` per version.

### New Class: `ReviewerState`

Represents the latest review state for one reviewer on one version.

The same `ReviewerState` record is reused for initial request, re-request, submit, and cancel operations.

| Field | IsConvenienceProperty | Notes |
|---|---|---|
| `Id` | No | Unique reviewer state ID |
| `VersionId` | No | Target version |
| `ReviewerId` | No | Reviewer this state applies to |
| `RequestedBy` | Yes | User or service account that most recently requested review |
| `RequestedOn` | Yes | Timestamp of the most recent request; used for audit and notification, not for comment grouping |
| `Status` | No | `AwaitingArchitect`, `AwaitingServiceTeam`, `Canceled` |
| `SubmissionDecision` | Yes | Latest submitted `Approve` or `Feedback` |
| `ContentHash` | No | SHA-256 hash of the approved revision's API surface, captured only for `Approve` |
| `SubmittedOn` | Yes | Timestamp of the latest submission; used as the start of the next review window |
| `ChangeHistory` | No | Ordered lifecycle history for this reviewer state |

### Change History Shape

`ReviewerState.ChangeHistory` is the lifecycle and audit source of truth.
This reuses/extends the existing APIView `ChangeHistory` model already used in the codebase.

Each entry includes:

- `ChangeAction`: `Requested`, `Submitted`, `Canceled`
- `ChangedBy`: the user or service responsible for the transition — the requester for `Requested`, the reviewer for `Submitted`, and the user who removed the reviewer for `Canceled`
- `ChangedOn`
- `SubmissionMessage` (present on `Submitted` entries only)
- `CommentIds` (present on `Submitted` entries only)
- `Notes` (optional)

## Database / Cosmos Changes

Add one container:

- `ReviewerStates` (partition key `/ReviewId`)

## API Surface Changes

### Endpoints

#### `POST /api/review/{reviewId}/version/{versionId}/reviewers`

In this proposal, the same endpoint that drives the add-reviewer behavior will also drive `ReviewerState` lifecycle behavior:

- The endpoint handles both first-time assignment and re-request using the same `ReviewerState` record.
- Initial assignment (reviewer not currently assigned):
   - Add reviewer membership.
   - Create `ReviewerState` if missing.
   - Set `Status = AwaitingArchitect`, update `RequestedBy` and `RequestedOn`, append `Requested` in `ChangeHistory`, and notify reviewer.
- Re-request (reviewer already assigned and existing `ReviewerState.Status = AwaitingServiceTeam`):
   - Reuse that same existing `ReviewerState` (the previously submitted record).
   - Set `Status = AwaitingArchitect`, update `RequestedBy` and `RequestedOn`, append another `Requested` in `ChangeHistory`, and notify reviewer.

##### Reviewer Removal Lifecycle

When a reviewer is removed from a review:

- Reuse the same `ReviewerState`, append `Canceled` in `ChangeHistory`, and set `Status = Canceled`.
- Retain the reviewer state record for audit trail; do not delete it.
- Removing a reviewer affects who is currently requested, but does not automatically invalidate that reviewer's latest submitted decision.
- If the reviewer is re-added later, reuse the same `ReviewerState`, set `Status = AwaitingArchitect`, update `RequestedBy` and `RequestedOn`, and record another `Requested` entry.

#### `POST /api/review/{reviewId}/version/{versionId}/submit-review`

Input:

- `revisionId` (required for `Approve` - needed to capture the `ContentHash`; optional for `Feedback`)
- `submissionDecision`
- `submissionMessage`

Behavior:

1. Resolve reviewer state for this reviewer and version:
   - This fallback path applies to human submitters only.
   - If a `ReviewerState` already exists for this reviewer and version, use that record.
   - If no `ReviewerState` exists:
      - Auto-add the submitting user to reviewer membership for this version.
      - Create a `ReviewerState` with `RequestedBy` = submitting user and `RequestedOn` = current time.
      - Append `Requested` to `ChangeHistory` for this newly created state.
2. Query for eligible `commentIds` using [comment grouping rules](#comment-grouping-rules).
3. Enforce empty-submit policy:
   - `Feedback` requires at least one grouped comment or a non-empty `submissionMessage`.
   - `Approve` may be submitted with no comments and no message.
4. Preserve existing unresolved-comment approval gating:
   - Any current server-side rule that blocks `Approve` while unresolved comments exist remains unchanged in this proposal.
   - This includes existing severity behavior (for example, unresolved `MUST FIX`/`SHOULD FIX` block `Approve` while `SUGGESTION`/`QUESTION` do not).
   - See [Open Scenarios](#open-scenarios) for the related edge case of a `MUST FIX` comment added after an `Approve` has already been submitted.
5. Submit the reviewer state:
   - Set `SubmissionDecision`, `SubmittedOn`, and `Status = AwaitingServiceTeam`.
   - Append a `Submitted` entry to `ChangeHistory` with `SubmissionMessage` and `CommentIds`.
6. Apply decision side effects:
   - `Approve`: read the `ContentHash` from `APICodeFileModel` on the specified revision (`revisionId`) and persist it on the reviewer state. This records the API surface fingerprint at approval time so the system can later determine whether a new revision has the same API surface and can auto-inherit approval, without downloading blobs.
   - Recompute version approval state on the server from each reviewer's current submitted decision for the version, then persist the result to the `APIVersionModel` record:
      - Precedence rule: `Feedback` supersedes `Approve`.
      - A version is `Not Approved` if any reviewer's current submitted decision for the version is `Feedback`.
      - A version is `Approved` only if there is at least one reviewer whose current submitted decision for the version is `Approve` and there is no `Feedback` for the version.
      - Reviewer assignment state does not by itself change approval state.
      - Removing a reviewer affects the current requested-reviewer set, but does not automatically invalidate that reviewer's previously submitted approval.
      - If there are no `Approve` submissions for the version, the version is `Not Approved`.
      - Copilot submissions are excluded from approval-state calculation.
7. Trigger one batch notification to the service team containing message and inline comments from the current review window.

## Comment Grouping Rules

For human reviewers, on submit the server queries comments within the window defined by the reviewer's current `ReviewerState`.

Comments are eligible if:

- `CreatedBy == ReviewerState.ReviewerId`
- `VersionId == ReviewerState.VersionId`
- `CreatedOn >= ReviewerState.SubmittedOn` (the previous submission time; or version creation time if no prior submission exists)
- `CreatedOn < current submit time`
- Not a diagnostic or system-generated comment, and not a Copilot-authored comment when grouping a human reviewer's submission

The window is always "since the last time this reviewer submitted on this version" — `RequestedOn` is not used as the window boundary.

Copilot grouping semantics are defined in [Copilot Review Behavior](#copilot-review-behavior).

## Copilot Review Behavior

Copilot review is a first-class review flow, not a special case of human review submission.

- Copilot review is triggered by the `Request Copilot Review` button.
- `Request Copilot Review` creates or reopens the Copilot `ReviewerState` for the current `VersionId`, sets `Status = AwaitingArchitect`, updates `RequestedBy` and `RequestedOn`, and records `Requested` in `ChangeHistory`.
- When the Copilot review run completes, the system submits that same `ReviewerState`: `Status` transitions to `Submitted`, submit fields are updated, and `Submitted` is recorded in `ChangeHistory`.
- Copilot comment grouping is evaluated within the active Copilot review window on the same `VersionId`.
- Window start is the previous `ReviewerState.SubmittedOn` (or version creation time if never submitted) and window end is the current submission time.
- Eligible comments must be Copilot-authored, match `VersionId`, fall within the window, and exclude diagnostic or system-generated comments.
- Copilot comments must never be bundled with a human reviewer's submission.
- For Copilot submissions, `SubmissionDecision` is stored as `Feedback`.
- Copilot submissions are advisory only: they do not approve a version and do not affect approval state.

## Notification Behavior

Notifications are triggered only by explicit review workflow actions.

- Submit-review notification (human and Copilot):
   - Trigger: `POST /api/review/{reviewId}/version/{versionId}/submit-review`.
   - Recipients: service team and subscribers only.
   - Delivery: one batch email per submit.
   - Payload includes:
      - reviewer,
      - submissionDecision,
      - version (`VersionId` / version label),
      - submission message,
      - comments (or deep links to threads) grouped for that submission.
   - `Approve` and `Feedback` use different subject/body content.
   - For `Approve`, the current approved email template is sufficient, with the optional submit-review message included when present.

- Reviewer-request notification:
   - Trigger: `POST /api/review/{reviewId}/version/{versionId}/reviewers` for initial assignment or re-request.
   - Recipients: requested reviewers.
   - Template: same email template for initial assignment and re-request.
   - Exception: in the no-reviewer-state submit fallback path, creating the implicit reviewer state does not send a reviewer-request notification.

- Events that do not trigger notifications:
   - Live comment creation does not trigger submit-review notification.
   - Removing a reviewer does not trigger a notification.
   - Diagnostic comments do not trigger submit-review notification batches.

## UI Behavior (Minimal)

- Current two-state `Approve` / `Revoke Approval` button is replaced with `Submit Review`.
- `Submit Review` opens a pop-up with:
  - Decision: `Feedback` or `Approve`
  - Optional comment field (used in notification email)
- Adding a reviewer uses the existing dropdown and click flow (unchanged).
- The `Re-Request Review` arrow is shown only when that reviewer's current `Status = AwaitingServiceTeam`, matching GitHub behavior.
- Clicking the arrow reuses the same `ReviewerState`, records `Requested` in `ChangeHistory`, updates `RequestedBy` and `RequestedOn`, sets `Status = AwaitingArchitect`, opens a new review window for that reviewer, and sends a notification.
- While `Status = AwaitingArchitect`, the arrow is not shown for that reviewer.
- After the reviewer submits again and `Status` returns to `Submitted`, the arrow is shown again.

## Testing Checklist

1. Add reviewer (initial assignment): reviewer membership is created, `ReviewerState` is `AwaitingArchitect`, `Requested` is appended to `ChangeHistory`, and reviewer notification is sent.
2. Re-request reviewer (requires existing `ReviewerState.Status = AwaitingServiceTeam`): that same existing `ReviewerState` is reused, `Status` becomes `AwaitingArchitect`, `RequestedBy` and `RequestedOn` are updated, another `Requested` is appended, and reviewer notification is sent.
3. Submit with no existing reviewer state (human fallback): submitter is auto-added as reviewer, `ReviewerState` is created, `Requested` is recorded, and submit succeeds without sending reviewer-request notification.
4. Reviewer removal: same `ReviewerState` is reused, `Status = Canceled`, and `Canceled` is appended in `ChangeHistory`.
5. Reviewer re-added after cancel: same `ReviewerState` is reopened (`Status = AwaitingArchitect`) and another `Requested` history entry is added.
6. Submit (`Feedback`) with no grouped comments and empty `submissionMessage` is rejected.
7. Submit (`Approve`) with no grouped comments and empty `submissionMessage` is accepted.
8. Submit appends a `Submitted` history entry with `SubmissionMessage` and `CommentIds`, and updates top-level `SubmissionDecision`, `SubmittedOn`, and `Status = AwaitingServiceTeam`.
9. `Approve` submit persists `ContentHash` from the specified `revisionId`.
9.1. `Feedback` submit succeeds without `revisionId`; if provided, `revisionId` is ignored for decision-state updates.
10. First submit uses version creation time as comment window start; later submits use previous `SubmittedOn`.
11. Re-request does not change comment window boundary; grouping remains based on previous `SubmittedOn`, not `RequestedOn`.
12. Comment grouping isolation by reviewer: comments from reviewer A are never grouped into reviewer B submission.
13. Comment grouping isolation by version: comments from version A are never grouped into version B submission.
14. Submit-review sends exactly one batch email containing reviewer, decision, version, message, and grouped comments/links.
15. Live comment creation does not send batch submit-review notifications.
16. Approval-state recomputation precedence: any `Feedback` decision keeps version not approved even when `Approve` exists.
17. Version is approved only when at least one current `Approve` exists and no current `Feedback` exists.
18. Removing a reviewer does not automatically invalidate that reviewer’s previously submitted decision.
19. Existing unresolved-comment approval gating remains unchanged (blocking cases still block `Approve`).
20. Copilot request creates or reopens only Copilot `ReviewerState`; Copilot submit updates that same state.
21. Copilot comment grouping stays isolated to Copilot submissions and is never bundled with human submissions.
22. Copilot submissions do not affect version approval-state computation.
23. UI rule: `Re-Request Review` arrow is shown only when current `Status = AwaitingServiceTeam`, and is hidden while `Status = AwaitingArchitect`.

---

## Open Scenario

The following edge case is acknowledged but left as an open design question for a future iteration. It does not affect the schema or API design described in this proposal.

### Unresolved MUST FIX Added After Approval

**Scenario:** Architect A submits `Approve`. The service team receives the approval email and prepares to release. Before release, Architect B (also a reviewer) adds a `MUST FIX` comment but has not yet submitted a `Feedback` review. The service team proceeds to release.

**Current behavior:** A live comment, even one with `MUST FIX` severity, does not by itself change the version's computed approval state. Approval state is recomputed only on submit. Because Architect B has not submitted, the version remains `Approved` and release is not blocked. This is likely to be confusing to the service team, who may not realize that a `MUST FIX` comment is outstanding.

**Options under consideration:**
- Leave as-is. Architect B must submit `Feedback` to flip approval state; this is consistent with the submit-review model.
- Automatically revert version approval state to `Not Approved` whenever any `MUST FIX` comment is added, regardless of submission status. This would close the gap but breaks the explicit-submit model.

**Resolution:** Open. This proposal does not change the current behavior.