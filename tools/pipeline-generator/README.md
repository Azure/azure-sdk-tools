# Pipeline generator

Creates and reconciles Azure DevOps CI, unified release, and test definitions from SDK
descriptors. The existing manual (`eng/pipelines/pipeline-generation-single.yml`) and
nightly (`eng/pipelines/pipeline-generation.yml`) entry points retain their defaults.
Nightly reconciliation corrects definition drift; it does **not** replay missed release events.

## Development

Use the .NET SDK selected by the repository's `global.json`:

```powershell
dotnet build tools\pipeline-generator\PipelineGenerator.sln
dotnet test tools\pipeline-generator\PipelineGenerator.sln
Invoke-Pester -Path eng\scripts\tests\Initialize-PostMergeSdk.Tests.ps1 -CI
```

Both the manual/nightly preparation and the post-merge pipeline install the generator with the
shared `install-pipeline-generation.yml` template, so they always use the same pinned package.

## Trusted post-merge preparation

`eng/pipelines/pipeline-generation-post-merge.yml` is an opt-in **internal** entry point.
The merge handler submits this one allowlisted definition using the tools `main` branch.
Do not grant callers the ability to select preparation code from an SDK PR or feature branch.
The SDK repository and merged commit select **data**, not the preparation YAML or scripts.
No SDK script is executed during preparation.

The entry point is self-contained. Its `PreparePostMerge` deployment job targets the
`post-merge-pipeline-preparation` environment, so Azure DevOps acquires that environment's
Exclusive lock before any preparation step starts. The same job checks out the trusted tools
commit, sparse-checks out SDK data, installs the pinned generator and launches it with credentials, and
publishes the Build artifact even after later failures. It disables deployment-hook artifact
downloads. The shared `eng/common` preparation job and its manual/nightly interface remain unchanged.
Its trusted `eng/scripts/Initialize-PostMergeSdk.ps1` checkout step clones the SDK `main`
branch (`PostMergeConstants.SdkDefaultBranch`), confirms the requested SHA is in its history,
checks that each descriptor is a tracked regular file and each previous path is absent,
disables Git hooks, sparse-checks out the exact SHA, and verifies HEAD.
It follows the existing non-cone sparse-checkout pattern without loading SDK scripts.
This script only checks out source: release orchestration remains in C#.

The `prepare-post-merge` C# command consumes the **already prepared local SDK root**.
It does not invoke or require the Git CLI, clone/fetch/checkout source, discover Git metadata,
or independently establish commit ancestry. The trusted pipeline step must perform those
checks before calling it; an arbitrary local directory is not evidence of a trusted merge.
The command validates request and trusted-context inputs and fails if a requested descriptor is not
found when scanning. It uses the explicit SDK root for scanning, so no `.git` directory is required. It owns
definition and permission preparation, run recognition, submission, and results.
Its `--repository-name` option defaults to `azure-sdk-for-net`:

```powershell
pipeline-generator prepare-post-merge `
  --merge-commit-sha $env:PREPARATION_SHA `
  --descriptor-paths $env:PREPARATION_DESCRIPTORS `
  --sdk-directory $env:PREPARATION_SDK_DIRECTORY `
  --results-directory $env:PREPARATION_RESULTS_DIRECTORY
```

Run this command only inside the trusted preparation pipeline, immediately after its
successful checkout validation with the same repository/SHA/descriptors. Do not modify the
SDK content between the steps. Local unit tests use fake operations and never authenticate
to or mutate Azure DevOps.

Queue-time `templateParameters`:

```json
{
  "RepositoryName": "azure-sdk-for-net",
  "MergeCommitSha": "0123456789abcdef0123456789abcdef01234567",
  "DescriptorPaths": [
    { "path": "sdk/service/ci.yml" },
    { "path": "sdk/new/ci.preview.yml", "previousPath": "sdk/old/ci.preview.yml" }
  ]
}
```

No PR number is required. The merge handler owns PR eligibility and descriptor-change
classification; neither it nor the observer should queue SDK releases separately.

