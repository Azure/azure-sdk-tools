# APIView Agent Interaction Model Proposal

This document describes how a user would interact with the APIView service through a conversational agent (e.g., a Copilot agent in VS Code or GitHub) to accomplish every goal currently achievable through the Angular SPA. The agent acts as an intermediary — translating user intent into API calls and presenting results in natural language or structured text.

> **Scope:** This outlines the *necessary interaction steps*, not whether the underlying endpoints exist today. A subsequent audit will verify endpoint coverage.

---

## 1. Authentication & Identity

### 1a. Establish Session
Before any operation, the agent must authenticate on behalf of the user.

- **User says:** "Connect to APIView" / is implicitly connected at session start.
- **Agent action:** Authenticate via token-based auth (Bearer token from GitHub OAuth or Managed Identity). The agent cannot use cookie-based auth — all interactions must go through token-authenticated endpoints.
- **Returned to user:** Confirmation of identity (username, roles, permissions).
- **Impact:** HIGH

### 1b. Check Permissions
- **User says:** "What can I do?" / "Am I an approver for Python?"
- **Agent action:** Retrieve the user's effective permissions and group memberships. Retrieve the list of approvers for a given language.
- **Information needed:** User profile, permission groups, language-specific approver lists.
- **Impact:** MEDIUM

---

## 2. Discovering & Browsing Reviews

### 2a. List / Search Reviews
- **User says:** "Show me all Python reviews" / "Find the review for Azure.Storage.Blobs" / "What reviews are pending approval?"
- **Agent action:** Query the reviews list with filters (language, package name, approval status, assigned reviewer, etc.) and pagination.
- **Returned to user:** A list of reviews with: package name, language, approval status, creator, last updated date, revision count.
- **Impact:** HIGH

### 2b. Get Review Details
- **User says:** "Open the review for azure-core" / "Tell me about review {reviewId}"
- **Agent action:** Fetch review metadata by ID or resolve from package name + language.
- **Returned to user:** Review ID, package name, language, creation date, approval status, number of revisions, link to the web UI (for optional follow-up).
- **Impact:** HIGH

### 2c. List Package Names
- **User says:** "What Python packages are tracked in APIView?"
- **Agent action:** Retrieve distinct package names for a given language.
- **Returned to user:** List of package names.
- **Impact:** LOW

---

## 3. Working with API Revisions

### 3a. List Revisions for a Review
- **User says:** "Show me all revisions for Azure.Storage.Blobs" / "What's the latest revision?"
- **Agent action:** Fetch revisions for a review with optional filtering (by type: Manual, Automatic, PullRequest) and sorting.
- **Returned to user:** List of revisions with: revision ID, version, type (manual/automatic/PR), approval status, approvers, release status, creation date, change history summary.
- **Impact:** HIGH

### 3b. Get Latest Revision
- **User says:** "What's the latest automatic revision for this review?"
- **Agent action:** Fetch the latest revision by type (All, Manual, Automatic, PullRequest).
- **Returned to user:** Revision details (version, status, approvers, etc.).
- **Impact:** HIGH

### 3c. View API Surface (Read the Code)
- **User says:** "Show me the API surface for Azure.Storage.Blobs v12.14.0" / "What does the public API look like?"
- **Agent action:** Fetch the rendered review content (code file) for a given revision. This is the core experience — the agent must present the token stream as readable, formatted text.
- **Returned to user:** The public API surface rendered as text with syntax structure preserved (namespaces, types, methods, parameters). The agent should present this hierarchically, potentially allowing the user to drill into specific sections.
- **Key challenge:** The UI renders a rich, navigable code panel. The agent must present this as structured text — possibly as code blocks with language-appropriate syntax highlighting, or as a hierarchical outline the user can expand.
- **Impact:** HIGH

### 3d. View API Surface as Outline / Summary
- **User says:** "Give me the outline of this API revision" / "Summarize the API surface"
- **Agent action:** Fetch the revision outline/summary text, which provides a condensed view of the API without the full token stream.
- **Returned to user:** A structured summary of the API surface (namespace → types → members).
- **Impact:** MEDIUM

