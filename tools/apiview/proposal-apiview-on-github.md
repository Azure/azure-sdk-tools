# Proposal: APIView on GitHub

## Objective

Move API review workflows to GitHub while retaining a much-scaled-down APIView service as the authoritative metadata and state service.

APIView remains responsible for tracking review metadata, approval state, review lifecycle state, and release-gating lookup data in CosmosDB. GitHub becomes the review surface and repository action surface.

In this model:

- Language repositories host the review artifacts and PR discussion.
- APIView listens to GitHub webhooks from those repositories.
- APIView persists review metadata and approval state to CosmosDB.
- APIView exposes APIs that pipelines and automation use for review creation and release-gating lookup.
- APIView uses the existing `azure-sdk-automation` GitHub App for actions inside target repositories.

---

## Why Retain a Scaled-Down APIView Service

GitHub is the right surface for review discussion, comments, approvals, and repository-local automation. It is not, by itself, a good authoritative index for API review state that release pipelines need to query efficiently and deterministically.

The API review system still needs a small service boundary for:

- Efficient lookup of approval state by package, API hash, version, language, and baseline intent.
- Durable metadata storage independent of GitHub search limits and PR history shape.
- Webhook-driven normalization of GitHub review events.
- Consistent release-gating APIs used across language repositories.
- Central ownership of review lifecycle transitions that are currently implicit in APIView.

Retaining this service preserves a central API review state authority while avoiding expensive and brittle GitHub queries during release validation.

**Conclusion:** APIView should stop being the primary UI and full review application, but remain the lightweight system of record for API review metadata.

---

## Key Architecture Decision

APIView remains the authoritative API review metadata and approval service. APIView updates its internal CosmosDB state and returns authoritative answers to release pipelines.

GitHub Actions OIDC federation to Azure DevOps is not secure enough for this workflow, so Azure DevOps must not be introduced as the API review state store.

---

## System Components

### 1. Language Repositories

Language repositories remain the home for API review artifacts and review PRs.

Each participating repository must:

- Have the existing `azure-sdk-automation` GitHub App installed by the EngSys team.
- Have webhooks or GitHub App event delivery configured by the EngSys team so APIView receives relevant repository events.
- Implement a service-team-owned script or workflow that generates `api.md` and `api.metadata.yml` for the API review artifact.

### 2. Scaled-Down APIView Service

APIView remains as a backend service only. The service owns:

- Webhook endpoint for GitHub events.
- APIs for creating, updating, querying, and closing API review records.
- Approval indexing and release-gating lookup APIs.
- CosmosDB persistence.
- GitHub App integration for actions in target repositories.

The service no longer needs to own the primary review UI, comment UI, or rich API navigation experience for this workflow. It may retain a minimal dashboard UI for browsing services and API review, approval, and release history, but detailed review links should point to GitHub.

This also means much of the existing APIView code for token-file rendering, review and revision management, and UI-era review state can be removed rather than carried forward into the scaled-down service.

### 3. CosmosDB

CosmosDB stores API review metadata and state, including:

- Review identity and lifecycle state.
- Repository, package, language, service, and API version metadata.
- Working PR, review PR, baseline branch or tag, and review branch references.
- API hash and artifact provenance.
- Approval state and approving architect identity.
- Webhook processing checkpoints and idempotency records.

CosmosDB becomes the durable query source used by SDK release pipelines.

### 4. `azure-sdk-automation` GitHub App

APIView uses the existing `azure-sdk-automation` GitHub App whenever it must take actions inside target repositories.

Examples include:

- Creating or updating synthetic review branches.
- Opening or updating API review PRs.
- Applying labels.
- Requesting architect reviewers.
- Posting comments or status summaries.
- Closing review PRs when the associated working PR is closed or completed.

The GitHub App must be installed in each target repository where APIView is expected to act.

---

## Core Workflow

### 1. Review Creation

API review creation is initiated through APIView's web service. A language repository workflow, command, or release process may request a review, but APIView owns the creation of the review record and the corresponding GitHub review PR.

The initiating workflow calls APIView with:

- Repository name.
- Package name.
- Working branch or working PR.
- Selected baseline tag or baseline intent.

APIView creates or updates a review record in CosmosDB, then uses `azure-sdk-automation` to create the necessary base and review branches in the language repo and open the review PR. As part of that action, APIView causes the language-team-owned script or workflow to generate `api.md` and `api.metadata.yml`. APIView consults the language repo's `ARCHITECTS` file to assign the appropriate reviewers. APIView then records the resulting PR number, PR URL, branch names, artifact paths, API hash, baseline, and lifecycle state directly in CosmosDB.

The review PR remains the human review artifact. APIView remains the metadata authority.

### 2. Review Tracking

Review PRs will include human-readable links or summaries, such as:

- Link to the associated working PR or branch.
- Package name.
- Baseline tag.
- Review artifact path.

These details are helpful for reviewers, but they are not the authoritative metadata source and should not be required for webhook correlation.

### 3. Webhook Processing

APIView listens to GitHub webhook events from target language repositories.

Relevant events include:

- Pull request opened, edited, synchronized, closed, reopened, and converted to or from draft.
- Pull request review submitted or dismissed.
- Check suite or workflow events if needed for freshness or artifact status.

APIView must tolerate GitHub webhook retries and duplicate deliveries. GitHub retries use the same `X-GitHub-Delivery` ID, which APIView can use for deduplication, but state updates should still be safe to replay because different events may describe the same resulting review state.

### 4. Approval Tracking

Architect approval is expressed through GitHub PR review state.

When APIView receives a review event, it resolves the associated API review record and verifies:

