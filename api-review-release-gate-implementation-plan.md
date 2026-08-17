# SDK Release Pipeline Update Plan

## Motivation
API Review Hub must be integrated into the release process so approval and release state are tracked by the new system, while maintaining compatability with APIView during the transition phase. When APIView is retired, the new new pipeline must continue to "just work".

## Current State
The current release pipeline depends heavily on `Create-APIReview.ps1`. This script combines three independent responsibilities:

- Uploading an artifact or token file to create an APIView revision.
- Interpreting APIView responses to determine whether a package is approved.
- Marking an APIView revision as shipped through the same upload path.

The script is invoked through `eng/common/pipelines/templates/steps/create-apireview.yml` in both Build and release flows. 

The problem is that this script is confusingly overloaded. There is no logical situation where you would ever be invoking more than one "mode" of this script at a time, and yet running the script *always* requires creating an APIView revision. Any tooling that subverts that will break all three use cases.

## Planned State
The plan is to replace pipeline use of the overloaded `Create-APIReview.ps1` with three focused scripts and a dedicated step template for each script:

1. Create an APIView revision during the transition to API Review Hub (ARH), from the Build stage only.
2. Determine release readiness through `azsdk package get-approval-status`, from the Build stage and again when those steps are replayed during release. This gate must not be disabled, bypassed, or overridden by local language-specific tooling or configuration.
3. Mark a released package in ARH and APIView during the release stage only through `azsdk package mark-released`.

`eng/common/scripts/Create-APIReview.ps1` will not be changed or removed. Pipeline owners will wire the new scripts into the appropriate jobs.
`eng/common/pipelines/templates/steps/create-apireview.yml` will not be changed or removed. Pipeline owners will wire the new step templates into the appropriate jobs.

**NOTE:** This proposal does not change `eng/common/pipelines/templates/steps/validate-all-packages.yml`, `Validate-All-Packages.ps1`, or the Azure DevOps Package work-item update flow. Those pipelines may continue recording their existing APIView-derived validation fields independently; the unified release gate introduced here does not depend on or replace that behavior. Any changes to `validate-all-packages.yml`, `Validate-All-Packages.ps1`, or their Package work-item behavior must be coordinated with Praveen, who owns that pipeline area.

## Proposed Scripts and Step Templates

### 1. Create APIView Revision
Suggested script:

- `eng/common/scripts/Create-APIViewRevision.ps1`

Required step template:

- `eng/common/pipelines/templates/steps/create-apiview-revision.yml`

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

Required step template:

- `eng/common/pipelines/templates/steps/get-package-approval-status.yml`

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
- API hash, expected to be `apiMdSha256` from `api.metadata.yml` when available.

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

Required step template:

- `eng/common/pipelines/templates/steps/mark-package-released.yml`

Responsibility:

- Resolve the released package metadata and APIView revision inputs required by the CLI.
- Invoke `azsdk package mark-released` under the pipeline service connection `ADO to ARH Service Connection`.
- Consume the established `PackageMarkReleasedResponse` contract without independently calling or interpreting either backend.
- Emit the ARH and APIView results returned by the command and propagate its nonzero exit when either update fails.

Inputs should include:

- Language.
- Package name.
- Package version.
- API hash.

Non-responsibilities:

- Do not create a new release-candidate revision.
- Do not call ARH or APIView directly or reimplement the CLI's backend result handling.

## Implementation Phases

### Phase 1: Extract Revision Creation

- Implement `Create-APIViewRevision.ps1` using only the revision-creation behavior currently embedded in `Create-APIReview.ps1`.
- Implement `create-apiview-revision.yml` as the pipeline entry point for the script.
- Preserve both the source-only `/autoreview/upload` path and token-file `/autoreview/create` path.
- Preserve existing artifact discovery, package iteration, request metadata, and creation eligibility rules.
- Exclude approval-policy evaluation and `MarkPackageAsShipped` behavior.
- Validate that created APIView revisions match current behavior.
- Leave `Create-APIReview.ps1` untouched.

### Phase 2: Add the Unified Approval Gate

- Implement `Get-PackageApprovalStatus.ps1` around `azsdk package get-approval-status`.
- Implement `get-package-approval-status.yml` as the mandatory pipeline entry point for the script.
- Resolve package/version and, when present, the hash from release-candidate artifacts before invoking the command.
- Pass `--api-hash` only when a matching `api.metadata.yml` is available; otherwise invoke the command without that option.
- Normalize CLI output and exit codes into deterministic pipeline success or failure.
- Do not duplicate backend-specific approval or fallback rules in PowerShell.