### 3e. Get Revision Content as Raw Data
- **User says:** "Give me the raw CodeFile JSON for this revision"
- **Agent action:** Fetch the revision content in CodeFile JSON format (for advanced users or for piping into other tools).
- **Returned to user:** Raw JSON or a download link.
- **Impact:** LOW

### 3f. Upload a Manual Revision
- **User says:** "Upload this DLL for review" / "Create a new revision from my build artifact"
- **Agent action:** Upload a file (SDK artifact: .dll, .jar, .whl, .api.json, etc.) to create a new review or add a revision to an existing review. May also specify a label, language, and file path.
- **Returned to user:** Confirmation with the new review ID, revision ID, and a link. If the parser runs asynchronously (sandboxed), inform the user that generation is in progress.
- **Impact:** MEDIUM

### 3g. Delete / Restore Revisions
- **User says:** "Delete revision {id}" / "Restore the deleted revisions"
- **Agent action:** Soft-delete or restore one or more revisions.
- **Returned to user:** Confirmation of deletion/restoration.
- **Impact:** LOW

---

## 4. Diffing Revisions

### 4a. Diff Two Revisions
- **User says:** "What changed between v12.13.0 and v12.14.0?" / "Diff the latest revision against the previous one"
- **Agent action:** Fetch the rendered review content specifying both an active revision and a diff revision. The backend computes the Myers diff and returns lines tagged as Added, Removed, or Unchanged.
- **Returned to user:** A diff view presented as structured text — added lines prefixed with `+`, removed lines prefixed with `-`, with context lines. The agent should summarize the changes at a high level ("3 new methods added to BlobClient, 1 method removed from BlobContainerClient") and optionally show the detailed diff.
- **Key challenge:** The UI shows side-by-side or inline diff with syntax highlighting. The agent must present this as a readable text diff or a structured summary.
- **Impact:** HIGH

### 4b. Summarize Changes
- **User says:** "Summarize what changed in this revision"
- **Agent action:** Fetch the diff between the current revision and the previous one, then summarize added/removed/changed API elements.
- **Returned to user:** Natural-language summary of changes (e.g., "Added `BlobClient.DownloadStreamingAsync()`, removed deprecated `BlobClient.Download()`, modified return type of `ListBlobs()`").
- **Impact:** MEDIUM

---

## 5. Comments & Conversations

### 5a. View Comments on a Review
- **User says:** "Show me the comments on this review" / "Are there any unresolved comments?"
- **Agent action:** Fetch all comments for a review, optionally filtered by comment type (human, AI, diagnostic) and resolved/unresolved status.
- **Returned to user:** List of comment threads with: commenter, text, severity, resolved status, element they're anchored to, up/down votes, and replies.
- **Impact:** HIGH

### 5b. View Conversation Summary
- **User says:** "How many active threads are there on the latest revision?"
- **Agent action:** Fetch conversation info (active thread counts) for a specific revision.
- **Returned to user:** Count of active/resolved threads.
- **Impact:** MEDIUM

### 5c. Add a Comment
- **User says:** "Comment on `BlobClient.Upload`: 'Should this accept a Stream parameter?'" / "Add a must-fix comment on the Dispose method"
- **Agent action:** Create a new comment specifying: reviewId, the API element ID (LineId/DefinitionId) the comment anchors to, comment text, optional severity (must-fix, info, etc.), optional resolution lock, and optional thread ID for replies.
- **Returned to user:** Confirmation that the comment was posted, with the new comment ID.
- **Key challenge:** The user needs a way to reference specific API elements. The agent should help by listing elements from the API surface and letting the user pick, or by fuzzy-matching element names to their IDs.
- **Impact:** HIGH

### 5d. Reply to a Comment Thread
- **User says:** "Reply to that thread: 'Good point, I'll add a Stream overload'"
- **Agent action:** Create a new comment with the threadId of the parent comment.
- **Returned to user:** Confirmation.
- **Impact:** HIGH

### 5e. Update a Comment
- **User says:** "Edit my last comment to say '...'"
- **Agent action:** Update the text of an existing comment by comment ID.
- **Returned to user:** Confirmation.
- **Impact:** MEDIUM

### 5f. Resolve / Unresolve a Thread
- **User says:** "Resolve that thread" / "Reopen the thread on BlobClient.Upload"
- **Agent action:** Resolve or unresolve a comment thread by element ID and optional thread ID.
- **Returned to user:** Confirmation.
- **Impact:** HIGH