- The PR is an API review PR tracked by APIView.
- The approver is authorized for the package according to the language repo's `ARCHITECTS` file.
- The PR is not in a state that should suppress approval indexing, such as draft.

If the approval is valid, APIView records the approved API hash in CosmosDB.

If the approved review is revoked or dismissed, APIView removes the corresponding `approvedHash` from CosmosDB. If the PR changes and produces a new API hash, no cleanup is needed for the previous approval because release gating only succeeds when the package being released matches an approved hash.

### 5. Release Gating

Release gating works similarly to today. Release pipelines compute the API hash for the package being released and call APIView to ask whether that hash is approved for the release scenario.

The APIView response should include:

- Approved or not approved.
- Matching review ID.
- Matching review PR URL.
- Approving architect.
- Approval timestamp.
- Baseline intent or version constraints used for the decision.
- A failure reason when approval is missing or invalid.

This keeps release gating deterministic without requiring pipelines to search GitHub PRs.

---

## APIView Service Responsibilities

### Required

- Accept review creation and update requests from trusted automation.
- Listen to GitHub webhooks for target language repositories.
- Persist API review metadata and state in CosmosDB.
- Maintain an approval index keyed by package, API hash, language, and release intent.
- Expose release-gating query APIs.
- Use `azure-sdk-automation` for repository actions.
- Validate webhook signatures and GitHub App permissions.
- Process webhook events idempotently.

### Not Required

- Host the primary API review UI.
- Recreate GitHub PR comments in APIView.
- Provide the existing full APIView navigation experience.
- Preserve legacy token-file, review, and revision management code that only supports the old APIView UI model.

---

## Data Model Sketch

### Review Record

- `reviewId`
- `repositoryOwner`
- `repositoryName`
- `language`
- `packageName`
- `servicePath`
- `workingPullRequestUrl`
- `workingBranch`
- `reviewPullRequestUrl`
- `reviewBranch`
- `baselineTag`
- `baselineIntent`
- `apiArtifactPath`
- `apiHash`
- `lifecycleState`
- `createdAt`
- `updatedAt`

### Approval Record

- `reviewId`
- `packageName`
- `language`
- `apiHash`
- `baselineIntent`
- `apiVersion`
- `approvedBy`
- `approvedAt`
- `approvalSourcePullRequestUrl`
- `approvalState`
- `revokedAt`
- `revokedReason`

### Webhook Event Record

- `deliveryId`
- `eventType`
- `repositoryName`
- `pullRequestNumber`
- `processedAt`
- `processingResult`

These shapes are illustrative. The final schema should be optimized for release-gating lookups and webhook idempotency.

---

## Security and Authentication

- GitHub webhook requests must be validated using the webhook secret or GitHub App event validation mechanism.
- GitHub webhook secrets must be rotated at least every 90 days. Rotation should be automated through the GitHub App or repository webhook configuration APIs where possible.
- APIView should call GitHub using installation tokens for the existing `azure-sdk-automation` GitHub App.
- APIView APIs called by language repository pipelines must authenticate with Entra ID.
- Repository actions should be limited to repositories where the GitHub App is installed.
- APIView should not accept approval state directly from untrusted clients; approval state should be derived from validated GitHub webhook events.

---

## Operational Requirements

- Webhook processing must be retry-safe.
- APIView must tolerate delayed, duplicate, or out-of-order GitHub events.
- Release-gating queries must be fast and deterministic.
- ADO pipeline logs and APIView service logs should provide enough diagnostics to explain why a release gate failed.
- APIView should record provenance for all approval decisions.
- Missing GitHub App installation should fail clearly with remediation instructions.
- Webhook secret rotation should be operationalized so APIView and webhook configuration stay in sync during each 90-day rotation window. APIView should support validating both the current and next webhook secret during a short rotation grace period.

---

## Migration Plan

### Phase 1: Service Foundation

- Add CosmosDB-backed review and approval records.
- Add APIView APIs for review creation and release-gating lookup.
- Add GitHub webhook ingestion for API review PR events.
- Add GitHub App client support for repository actions through `azure-sdk-automation`.

### Phase 2: Language Repository Integration

- Update language repository workflows to call APIView for review creation and release-gating lookup.
- EngSys installs `azure-sdk-automation` in each participating language repository.
- Add APIView review creation entry points for each participating language repository.
- EngSys configures repository webhook or GitHub App event delivery for each participating language repository.

### Phase 3: Release Pipeline Integration

- Update SDK release pipelines to query APIView for approval state.
- Validate approval lookup behavior across beta, GA, hotfix, and multi-baseline review scenarios.

### Phase 4: APIView UI Reduction

- Remove or retire UI capabilities that are replaced by GitHub PR review.
- Keep only a minimal dashboard for service history and API review, approval, and release history, plus operational views needed to debug metadata, approvals, and release-gating decisions.
- Link detailed review, approval, and discussion views to the corresponding GitHub PRs.

---

## Explicit Non-Goals

- Rebuild the full APIView UI experience in GitHub.
- Make GitHub search the source of truth for release gating.
- Introduce Azure DevOps as the API review metadata or approval state store.

---

## Open Questions

- How long should APIView retain closed review records and webhook event records?

---

## Success Criteria

- API review PRs are created and updated in GitHub language repositories.
- APIView receives and processes webhook events from participating repositories.
- APIView stores review metadata and approval state in CosmosDB.
- SDK release pipelines query APIView for approval state.
- Repository actions are performed through the existing `azure-sdk-automation` GitHub App.
- GitHub App installation gaps and approval lookup failures produce clear, actionable diagnostics.