# Release-plan spec commit pinning

SDK generation must not change API versions merely because more spec changes have
merged to the repository. The release plan's `SpecCommitSHA` identifies the confirmed
spec snapshot; `SpecAPIVersion` identifies the API version within that snapshot.
This implements the commit-pinning part of the
[parallel release-plan proposal](https://github.com/Azure/azure-sdk-tools/issues/16848#issuecomment-5487015153).

## Configure and confirm a target

Public SDK target configuration requires a local `typeSpecProjectPath` in a clean
specs checkout with the project's TypeSpec compiler installed. This also applies
to `update-spec-pr`. Local `HEAD` must match the selected PR HEAD SHA (unmerged) or
merge commit SHA (merged), both before and after compiler validation. The API
version must be declared at that exact SHA. Unambiguous snapshot metadata supplies
the version; `apiVersion` / `--api-version` is an optional selection from
compiler-declared `AvailableApiVersions`, not a freeform override. An undeclared
version is rejected before any write. The tools do not check out commits or stash
user work. Target validation emits metadata to a temporary directory outside the
checkout and inspects compiler-declared API versions; it does not generate SDK code.

1. Read the current plan before updating or reusing it. Preview with create,
   update, or update-spec-pr, leaving `confirmTarget` false
   (the default). `requires_confirmation: true` means no work items were written.
   Show `proposed_spec_target`: project, packages, API version, SDK release type,
   spec PR, `SpecCommitSHA`, commit URL, `IsSpecMerged`, and available API versions.
  For updates, retain the returned target's **`ExpectedTargetRevision`** verbatim.
2. Ask the user to approve the exact target, including the metadata-derived API
  version. If metadata is missing or ambiguous, the selected version remains
  empty until the user chooses from available versions; never choose the
  first/latest or invent a version.
3. Repeat the same operation with the exact explicit `specCommitSha` and
  `confirmTarget: true`, retaining the other approved inputs. `apiVersion` can be
  omitted when metadata unambiguously resolves the approved version; otherwise
  preserve the user's declared-version selection. Both public update and
  update-spec-pr confirmation **require `expectedTargetRevision` /
  `--expected-target-revision` copied verbatim from the preview's
  `ExpectedTargetRevision`**. Optionally pass `expectedSpecCommitSha` /
  `--expected-spec-commit-sha` from `ExpectedPreviousSpecCommitSHA`: the
  **previously observed stored pin**, or `none` for an unpinned plan, **not the
  proposed new SHA**. Preserve supplied guards across preview and confirmation.
  If the PR, target, or either work-item revision changes, preview again and
  obtain fresh approval; never fetch a replacement token at confirmation and
  silently reuse the old approval.
4. Read back the saved plan and verify `SpecAPIVersion` and `SpecCommitSHA` against
   the approval before generating.

`confirmTarget` / `--confirm-target` is a caller-supplied boolean, not an external
or cryptographic approval record. Authorized callers can confirm explicit valid
inputs using an explicitly inspected plan's `TargetRevision` without a prior
preview call. Interactive agents should still follow the preview/approval flow.
Treat the revision token as opaque: it represents positive parent ID, parent
revision, API Spec ID, and API Spec revision, formatted invariantly as
`parentID:parentRevision:apiSpecID:apiSpecRevision`. Do not construct or normalize
it. Any parent or child revision change invalidates the precondition, including
same-SHA SDK release type, metadata, or child changes.

`expectedTargetRevision` is optional for preview but required for a confirmed
public update. Omitting it returns a no-write preview even with a valid SHA and
`confirmTarget: true`; a supplied blank or mismatched token rejects before
compiler validation or writes. `expectedSpecCommitSha` remains an optional extra
guard. Public create accepts neither update guard because there are no previous
records. Private Preview and tracking-only creation do not require a token.

Creation derives SDK release type from `apiReleaseType`: Public Preview becomes
`beta`, GA becomes `stable`. Do not pass `sdkReleaseType` to create. A plan can be
created before a PR exists by omitting version/SHA inputs; this is tracking-only,
has no pin, and cannot generate SDKs. Private Preview remains spec-only.

Reuse is scoped to project, API version, and API release type. Create/get never
retarget an existing plan. Same-version follow-up PRs require an explicit update
and fresh confirmation; a different API version needs a separate plan. SDK info
`apiVersion` is the spec API version, not a semantic SDK package version.

## Generation: draft review versus auto-release

Generation and regeneration consume the stored target without rerunning compiler
validation. A previously confirmed plan needs no local clone: pass its stored
repository-relative project path. Pass the stored API version and SHA explicitly
to detect a changed target; caller inputs cannot override the plan. Generation's
`specCommitSha` / `--spec-commit-sha` is an **expected stored pin**, not a new pin.
Do not confuse it with the update-only `--expected-spec-commit-sha` and
`--expected-target-revision` preconditions.

- The default `requireMergedSpec: false` queues `TriggerSource: sdk-review` for
  manual review. New SDK PRs are drafts and receive no `auto-release` label, even
  when the stored spec target is already merged.
- Supported manual **data-plane pre-merge draft** generation uses the confirmed
  PR HEAD SHA, never GitHub's synthetic merge commit or a moving PR merge ref.
- Auto-release is opt-in: `requireMergedSpec: true` (`--require-merged-spec` in the
  CLI) queues `TriggerSource: sdk-release`. The linked public PR must be merged
  and its merge commit must match the stored SHA. After merge, explicitly update
  and confirm the merged target; a draft pin does not automatically advance or
  opt in to auto-release. Generation creates SDK PRs, not published packages.
- Legacy, tracking-only, or otherwise unconfigured plans stop before generation.
  The next step is to preview and confirm `update-spec-pr` from the intended local
  snapshot. Generation never backfills a target or falls back to `main` or local
  HEAD, even if the linked PR has merged. Invalid links, SHAs, or missing API
  versions also stop generation.
- Existing SDK PR branch reuse and released-SDK, pending/in-progress, and
  conflicting-plan guards remain in place. Private Preview cannot generate SDKs
  through this pipeline.

## Storage and partial failures

Use the **existing parent Release Plan field `Custom.SpecCommitSHA`** for the full
40-character SHA; no new Azure DevOps field or child-field migration is required.

Both public update paths use the confirmed-target writer. It checks the carried
parent/child revision token against the current records **before clearing the
pin**, then **clears the parent pin → updates the API Spec child's PR link/API
version → publishes the parent pin** with `/rev` tests on every patch. The link-only
path passes no additional parent metadata or SDK package changes. An incomplete
update does not restore the old pin; generation is blocked while the plan is
unpinned. Do not restore the old pin or blindly retry a revision conflict;
retrieve the current plan, preview, and obtain fresh approval. These are not
atomic cross-work-item writes.

## Pipeline contract

The build request sets `SourceVersion` to the stored SHA and keeps `SourceBranch`
as `main`. No new pipeline template parameter is introduced. The existing
[SDK generation template](https://github.com/Azure/azure-rest-api-specs/blob/2094b1ceebc1009888dbcd926eeec827ec11f04b/eng/pipelines/templates/stages/archetype-spec-gen-sdk.yml)
sets `SpecRepoCommit` from `Build.SourceVersion`, checks out that commit, and
passes it to the SDK generator. **Both `ApiVersion` and `SdkReleaseType` are
forwarded for interactive and automation calls.** The saved template parameters
also include the TypeSpec project (`ConfigPath`), `ConfigType`, release plan work
item ID, and the selected review/release `TriggerSource`.

This pins spec inputs, not the entire SDK toolchain or all dependencies. The
pipeline and generator must remain compatible with the pinned spec snapshot.

## Scope and limitations

This change configures the release target and pins generation inputs. It does not
add worker-side validation, guarded job completion, or verification of the
generated SDK's API version and release type. Queueing, file pushes, PR creation,
and labeling are not atomic with target updates.

Existing callers must explicitly confirm a valid target before generating; lookup
and generation alone do not advance a pin. No schema provisioning or automatic
legacy backfill is included. This change adds no persisted `Queued` state or
automatic resumer and does not by itself complete the broader parallel-release
workflow.