### 5g. Batch Operations on Threads
- **User says:** "Resolve all threads on this revision" / "Resolve all AI-generated comments"
- **Agent action:** Batch resolve/unresolve with voting and optional replies.
- **Returned to user:** Confirmation with count of affected threads.
- **Impact:** MEDIUM

### 5h. Upvote / Downvote a Comment
- **User says:** "Upvote that comment" / "Disagree with that suggestion"
- **Agent action:** Toggle upvote or downvote on a specific comment.
- **Returned to user:** Confirmation.
- **Impact:** LOW

---

## 6. Approval Workflow

### 6a. Check Approval Status
- **User says:** "Is this revision approved?" / "Who approved the latest revision?"
- **Agent action:** Fetch the revision's approval status, list of approvers, and change history.
- **Returned to user:** Approved (yes/no), list of approvers, and recent approval history entries.
- **Impact:** HIGH

### 6b. Check Approval Prerequisites
Before the user attempts to approve, the agent should proactively verify:
1. Does the revision have a package version set?
2. Are there any unresolved "Must Fix" comments (human, AI, or diagnostic)?
3. Is a Copilot review required for this language, and has it been completed?
4. Is the user authorized as an approver for this language?

- **User says:** "Can I approve this?" / "What's blocking approval?"
- **Agent action:** Check all four guards and report any blockers.
- **Returned to user:** "Ready to approve" or a list of blocking conditions (e.g., "2 unresolved must-fix comments remain", "Copilot review has not been run yet").
- **Impact:** HIGH

### 6c. Approve / Revert Approval
- **User says:** "Approve this revision" / "Revert my approval"
- **Agent action:** Toggle approval on the revision. The backend handles the binary toggle (approve if not approved, revert if already approved by this user).
- **Returned to user:** Confirmation with updated approval status and approver list.
- **Impact:** HIGH

### 6d. Approve a Package Name / First Release
- **User says:** "Approve the package name for this review"
- **Agent action:** Toggle review-level (first-release) approval.
- **Returned to user:** Confirmation.
- **Impact:** MEDIUM

---

## 7. Copilot / AI-Powered Review

### 7a. Request AI Review
- **User says:** "Run Copilot review on this revision" / "Get AI feedback"
- **Agent action:** Trigger Copilot AI review generation for the active revision (optionally with a diff baseline).
- **Returned to user:** Confirmation that the job was submitted. The agent should poll for completion or inform the user when results are available.
- **Impact:** MEDIUM

### 7b. Check AI Review Status
- **User says:** "Is the AI review done yet?"
- **Agent action:** Poll the Copilot review job status by job ID.
- **Returned to user:** Job status (pending, in-progress, completed, failed). When completed, present the AI-generated comments.
- **Impact:** MEDIUM

### 7c. Check if Copilot Review Is Required
- **User says:** "Does this language require Copilot review before approval?"
- **Agent action:** Query whether the configuration requires Copilot review for the given language.
- **Returned to user:** Yes/no, and whether this specific version has already been reviewed.
- **Impact:** MEDIUM

### 7d. Get Quality Score
- **User says:** "What's the quality score for this revision?"
- **Agent action:** Fetch review quality score metrics for the revision.
- **Returned to user:** Quality score breakdown.
- **Impact:** LOW

---

## 8. Cross-Language Review

### 8a. View the Same API Across Languages
- **User says:** "Show me how BlobClient looks across C#, Java, and Python" / "Cross-language view for this API"
- **Agent action:** Fetch cross-language content for the same API concept using CrossLanguageDefinitionId mappings.
- **Returned to user:** Side-by-side (or sequential) view of the same API element rendered in each language SDK.
- **Impact:** MEDIUM

### 8b. Find Related Reviews in a Project
- **User says:** "What other language reviews are in the same project?"
- **Agent action:** Fetch related reviews from the same project (TypeSpec project grouping).
- **Returned to user:** List of related reviews with language, package name, and approval status.
- **Impact:** MEDIUM

---

## 9. Projects & Namespace Approval

