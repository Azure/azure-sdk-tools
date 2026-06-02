# Proposal: Phase 1 — Migrate API Reviews to GitHub

## Objective
Shift API reviews from APIView to GitHub-based workflows, eliminating dependency on the APIView UI. Once migration is complete, APIView will be decommissioned and most if not all of its infrastructure torn down.

Phase 1 focuses on replacing core review workflows using GitHub capabilities with minimal supporting automation.

---

## Why Shift API Reviews to GitHub

### 1. Unsustainable APIView Maintenance Model
- APIView’s maintenance burden has grown significantly over time.
- The system has underlying architectural weaknesses:
  - Complex state management (reviews, revisions, versions, approvals)
  - Tight coupling to parser-specific logic (e.g., diagnostics)
  - Deprecated experiments like Swagger review mode

- These weaknesses result in:
  - Increasing operational complexity
  - Higher cost of change for incremental improvements
  - Ongoing maintenance costs that continue to grow

- In an SFI (Security-First Initiative) environment:
  - Systems must continuously meet evolving security and compliance requirements
  - APIView’s architecture increases the cost and risk of maintaining compliance
  - Maintaining a bespoke review system is no longer justified relative to its value

**Conclusion:** Continuing investment in APIView as a primary review surface is not sustainable.

---

### 2. Leverage GitHub Platform Strengths
API reviews are fundamentally code review workflows. GitHub already provides a mature platform for this model.

Key advantages:

- **Established usage patterns**
  - All engineering work already happens in GitHub
  - PR-based review is the standard workflow

- **Built-in review capabilities**
  - Diff-based reviews
  - Inline comments and threaded discussions
  - Native resolution of comments
  - Clear approval states

- **Integrated enforcement**
  - CI/status checks for gating
  - Required reviewers and branch protection rules

- **Single source of truth**
  - Code, discussion, and approval live in one system
  - Eliminates duplication and inconsistency across systems

- **Extensibility**
  - Actions, bots, and automation integrate directly into PR workflows
  - No need to recreate infrastructure already available

**Conclusion:** GitHub provides a scalable, maintainable, and user-aligned platform for API reviews.

---

## Assumptions

- We need to be able to compare arbitrary versions against one another, as we do in APIView today.
- We must make this process work within the release processes we already follow today.

---

## Scope (Phase 1)

### 1. Review Creation (Diff-Based)
- API reviews are synthetic GitHub pull requests
- API surface is represented as a generated `API.md` artifact
  - Initially: leverage existing parsers which generate token files. Convert the token file to `API.md` using `Export-APIViewMarkdown.ps1`
  - Alternative: ideally, parsers would generate `API.md` directly without needing to generate tokens. The reason to do this would be speed. If we ever want something where we get the API quickly, passing it through bulky parsers that produce useless tokens will likely be the bottleneck. However, for the proposal, this is out of scope.
- Reviews must be diff-based because GitHub PRs are diff-based. That means new services will appear as all addition (all green) PRs.
- `API.md` will be accurate for tagged release commits. The `API.md` for that package will match that of the package corresponding to the release tag.
- The creation of PR is much more "heavy handed" than we currently have. To arbitrarily compare APIViews today you just select different dropdowns. This system will require creating branches and opening a PR. If you want to compare a proposed change against multiple versions (for example, last beta AND last GA) you'll need multiple PRs.
- Baseline and target refs must be explicitly selected from tags/commits based on review intent (for example, last GA vs candidate, previous beta vs current beta).
- Process:
  - Three branches are needed: a base branch, a synthetic review branch, and the working branch.
    - The **working branch** is the branch containing the actual code changes the service team wants to release. They will make commits and changes to THIS branch, and the API changes will be calculated and synced to the synthetic review branch.
    - The **base branch** is a synthetic branch created on demand from the selected baseline tag/commit. It must be a branch because you cannot open PRs against tags. The pipeline must regenerate `API.md` on this base branch using the same parser/toolchain version that will be used for the review branch.
    - The **review branch** is a synthetic branch that contains only the regenerated `API.md` (and possibly select other files, like samples) for the working branch, produced with the same parser/toolchain as the base branch. In this way we exclude code changes and restrict the PR to API changes while avoiding parser-version-only diffs. One benefit to using GitHub here is that we can expand what we include on this branch to include things like dependencies which are shoehorned into the current APIView token files. This would allow us to present those diffs more naturally than we do today (Note: this is out of scope for today but possible as a future enhancement).
  - Review PR metadata must include a machine-readable pointer to the corresponding working PR (for example `Working-PR: <url>` in the PR body).
  - Copilot and automation should treat comments on the review PR as instructions to update the working PR branch, not the review branch.
  - The end-user flow remains PR-centric: a user can open the review PR and ask "address these comments"; tooling resolves the linked working PR and applies code changes there, then sync automation regenerates and updates `API.md` on the review branch.
  - If a working PR has multiple review PRs (for example GA baseline and beta baseline), each review PR must identify both the working PR and its baseline intent so automation can route and report updates deterministically.