### Phase 3: Separate Release Completion

- Implement `Mark-PackageReleased.ps1`.
- Implement `mark-package-released.yml` as the release-stage pipeline entry point for the script.
- Resolve the inputs for and invoke `azsdk package mark-released`.
- Treat the CLI's ARH and APIView results and exit code as authoritative.
- Make the command's partial-failure details visible so remediation is clear.

### Phase 4: Pipeline Migration

- Wire the creation-only script and approval-status script into the Build stage. Replay only the approval-status step during the release stage.
- In each ARH-enabled SDK repository, add build-stage steps that run `create-apireview-hub-artifacts-{lang}` and publish the resulting metadata for the release gate.
- Do not require ARH artifact-generation steps in repositories that have not been onboarded to ARH.
- Ensure the replayed approval-status step gates publishing during release.
- Wire mark-released into the release stage only, after publishing.
- Migrate callers incrementally from `create-apireview.yml` to the three focused step templates.
- Keep the legacy script and template available until all callers have migrated.
- Do not modify `validate-all-packages.yml`, `Validate-All-Packages.ps1`, or Package work-item update pipelines as part of this migration.
- Coordinate with Praveen before proposing any follow-up changes to those validation or work-item flows.

## Script Design Requirements

- Use explicit parameters and avoid behavior switches such as `MarkPackageAsShipped` that change a script's responsibility.
- Give each script a dedicated step template that exposes its inputs explicitly and propagates its exit status.
- Preserve source-only and pre-generated-token creation paths.
- Validate required artifact, build, repository, and package inputs before calling APIView.
- Keep APIView authentication helpers shared only for revision creation; release completion authentication remains owned by the CLI command.
- Use structured JSON/YAML parsing for package metadata and CLI responses.
- Never recompute the hash from content different from the release candidate artifact.
- Treat missing ARH metadata as an omitted optional CLI input, not a script or CI failure.
- Make approval status a mandatory centralized release gate that local language-specific tooling and configuration cannot disable, bypass, or override.
- Include package name, version, and hash in diagnostic output without exposing tokens.
- Propagate nonzero exits for authentication, transport, malformed response, unapproved status, CLI failure, and backend update failures.

## Compatibility and Risks

- **Artifact determinism:** Approval and mark-released must use the exact API hash associated with the package being published.
- **Onboarding dependency:** ARH-enabled repositories must run `create-apireview-hub-artifacts-{lang}` during their build stage and make its metadata available to the release job.
- **Future enforcement:** API hash is optional only during the APIView-to-ARH transition. It becomes required once ARH onboarding is complete and APIView fallback is removed.
- **CLI availability:** Release agents must provide AZSdkCli 0.6.35 or later for the required `azsdk package get-approval-status` and `azsdk package mark-released` commands.
- **Behavior preservation:** The creation-only script must retain source-only upload because not every language pipeline produces a review token file.
- **CLI command removal:** Any out-of-repository consumers of the two APIView revision commands would break. Repository search found no callers, but release notes should identify the removal.
- **Dual-backend consistency:** The CLI owns approval reconciliation across APIView and ARH; scripts should not second-guess it.
- **Gate integrity:** Language-specific tooling or configuration must not suppress the approval check or override its result. The pipeline must fail whenever the centralized CLI decision is unapproved or the check cannot complete successfully.
- **Partial release updates:** `azsdk package mark-released` attempts ARH and APIView independently and preserves each result in `PackageMarkReleasedResponse`. The script must surface those results unchanged; command retries require both backend operations to be idempotent because the command invokes both on each run.
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
8. Validate that `Mark-PackageReleased.ps1` passes the complete input set to `azsdk package mark-released` and propagates its successful response.
9. Simulate each one-backend failure response and verify the script surfaces both backend results and returns failure without implementing its own backend calls or policy.
10. Run a multi-package pipeline and verify package isolation.
11. Confirm mark-released runs only in the release stage and is not included in Build-stage replay.
12. Confirm `Create-APIReview.ps1` remains unchanged throughout migration.

## Open Questions

1. Is the APIView mark-as-shipped operation idempotent, and can its existing request logic be moved without changing behavior?
2. Which existing helper functions can be reused without importing the overloaded script itself?