### 9a. View Project Details
- **User says:** "Show me the project for azure-core"
- **Agent action:** Fetch project information by project ID.
- **Returned to user:** Project details, associated reviews.
- **Impact:** LOW

### 9b. View Namespace Approval Status
- **User says:** "Is the namespace approved for all languages?"
- **Agent action:** Fetch namespace approval info and history for a project.
- **Returned to user:** Per-language namespace approval status and history.
- **Impact:** MEDIUM

### 9c. Update Namespace Approval Status
- **User says:** "Approve the namespace for Python" / "Request changes on the Java namespace"
- **Agent action:** Update namespace approval status for a specific language within a project, with notes.
- **Returned to user:** Confirmation with updated status.
- **Impact:** MEDIUM

### 9d. Request Namespace Review
- **User says:** "Request namespace review for this TypeSpec review"
- **Agent action:** Request namespace approval for a TypeSpec-based review.
- **Returned to user:** Confirmation that the request was submitted.
- **Impact:** MEDIUM

---

## 10. Pull Request Integration

### 10a. View PRs Linked to a Revision
- **User says:** "What PRs are associated with this revision?"
- **Agent action:** Fetch PRs associated with a specific API revision.
- **Returned to user:** List of PRs with: PR number, repo, link, commit SHA.
- **Impact:** MEDIUM

### 10b. Look Up Review from a PR
- **User says:** "What APIView review is associated with PR #1234 in azure-sdk-for-python?"
- **Agent action:** Query reviews by PR number, repo name, and/or commit SHA.
- **Returned to user:** Review and revision details, with a link to the APIView UI.
- **Impact:** HIGH

### 10c. Create Revision from PR Build (CI Integration)
- **User says:** (Typically automated, but an agent could trigger this) "Create an API revision from build {buildId}"
- **Agent action:** Call the CI pipeline integration endpoint to create a revision from a DevOps build, checking if the API surface changed compared to the baseline.
- **Returned to user:** New revision details, or confirmation that the API surface is unchanged.
- **Impact:** LOW

---

## 11. Release Gating

### 11a. Check Release Readiness
- **User says:** "Is Azure.Storage.Blobs approved for release?" / "Can we ship v12.14.0?"
- **Agent action:** Query the release gate endpoint with language, package name, and optionally package version.
- **Returned to user:** 
  - **Approved (200):** "Ready to release — API revision is approved."
  - **Namespace-approved (201):** "Namespace is approved, but the specific revision may not be."
  - **Pending (202):** "Release blocked — approval is still pending."
  - **Not found (404):** "No review exists for this package."
- **Impact:** HIGH

### 11b. Mark a Revision as Released
- **User says:** "Mark this revision as shipped" / (Typically automated via release pipeline)
- **Agent action:** Upload or create with `setReleaseTag=true` and `compareAllRevisions=true` to find the matching approved revision and stamp it as released.
- **Returned to user:** Confirmation that the revision is now marked as "Shipped" with the release timestamp.
- **Impact:** LOW

### 11c. Resolve Review from Package/URL
- **User says:** "Find the review for azure-core Python 1.30.0" / "Look up this APIView URL"
- **Agent action:** Resolve a review/revision from a package name + language + version, or from an APIView URL.
- **Returned to user:** Review and revision metadata.
- **Impact:** HIGH

---

## 12. Usage Samples

### 12a. View Samples for a Review
- **User says:** "Are there usage samples for this review?" / "Show me the code samples"
- **Agent action:** Fetch sample revisions for the review, then fetch content for the active sample.
- **Returned to user:** Sample code presented in a code block.
- **Impact:** LOW

### 12b. Upload / Update a Sample
- **User says:** "Add this code as a usage sample" / "Update the sample with this new code"
- **Agent action:** Create or update a sample revision with provided content (text) or a file upload, plus a title.
- **Returned to user:** Confirmation with the sample revision ID.
- **Impact:** LOW

### 12c. Delete Samples
- **User says:** "Delete that sample"
- **Agent action:** Soft-delete the sample revision(s).
- **Returned to user:** Confirmation.
- **Impact:** LOW

---

## 13. Notifications & Subscriptions