The initial rollout supports exactly `azure-sdk-for-java`, `azure-sdk-for-js`,
`azure-sdk-for-python`, `azure-sdk-for-net`, and `azure-sdk-for-go`, owned by `Azure`.
`RepositoryName` defaults to `azure-sdk-for-net`; C++ and Rust are not enabled.
The pipeline checkout step always uses the SDK `main` branch, set by the
`PostMergeConstants.SdkDefaultBranch` constant (mirrored in the checkout script), because the
SDK auto-release gates require it.
That step verifies the full lowercase SHA is in the branch's history. Each target must be an existing
regular file at that SHA, with a canonical `sdk/<directory>/ci.yml` or
`sdk/<directory>/ci.<variant>.yml` path. Variants follow the scanner's .NET `\w+` rule
(Unicode categories Ll, Lu, Lt, Lo, Lm, Mn, Nd, Pc), not an ASCII-only approximation.
Directory segments allow Unicode letters/numbers, `_`, `-`, and `.` (but never `.` or `..`
segments). Requests contain 1-100 distinct descriptors. Paths use `/`, never `\`.
Rename mappings are one-to-one; the old descriptor must no longer exist at the merged SHA.
Rename chains/swaps, ambiguous definitions, manual identity changes, symlinked descriptors, and
scanner-incompatible filenames fail closed and need explicit operator reconciliation.
Obsolete definitions are never deleted.

The post-merge deployment job invokes the command, whose internal resource
profile mirrors the existing manual/nightly configuration for the five enabled SDKs
(`PostMergeResourceProfileTests` fails if the two drift apart).
It reuses the `generate` implementation and conventions, deduplicates recursive scan
directories, and limits unified generation and release selection to the exact descriptor
targets. It also prepares applicable test definitions, without queueing
them. All generation and resource authorization must complete before **any** release submission.
Configured service connections must exist; missing endpoint and variable-group pipeline
permissions are granted. A permission an administrator explicitly revoked is never re-granted:
post-merge preparation stops before any release submission, while the existing manual/nightly
path leaves it unchanged. Failures are not converted into `continueOnError` successes.

`prepare-post-merge` calls the `generate` implementation in-process with the exact descriptor
targets, which enables strict identity checks; `generate`'s command line is unchanged. If any
requested descriptor is not found under the scanned path, generation fails before changing any
definition or permission. Without exact targets (manual/nightly), `generate` behaves exactly as
before; it never repoints an existing definition's YAML path. After all resource authorizations
succeed, each prepared definition (release and test) is added to `definitions.json` in the
`release-run-results` artifact for diagnosis. Each JSON array entry has:

```json
{
  "descriptorPath": "sdk/service/ci.yml",
  "definitionId": 123,
  "convention": "up",
  "category": "unified",
  "operation": "created"
}
```

`operation` is `created`, `updated`, or `unchanged`. Existing definitions are selected too:
creation may have succeeded in a previous failed preparation. A supported relocation retains
the definition ID and updates its name and YAML filename. Both old and new definitions existing,
or an unexpected name already owning the descriptor, is an error rather than a new competing pipeline.

Releases are queued through the Build API with `reason: individualCI`, `sourceBranch:
refs/heads/main`, and `sourceVersion` equal to `MergeCommitSha`. This is necessary for the
existing Java/.NET/Python auto-release stage inclusion; a `Manual` queue could enter a different
release path. No stage selection, release parameters, skip variables, or approval overrides
are supplied. Each SDK retains its normal package/PR eligibility checks and approval behavior;
preparation success is **submission**, not proof of package eligibility or release success.

### Results and recovery

The Build artifact `release-run-results` is published with `PublishBuildArtifacts@1` and
contains `release-runs.json`:

```json
{
  "schemaVersion": 1,
  "repository": "Azure/azure-sdk-for-net",
  "sourceVersion": "0123456789abcdef0123456789abcdef01234567",
  "releaseRuns": [
    {
      "descriptorPath": "sdk/service/ci.yml",
      "definitionId": 123,
      "runId": 456,
      "url": "https://dev.azure.com/azure-sdk/internal/_build/results?buildId=456"
    }
  ]
}
```

IDs and URLs come from actual API responses. The web link is resolved from the returned
build ID when necessary; an API self URL is not presented as a run link. Matching existing
`IndividualCI` runs for the exact definition/repository/branch/SHA are recognized without
another submission, including completed or failed runs. Preparation does not retry failed releases.
The artifact is updated after each successful submission/recognition, and publication uses
`always()` so later errors preserve partial results while the agent remains alive. Queue
responses (`queue-response-<definitionId>.json`) and `definitions.json` are also included for
diagnosis. An agent loss before upload
cannot guarantee artifact availability.

The preparation deployment job holds a sequential Azure DevOps environment lock. Build history
is the only source of truth for submissions: before each POST, preparation looks for an existing
`IndividualCI` run for the exact definition/repository/branch/SHA and records it instead of
queueing again. Each queued build also receives a stable `postMergePreparation` correlation
property. The POST is sent once per attempt and never retried automatically. If its response is
lost, the attempt fails; a replay finds the run in history if Azure DevOps created it, and queues
only if no run exists. A duplicate is possible only if a replay starts while a lost POST is still
being committed. The lock serializes cooperating preparation runs, **not** Azure DevOps'
independent CI trigger: a preflight lookup cannot guarantee exactly-once submission against
concurrent normal CI.

To recover a missed event or a failed prerequisite, an authorized operator can requeue the
trusted preparation definition with the **same three inputs**, while the source and build
records are retained. The whole prerequisite phase runs again before submission. This is a
bounded operational replay, not an automatic nightly release backfill. Retain preparation
artifacts and build history through the operational replay window; do not delete/recreate
definitions during it. If build history has expired, do not replay without a manual release
audit. Do not replay merely to retry a failed or canceled release; use the existing release
recovery process.

### Rollout prerequisites

No production deployment or release submission is performed by development/tests.

1. Merge these changes to tools `main`, let the generator CI publish a package containing
   `prepare-post-merge`, then bump the shared pin in `install-pipeline-generation.yml` in a
   follow-up change. That pin also moves nightly/manual preparation, so validate nightly with
   the new package. Until then the current pin rejects the unknown verb, so post-merge runs fail
   before any definition, permission, or release changes.
2. Register the internal preparation definition at the
   exact entry-point path. Restrict its queue/edit permissions and allowlist its ID in the app.
3. Provision `post-merge-pipeline-preparation` in `azure-sdk/internal`, enable its **Exclusive
   lock** check, and authorize only the trusted preparation pipeline. The command does not
   re-verify this setup on each run; keep the check enabled, because it is what prevents
   overlapping runs for the same merge from queueing duplicate releases. Keep
   `lockBehavior: sequential`. Do not add Approval
   or other manual checks: the lock is granted and released automatically, so runs never need
   someone to unblock them.
4. Give the `opensource-api-connection` identity the existing definition/resource permission
   management rights plus Build queue/read rights. Do not grant the app those
   resource-management rights.
5. Validate the compiled Azure DevOps YAML, service-connection authorization, queue reason,
   returned web link, and artifact download in an isolated non-production setup before enabling
   the merge handler. Local mocked tests cannot establish those deployment permissions.

SDKs without the normal
auto-release templates will retain their ordinary `IndividualCI` behavior; this change does not
add language-specific release support.
