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
version must be declared at that exact SHA. The tools do not check out commits or
stash user work. Target validation emits metadata to a temporary directory outside
the checkout and inspects compiler-declared API versions; it does not generate SDK
code.

1. Read the current plan before updating or reusing it. Preview with create,
   update, or update-spec-pr, leaving `confirmTarget` false
   (the default). `requires_confirmation: true` means no work items were written.
   Show `proposed_spec_target`: project, packages, API version, SDK release type,
   spec PR, `SpecCommitSHA`, commit URL, `IsSpecMerged`, and available API versions.
2. Ask the user to approve the exact target. If a version is missing or ambiguous,
   ask them to choose from the available versions; never choose the first/latest.
3. Repeat the same operation with the exact explicit `apiVersion`, `specCommitSha`,
   and `confirmTarget: true`, retaining the other approved inputs. For updates,
   also pass `expectedSpecCommitSha` / `--expected-spec-commit-sha` from the preview's
   **`proposedTarget.ExpectedPreviousSpecCommitSHA`** (the
   `ExpectedPreviousSpecCommitSHA` field on the returned `proposed_spec_target`).
   This is the **previously observed stored pin**, or `none` for an unpinned plan,
   **not the proposed new SHA**. Preserve it across preview and confirmation. If
   the PR, target, expected pin, or work-item revision changes, read and preview
   again and obtain fresh approval; do not silently replace the precondition.
4. Read back the saved plan and verify `SpecAPIVersion` and `SpecCommitSHA` against
   the approval before generating.

`confirmTarget` / `--confirm-target` is a caller-supplied boolean, not an external
or cryptographic approval record. Authorized callers can confirm explicit valid
inputs directly without a prior preview call. Interactive agents should still
follow the preview/approval flow. The expected-previous-pin parameter is optional
in the API, but callers should supply it on updates to preserve the preview's
precondition. Create does not accept this update-only parameter.

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
Do not confuse it with the update-only `--expected-spec-commit-sha` precondition.

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
This aligns with the parent-field storage in
[PR #17000](https://github.com/Azure/azure-sdk-tools/pull/17000); coordinate the
overlapping changes.

Target updates check the expected pin and work-item revisions, then **clear the
parent pin → update the API Spec child's PR link/API version → publish the parent
pin** using revision guards. Once the old pin is cleared, a partial failure leaves
the plan unpinned and generation blocked. Do not restore the old pin or blindly
retry a revision conflict; retrieve the current plan and review the target again.
These are not atomic cross-work-item writes.

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

### Saved-job validation and guarded completion

Two **CLI-only** commands support the companion specs template; they are not MCP
tools:

- `azsdk spec-workflow validate-sdk-run` takes `--workitem-id`, `--pipeline-run`,
  and `--language`. It compares the actual saved build's `SourceVersion` and
  `TemplateParameters` (project, API version, SDK release type, and plan work item
  ID) with the current target. It also checks the language's pipeline definition
  and the plan's recorded generation build URL. An older job is rejected even
  when its SHA matches a newer job's SHA.
- `azsdk spec-workflow complete-sdk-run` accepts the same identifiers plus
  `--sdk-pr` and `--status`. It repeats validation and conditionally records the
  result using `/rev`, parent-pin, and latest-generation-URL checks. A conflict
  is not automatically retried. Supported statuses include `draft`, `ready for
  review`, `No changes`, and `Failed to generate SDK.`; `ready for review` requires
  a saved `sdk-release` run.

The companion template validates before pushing SDK changes, then uses guarded
completion after PR creation and before auto-release labeling. Validation logs
the job's **actual saved snapshot**, not a claim about generated output. Universal
verification of the generated SDK's API version and release type is **deferred**:
generator output does not expose a uniform cross-language contract for it.

Queueing, file pushes, PR creation, and labeling are not atomic with target
updates. A target can change after the pre-push check and before a later publishing
action. Completion guards protect stored results; they do not undo a push or
provide exactly-once queueing or publishing.

## Companion automation and rollout

The companion specs automation change is implemented: it explicitly configures
the selected same-version plan's target before generation and requires the linked
merged commit. It passes the observed pin (or `none`) as
`--expected-spec-commit-sha`, then reads back and checks the saved target. Lookup
and generation alone do not advance a pin.

Publish the new CLI and upgrade the CLI used by automation **before** rolling out
the companion automation. **An old pinned spec SHA also selects old pipeline
YAML.** Protective rollout requires a compatible pipeline template and helper at
the selected commit, or new target inputs explicitly previewed and confirmed by
the caller. A new CLI alone does not protect all historical pinned pipelines;
never silently advance a pin to obtain newer safeguards.

No schema provisioning or automatic legacy backfill is part of rollout. This
change adds no persisted `Queued` state or automatic resumer; it does not by
itself complete the broader parallel-release workflow.