### 13a. Subscribe / Unsubscribe from a Review
- **User says:** "Notify me about changes to this review" / "Stop notifications for this review"
- **Agent action:** Toggle the subscription state for the review.
- **Returned to user:** Confirmation.
- **Impact:** LOW 

### 13b. Add Reviewers
- **User says:** "Add @jsmith and @jdoe as reviewers on this revision"
- **Agent action:** Add specified users as reviewers on an API revision.
- **Returned to user:** Confirmation with updated reviewer list.
- **Impact:** MEDIUM

---

## 14. User Profile & Preferences

### 14a. View Profile
- **User says:** "Show my profile" / "What are my APIView settings?"
- **Agent action:** Fetch user profile and preferences.
- **Returned to user:** Username, email, permissions, current preferences (theme, layout options).
- **Impact:** LOW

### 14b. Update Preferences
- **User says:** "Show hidden APIs by default" / "Switch to dark theme"
- **Agent action:** Update user preferences (show hidden APIs, hide line numbers, show/hide comments, theme selection, etc.).
- **Returned to user:** Confirmation.
- **Impact:** LOW

---

## 15. Administration

### 15a. Manage Permission Groups (Admin)
- **User says:** "Create a new approver group for Rust" / "Add @jsmith to the Python approvers group"
- **Agent action:** Create, update, or delete permission groups; add or remove members.
- **Returned to user:** Confirmation with updated group details.
- **Impact:** LOW

### 15b. View Approvers for a Language
- **User says:** "Who are the approvers for Java?"
- **Agent action:** Fetch the approver list for the specified language.
- **Returned to user:** List of approved reviewers.
- **Impact:** MEDIUM

### 15c. Delete a Review (Admin)
- **User says:** "Delete the entire review for {package}"
- **Agent action:** Soft-delete the review (admin-only).
- **Returned to user:** Confirmation.
- **Impact:** LOW

### 15d. Get Admin Contact Info
- **User says:** "Who are the APIView admins?"
- **Agent action:** Fetch admin usernames.
- **Returned to user:** List of admin users.
- **Impact:** LOW

---

## 16. Composite / Multi-Step Workflows

These are workflows where the agent adds value by orchestrating multiple API calls into a single, coherent interaction.

### 16a. Full Review Readiness Check
- **User says:** "Is azure-core Python ready for release?"
- **Agent does:**
  1. Resolve the review from package name + language.
  2. Fetch the latest automatic revision.
  3. Check approval status and list approvers.
  4. Check for unresolved must-fix comments.
  5. Check Copilot review status.
  6. Query the release gate endpoint.
- **Returned to user:** A complete readiness report: "azure-core Python v1.30.0 — Approved by @archboard, @reviewer2. No unresolved must-fix comments. Copilot review completed. Release gate: APPROVED (HTTP 200)."
- **Impact:** HIGH

### 16b. New API Review Walkthrough
- **User says:** "I have a new Python package to review — walk me through it."
- **Agent does:**
  1. Accept the artifact upload (wheel file).
  2. Wait for parsing to complete.
  3. Present the API surface outline.
  4. Highlight areas that might need attention (new namespaces, breaking changes from a prior version).
  5. Offer to run Copilot AI review.
  6. Present AI comments alongside the API surface.
  7. Guide the user through resolving comments or approving.
- **Impact:** MEDIUM

### 16c. Cross-Language Consistency Check
- **User says:** "Compare the BlobClient API across all language SDKs."
- **Agent does:**
  1. Find all related reviews in the same project.
  2. Fetch the cross-language content for each.
  3. Present a comparison highlighting discrepancies (missing methods in one language, different parameter names, etc.).
- **Impact:** MEDIUM

### 16d. Approval Audit Trail
- **User says:** "Show me the approval history for this review."
- **Agent does:**
  1. Fetch all revisions for the review.
  2. For each, extract the change history entries.
  3. Present a chronological audit trail: who approved, when, was it carried forward, was it reverted.
- **Impact:** MEDIUM

### 16e. Monitor CI Pipeline Integration
- **User says:** "What happened with the latest CI build for azure-storage-blobs?"
- **Agent does:**
  1. Look up the most recent automatic revision.
  2. Report whether approval was carried forward from a prior revision.
  3. Check if there are linked PRs.
  4. Report the release gate status.
- **Impact:** MEDIUM

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
