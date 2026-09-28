# Release-plan spec commit pinning

SDK generation must not change API versions merely because more spec changes have
merged to the repository. The release plan's `SpecCommitSHA` identifies the confirmed
spec snapshot; `SpecAPIVersion` identifies the API version within that snapshot.
This implements the commit-pinning part of the
[parallel release-plan proposal](https://github.com/Azure/azure-sdk-tools/issues/16848#issuecomment-5487015153).

## Configure and confirm a target

Configuring a public SDK target through create, update, or `update-spec-pr`
requires a local `typeSpecProjectPath` with the project's compiler installed.
The checkout must be clean, with `HEAD` at the selected PR HEAD SHA (unmerged) or
merge SHA (merged), before and after metadata compilation. The existing TypeSpec
emitter writes metadata outside the checkout. Available versions are the distinct
package `ApiVersion` values in that metadata, not an enumeration of all
source-declared versions. There is no second compiler pass or SDK generation,
and tools do not check out commits or stash work.

1. **Preview** — Read the existing plan before an update, then call create,
   update, or update-spec-pr with `confirmTarget: false` (the default).
   `requires_confirmation: true` means no work items were written. Show the
  proposed project, packages, API version, SDK type, PR, SHA and commit URL,
  and metadata-reported `AvailableApiVersions`. Retain the update
   preview's `ExpectedTargetRevision` verbatim.
2. **Select and approve** — One distinct metadata version is selected
  automatically. With multiple versions, ask the user to choose from the
  metadata-reported list. With none, validation fails; fix the metadata.
  Explicit `apiVersion` / `--api-version`
   must be in that list, not merely declared in source. Ask for approval of the
   exact target; never silently choose first/latest or invent a version.
3. **Confirm** — Repeat the approved inputs with the exact `specCommitSha` /
   `--spec-commit-sha` and `confirmTarget: true` / `--confirm-target`. Public
   updates also require `expectedTargetRevision` / `--expected-target-revision`
   copied verbatim from the preview. Preserve any explicit API version selection.
4. **Verify** — Read back `SpecAPIVersion` and `SpecCommitSHA` before generating.

The revision token covers the parent and API Spec records; treat it as opaque.
If the PR, target, or either record's revision changes, even at the same SHA,
preview again and obtain fresh approval; never silently replace the token.
Missing update tokens return a no-write preview; blank or mismatched tokens reject
before compilation or writes. Create takes no revision token. `confirmTarget` is
a caller assertion, not a persisted approval record.

Creation derives SDK release type from `apiReleaseType`: Public Preview becomes
`beta`, GA becomes `stable`. Do not pass `sdkReleaseType` to create. A plan can be
created before a PR exists by omitting version/SHA inputs; this is tracking-only,
has no pin, and cannot generate SDKs. Private Preview remains spec-only.

Reuse is scoped to project, API version, and API release type. Create/get never
retarget an existing plan. Same-version follow-up PRs require an explicit update
and fresh confirmation; a different API version needs a separate plan. SDK info
`apiVersion` is the spec API version, not a semantic SDK package version.

## Generation and regeneration

Generation and regeneration consume the stored target without compiler validation
or a local clone. Pass the stored repository-relative project path and SDK release
type. API version, path, SDK type, and spec PR inputs are consistency checks, not
overrides. The SHA comes only from stored `SpecCommitSHA`.

- Generation uses the confirmed PR HEAD or merge SHA, never a moving PR merge
  ref, and does not advance the pin when the PR merges. A different snapshot
  requires an explicit target update and confirmation.
- Legacy, tracking-only, or invalid targets stop before generation. Preview and
  confirm `update-spec-pr` from the intended local snapshot; generation never
  backfills, reselects, or falls back to `main` or local HEAD.
- `TriggerSource: sdk-release` and downstream SDK PR/publication policies are
  unchanged. Pinning adds no release-mode switch or merged-spec gate.
- Existing SDK PR branch reuse and released-SDK, pending/in-progress, and
  conflicting-plan guards remain. Private Preview cannot use this pipeline.

## Storage and partial failures

The full 40-character SHA uses the existing parent `Custom.SpecCommitSHA` field;
no new schema or child-field migration is required. Both public update paths check
the revision token before clearing the parent pin, updating the API Spec child's
PR/version, and publishing the pin, with `/rev` checks on every patch. The
link-only path adds no parent metadata or SDK package changes. These writes are
not atomic: partial failure leaves the plan unpinned and blocks generation.
Do not restore the old pin or blindly retry; read, preview, and obtain fresh approval.

## Pipeline contract

The build request sets `SourceVersion` to the stored SHA and keeps `SourceBranch`
as `main`. No new pipeline template parameter is introduced. The existing
[SDK generation template](https://github.com/Azure/azure-rest-api-specs/blob/2094b1ceebc1009888dbcd926eeec827ec11f04b/eng/pipelines/templates/stages/archetype-spec-gen-sdk.yml)
sets `SpecRepoCommit` from `Build.SourceVersion`, checks out that commit, and
passes it to the SDK generator. Both `ApiVersion` and `SdkReleaseType` are forwarded
for interactive and automation calls, alongside `ConfigPath`, `ConfigType`, the
release plan work item ID, and `TriggerSource: sdk-release`.

## Scope and limitations

Pinning covers spec inputs, not the toolchain or dependencies; the pipeline and
generator must remain compatible with that snapshot. It adds no worker-side
output validation, guarded job completion, persisted `Queued` state, or automatic
resumer. Queueing, pushes, PR creation, and labeling are not atomic with target
updates; this is not the complete parallel-release workflow.
