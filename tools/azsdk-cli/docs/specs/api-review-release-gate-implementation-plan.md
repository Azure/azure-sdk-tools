# SDK Release Pipeline Update Plan

## Motivation
API Review Hub must be integrated into the release process so approval and release state are tracked by the new system, while maintaining compatibility with APIView during the transition phase. When APIView is retired, the new pipeline must continue to "just work".

## Current State
The current release pipeline depends heavily on `Create-APIReview.ps1`. This script combines three independent responsibilities:

- Uploading an artifact or token file to create an APIView revision.
- Interpreting APIView responses to determine whether a package is approved.
- Marking an APIView revision as shipped through the same upload path.

The script is invoked through `eng/common/pipelines/templates/steps/create-apireview.yml` in both Build and release flows. 

PR pipelines separately invoke `eng/common/scripts/Detect-Api-Changes.ps1` through `eng/common/pipelines/templates/steps/detect-api-changes.yml`. Despite its name, this script does not compare API surfaces locally. It identifies changed Track 2 packages, locates their API artifacts, and calls APIView's `CreateAPIRevisionIfAPIHasChanges` endpoint. APIView performs the comparison, creates or updates a PR-scoped revision when needed, associates it with the PR and commit, and later cleans it up after the PR closes. The script has no ARH or release-gate responsibility.

The problem is that this script is confusingly overloaded. There is no logical situation where you would ever be invoking more than one "mode" of this script at a time, and yet running the script *always* requires creating an APIView revision. Any tooling that subverts that will break all three use cases.

## Planned State
The plan is to replace pipeline use of the overloaded `Create-APIReview.ps1` with three focused scripts and a dedicated step template for each script:

1. Create an APIView revision during the transition to API Review Hub (ARH), from the Build stage only.
2. Determine release readiness through `azsdk package get-approval-status` at the existing approval-check location in each language pipeline. This gate may be bypassed only through its dedicated, authorized `Skip.CheckPackageApproval` break-glass variable.
3. Mark a released package in ARH and APIView during the release stage only through `azsdk package mark-released`. This step may be bypassed only through its separate, authorized `Skip.MarkPackageReleased` break-glass variable.

Each break-glass variable must use the same authorized-requester pattern as `Skip.CreateApiReview`. The variables are intentionally independent: no single variable or local language-specific setting may disable more than one of these operations.

`eng/common/scripts/Create-APIReview.ps1` will not be changed or removed. Pipeline owners will wire the new scripts into the appropriate jobs.
`eng/common/pipelines/templates/steps/create-apireview.yml` will not be changed or removed. Pipeline owners will wire the new step templates into the appropriate jobs.

`Detect-Api-Changes.ps1` and `detect-api-changes.yml` are also unchanged by this proposal. They remain the PR-only APIView revision path during the transition. Once ARH owns PR revision creation and APIView is retired, they can be retired alongside `Create-APIReview.ps1` and `create-apireview.yml`.

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
- Preserve the authorized `Skip.CreateApiReview` break-glass mechanism from `create-apireview.yml` in the new step template so revision creation can be skipped during an APIView outage.
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

- Resolve the package name, package version, and optional canonical API hash from each explicitly supplied package-info file.
- Invoke `azsdk package get-approval-status` independently for every package-info file.
- Read each package's `ReleaseStatus`; evaluate every package, but enforce approval failures only when `ReleaseStatus` is not `Unreleased`.
- Consume the established `ReleaseGateDecision` response contract without redefining or independently interpreting it.
- Treat the CLI result as authoritative because the command owns ARH evaluation and explicit APIView fallback.
- Preserve a dedicated `Skip.CheckPackageApproval` break-glass mechanism, authorized using the same user allowlist pattern as `Skip.CreateApiReview`.
- Preserve each language pipeline's existing approval-check placement and conditions rather than introducing a common Build- or release-stage location.
- Do not invoke or replay the template in the release stage.
- Do not honor `Skip.CreateApiReview`, `Skip.MarkPackageReleased`, a shared master skip variable, language-specific tooling, or local configuration as a reason to bypass the check or convert an unapproved result for a package marked for release into pipeline success.
- Do not fail before invoking the CLI solely because ARH artifacts or an API hash are absent.
- Emit useful status details and fail the pipeline when a package marked for release is not approved. Ignore approval-check failures for packages whose `ReleaseStatus` is `Unreleased` so unrelated packages in the same service directory do not block the intended release.

