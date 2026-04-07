# APIView Agent Interaction Model Proposal

This document describes how a user would interact with the APIView service through a conversational agent (e.g., a Copilot agent in VS Code or GitHub) to accomplish every goal currently achievable through the Angular SPA. The agent acts as an intermediary — translating user intent into API calls and presenting results in natural language or structured text.

---

## Table of Contents

- [1. Authentication & Identity](#1-authentication--identity)
  - [1a. Establish Session](#1a-establish-session)
  - [1b. Check Permissions](#1b-check-permissions)
- [2. Discovering & Browsing Packages](#2-discovering--browsing-packages)
  - [2a. List / Search Packages](#2a-list--search-packages)
  - [2b. Get Package Details](#2b-get-package-details)
  - [2c. List Package Names](#2c-list-package-names)
- [3. Working with API Revisions](#3-working-with-api-revisions)
  - [3a. List Revisions for a Package](#3a-list-revisions-for-a-package)
  - [3b. Get Latest Revision](#3b-get-latest-revision)
  - [3c. View API Surface](#3c-view-api-surface)
  - [3d. Get CodeFile](#3d-get-codefile)
  - [3e. View API Surface as Outline / Summary](#3e-view-api-surface-as-outline--summary)
  - [3f. Upload a New Revision](#3f-upload-a-new-revision)
  - [3g. Delete / Restore Revisions](#3g-delete--restore-revisions)
- [4. Diffing Revisions](#4-diffing-revisions)
  - [4a. Diff Two Revisions](#4a-diff-two-revisions)
- [5. Comments & Conversations](#5-comments--conversations)
  - [5a. View Comments on a Review](#5a-view-comments-on-a-review)
  - [5b. View Conversation Summary](#5b-view-conversation-summary)
  - [5c. Add a Comment](#5c-add-a-comment)
  - [5d. Reply to a Comment Thread](#5d-reply-to-a-comment-thread)
  - [5e. Update a Comment](#5e-update-a-comment)
  - [5f. Resolve / Unresolve a Thread](#5f-resolve--unresolve-a-thread)
  - [5g. Batch Resolve Correlated Threads](#5g-batch-resolve-correlated-threads)
  - [5h. Upvote / Downvote a Comment](#5h-upvote--downvote-a-comment)
- [6. Approval Workflow](#6-approval-workflow)
  - [6a. View My Approval Queue](#6a-view-my-approval-queue)
  - [6b. Check Approval Status](#6b-check-approval-status)
  - [6c. Check Approval Prerequisites](#6c-check-approval-prerequisites)
  - [6d. Approve / Revert Approval](#6d-approve--revert-approval)
  - [6e. Approve a Package Name / First Release](#6e-approve-a-package-name--first-release)
  - [6f. Request Architect Review](#6f-request-architect-review)
- [7. Copilot / AI-Powered Review](#7-copilot--ai-powered-review)
  - [7a. Request AI Review](#7a-request-ai-review)
  - [7b. Get AI Overview of an API](#7b-get-ai-overview-of-an-api)
  - [7c. Summarize Changes Between Revisions](#7c-summarize-changes-between-revisions)
- [8. Cross-Language Review](#8-cross-language-review)
  - [8a. Check Cross-Language Review Status](#8a-check-cross-language-review-status)
  - [8b. Find Related Reviews in a Project](#8b-find-related-reviews-in-a-project)
- [9. Projects & Namespace Approval](#9-projects--namespace-approval)
  - [9a. View Project Details](#9a-view-project-details)
  - [9b. View Namespace Approval Status](#9b-view-namespace-approval-status)
  - [9c. Update Namespace Approval Status](#9c-update-namespace-approval-status)
  - [9d. Request Namespace Review](#9d-request-namespace-review)
- [10. Pull Request Integration](#10-pull-request-integration)
  - [10a. View PRs Linked to a Revision](#10a-view-prs-linked-to-a-revision)
  - [10b. Look Up Review from a PR](#10b-look-up-review-from-a-pr)
  - [10c. Create Revision from PR Build (CI Integration)](#10c-create-revision-from-pr-build-ci-integration)
- [11. Release Gating](#11-release-gating)
  - [11a. Check Release Readiness](#11a-check-release-readiness)
  - [11b. Mark a Revision as Released](#11b-mark-a-revision-as-released)
- [12. Usage Samples](#12-usage-samples)
  - [12a. View Samples for a Review](#12a-view-samples-for-a-review)
  - [12b. Upload / Update a Sample](#12b-upload--update-a-sample)
  - [12c. Delete Samples](#12c-delete-samples)
- [13. Notifications & Subscriptions](#13-notifications--subscriptions)
  - [13a. Subscribe / Unsubscribe from a Review](#13a-subscribe--unsubscribe-from-a-review)
  - [13b. Add Reviewers](#13b-add-reviewers)
- [14. Administration](#14-administration)
  - [14a. View Approvers for a Language](#14a-view-approvers-for-a-language)
  - [14b. Get Admin Contact Info](#14b-get-admin-contact-info)
- [15. Composite / Multi-Step Workflows](#15-composite--multi-step-workflows)
  - [15a. Full Review Readiness Check](#15a-full-review-readiness-check)
  - [15b. New API Review Walkthrough](#15b-new-api-review-walkthrough)
  - [15c. Cross-Language Consistency Check](#15c-cross-language-consistency-check)
  - [15d. Approval Audit Trail](#15d-approval-audit-trail)
  - [15e. Monitor CI Pipeline Integration](#15e-monitor-ci-pipeline-integration)
- [Key Design Considerations for Agent Implementation](#key-design-considerations-for-agent-implementation)

---

## 1. Authentication & Identity

### 1a. Establish Session
Before any operation, the agent must authenticate on behalf of the user.

- **Persona:** Both
- **User says:** "Connect to APIView" / is implicitly connected at session start.
- **Agent action:** Authenticate via token-based auth (Bearer token from GitHub OAuth or Managed Identity). The agent cannot use cookie-based auth — all interactions must go through token-authenticated endpoints.
- **Returned to user:** Confirmation of identity (username, roles, permissions).
- **Impact:** HIGH
- **Implemented:** 🟡 `APIViewAuthenticationService` in azsdk CLI handles token auth (Azure Managed Identity + GitHub tokens). No standalone "whoami" endpoint exists under token auth; `GET /api/auth` and `GET /api/permissions/me` are cookie-auth only.

### 1b. Check Permissions
- **Persona:** Both
- **User says:** "What can I do?" / "Am I an approver for Python?"
- **Agent action:** Retrieve the user's effective permissions and group memberships. Retrieve the list of approvers for a given language.
- **Information needed:** User profile, permission groups, language-specific approver lists.
- **Impact:** MEDIUM
- **Implemented:** ❌ `GET /api/permissions/me`, `GET /api/permissions/groups` are cookie-auth only. No token-auth endpoint exists. Not in CLI/MCP.

---

## 2. Discovering & Browsing Packages

> **Terminology note:** In this document, user-facing language refers to **packages** (e.g., "Azure.Storage.Blobs"). Internally in the APIView codebase and API endpoints, these are called **reviews** (e.g., `GET /api/reviews`). A single "review" maps to a tracked package and contains one or more API revisions.

### 2a. List / Search Packages
- **Persona:** Both
- **User says:** "Show me all Python packages" / "Find the package Azure.Storage.Blobs" / "What packages are pending approval?" / "What's the Python package for blob storage?"
- **Agent action:** Query the package list with filters (language, package name, approval status, assigned reviewer, etc.) and pagination. The agent should support natural-language queries that describe functionality (e.g., "blob storage") by searching against package names and descriptions.
- **Returned to user:** A list of packages with: package name, language, approval status, creator, last updated date, revision count.
- **Impact:** HIGH
- **Implemented:** ❌ `GET /api/reviews` is cookie-auth only. Not in CLI/MCP.

### 2b. Get Package Details
- **Persona:** Both
- **User says:** "Open the package azure-core" / "Tell me about package {packageName}" / "Find the review for azure-core Python 1.30.0" / "Look up this APIView URL"
- **Agent action:** Fetch package metadata by ID, or resolve from package name + language + version, or from an APIView URL.
- **Returned to user:** Package ID, package name, language, creation date, approval status, number of revisions, link to the web UI (for optional follow-up).
- **Impact:** HIGH
- **Implemented:** 🟡 `GET /api/reviews/resolve` (token-auth) can resolve a package from package name + language + version or URL (`?packageQuery=...&language=...&version=...&link=...`). `GET /api/reviews/{reviewId}` is cookie-auth only. Not yet exposed as a standalone CLI command.

### 2c. List Package Names
- **Persona:** Both
- **User says:** "What Python packages are tracked in APIView?"
- **Agent action:** Retrieve distinct package names for a given language.
- **Returned to user:** List of package names.
- **Impact:** LOW
- **Implemented:** ❌ `GET /api/reviews/languages/{language}/packagenames` is cookie-auth only. Not in CLI/MCP.

---

## 3. Working with API Revisions

### 3a. List Revisions for a Package
- **Persona:** Both
- **User says:** "Show me all revisions for Azure.Storage.Blobs" / "What's been released?" / "Show me the GA releases" / "Any preview versions?" / "What's the latest revision?"
- **Agent action:** Fetch revisions for a package with optional sorting. The agent should always exclude deleted revisions. The agent should support filtering by:
  - **Release status:** released vs. unreleased (e.g., "show only released versions").
  - **Release track:** GA vs. preview/beta/alpha (derived from semantic version — e.g., `1.0.0` is GA, `1.0.0-beta.1` or `1.0.0-preview.3` is preview). The agent should parse the version string to classify this.
  
  The backend also supports filtering by type (Manual, Automatic, PullRequest), but the agent should not expose this distinction by default — users generally care about versions and release status, not how a revision was created.
- **Returned to user:** List of revisions with: version, release track (GA/preview), approval status, approvers, release status (released/unreleased), creation date, change history summary.
- **Impact:** HIGH
- **Implemented:** ❌ `POST /api/apirevisions` (list revisions) is cookie-auth only. Not in CLI/MCP.

### 3b. Get Latest Revision
- **Persona:** Both
- **User says:** "What's the latest revision for this package?" / "What's the latest released version?" / "What's the latest GA release?" / "What's the latest preview?"
- **Agent action:** Fetch the latest non-deleted revision. The agent should support asking for the latest *released* revision and/or the latest revision on a specific release track (GA vs. preview/beta/alpha). The backend can filter by type (All, Manual, Automatic, PullRequest), but the agent should default to the most recent revision regardless of type.
- **Returned to user:** Revision details (version, release track, approval status, release status, approvers, quality score if available, etc.).
- **Impact:** HIGH
- **Implemented:** ❌ `GET /api/apirevisions/{reviewId}/latest` is cookie-auth only. Not in CLI/MCP.

### 3c. View API Surface
- **Persona:** Both
- **User says:** "Show me the API surface for Azure.Storage.Blobs v12.14.0" / "What does the public API look like?"
- **Agent action:** Fetch the rendered review content as text for a given revision. This is the core experience — the agent presents the API surface as readable, formatted text.
- **Returned to user:** The public API surface rendered as text with syntax structure preserved (namespaces, types, methods, parameters), presented as a code block with language-appropriate syntax highlighting.
- **Impact:** HIGH
- **Implemented:** ✅ `GET /api/apirevisions/getRevisionContent` (token-auth) with `contentReturnType=text`. CLI: `azsdk apiview get-content --url <url> --content-return-type text`.

### 3d. Get CodeFile
- **Persona:** Both
- **User says:** "Give me the CodeFile for this revision" / "Download the raw CodeFile JSON"
- **Agent action:** Fetch the revision content in CodeFile JSON format. This is for advanced users, tooling integration, or piping into other analysis tools.
- **Returned to user:** Raw CodeFile JSON.
- **Impact:** LOW
- **Implemented:** ✅ `GET /api/apirevisions/getRevisionContent` with `contentReturnType=codefile` (token-auth). CLI: `azsdk apiview get-content --content-return-type codefile`.

### 3e. View API Surface as Outline / Summary
- **Persona:** Both
- **User says:** "Give me the outline of this API revision" / "Summarize the API surface"
- **Agent action:** Fetch the revision outline/summary text, which provides a condensed view of the API without the full token stream.
- **Returned to user:** A structured summary of the API surface (namespace → types → members).
- **Impact:** LOW
- **Implemented:** ✅ `GET /api/apirevisions/{apiRevisionId}/outline` (token-auth). Not yet exposed as a CLI command.

### 3f. Upload a New Revision
- **Persona:** Service Teams
- **User says:** "Upload this DLL for review" / "Create a new revision from my build artifact"
- **Agent action:** For most languages, the agent should advise the user to push their changes to GitHub, which will automatically generate a new API revision via CI. Manual upload is only appropriate for languages that do not have CI-based automatic revision generation (e.g., Swift). If the user insists or the language requires it, upload a file (SDK artifact: .dll, .jar, .whl, .api.json, etc.) to create a new package or add a revision to an existing one. May also specify a label, language, and file path.
- **Returned to user:** For CI-supported languages: guidance to push to GitHub. For manual-only languages: confirmation with the package name, revision version, and a link. If the parser runs asynchronously (sandboxed), inform the user that generation is in progress.
- **Impact:** LOW
- **Implemented:** 🟡 `POST /autoreview/upload` (token-auth) supports CI/automated uploads. `POST /api/reviews` (manual create) is cookie-auth only. Not exposed as a manual-upload CLI command.

### 3g. Delete / Restore Revisions
- **Persona:** Both
- **User says:** "Delete revision {id}" / "Restore the deleted revisions"
- **Agent action:** This should only be permitted for APIView admins. If the user is not an admin, the agent should decline and suggest contacting an admin. If authorized, soft-delete or restore one or more revisions.
- **Returned to user:** Confirmation of deletion/restoration, or a message directing the user to an admin.
- **Impact:** LOW
- **Implemented:** ❌ `PUT /api/apirevisions/delete` and `PUT /api/apirevisions/restore` are cookie-auth only. Not in CLI/MCP.

---

## 4. Diffing Revisions

### 4a. Diff Two Revisions
- **Persona:** Both
- **User says:** "What changed between v12.13.0 and v12.14.0?" / "Diff the latest revision against the previous one"
- **Agent action:** Fetch the rendered review content specifying both an active revision and a diff revision. The backend computes the Myers diff and returns lines tagged as Added, Removed, or Unchanged.
- **Returned to user:** A diff view presented as structured text — added lines prefixed with `+`, removed lines prefixed with `-`, with context lines. The agent should summarize the changes at a high level ("3 new methods added to BlobClient, 1 method removed from BlobContainerClient") and optionally show the detailed diff.
- **Key challenge:** The UI shows side-by-side or inline diff with syntax highlighting. The agent must present this as a readable text diff or a structured summary.
- **Impact:** HIGH
- **Implemented:** ❌ `GET /api/reviews/{reviewId}/content` (with diff params) is cookie-auth only. The token-auth `getRevisionContent` endpoint does not support diff mode. Not in CLI/MCP.

---

## 5. Comments & Conversations

### 5a. View Comments on a Review
- **Persona:** Both
- **User says:** "Show me the comments on this review" / "Are there any unresolved comments?" / "Are there any blockers?" / "Show me all must-fix comments"
- **Agent action:** Fetch all comments for a review, with filtering by resolved/unresolved status and by severity (must-fix, should-fix, info, etc.). The agent should not expose filtering by comment source (human, AI, diagnostic) — all comments should be treated equally regardless of origin, to avoid users selectively ignoring feedback.
- **Returned to user:** List of comment threads with: commenter, text, severity, resolved status, element they're anchored to, up/down votes, and replies. When the user asks about "blockers," the agent should return unresolved must-fix comments.
- **Impact:** HIGH
- **Implemented:** ✅ `GET /api/comments/getRevisionComments` (token-auth). CLI: `azsdk apiview get-comments --url <url>`. MCP: `azsdk_apiview_get_comments`.

### 5b. View Conversation Summary
- **Persona:** Both
- **User says:** "What's the discussion status on this revision?" / "Summarize the open threads" / "What feedback is outstanding?"
- **Agent action:** Fetch all comments for the revision and synthesize a summary: count of active vs. resolved threads, breakdown by severity (must-fix, should-fix, info), and a brief description of each unresolved thread (element name + topic).
- **Returned to user:** A concise summary (e.g., "3 unresolved threads: 1 must-fix on `BlobClient.Upload` (missing Stream overload), 1 should-fix on `BlobContainerClient.Delete` (naming convention), 1 info on `BlobServiceClient` (documentation suggestion). 12 threads resolved.").
- **Impact:** MEDIUM
- **Implemented:** 🟡 The agent can derive this from `GET /api/comments/getRevisionComments` (token-auth), though it requires client-side aggregation. The dedicated conversation-info endpoint `GET /api/comments/{reviewId}/{apiRevisionId}` is cookie-auth only.
- **Alternative:** The APIView backend could expose a dedicated AVC-powered endpoint that synthesizes a conversation summary with richer context (e.g., grouping related threads, highlighting patterns across comments). AVC does not currently have such an endpoint, but it would offload the summarization from the host LLM and benefit from domain-tuned prompts.

### 5c. Add a Comment
- **Persona:** Both
- **User says:** "Comment on `BlobClient.Upload`: 'Should this accept a Stream parameter?'" / "Add a must-fix comment on the Dispose method"
- **Agent action:** Create a new comment specifying: reviewId, the API element ID (LineId/DefinitionId) the comment anchors to, comment text, optional severity (must-fix, info, etc.), optional resolution lock, and optional thread ID for replies.
- **Returned to user:** Confirmation that the comment was posted, with the new comment ID.
- **Key challenge:** The user needs a way to reference specific API elements. The agent should help by listing elements from the API surface and letting the user pick, or by fuzzy-matching element names to their IDs.
- **Impact:** HIGH
- **Implemented:** ❌ `POST /api/comments` is cookie-auth only. Not in CLI/MCP.

### 5d. Reply to a Comment Thread
- **Persona:** Both
- **User says:** "Reply to that thread: 'Good point, I'll add a Stream overload'"
- **Agent action:** Create a new comment with the threadId of the parent comment.
- **Returned to user:** Confirmation.
- **Key challenge:** Replying through an agent feels awkward — the user is dictating a response to a person through a middleman. For quick acknowledgments or short replies this may work, but for substantive back-and-forth discussion, the agent should offer a direct link to the thread in the APIView web UI instead.
- **Impact:** HIGH
- **Implemented:** ❌ `POST /api/comments` (with threadId) is cookie-auth only. Not in CLI/MCP.

### 5e. Update a Comment
- **Persona:** Both
- **User says:** "Edit my last comment to say '...'"
- **Agent action:** Update the text of an existing comment by comment ID.
- **Returned to user:** Confirmation.
- **Key challenge:** Editing through an agent requires the user to re-dictate the full comment text or describe the change, which is far clunkier than directly editing in a text box. The agent should offer a link to the comment in the web UI for non-trivial edits.
- **Impact:** LOW
- **Implemented:** ❌ `PATCH /api/comments/{reviewId}/{commentId}/updateCommentText` is cookie-auth only. Not in CLI/MCP.

### 5f. Resolve / Unresolve a Thread
- **Persona:** Both
- **User says:** "Resolve that thread" / "Reopen the thread on BlobClient.Upload"
- **Agent action:** Resolve or unresolve a comment thread by element ID and optional thread ID.
- **Returned to user:** Confirmation.
- **Key challenge:** Explicitly telling an agent to resolve a thread feels unnatural. A more ergonomic pattern would be for the agent to infer intent from conversational cues — e.g., if the user says "Yeah, I'll fix that" in response to a comment summary, the agent could offer to resolve the thread on their behalf. Even so, this intermediary pattern is still awkward compared to clicking a button in the UI; the agent should always offer a direct link as an alternative.
- **Impact:** HIGH
- **Implemented:** ❌ `PATCH /api/comments/{reviewId}/resolveComments` and `unResolveComments` are cookie-auth only. Not in CLI/MCP.

### 5g. Batch Resolve Correlated Threads
- **Persona:** Service Teams
- **User says:** (Triggered contextually, not directly) — When the user resolves or replies to a comment that has a correlation ID, the agent detects other threads sharing the same correlation ID and asks: "There are 4 other instances of this same comment. Would you like to resolve those too?"
- **Agent action:** If the user confirms, batch resolve/unresolve all threads sharing the same correlation ID.
- **Returned to user:** Confirmation with count of affected threads.
- **Key challenge:** This should never be exposed as a general-purpose "resolve all" command. It only applies when threads share a correlation ID (e.g., the same AI-generated comment repeated across multiple API elements). The agent should surface this opportunistically, not on demand.
- **Impact:** MEDIUM
- **Implemented:** ❌ `PATCH /api/comments/{reviewId}/commentsBatchOperation` is cookie-auth only. Not in CLI/MCP.

### 5h. Upvote / Downvote a Comment
- **Persona:** Both
- **User says:** (Unlikely to be explicit) — More realistically, the agent infers disagreement from conversational cues like "I don't think that's right" or "That suggestion doesn't apply here" when discussing a specific comment.
- **Agent action:** Toggle upvote or downvote on a specific comment. Downvotes are only permitted on AI-generated comments, and require the user to provide feedback explaining why they disagree. The agent should gather this feedback before submitting the downvote.
- **Returned to user:** Confirmation, including a note that the feedback was recorded.
- **Key challenge:** Users are extremely unlikely to say "upvote that comment" — upvotes add little value through an agent since they're a quick UI gesture. Downvotes are more likely to arise from expressed disagreement, but the agent must collect required feedback (why the AI comment is wrong or doesn't apply) before it can submit the downvote. The agent should prompt for this if not already provided.
- **Impact:** LOW
- **Implemented:** ❌ `PATCH /api/comments/{reviewId}/{commentId}/toggleCommentUpVote|DownVote` are cookie-auth only. Not in CLI/MCP.

---

## 6. Approval Workflow

### 6a. View My Approval Queue
- **Persona:** Architects
- **User says:** "What needs my review?" / "What's waiting for my approval?" / "Show me my pending reviews" / "Do I have anything to approve?"
- **Agent action:** Query the package list filtered to items where the current user is an assigned reviewer or an eligible approver and the revision is not yet approved. The agent should prioritize by urgency: revisions with unresolved must-fix comments or pending Copilot review should surface first, followed by revisions simply awaiting approval.
- **Returned to user:** A prioritized list of packages/revisions awaiting the user's attention, with: package name, language, version, why it needs attention (assigned as reviewer, eligible approver, has unresolved comments), and a link to the web UI.
- **Key challenge:** This requires combining data from multiple sources — the user's approver group memberships, assigned reviewers on revisions, approval status, and comment status. No single endpoint exists for this today.
- **Impact:** HIGH
- **Implemented:** ❌ Would require a composite query across `GET /api/reviews` (with reviewer/approver filters) and permission group data, all cookie-auth only. Not in CLI/MCP.

### 6b. Check Approval Status
- **Persona:** Both
- **User says:** "Is this revision approved?" / "Who approved the latest revision?"
- **Agent action:** Fetch the revision's approval status, list of approvers, and change history. The agent should also proactively include the quality score and check whether an AI review is required for this language and whether one has been completed — if not, nudge the user (e.g., "Here's the approval status. The quality score is 82/100. I notice this revision hasn't had an AI review yet, and one is required for Python. Want me to run that?").
- **Returned to user:** Approved (yes/no), list of approvers, quality score, recent approval history entries, and a nudge if AI review is missing.
- **Impact:** HIGH
- **Implemented:** ❌ Approval status is embedded in revision data from cookie-auth endpoints (`GET /api/apirevisions/{reviewId}/latest`). No dedicated token-auth endpoint. Not in CLI/MCP.

### 6c. Check Approval Prerequisites
Before the user attempts to approve, the agent should proactively verify:
1. Does the revision have a package version set?
2. Are there any unresolved "Must Fix" comments (human, AI, or diagnostic)?
3. Is a Copilot review required for this language, and has it been completed?
4. Is the user authorized as an approver for this language?

- **Persona:** Both
- **User says:** "Can I approve this?" / "What's blocking approval?"
- **Agent action:** Check all four guards and report any blockers.
- **Returned to user:** "Ready to approve" or a list of blocking conditions (e.g., "2 unresolved must-fix comments remain", "Copilot review has not been run yet").
- **Impact:** HIGH
- **Implemented:** 🟡 Individual checks exist across auth boundaries: `isReviewByCopilotRequired` (cookie), `isReviewVersionReviewedByCopilot` (cookie), comments (token-auth via `getRevisionComments`). No single composite endpoint. Not in CLI/MCP.

### 6d. Approve / Revert Approval
- **Persona:** Architects
- **User says:** "Approve this revision" / "Revert my approval"
- **Agent action:** Toggle approval on the revision. The backend handles the binary toggle (approve if not approved, revert if already approved by this user).
- **Returned to user:** Confirmation with updated approval status and approver list.
- **Impact:** HIGH
- **Implemented:** ❌ `POST /api/apirevisions/{reviewId}/{apiRevisionId}` (toggle approval) is cookie-auth only. Not in CLI/MCP.

### 6e. Approve a Package Name / First Release
- **Persona:** Architects
- **User says:** "Approve the package name for this review"
- **Agent action:** Toggle review-level (first-release) approval.
- **Returned to user:** Confirmation.
- **Impact:** MEDIUM
- **Implemented:** ❌ `POST /api/reviews/{reviewId}/{apiRevisionId}` (toggle review-level approval) is cookie-auth only. Not in CLI/MCP.

### 6f. Request Architect Review
- **Persona:** Service Teams
- **User says:** "Ask the architect to review this" / "Can you get an architect to look at this?" / "Request a re-review — I've addressed the comments" / "Ping the reviewers"
- **Agent action:** Identify the appropriate architect(s) for this language from the approver groups and add them as reviewers on the revision (or notify them if already assigned). If the user is requesting a *re-review* after addressing feedback, the agent should include context in the notification (e.g., "All must-fix comments have been resolved since the last review"). The agent should also proactively suggest this when it detects that all comments have been resolved but the revision is still unapproved.
- **Returned to user:** Confirmation that the review request was sent, with the names of the notified architects and a link to the revision.
- **Key challenge:** "Request a review" is ambiguous — it could mean assigning a reviewer, sending a notification, or both. The agent should default to adding the user as a reviewer on the revision *and* triggering a notification. There is no dedicated "request review" endpoint; this is a combination of adding reviewers (13b) and the notification system.
- **Impact:** HIGH
- **Implemented:** ❌ `POST /api/apirevisions/{reviewId}/{apiRevisionId}/reviewers` is cookie-auth only. No notification-trigger endpoint exists under token-auth. Not in CLI/MCP.

---

## 7. Copilot / AI-Powered Review

### 7a. Request AI Review
- **Persona:** Both
- **User says:** "Please review this API" / "How is this API?" / "Review the changes from v12.13.0 to v12.14.0" / "What do you think of this API surface?"
- **Agent action:** First, disambiguate what the user wants reviewed. The far more common case is reviewing *what changed* between versions (diff review), not reviewing the entire API surface from scratch. If the user's request is ambiguous (e.g., "how is this API?" or "review this"), the agent should check whether a previous revision exists:
  - **If a prior revision exists:** Default to a diff review against the previous version, and confirm with the user: "I'll review what changed since v12.13.0 — or would you prefer a full review of the entire API surface?"
  - **If no prior revision exists (first revision):** Proceed with a full API surface review.
  - **If the user specifies two versions:** Proceed with a diff review between those versions.
  
  Once the scope is determined, trigger Copilot AI review generation for the active revision (with a diff baseline for diff reviews). The agent should stream results back to the user as they become available — the user should see comments appearing incrementally, not wait for a batch job to complete and then ask for results.
- **Returned to user:** AI-generated comments presented incrementally as they are produced, anchored to specific API elements, with severity and explanations.
- **Key challenge:** Today the review endpoint is a batch operation — the agent submits a job, then polls `CopilotPollingBackgroundHostedService` for completion. This means the user would have to ask "is my review done yet?", which is a non-starter. The endpoint needs to behave more like a streaming response: the user says "review this" and comments start appearing in the conversation as they're generated. This likely requires rearchitecting the review endpoint from fire-and-poll to a streaming or server-sent events model.
- **Impact:** HIGH
- **Copilot service:** ✅ `POST /api-review/start` accepts `language`, `target`, `base` (optional diff baseline), `outline`, and `comments`; returns a `jobId`. `GET /api-review/{jobId}` polls for results. Separate prompty templates handle full review vs. diff review.
- **Implemented:** 🟡 Cookie-auth: `POST /api/apirevisions/{reviewId}/generateReview` does full orchestration — fetches revision content, assembles payload, calls Copilot service, enqueues background polling, and persists comments back to Cosmos DB via `CopilotPollingBackgroundHostedService`. Token-auth: `POST /api/reviews/start-copilot-review-job` and `GET /api/reviews/get-copilot-review-job/{jobId}` exist as raw proxies — the caller must supply pre-assembled `target`/`base`/`outline` text, and results are returned directly without being persisted as comments. No streaming support exists in either path. Not in CLI/MCP.

### 7b. Get AI Overview of an API
- **Persona:** Both
- **User says:** "Give me an overview of this API" / "What does this package do?" / "Explain this API to me"
- **Agent action:** Call the APIView backend, which proxies the request to Azure Verified Copilot (AVC) to generate a natural-language overview of the API surface. This is distinct from the structural outline (3e) — it provides a human-readable narrative.
- **Returned to user:** A natural-language summary (e.g., "Azure.Storage.Blobs provides client classes for interacting with Azure Blob Storage. The main entry points are `BlobServiceClient`, `BlobContainerClient`, and `BlobClient`, which support creating containers, uploading/downloading blobs, and managing metadata.").
- **Impact:** MEDIUM
- **Copilot service:** ✅ `POST /api-review/summarize` accepts `language` and `target` (API text) with no `base`. Uses `summarize_api.prompty` to generate a natural-language overview.
- **Implemented:** ❌ The Copilot service endpoint exists but is not wired through any APIView backend endpoint (neither cookie-auth nor token-auth). An agent would need to either call the Copilot service directly (requires separate auth) or the backend needs a new pass-through endpoint.
- **Alternative:** The agent could skip the Copilot service proxy and instead fetch the API surface text (3c) and generate its own summary using the host LLM (e.g., GitHub Copilot). This avoids the need for a new backend endpoint but loses the benefit of the Copilot service's domain-tuned prompts and caching.

### 7c. Summarize Changes Between Revisions
- **Persona:** Both
- **User says:** "Summarize what changed in this revision"
- **Agent action:** Call the APIView backend's Copilot diff-summary endpoint, which computes the diff between the current revision and the previous one and returns a natural-language summary of added/removed/changed API elements.
- **Returned to user:** Natural-language summary of changes (e.g., "Added `BlobClient.DownloadStreamingAsync()`, removed deprecated `BlobClient.Download()`, modified return type of `ListBlobs()`").
- **Impact:** MEDIUM
- **Copilot service:** ✅ `POST /api-review/summarize` accepts `language`, `target`, and `base`. When `base` is provided, uses `summarize_diff.prompty` with `create_diff_with_line_numbers()` to generate a natural-language change summary.
- **Implemented:** ❌ Same as 7b — the Copilot service endpoint exists but is not wired through any APIView backend endpoint. Not in CLI/MCP.
- **Alternative:** The agent could fetch the raw diff via the diff endpoint (4a) and generate its own summary using the host LLM (e.g., GitHub Copilot). This avoids dependency on the Copilot service endpoint but loses the benefit of domain-tuned prompts and caching.

---

## 8. Cross-Language Review

### 8a. Check Cross-Language Review Status
- **Persona:** Architects
- **User says:** "Are there any cross-language threads open for my review?" / "What's the cross-language status for this project?" / "Are all languages aligned?"
- **Agent action:** For a given project (TypeSpec grouping), fetch the related reviews across all language SDKs and check for unresolved cross-language comment threads, approval gaps, or consistency issues.
- **Returned to user:** A summary of cross-language review status: which languages have been reviewed, which have open threads, and any languages that are lagging behind (e.g., "Python and Java are approved. C# has 2 unresolved must-fix threads. Go has not been reviewed yet.").
- **Impact:** MEDIUM
- **Implemented:** ❌ `GET /api/reviews/crossLanguageContent` and `GET /api/apirevisions/{crossLanguageId}/crosslanguage` are cookie-auth only. Not in CLI/MCP.

### 8b. Find Related Reviews in a Project
- **Persona:** Both
- **User says:** "What other language reviews are in the same project?"
- **Agent action:** Fetch related reviews from the same project (TypeSpec project grouping).
- **Returned to user:** List of related reviews with language, package name, and approval status.
- **Impact:** MEDIUM
- **Implemented:** ❌ `GET /api/projects/reviews/{reviewId}/related` is cookie-auth only. Not in CLI/MCP.

---

## 9. Projects & Namespace Approval

### 9a. View Project Details
- **Persona:** Both
- **User says:** "Show me the project for azure-core"
- **Agent action:** Fetch project information by project ID.
- **Returned to user:** Project details, associated reviews.
- **Impact:** LOW
- **Implemented:** ❌ `GET /api/projects/{projectId}` is cookie-auth only. Not in CLI/MCP.

### 9b. View Namespace Approval Status
- **Persona:** Both
- **User says:** "Are the namespaces approved for all languages?"
- **Agent action:** Fetch namespace approval info and history for a project.
- **Returned to user:** Per-language namespace approval status and history.
- **Impact:** MEDIUM
- **Implemented:** ❌ `GET /api/projects/{projectId}/namespaces` is cookie-auth only. Not in CLI/MCP.

### 9c. Update Namespace Approval Status
- **Persona:** Architects
- **User says:** "Approve the namespace for Python" / "Request changes on the Java namespace"
- **Agent action:** Update namespace approval status for a specific language within a project, with notes.
- **Returned to user:** Confirmation with updated status.
- **Impact:** MEDIUM
- **Implemented:** ❌ `PATCH /api/projects/{projectId}/namespaces/{language}` is cookie-auth only. Not in CLI/MCP.

### 9d. Request Namespace Review
- **Persona:** Service Teams
- **User says:** "Request namespace review for this TypeSpec review"
- **Agent action:** Request namespace approval for a TypeSpec-based review.
- **Returned to user:** Confirmation that the request was submitted.
- **Impact:** MEDIUM
- **Implemented:** ❌ `POST /api/reviews/{reviewId}/requestNamespaceReview/{activeApiRevisionId}` is cookie-auth only. Not in CLI/MCP.

---

## 10. Pull Request Integration

### 10a. View PRs Linked to a Revision
- **Persona:** Both
- **User says:** "What PRs are associated with this revision?"
- **Agent action:** Fetch PRs associated with a specific API revision.
- **Returned to user:** List of PRs with: PR number, repo, link, commit SHA.
- **Impact:** LOW
- **Implemented:** ❌ `GET /api/pullrequests/{reviewId}/{apiRevisionId}` is cookie-auth only. Not in CLI/MCP.

### 10b. Look Up Review from a PR
- **Persona:** Both
- **User says:** "What APIView review is associated with PR #1234 in Python?"
- **Agent action:** Query reviews by PR number, repo name, and/or commit SHA.
- **Returned to user:** Review and revision details, with a link to the APIView UI.
- **Impact:** MEDIUM
- **Implemented:** ✅ `GET /api/pullrequests?pullRequestNumber=...&repoName=...&commitSHA=...` is anonymous (no auth required). Not in CLI/MCP as a standalone command but `azsdk_get_pull_request` MCP tool returns APIView links.

### 10c. Create Revision from PR Build (CI Integration)
- **Persona:** Service Teams
- **User says:** (Typically automated, but an agent could trigger this) "Create an API revision from build {buildId}"
- **Agent action:** Call the CI pipeline integration endpoint to create a revision from a DevOps build, checking if the API surface changed compared to the baseline.
- **Returned to user:** New revision details, or confirmation that the API surface is unchanged.
- **Impact:** LOW
- **Implemented:** ✅ CLI: `azsdk apiview create-pull-request-revision` and `azsdk apiview create-ci-revision` (both use token-auth via `POST /autoreview/upload`).

---

## 11. Release Gating

### 11a. Check Release Readiness
- **Persona:** Both
- **User says:** "Is Azure.Storage.Blobs approved for release?" / "Can we ship v12.14.0?"
- **Agent action:** Query the release gate endpoint with language, package name, and optionally package version.
- **Returned to user:** 
  - **Approved (200):** "Ready to release — API revision is approved."
  - **Namespace-approved (201):** "Namespace is approved, but the specific revision may not be."
  - **Pending (202):** "Release blocked — approval is still pending."
  - **Not found (404):** "No review exists for this package."
- **Impact:** HIGH
- **Implemented:** ✅ `GET /review?language=...&packageName=...&packageVersion=...` (no auth restriction on this MVC endpoint). Also derivable from `azsdk apiview create-ci-revision` status codes (200/201/202).

### 11b. Mark a Revision as Released
- **Persona:** Service Teams
- **User says:** "Mark this revision as shipped" / (Typically automated via release pipeline)
- **Agent action:** Upload or create with `setReleaseTag=true` and `compareAllRevisions=true` to find the matching approved revision and stamp it as released.
- **Returned to user:** Confirmation that the revision is now marked as "Shipped" with the release timestamp.
- **Impact:** LOW
- **Implemented:** ✅ `POST /autoreview/upload` (token-auth) with `setReleaseTag=true` and `compareAllRevisions=true`. CLI: `azsdk apiview create-ci-revision` with corresponding flags.

---

## 12. Usage Samples

### 12a. View Samples for a Review
- **Persona:** Both
- **User says:** "Are there usage samples for this review?" / "Show me the code samples"
- **Agent action:** Fetch sample revisions for the review, then fetch content for the active sample.
- **Returned to user:** Sample code presented in a code block.
- **Impact:** LOW
- **Implemented:** ❌ `GET /api/samplesrevisions/{reviewId}/content` and `POST /api/samplesrevisions` are cookie-auth only. Not in CLI/MCP.

### 12b. Upload / Update a Sample
- **Persona:** Service Teams
- **User says:** "Add this code as a usage sample" / "Update the sample with this new code"
- **Agent action:** Create or update a sample revision with provided content (text) or a file upload, plus a title.
- **Returned to user:** Confirmation with the sample revision ID.
- **Impact:** LOW
- **Implemented:** ❌ `POST /api/samplesrevisions/{reviewId}/create` and `PATCH /api/samplesrevisions/{reviewId}/update` are cookie-auth only. Not in CLI/MCP.

### 12c. Delete Samples
- **Persona:** Service Teams
- **User says:** "Delete that sample"
- **Agent action:** Soft-delete the sample revision(s).
- **Returned to user:** Confirmation.
- **Impact:** LOW
- **Implemented:** ❌ `PUT /api/samplesrevisions/delete` is cookie-auth only. Not in CLI/MCP.

---

## 13. Notifications & Subscriptions

### 13a. Subscribe / Unsubscribe from a Review
- **Persona:** Both
- **User says:** "Notify me about changes to this review" / "Stop notifications for this review"
- **Agent action:** Toggle the subscription state for the review.
- **Returned to user:** Confirmation.
- **Impact:** LOW
- **Implemented:** ❌ `POST /api/reviews/{reviewId}/toggleSubscribe` is cookie-auth only. Not in CLI/MCP.

### 13b. Add Reviewers
- **Persona:** Both
- **User says:** "Add @jsmith and @jdoe as reviewers on this revision"
- **Agent action:** Add specified users as reviewers on an API revision.
- **Returned to user:** Confirmation with updated reviewer list.
- **Impact:** LOW
- **Implemented:** ❌ `POST /api/apirevisions/{reviewId}/{apiRevisionId}/reviewers` is cookie-auth only. Not in CLI/MCP.

---

## 14. Administration

### 14a. View Approvers for a Language
- **Persona:** Both
- **User says:** "Who are the approvers for Java?"
- **Agent action:** Fetch the approver list for the specified language.
- **Returned to user:** List of approved reviewers.
- **Impact:** MEDIUM
- **Implemented:** ❌ Approver lists are embedded in permission groups (`GET /api/permissions/groups`), cookie-auth only. Not in CLI/MCP.

### 14b. Get Admin Contact Info
- **Persona:** Both
- **User says:** "Who are the APIView admins?"
- **Agent action:** Fetch admin usernames.
- **Returned to user:** List of admin users.
- **Impact:** LOW
- **Implemented:** ❌ `GET /api/permissions/users` is cookie-auth only (admin). Not in CLI/MCP.

---

## 15. Composite / Multi-Step Workflows

These are workflows where the agent adds value by orchestrating multiple API calls into a single, coherent interaction.

### 15a. Full Review Readiness Check
- **Persona:** Service Teams
- **User says:** "Is azure-core Python ready for release?"
- **Composes:**
  1. **2b** (Get Package Details) — Resolve the review from package name + language.
  2. **3b** (Get Latest Revision) — Fetch the latest revision.
  3. **6b** (Check Approval Status) — Check approval status and list approvers.
  4. **5a** (View Comments) — Check for unresolved must-fix comments.
  5. **6c** (Check Approval Prerequisites) — Verify Copilot review status and other guards.
  6. **11a** (Check Release Readiness) — Query the release gate endpoint.
- **Returned to user:** A complete readiness report: "azure-core Python v1.30.0 — Approved by @archboard, @reviewer2. No unresolved must-fix comments. Copilot review completed. Release gate: APPROVED (HTTP 200)."
- **Impact:** HIGH
- **Implemented:** 🟡 Steps 1 (2b), 4 (5a), and 6 (11a) have token-auth endpoints. Steps 2 (3b), 3 (6b), and 5 (6c) require cookie-auth endpoints. Cannot be fully assembled via token-auth today.

### 15b. New API Review Walkthrough
- **Persona:** Both
- **User says:** "I have a new Python package to review — walk me through it."
- **Composes:**
  1. **3f** (Upload a New Revision) — Accept the artifact upload (wheel file) and wait for parsing.
  2. **3e** (View API Surface as Outline) — Present the API surface outline.
  3. **3c** (View API Surface) — Show the full API surface, highlighting areas that might need attention.
  4. **7a** (Request AI Review) — Offer to run Copilot AI review.
  5. **5a** (View Comments) — Present AI comments alongside the API surface.
  6. **5f** (Resolve / Unresolve a Thread) + **6d** (Approve / Revert Approval) — Guide the user through resolving comments or approving.
- **Impact:** MEDIUM
- **Implemented:** 🟡 Steps 2–3 (3e, 3c) use token-auth. Steps 1 (3f — manual upload), 4 (7a), 5–6 (5a read-only ✅, but 5f and 6d write operations) lack token-auth endpoints.

### 15c. Cross-Language Consistency Check
- **Persona:** Architects
- **User says:** "Compare the BlobClient API across all language SDKs."
- **Composes:**
  1. **8b** (Find Related Reviews in a Project) — Find all related reviews in the same project.
  2. **8a** (Check Cross-Language Review Status) — Fetch the cross-language content for each.
  3. **3c** (View API Surface) — Fetch each language's API surface and present a comparison highlighting discrepancies.
- **Impact:** MEDIUM
- **Implemented:** ❌ Steps 1–2 (8b, 8a) are cookie-auth only. Step 3 (3c) is available via token-auth but the prerequisite steps are not.

### 15d. Approval Audit Trail
- **Persona:** Both
- **User says:** "Show me the approval history for this review."
- **Composes:**
  1. **3a** (List Revisions for a Package) — Fetch all revisions for the review.
  2. **6b** (Check Approval Status) — For each revision, extract approval status and change history entries.
  3. Present a chronological audit trail: who approved, when, was it carried forward, was it reverted.
- **Impact:** MEDIUM
- **Implemented:** ❌ Steps 1–2 (3a, 6b) are cookie-auth only.

### 15e. Monitor CI Pipeline Integration
- **Persona:** Service Teams
- **User says:** "What happened with the latest CI build for azure-storage-blobs?"
- **Composes:**
  1. **3b** (Get Latest Revision) — Look up the most recent automatic revision.
  2. **6b** (Check Approval Status) — Report whether approval was carried forward from a prior revision.
  3. **10a** (View PRs Linked to a Revision) — Check if there are linked PRs.
  4. **11a** (Check Release Readiness) — Report the release gate status.
- **Impact:** MEDIUM
- **Implemented:** 🟡 Step 4 (11a) is available via token-auth. Steps 1–3 (3b, 6b, 10a) are cookie-auth only.

---

## Key Design Considerations for Agent Implementation

### Element Referencing
The biggest UX challenge is allowing users to reference specific API elements (for commenting, navigating, etc.) without a visual code panel. Options:
- **By name:** "Comment on `BlobClient.Upload`" → agent fuzzy-matches against `LineId` values.
- **By outline number:** "Comment on item #3 in the namespace list" → agent maintains a numbered outline.
- **By search:** "Find the method that accepts a `Stream` parameter" → agent searches the token stream.

### Output Formatting
- API surfaces should be presented as syntax-highlighted code blocks.
- Diffs should use standard unified-diff or `+`/`-` format.
- Comments should be threaded with clear attribution and timestamps.
- Status reports should be structured tables or bullet lists.

### Token Auth Requirement
The agent cannot use cookie-based browser authentication. All operations must be available via token-authenticated endpoints. Today, not all SPA endpoints have token-auth equivalents — the endpoint audit will identify gaps.

### Real-Time Updates
The SPA uses SignalR for real-time updates (approval changes, AI job completion). The agent could either:
- **Poll** for changes periodically.
- **Subscribe** to a SignalR hub directly (more complex but more responsive).
- **Fire-and-forget** with status checks on demand.

### Stateless vs. Stateful Conversations
The agent should maintain session context (current review, current revision, current element) to enable conversational follow-ups like "approve it", "show the diff", "add a comment there" — where "it"/"the"/"there" refers to the previously discussed context.