- The creation process will likely be initiated by a pipeline where you specify your working branch and baseline tag/commit, then open a specially tagged PR between the synthetic review and base branches. A second pipeline would likely be needed to sync changes from the working branch to the review branch and regenerate `API.md` with the same parser/toolchain version used for the base branch.

**Outcome:** PR becomes the canonical API review artifact.

**Wins:** No dependency on APIView backend or UI. Can easily include more files (like Java POM.xml or dependencies) in the diff than just API text.

**Losses:** Lose the ability to quickly compare a version against any other version. Lose the ability to see the version approval history of a package because now each package will logically be represented by a bunch of unconnected closed PRs. Requires a deliberate step to generate the review PR, unlike APIView where everything happens automatically.

---

### 2. `API.md` Freshness Enforcement
- `API.md` must be checked into the repository and kept up to date with every PR that changes the public API surface.
- CI will regenerate `API.md` from source and compare it against the committed version. If they differ, the check fails.
- This ensures `API.md` is always accurate at any given commit and avoids drift between the code and the declared API surface.
- The CI check should provide an easy remediation path: ideally, a mechanism to push the corrected `API.md` as a commit directly to the PR branch (for example, via a bot comment with a "fix this" action or a pipeline that auto-commits the regenerated file on request).

#### PR workflow shape
- Use two GitHub Actions, modeled after TypeSpec's `consistency.yml` and `commenter.yml` split.
- The consistency workflow runs only on `pull_request` events.
- It should detect the affected package(s) from the PR diff, then regenerate `API.md` for each package in a matrix job so multi-package PRs are handled deterministically.
- For each package, the workflow should compare the generated `API.md` to the committed file in the branch.
- If any package differs, the job fails and uploads the generated `API.md` plus a diff summary as an artifact.
- A separate commenter workflow should run after the consistency workflow completes, read the artifact, and post or update a PR comment with the failure summary and a remediation action.
- The remediation action should be a trusted path that commits the regenerated `API.md` back to the PR branch, so the reviewer can fix drift without manually copying files.
- The commenter workflow should not rebuild the package or execute untrusted PR code; it should only consume the consistency workflow artifact and update the PR conversation.

#### Python package generation
- The Python implementation should reuse the existing package generation tooling already used for API review, then export the result into `API.md`.
- This keeps the consistency check aligned with the same source-of-truth generator that produces review artifacts today.
- If we later move the Python stack to generate `API.md` directly, the workflow contract stays the same: regenerate, compare, fail on drift, and offer a one-click fix path.

**Outcome:** `API.md` is a reliable, always-current representation of the package's public API at every commit.

**Wins:** Eliminates stale API artifacts. Ensures review PRs always reflect actual code. Enables hash-based approval validation at any point in history.

**Losses:** Requires teams to regenerate `API.md` locally or rely on the auto-fix mechanism.

---

### 3. Commenting and Discussion
- Completely leverages native GitHub PR review features:
  - Inline comments
  - Threaded discussions
  - Comment resolution

- Architects participate via:
  - Requested reviewers
  - @mentions

- Comment lifecycle:
  - Open → discussion → resolved

**Outcome:** GitHub replaces APIView’s comment system entirely.

**Wins:** Gain requested features like multi-line comments for free as part of the GH experience. Benefit from any future GH UX enhancements.

