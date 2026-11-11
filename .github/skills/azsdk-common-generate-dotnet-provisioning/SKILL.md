---
name: azsdk-common-generate-dotnet-provisioning
license: MIT
metadata:
  version: "1.0.0"
  distribution: shared
description: 'Prepare TypeSpec provisioning configuration, reuse or create a spec PR, and initiate .NET provisioning library generation. USE FOR: "generate a .NET provisioning library", "add provisioning support", "run provisioning SDK generation". NOT FOR: normal management SDK generation, publishing packages, or regenerating from a generator PR. INVOKES: azsdk_prepare_dotnet_provisioning_config, azsdk_create_pull_request, azsdk_run_generate_dotnet_provisioning.'
compatibility: "azure-sdk-mcp server; GitHub and internal Azure DevOps access; local spec checkout and editing/git tools when configuration is missing"
---

# Generate a .NET provisioning library

1. Identify the public `Azure/azure-rest-api-specs` TypeSpec project, desired
   release type (`beta` or `stable`), and any existing spec PR. Do not guess the
   service, namespace, or API version. A release plan is not required.
2. Inspect `tspconfig.yaml` at the supplied PR's merge ref, or at `main` when
   there is no PR. The emitter is
   `@azure-typespec/http-client-csharp-provisioning`, configured under `options`
   with an `Azure.Provisioning.*` namespace and `emitter-output-dir`.
   If already configured, skip edits and PR creation.
3. If missing, reuse a local specs checkout or obtain one. Look for an existing
   open configuration PR for this project before creating another. Check out
   that PR's branch when present; preserve unrelated changes.
   Call `azsdk_prepare_dotnet_provisioning_config` with the local project
   directory. It derives the namespace and output layout from the current .NET
   management emitter, preserving all other configuration and comments. If the
   layout cannot be inferred, ask for the intended namespace/output layout
   instead of inventing one. Preserve any intentional provisioning API version.
4. Review the diff, validate the TypeSpec configuration with repository-supported
   checks, and commit/push only the intended configuration change. Reuse the
   existing PR, or call `azsdk_create_pull_request` to open a draft spec PR.
   No file changes means no new commit or no-op PR. Do not replace existing
   emitters, add provisioning to the default `emit` list, or change REST APIs.
5. **As soon as the spec PR exists, do not wait for it to merge.** Call
   `azsdk_run_generate_dotnet_provisioning` with the repository-relative
   project directory (`specification/...`), release type, and PR number.
   If configuration already exists on `main` and no PR is needed, omit the PR
   number. The tool rechecks the selected remote configuration before queueing.
6. Report the spec PR when applicable and returned pipeline URL. Queue success
   is not generation success: inspect the run before claiming a generated SDK
   PR exists. Do not queue a duplicate run just to check progress.

## Pipeline contract

Pipeline **8473**, **SDK Generation - .NET Provisioning Libraries**, uses
`Azure/azure-rest-api-specs`'s `eng/pipelines/spec-gen-sdk.yml`, with definition
variables `TypeSpecEmitter=@azure-typespec/http-client-csharp-provisioning` and
`SdkRepoUrl=https://github.com/Azure/azure-sdk-for-net`.

The tool supplies `ConfigType=TypeSpec`, `ConfigPath`, `CreatePullRequest=true`,
and `SdkReleaseType`. Open PRs use `refs/pull/<number>/merge`; merged PRs use
their base branch. `TypeSpecEmitter` is a definition variable, **not** a YAML
queue parameter. No release-plan status is updated.

Do not substitute ordinary .NET pipeline 7412, the generator-PR regeneration
pipeline, `azsdk_run_generate_sdk`, or `azsdk_release_sdk`. Never run a real
pipeline without an identified target and the user's generation request.
Failures (including inaccessible PR merge refs) must be reported, not bypassed
by switching to `main`. No local SDK generation or raw tsp-client is needed.