Inputs should be explicit rather than inferred from revision-creation behavior:

- Language, supplied once for the script invocation because package-info files do not standardly contain it.
- Package-info file paths, with each file representing one package to evaluate.
- Each package-info file must contain the package name and package version required by the CLI.
- For ARH-enabled packages, the package-info file must also contain `ApiHash`, copied unchanged from `apiMdSha256` in the matching `api.metadata.yml`.

The approval script must not use the legacy `PackageName`/`Artifacts`/`PackageInfoFiles` discovery precedence from revision creation. It must process only the explicitly supplied package-info files and evaluate every package independently. An unapproved or failed check is fatal only when that package's `ReleaseStatus` is not `Unreleased`; failures for `Unreleased` packages are logged and ignored.

CI metadata:

1. Package name and version from `packages_extended/PackageInfo/<package>.json`.
2. For ARH-enabled repositories, `ApiHash` in that same file, populated from the matching release candidate's `api.metadata.yml` `apiMdSha256` value.

ARH-enabled repositories must add build-stage steps that run their language-specific `create-apireview-hub-artifacts-{lang}` pipeline and copy each resulting `apiMdSha256` value into the matching package-info file as `ApiHash` before publishing `packages_extended`. The approval gate must consume the hash from package info rather than rediscovering or correlating separate ARH artifacts. Repositories not yet onboarded to ARH will publish package-info files without `ApiHash`.

Package name and version remain required. During the transition, a missing `ApiHash` property must not fail CI: the script must invoke the CLI for that package without `--api-hash`. The CLI already owns APIView fallback and may return an approved result without ARH artifacts. The script must propagate that result rather than implementing its own fallback or treating the missing hash as an error.

This optionality is temporary. After all supported repositories are onboarded to ARH and APIView fallback is retired, `ApiHash` will become required in every package-info file and a missing value must fail the release gate. That enforcement change should be made explicitly at the end of the transition rather than introduced incrementally by this script.

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

- Use the explicitly supplied language and resolve the published package name, package version, and optional API hash from each explicitly supplied package-info file.
- Invoke `azsdk package mark-released` independently for every package-info file under the pipeline service connection `ADO to ARH Service Connection`.
- Consume the established `PackageMarkReleasedResponse` contract without independently calling or interpreting either backend.
- Preserve a dedicated `Skip.MarkPackageReleased` break-glass mechanism in the step template, authorized using the same user allowlist pattern as `Skip.CreateApiReview`.
- Do not honor `Skip.CreateApiReview`, `Skip.CheckPackageApproval`, a shared master skip variable, language-specific tooling, or local configuration as a reason to bypass or override this step.
- Emit the ARH and APIView results returned by the command and propagate its exit code. The command succeeds when either backend succeeds and fails only when neither backend succeeds.

Inputs should include:

- Language, supplied once for the script invocation because package-info files do not standardly contain it.
- Package-info file paths, with each file representing one published package to mark.
- Each package-info file must contain the published package `Name` and `Version`. During the transition, `ApiHash` is optional and is passed to the CLI when present.

When `ApiHash` is absent, the script must invoke the CLI without `--api-hash`; the CLI then skips ARH and attempts APIView. The script must process only explicitly supplied package-info files, attempt every valid package independently, and fail the invocation if the CLI reports that no backend marked any package successfully.

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
- Implement `get-package-approval-status.yml` as the single pipeline entry point for the script, called from the existing approval-check location in each language pipeline with its existing conditions.
- Accept explicit package-info file paths and resolve each package's name, version, and optional `ApiHash` from its file.
- Invoke the command independently for every package-info file and aggregate failures only after every package has been evaluated.
- Read `ReleaseStatus` from each package-info file and aggregate only failures for packages marked for release; log and ignore failures for `Unreleased` packages.
- Pass `--api-hash` only when the current package-info file contains `ApiHash`; otherwise invoke the command without that option.
- Normalize CLI output and exit codes into deterministic pipeline success or failure.
- Do not duplicate backend-specific approval or fallback rules in PowerShell.

