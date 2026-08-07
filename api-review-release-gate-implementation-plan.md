# API Review Release Workflow Decomposition Plan

## Motivation
API Review Hub must be integrated into the release process so approval and release state are tracked by the new system during the transition from APIView. The existing `Create-APIReview.ps1` is confusingly overloaded: despite its name, it creates revisions, evaluates release approval, and marks revisions as shipped. Splitting these responsibilities makes each pipeline step's purpose and stage placement explicit while allowing APIView compatibility to be retired independently.

## Goal
Replace pipeline use of the overloaded `Create-APIReview.ps1` with three focused scripts:

1. Create an APIView revision during the transition to API Review Hub (ARH), from the Build stage only.
2. Determine release readiness through `azsdk package get-approval-status`, from the Build stage and again when those steps are replayed during release. This gate must not be disabled, bypassed, or overridden by local language-specific tooling or configuration.
3. Mark a released package in ARH during the release stage only, while preserving the legacy APIView update.

`eng/common/scripts/Create-APIReview.ps1` will not be changed or removed. Pipeline owners will wire the new scripts into the appropriate jobs.

This proposal does not change `eng/common/pipelines/templates/steps/validate-all-packages.yml`, `Validate-All-Packages.ps1`, or the Azure DevOps Package work-item update flow. Those pipelines may continue recording their existing APIView-derived validation fields independently; the unified release gate introduced here does not depend on or replace that behavior.

Any future changes to `validate-all-packages.yml`, `Validate-All-Packages.ps1`, or their Package work-item behavior must be coordinated with Praveen, who owns that pipeline area.

The unused `azsdk apiview create-ci-revision` and `azsdk apiview create-pull-request-revision` commands will be removed. They cannot replace the existing source-artifact and token-file workflows without additional investment that is not justified while APIView is being deprecated.

## Current State
`Create-APIReview.ps1` currently combines three independent responsibilities:

- Uploading an artifact or token file to create an APIView revision.
- Interpreting APIView responses to determine whether a package is approved.
- Marking an APIView revision as shipped through the same upload path.

The script is invoked through `eng/common/pipelines/templates/steps/create-apireview.yml` in both Build and release flows. The new design separates those operations so revision creation runs only during Build, approval checks run during Build and are replayed during release, and marking a package as released remains a release-only operation.

