# Release-plan spec commit pinning

SDK generation must not change API versions merely because more spec changes have
merged to the repository. The release plan's `SpecCommitSha` identifies the spec
snapshot; `SpecAPIVersion` identifies the API version within that snapshot.
This implements the commit-pinning part of the
[parallel release-plan proposal](https://github.com/Azure/azure-sdk-tools/issues/16848#issuecomment-5487015153).

## Behavior

- Public release-plan creation records the linked PR's merge commit when the PR
  is merged. Creating a plan before merge remains supported, with an empty pin.
- Reusing a plan for the same TypeSpec path, API version, and release type updates
  its spec link and pin when a merged follow-up PR is supplied to creation.
  An older or same-time merged PR does not roll the plan backward, and a stale
  caller cannot overwrite a pin that changed concurrently.
  Supplying an unmerged PR to creation does not replace an existing plan's pin.
- Explicit spec PR updates save the link and SHA together on the API Spec child.
  Linking an unmerged PR clears the previous SHA. The stored API version is not
  changed by linking a PR; use a separate plan for a different version.
  Pin-only updates to the same linked PR preserve SDK generation statuses,
  including `Pending`, rather than requesting another run.
- Generation and regeneration use the stored SHA, including when the caller
  omits the PR number. An existing SDK PR's branch is still reused.
- An unpinned legacy plan is backfilled only from its linked, merged public spec
  PR. The pin must be saved before a pipeline is queued. A revision check prevents
  backfill from overwriting a concurrent spec update. This does not reset SDK
  generation statuses or relink the PR.
- Missing or invalid links, unmerged PRs, malformed SHAs, and failed pin writes
  stop generation. There is no fallback to `main`, a PR merge ref, or local HEAD.
  A stored valid pin does not require another spec PR lookup on every generation.
- A supplied PR number, API version, or project cannot override the corresponding
  stored plan input. For projects with no single stored API version, the pinned
  snapshot still fixes the default spec input.
- Private-preview plans still use their existing spec-only completion flow;
  pipeline SDK generation remains disabled for them.

The existing released-SDK, pending/in-progress generation, and conflicting-plan
guards remain in place. This change does not add a persisted `Queued` state,
automatic resumption, or transactional exactly-once pipeline queueing.

## Storage and deployment prerequisite

**Before publishing the CLI, add a String field with reference name
`Custom.SpecCommitSha` to the Release project's API Spec work item type.** It must
allow an empty value and a full 40-character Git SHA. Do not add it only to the
parent Release Plan type: the API Spec child owns the pin alongside
`Custom.ActiveSpecPullRequestUrl` and `Custom.APISpecversion`.

No bulk migration or production work-item edits are performed by this change.
Existing plans are backfilled on generation. If the linked PR is wrong or absent,
link the intended PR explicitly rather than inferring it from the current spec
repository state. Older CLI clients cannot preserve these guarantees and should
be upgraded, including the CLI installed by automation.

## Pipeline contract

The build request sets `SourceVersion` to the stored SHA and keeps `SourceBranch`
as `main`. No new pipeline template parameter is introduced. The existing
[SDK generation template](https://github.com/Azure/azure-rest-api-specs/blob/2094b1ceebc1009888dbcd926eeec827ec11f04b/eng/pipelines/templates/stages/archetype-spec-gen-sdk.yml)
sets `SpecRepoCommit` from `Build.SourceVersion`, checks out that commit, and
passes it to the SDK generator. `ApiVersion` is forwarded for both interactive
and automation calls; the existing automation handling of `SdkReleaseType` is
unchanged.

This pins spec inputs, not the entire SDK toolchain or all dependencies. The
pipeline and generator must remain compatible with the pinned spec snapshot.

## Specs automation follow-up

The specs repository's
[release-plan orchestration](https://github.com/Azure/azure-rest-api-specs/blob/2094b1ceebc1009888dbcd926eeec827ec11f04b/eng/tools/release-plan/src/release-plan.ts)
currently returns a plan found by PR or by path/version without linking a new
follow-up PR. To implement the complete same-version automation flow, it must
explicitly update the selected plan's spec PR before regenerating. A lookup or a
generation request alone deliberately does not advance an existing pin.

Roll out the schema and CLI first, then update and validate that orchestration.
Until then, use the explicit spec PR update command/tool for merged follow-ups.
Do not close the broader parallel-release workflow issue solely on this change.
