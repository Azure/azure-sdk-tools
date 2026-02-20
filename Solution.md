**Possible Solution**:
# APIView: GitHub-like Submit Review

## Goal

Mimic GitHub's submit review behavior so that:

- comments can be grouped into a single reviewer submission,
- approval/request-changes decisions are explicit state transitions,
- notifications can be sent as a batch at submit time,
- we can reliably distinguish current reviewer intent from historical comments.

## Non-goals

- Do **not** rewrite historical comments or thread behavior.

## Proposed model (GitHub-like)

Use **submitted review events only** and keep comments as they are today.

### New class: `ReviewSubmissionModel`

Represents a submitted review event (Approve/RequestChanges).

Suggested fields:

- `Id`
- `ReviewId`
- `APIRevisionId`
- `ReviewerId`
- `Decision` (`Approve`, `RequestChanges`)
- `SummaryComment` (submit dialog text)
- `CommentIds` (`List<string>`; IDs of comments grouped into this submission)
- `SubmittedOn`
- `IsDeleted`

### Change existing class: `CommentItemModel`

No required schema changes.

Optional optimization:

- `ReviewSubmissionId` (nullable)

For the first iteration, grouping is done from `ReviewSubmissionModel.CommentIds` so comments themselves remain unchanged.

## Database/Cosmos changes

Current comment container exists (`Comments`, partition `/ReviewId`).

### Option A (minimal risk, preferred)

Add one new container:

1. `ReviewSubmissions` (partition key `/ReviewId`)

Why:

- Keeps comment read/write path unchanged.
- Submission record is the only new write path.
- Keeps timeline and notification queries explicit.

### Option B (more compact, less flexible)

Store submission metadata inside `ReviewListItemModel.ChangeHistory` only.

Not preferred because timeline queries/analytics become harder and less explicit.

## API surface changes

### New endpoints (lean + MVC equivalents as needed)

- `POST /reviews/{reviewId}/comments` (existing add path)
  - No behavior change.

- `POST /reviews/{reviewId}/submit-review`
  - Input: `apiRevisionId`, `decision`, `summaryComment`, `commentIds`.
  - Behavior:
    1. Validate `commentIds` belong to `reviewId` and are eligible for this reviewer/revision.
    2. Create `ReviewSubmissionModel` with those `commentIds`.
    3. Apply decision side effects:
       - `Approve`: call existing review/revision approval manager logic.
       - `RequestChanges`:  record in change history and keep review open.
    4. Trigger batch notification for the new submission.

- `GET /reviews/{reviewId}/submissions`
  - Returns timeline of review submissions.
 
## How comments (CommentIds) are gathered

on submit, server computes:
comments where:
- `CreatedBy` == reviewer
- `ReviewId` == current review
- `APIRevisionId` == active revision
- `CreatedOn` > reviewer’s last submission time for this review/revision
- and not already included in a prior submission

## Notification behavior

Batch notification should happen on **Submit Review event** only:

- Collect comments using `ReviewSubmissionModel.CommentIds`.
- Send one email/notification payload containing:
  - reviewer,
  - decision,
  - summary comment,
  - list of comments (or deep links to threads).

No batching requirement when comments are being added live.

## UI behavior (minimal)

- Reviewer adds comments as today.
- "Submit Review" dialog includes:
  - Decision: Comment / Approve / Request changes
  - Optional summary text
- On submit, selected/current comments are grouped into a `ReviewSubmission` event.

## Migration plan

1. Create new repository for `ReviewSubmissions`.
2. Keep all existing comments unchanged and ungrouped (legacy data).
3. New grouping applies only to new submit-review events.

## Testing checklist

1. Single reviewer submits with multiple `commentIds` => one submission, one batch notification.
2. Single reviewer submits `Approve` => existing approval state updates and submission recorded.
3. Two reviewers submit independently => two submissions, no cross-contamination.
4.  Single reviewer submits `RequestChanges` => review remains open and submission recorded.
5. Submit includes invalid/foreign `commentIds` => request is rejected.
6. Legacy comments remain visible and queryable.

## Questions
- Empty submit: define whether Approve/RequestChanges with zero comments is allowed.
    - Approve -> Yes
    - Request Changes -> No
- Post-submit edits: define whether editing/deleting an included comment changes historical submission rendering.
    - No ->  The history entry would basically say "Summer submitted a review with Comments" because there's already an "Approval" event. Since the history wouldn't go into what comments were made, editing a comment probably shouldn't change the history entry.
- Ownership validation: clarify if commentIds must be created by submitting reviewer only, or can include bot/other comments.
    - AVC will be treated as any other reviewer, so I think it's appropriate to log an AVC review in the same way. The difference being that with AVC, you actually receive all comments in a batch, so that should actually simplify the logic in that case. There's no need to "collect" the comments.