### Phase 3: Separate Release Completion

- Implement `Mark-PackageReleased.ps1`.
- Implement `mark-package-released.yml` as the release-stage pipeline entry point for the script.
- Accept an explicit language and package-info file paths, then resolve each package's published `Name`, `Version`, and optional `ApiHash` from its file.
- Invoke `azsdk package mark-released` independently for every package-info file and aggregate failures only after every valid package has been attempted.
- Treat the CLI's ARH and APIView results and exit code as authoritative.
- Make the command's partial-failure details visible so remediation is clear.

### Phase 4: Pipeline Migration

- Replace each language pipeline's existing approval check with `get-package-approval-status.yml` at the same location and under the same conditions. Do not add the template to the release stage.
- In each ARH-enabled SDK repository, add build-stage steps that run `create-apireview-hub-artifacts-{lang}`, copy each resulting hash into the matching package-info file, and publish the enriched `packages_extended` artifact for the release gate.
- Do not require ARH artifact-generation steps in repositories that have not been onboarded to ARH.
- Wire mark-released into the release stage only, after publishing.
- Migrate callers incrementally from `create-apireview.yml` to the three focused step templates.
- Keep the legacy script and template available until all callers have migrated.
- Do not modify `validate-all-packages.yml`, `Validate-All-Packages.ps1`, or Package work-item update pipelines as part of this migration.
- Coordinate with Praveen before proposing any follow-up changes to those validation or work-item flows.

## Script Design Requirements

- Use explicit parameters and avoid behavior switches such as `MarkPackageAsShipped` that change a script's responsibility.
- Give each script a dedicated step template that exposes its inputs explicitly and propagates its exit status.
- Use explicit package-info file paths for approval and mark-released operations; do not reuse revision-creation package discovery or selection precedence.
- Preserve source-only and pre-generated-token creation paths.
- Validate required artifact, build, repository, and package inputs before calling APIView.
- Keep APIView authentication helpers shared only for revision creation; release completion authentication remains owned by the CLI command.
- Use structured JSON/YAML parsing for package metadata and CLI responses.
- Populate each package-info `ApiHash` from its matching release candidate artifact and never recompute it from different content.
- Treat a missing package-info `ApiHash` as an omitted optional CLI input, not a script or CI failure, during the transition.
- Permit approval-status and mark-released bypasses only through their respective `Skip.CheckPackageApproval` and `Skip.MarkPackageReleased` variables; do not provide a shared switch that disables more than one operation.
- Include package name, version, and hash in diagnostic output without exposing tokens.
- For packages marked for release, propagate nonzero exits for authentication, transport, malformed response, unapproved status, and CLI failure, including when no release backend succeeds.

## Compatibility and Risks