**Losses:** Lose the clickable navigation features that allow you to navigate between concepts. Lose custom formatting and custom filters (example: documentation) that exist in APIView today. There are no GitHub equivalents of those.

---

### 4. Architect Review Model
- Architects are assigned as required reviewers on PRs. This can be done automatically via CODEOWNERS.
- Architect feedback is expressed via:
  - Comments
  - GitHub review states:
    - Approve
    - Request Changes
    - Comment

**Outcome:** Approval and feedback semantics align with GitHub’s model.

**Wins:** Aligns with GitHub's model. Leverages GitHub's built-in notifications so service team communication should be improved.

**Losses:** GitHub does not support per-comment severity, so that granularity is lost.

---

### 5. Approval and Gating

Approval shifts from APIView to GitHub but CI enforcement remains largely the same.

- Approvals are tied to a composite key of the package name, API hash and (conditionally) the version
- Review branches must include the "Dismiss stale approvals when new commits are pushed" feature. This is to ensure that the UI reflects the correct approval status and does not show approval while the CI fails.
- SDK release pipelines verify approval status largely the same as they do today.
- Approved PRs are never merged. They exist only for review and approval purposes and can be closed once the associated working branch is merged or closed.
- Review-only PRs should be labeled for discoverability/audit (for example `api-review`, `review-only`).
- Temporary review branches can be deleted after a retention window; PR records, comments, and approvals remain in GitHub after branch deletion.

**Approval Index (ADO Package Work Item)**
- It would be incredibly slow and inefficient for release pipelines to search review PRs for a matching API hash and approval. This would also likely run into GitHub query quota limits.
- Instead of introducing a new external Azure resource, approval state will be persisted on the existing ADO Package Work Item used by SDK pipelines.
- This aligns with current engineering-system ownership: the schema is flexible, already managed by existing pipelines, and operationally maintained today.
- A GitHub trigger will update the Package Work Item when PR approvals are granted, revoked, or become stale.
- There is a potential problem here. If you open two reviews, one comparing your change to the last GA and one comparing your change to the last beta, an approval on either could approve the API hash for both. This can be mitigated by marking the beta-baseline PR as Draft and ensuring Draft approvals do not update the Package Work Item approval fields.

**Initial Releases (New Packages):**
- Initial releases (typically betas) require approval of the namespace.
- In this model, initial reviews appear as all-green (all-addition) PRs since the package has no prior API surface.
- The "initial approval" is simply the PR approval on this all-addition review.
- The base branch for an initial release is a clone of main with `api.md` removed, rather than attempting to find a commit with no trace of the unreleased package (which may be difficult if the package has been in development on main).

**CI Enforcement:**
- The SDK release pipelines will validate that gated releases are approved for release. They will do this by computing the hash of the API surface area and consulting the index for an approval.
- If missing, the CI will fail.
- Logic for treating approvals 

**Outcome:** Approval is tracked and gated via the existing ADO Package Work Item, avoiding the need for a separate new store.

**Wins:** Leverage GitHub approval features (like dismissing stale reviews) while reusing existing ADO package lifecycle infrastructure.

**Losses:** Approval state remains external to GitHub and depends on ADO work-item synchronization reliability.

---

## Explicit Non-Goals (Capabilities Removed)

These are features in APIView that would not be migrated to this Github-centric workstream.

### Diagnostics
- APIView diagnostics will not be migrated

**Rationale:**
- Diagnostics are tightly coupled to parser implementations
- GitHub has no native equivalent of domain-specific, parser-aware inline diagnostics
- The closest mechanisms (Check Runs API annotations, bot-posted review comments) require custom infrastructure per language

**Replacement:**
- Affected language teams should migrate diagnostics to:
  - Language-specific static analyzers (e.g., linters, analyzers)
  - Executed as part of CI pipelines
  - Optionally surfaced as PR annotations via the Check Runs API

**Result:**
- Diagnostics shift from review-time to CI-time validation
- Each language team owns both the analyzer logic and the GitHub integration

---

### Comment Severity
- APIView-style comment severity distinctions (for example must-fix, should-fix, suggestion) will not be migrated as a first-class system capability