The Azure SDK CLI also exposes `apiview create-ci-revision` and `apiview create-pull-request-revision`, which originated from [Support API View creation and validation (issue #13813)](https://github.com/Azure/azure-sdk-tools/issues/13813). The intended pipeline adoption did not occur, and neither command is invoked by repository pipelines or tools. The commands do not cover all existing behavior, notably source-only artifact upload, so they will be removed rather than expanded.

## Proposed Scripts

### 1. Create APIView Revision
Suggested script:

- `eng/common/scripts/Create-APIViewRevision.ps1`

Responsibility:

- Preserve only the APIView revision-creation behavior from `Create-APIReview.ps1`.
- Upload a source artifact to `/autoreview/upload` when no pre-generated review token file exists.
- Create a revision through `/autoreview/create` when a review token file is available.
- Preserve package selection, language-specific artifact discovery, multi-package processing, branch/version eligibility, and applicable package metadata.
- Preserve the existing APIView authentication and request behavior required by current pipelines.
- Return a clear failure when authentication, upload, or revision creation fails, without interpreting approval status.

Non-responsibilities:

- Do not determine release approval.
- Do not mark a revision or package as released.
- Do not call ARH.
- Do not invoke the Azure SDK CLI APIView revision commands.

This script is temporary and can be retired when revision creation has fully transitioned to ARH, as can `Create-APIReview.ps1`.

### 2. Get API Review Approval Status
Suggested script:

- `eng/common/scripts/Get-PackageApprovalStatus.ps1`

Responsibility:

- Resolve the package name and package version required by the CLI.
- Resolve the canonical API hash when ARH artifacts are available.
- Invoke `azsdk package get-approval-status`.
- Consume the established `ReleaseGateDecision` response contract without redefining or independently interpreting it.
- Treat the CLI result as authoritative because the command owns ARH evaluation and explicit APIView fallback.
- Prevent language-specific tooling or configuration from skipping the check or converting an unapproved result into pipeline success.
- Do not fail before invoking the CLI solely because ARH artifacts or an API hash are absent.
- Emit useful status details and fail the pipeline when the package is not approved for release.

Inputs should be explicit rather than inferred from revision-creation behavior:

- Package name.
- Package version.
- Optional API hash, expected to be `apiMdSha256` from `api.metadata.yml` when available.

CI metadata:

1. Package version from `packages_extended/PackageInfo/<package>.json`.
2. For ARH-enabled repositories, API hash from the matching `api.metadata.yml` produced for the release candidate.

ARH-enabled repositories must add build-stage steps that run their language-specific `create-apireview-hub-artifacts-{lang}` pipeline so `api.metadata.yml` and its hash are available to the release gate. Repositories not yet onboarded to ARH will not have these steps or artifacts.

Package name and version remain required. During the transition, a missing `api.metadata.yml` or API hash must not fail CI: the script must invoke the CLI without `--api-hash`. The CLI already owns APIView fallback and may return an approved result without ARH artifacts. The script must propagate that result rather than implementing its own fallback or treating missing ARH artifacts as an error.

This optionality is temporary. After all supported repositories are onboarded to ARH and APIView fallback is retired, the API hash will become required and missing ARH artifacts must fail the release gate. That enforcement change should be made explicitly at the end of the transition rather than introduced incrementally by this script.

Non-responsibilities:

- Do not create or upload revisions.
- Do not implement independent APIView or ARH approval logic.
- Do not mark packages as released.

### 3. Mark API Review Released
Suggested script:

- `eng/common/scripts/Mark-PackageReleased.ps1`

Responsibility:

- Call the ARH `releases/mark-released` endpoint for the released package/version/API hash.
- Authenticate to ARH using the pipeline service connection `ADO to ARH Service Connection`.
- Preserve the legacy APIView behavior that marks the corresponding revision as shipped.
- Report each backend result independently and fail if either required update fails.
- Retry and remediate each backend independently so an APIView failure does not repeat or alter a successful ARH update, and vice versa.

Inputs should include:

- Package name.
- Package version.
- API hash.
- APIView revision inputs required by the legacy mark-as-shipped operation.

Non-responsibilities:

- Do not create a new release-candidate revision.
- Do not determine whether a package is approved.

## Implementation Phases

### Phase 1: Extract Revision Creation

- Implement `Create-APIViewRevision.ps1` using only the revision-creation behavior currently embedded in `Create-APIReview.ps1`.
- Preserve both the source-only `/autoreview/upload` path and token-file `/autoreview/create` path.
- Preserve existing artifact discovery, package iteration, request metadata, and creation eligibility rules.
- Exclude approval-policy evaluation and `MarkPackageAsShipped` behavior.
- Validate that created APIView revisions match current behavior.
- Leave `Create-APIReview.ps1` untouched.

### Phase 2: Remove Unused AZSdkCli Commands

- Remove `azsdk apiview create-ci-revision` and `azsdk apiview create-pull-request-revision`.
- Remove their command registration, handlers, command-specific options, service methods, tests, and documentation.
- Retain APIView services and options used by other supported AZSdkCli commands.
- Do not add replacement commands because APIView revision creation is temporary and remains pipeline-specific during deprecation.

### Phase 3: Add the Unified Approval Gate

- Implement `Get-PackageApprovalStatus.ps1` around `azsdk package get-approval-status`.
- Resolve package/version and, when present, the hash from release-candidate artifacts before invoking the command.
- Pass `--api-hash` only when a matching `api.metadata.yml` is available; otherwise invoke the command without that option.
- Normalize CLI output and exit codes into deterministic pipeline success or failure.
- Do not duplicate backend-specific approval or fallback rules in PowerShell.

### Phase 4: Separate Release Completion

- Implement `Mark-PackageReleased.ps1`.
- Call ARH `releases/mark-released` with package/version/hash.
- Perform the legacy APIView mark-as-shipped operation separately.
- Make partial failures visible so retries and remediation are clear.

### Phase 5: Pipeline Migration

- Wire the creation-only script and approval-status script into the Build stage. Replay only the approval-status step during the release stage.
- In each ARH-enabled SDK repository, add build-stage steps that run `create-apireview-hub-artifacts-{lang}` and publish the resulting metadata for the release gate.
- Do not require ARH artifact-generation steps in repositories that have not been onboarded to ARH.
- Ensure the replayed approval-status step gates publishing during release.
- Wire mark-released into the release stage only, after publishing.
- Migrate callers incrementally from `create-apireview.yml` to focused templates or direct script tasks.
- Keep the legacy script and template available until all callers have migrated.
- Do not modify `validate-all-packages.yml`, `Validate-All-Packages.ps1`, or Package work-item update pipelines as part of this migration.
- Coordinate with Praveen before proposing any follow-up changes to those validation or work-item flows.

## Script Design Requirements

- Use explicit parameters and avoid behavior switches such as `MarkPackageAsShipped` that change a script's responsibility.
- Preserve source-only and pre-generated-token creation paths.
- Validate required artifact, build, repository, and package inputs before calling APIView.
- Keep authentication helpers shared only where doing so does not couple the workflows again.
- Use structured JSON/YAML parsing for package metadata and CLI responses.
- Never recompute the hash from content different from the release candidate artifact.
- Treat missing ARH metadata as an omitted optional CLI input, not a script or CI failure.
- Make approval status a mandatory centralized release gate that local language-specific tooling and configuration cannot disable, bypass, or override.
- Include package name, version, and hash in diagnostic output without exposing tokens.
- Propagate nonzero exits for authentication, transport, malformed response, unapproved status, and backend update failures.

## Compatibility and Risks

- **Artifact determinism:** Approval and mark-released must use the exact API hash associated with the package being published.
- **Onboarding dependency:** ARH-enabled repositories must run `create-apireview-hub-artifacts-{lang}` during their build stage and make its metadata available to the release job.
- **Future enforcement:** API hash is optional only during the APIView-to-ARH transition. It becomes required once ARH onboarding is complete and APIView fallback is removed.
- **CLI availability:** Release agents must provide a version of `azsdk` containing `package get-approval-status`.
- **Behavior preservation:** The creation-only script must retain source-only upload because not every language pipeline produces a review token file.
- **CLI command removal:** Any out-of-repository consumers of the two APIView revision commands would break. Repository search found no callers, but release notes should identify the removal.
- **Dual-backend consistency:** The CLI owns approval reconciliation across APIView and ARH; scripts should not second-guess it.
- **Gate integrity:** Language-specific tooling or configuration must not suppress the approval check or override its result. The pipeline must fail whenever the centralized CLI decision is unapproved or the check cannot complete successfully.
- **Partial release updates:** ARH is the authoritative release record, so its accuracy takes precedence over the legacy APIView update. If either backend succeeds while the other fails, logging must preserve each result and retries must not corrupt or obscure the ARH state.
- **Multi-package runs:** Each package must be evaluated and marked independently.
- **Stage placement:** Revision creation runs only in the Build stage. Approval status runs in Build and is replayed during release. Mark-released runs only in the release stage.
- **Transition lifetime:** The APIView revision script is intentionally temporary and should not accumulate ARH behavior.
- **Independent work-item flow:** Existing package validation and Azure DevOps work-item updates remain untouched. Their legacy APIView-derived fields have already produced stale information, and there is currently no plan to update Package work items with ARH approval or release state. Any changes to this flow must be coordinated with Praveen.

## Validation Plan

1. Compare `Create-APIViewRevision.ps1` output against current source-only upload and token-file creation behavior.
2. Verify artifact discovery, package selection, branch/version eligibility, metadata, and multi-package processing remain equivalent.
3. Verify the two Azure SDK CLI commands, their tests, service methods, and documentation are removed without affecting other APIView commands.
4. Validate `Get-PackageApprovalStatus.ps1` with ARH approval, APIView fallback approval, combined unapproved, malformed-response, and CLI-failure cases.
5. Confirm ARH-enabled repositories pass the matching package/version/hash to approval status in Build and when that step is replayed during release.
6. Confirm missing `api.metadata.yml` or API hash does not fail the script before the CLI runs, and that the script propagates a successful CLI result produced through APIView fallback.
7. Verify language-specific tooling and configuration cannot skip the approval check or override an unapproved or failed CLI result.
8. Validate `Mark-PackageReleased.ps1` against ARH and APIView success paths.
9. Simulate one-backend failure during mark-released and verify the failure is diagnosable and safely retryable.
10. Run a multi-package pipeline and verify package isolation.
11. Confirm mark-released runs only in the release stage and is not included in Build-stage replay.
12. Confirm `Create-APIReview.ps1` remains unchanged throughout migration.

## Open Questions

1. Is the APIView mark-as-shipped operation idempotent, and can its existing request logic be moved without changing behavior?
2. Which existing helper functions can be reused without importing the overloaded script itself?

## Recommended First Implementation Slice

- [x] Implement and test `Get-PackageApprovalStatus.ps1` first because it establishes the new release gate without changing revision creation or release completion.
- [x] Implement `Create-APIViewRevision.ps1` and migrate the Build-stage caller, keeping creation Build-only while approval status is replayed during release.
- [ ] Implement `Mark-PackageReleased.ps1` and add it only to the release stage after publishing.
- [x] Retain `Create-APIReview.ps1` unchanged until migration is complete.