- **Artifact determinism:** When an API hash is available, approval and mark-released must use the exact hash associated with the package being published. The hash must travel with that package's package-info file so the gate does not correlate separate artifact sets at runtime.
- **Onboarding dependency:** ARH-enabled repositories must run `create-apireview-hub-artifacts-{lang}` during their build stage and persist each resulting hash in the matching package-info file.
- **Future enforcement:** API hash is optional only during the APIView-to-ARH transition. It becomes required once ARH onboarding is complete and APIView fallback is removed.
- **CLI availability:** Pipeline agents that run approval or release completion must provide AZSdkCli 0.6.37 or later for the required `azsdk package get-approval-status` and `azsdk package mark-released` commands and the documented missing-hash and partial-success behavior.
- **Behavior preservation:** The creation-only script must retain source-only upload because not every language pipeline produces a review token file.
- **CLI command removal:** Any out-of-repository consumers of the two APIView revision commands would break. Repository search found no callers, but release notes should identify the removal.
- **Dual-backend consistency:** The CLI owns approval reconciliation across APIView and ARH; scripts should not second-guess it.
- **Gate integrity:** Outside the template-specific authorized bypass, language-specific tooling or configuration must not suppress the approval check or override its result for a package marked for release. The script still evaluates every supplied service-directory package, but only packages whose `ReleaseStatus` is not `Unreleased` enforce the centralized CLI decision.
- **Break-glass isolation:** `Skip.CreateApiReview`, `Skip.CheckPackageApproval`, and `Skip.MarkPackageReleased` must each affect only their named operation and never serve as aliases for a shared master bypass.
- **Partial release updates:** With an API hash, `azsdk package mark-released` attempts ARH and APIView independently and preserves each result in `PackageMarkReleasedResponse`; without a hash, it skips ARH and attempts APIView. The command succeeds when either attempted backend succeeds and fails only when neither succeeds. The script must surface those results unchanged. Hash-bearing retries require both backend operations to be idempotent because the command invokes both on each retry.
- **Multi-package runs:** Approval and mark-released script invocations may process multiple explicit package-info files, but each package must be handled independently and retain its own `ApiHash`; one package's metadata or result must never be reused for another. Approval checks run for all supplied service-directory packages, while failures gate the pipeline only for packages marked for release.
- **Stage placement:** Revision creation runs only in the Build stage. Approval status remains at each language pipeline's existing approval-check location and is not added to release. Mark-released runs only in the release stage.
- **Transition lifetime:** The APIView revision script is intentionally temporary and should not accumulate ARH behavior.
- **Independent work-item flow:** Existing package validation and Azure DevOps work-item updates remain untouched. Their legacy APIView-derived fields have already produced stale information, and there is currently no plan to update Package work items with ARH approval or release state. Any changes to this flow must be coordinated with Praveen.

## Validation Plan

1. Compare `Create-APIViewRevision.ps1` output against current source-only upload and token-file creation behavior.
2. Verify artifact discovery, package selection, branch/version eligibility, metadata, and multi-package processing remain equivalent.
3. Verify the two Azure SDK CLI commands, their tests, service methods, and documentation are removed without affecting other APIView commands.
4. Validate `Get-PackageApprovalStatus.ps1` with ARH approval, APIView fallback approval, combined unapproved, malformed-response, and CLI-failure cases.
5. Confirm ARH-enabled repositories copy each matching `apiMdSha256` into that package's package-info `ApiHash` and preserve it through the existing approval-check location.
6. Confirm a missing package-info `ApiHash` does not fail the script before the CLI runs, and that the script propagates a successful CLI result produced through APIView fallback.
7. Verify each language pipeline invokes the shared template at its existing approval-check location and under its existing conditions, only an authorized `Skip.CheckPackageApproval` setting skips it, and the template is not invoked during release.
8. Validate that `Mark-PackageReleased.ps1` reads each explicit package-info file, passes its name, version, language, and optional hash to `azsdk package mark-released`, and propagates its response.
9. Simulate each one-backend failure response and verify the script surfaces both backend results and succeeds when the other backend succeeds; verify it returns failure only when neither backend succeeds.
10. Verify only an authorized `Skip.MarkPackageReleased` setting skips mark-released, and that no break-glass variable disables any other operation.
11. Run a multi-package pipeline and verify every explicit package-info file is evaluated once with its own name, version, and optional hash; failures for `Unreleased` packages are ignored, while any failure for a package marked for release fails the invocation.
12. Confirm mark-released runs only in the release stage.
13. Confirm `Create-APIReview.ps1` remains unchanged throughout migration.

## Open Questions

1. Is the APIView mark-as-shipped operation idempotent, and can its existing request logic be moved without changing behavior?
2. Which existing helper functions can be reused without importing the overloaded script itself?