**Rationale:**
- GitHub review model provides only high-level review states (Approve, Request Changes, Comment)
- Comment-level severity semantics are not native and cannot be reliably enforced without custom tooling

**Replacement:**
- Blocking feedback is represented via “Request Changes” at review level
- Non-blocking guidance remains as regular comments
- Teams can adopt labeling/convention-based guidance in comment text, but this is advisory rather than enforced metadata

**Result:**
- Fine-grained severity taxonomy is lost in Phase 1
- Merge gating remains possible, but only at blocking/non-blocking granularity

---

### Navigation
- APIView-style API navigation experiences will not be migrated.

**Rationale:**
- GitHub's file tree navigation is repository/file oriented, not API-model oriented
- APIView's logical API tree (namespaces, classes, members) depends on parser-produced semantic structure
- Deep symbol navigation (for example usage-to-declaration links) is not natively available in GitHub PR diffs for generated API artifacts

**Replacement:**
- None. Any method of adding links to the markdown would reduce the value of `api.md` as a textual representation of the API. And such links would not be clickable in diff mode.

**Result:**
- Logical symbol-aware navigation is significantly reduced (eliminated) compared to APIView
- Review remains viable but requires more manual scrolling/searching in GitHub
- Navigation fidelity is a known regression accepted for Phase 1

---

### Namespace Approvals
- APIView namespace approval workflows will not be migrated as part of this proposal.

**Rationale:**
- Namespace approvals in APIView are admin-only and operationally separate from normal PR review
- Migrating this requires governance/process integration beyond API diff review mechanics
- This proposal focuses on API review workflow migration, not namespace governance redesign

**Replacement:**
- Continue using the current namespace approval mechanism (email-based workflow)
- Record namespace approval decisions in the existing operational channel used today
- Keep namespace approval as a prerequisite check in release readiness processes where applicable

**Result:**
- Namespace governance remains external to GitHub PR review in Phase 1
- Teams retain existing approval process continuity while API review migrates

---

## Future Work

These items are out of scope for Phase 1.

### Cross-language coordination

The release planner links language SDKs to the organizing TypeSpec. A possible future enhancement is to generate a coordination PR in `azure-rest-api-specs` that mirrors `API.md` artifacts produced by participating language SDK PRs, shown alongside the TypeSpec change. This would provide a single discussion surface for cross-language review while keeping approval and release gating authoritative in the language repositories. The coordination PR would be informational only and include provenance for each mirrored artifact (source repo, PR/commit, pipeline run, and artifact hash). Because this is not required to meet Phase 1 objectives, it is deferred to future work.

### AI-assisted Reviews

As of May 2026, the [GitHub Copilot code review agent](https://docs.github.com/en/copilot/tutorials/customize-code-review) supports Copilot instructions but does not support skills. It also limits custom instructions to 4,000 characters and 1,000 lines. These limits are insufficient to encode current design guidance (for example, the Python design guidelines alone exceed 50k characters). Over time, GitHub may raise these limits or introduce additional customization mechanisms (for example, skills).

At this time, we can provide basic guidance by using `api.instructions.md` in `.github/instructions` with `applyTo: "**/api.md"`, subject to the 4,000-character and 1,000-line limits. While we would provide the initial content for these files, they would ultimately be owned and maintained by the language teams.

Additionally, we can develop richer skills and custom agents to aid in API review that are usable today via the GitHub Copilot VS Code extension or the GitHub Copilot CLI. These surfaces fully support skills, custom instructions without length limits, and MCP tooling. However, using them requires the architect to drop down into the IDE or CLI rather than reviewing directly on github.com. This is a viable interim path for delivering higher-quality AI-assisted feedback while GitHub's native code review agent catches up. When the code review agent eventually gains skill support, these same skills should be directly leverageable without rewriting them.

Ultimately, we want language-team-authored skills to be used intelligently by the code review agent to produce higher-quality API feedback directly on github.com.

Because GitHub support for code review customization is so limited, this is deferred to future work.

---

## Success Criteria

- 100% of new API reviews occur on GitHub PRs
- CI reliably gates merges based on approval requirements
- Architects no longer rely on APIView for review workflows
