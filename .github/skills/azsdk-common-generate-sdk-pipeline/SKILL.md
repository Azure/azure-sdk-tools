---
name: azsdk-common-generate-sdk-pipeline
license: MIT
metadata:
  version: "1.1.0"
  distribution: shared
description: 'Run SDK generation pipelines and create SDK pull requests. USE FOR: "run SDK generation for all languages", "generate SDK for release plan <id>", "generate SDK for release <id>", "pipeline SDK generation", "generate SDK without a local clone", "create SDK pull requests", "generate a .NET provisioning library", "add .NET provisioning support". NOT FOR: local SDK generation, publishing packages, API design review, or generator-PR regeneration. INVOKES: azsdk_get_release_plan, azsdk_run_generate_sdk, azsdk_get_sdk_pull_request_link; for .NET provisioning, azsdk_prepare_dotnet_provisioning_config, azsdk_create_pull_request, azsdk_run_generate_dotnet_provisioning.'
compatibility: "azure-sdk-mcp server. Standard generation requires a release plan; .NET provisioning does not. Missing provisioning configuration requires a local spec checkout and editing/git tools. Supports .NET, Java, JavaScript, Python, Go"
---

# Generate SDK via Pipeline

This skill runs SDK generation pipelines and produces SDK pull requests. For
management/data-plane SDKs, use the standard release-plan workflow below.
For **.NET provisioning libraries**, first follow the provisioning branch:
prepare missing configuration in a spec PR, then generate without waiting for merge.

## Triggers

USE FOR: run SDK generation for all languages, generate SDK for a release plan, generate SDK for a release ID, pipeline SDK generation, generate SDK without a local clone, create SDK pull requests, generate a .NET provisioning library
WHEN: "run SDK generation, run SDK generation for all languages for release plan <id>", "generate SDK for release plan <id>", "generate SDK for release <id>", "pipeline SDK generation", "generate SDK without a local clone", "generate a .NET provisioning library"
DO NOT USE FOR: generating a single SDK locally from a local clone (use `azsdk-common-generate-sdk-locally`); releasing or publishing an already-generated package (use `azsdk-common-sdk-release`); API design review

## .NET provisioning libraries

1. Identify the public `Azure/azure-rest-api-specs` TypeSpec project, release
   type (`beta` or `stable`), and any existing spec PR. Inspect `tspconfig.yaml`
   at the PR merge ref, or at `main` if there is no PR. The emitter is
   `@azure-typespec/http-client-csharp-provisioning`, under `options`, with an
   `Azure.Provisioning.*` namespace and `emitter-output-dir`.
2. If configuration is missing, find an existing open configuration PR before
   creating another. Reuse its branch and a local specs checkout, or obtain a
   checkout when needed. Call `azsdk_prepare_dotnet_provisioning_config` with
   the local project directory. It derives missing options from the .NET
   management emitter, preserving comments, other emitters, and existing
   provisioning settings/API version. If the namespace/layout is ambiguous,
   ask rather than guess. Do not change the default `emit` list or REST APIs.
3. Review and validate the diff with repository-supported TypeSpec checks.
   Commit/push the configuration to the existing PR, or open a draft spec PR
   with `azsdk_create_pull_request`. Already configured means no edits,
   no new commit, and no no-op PR.
4. **Do not wait for the spec PR to merge.** Call
   `azsdk_run_generate_dotnet_provisioning` with the repository-relative
   project directory (`specification/...`), release type, and PR number.
   If configuration exists on `main` and no PR is needed, omit the PR number.
   Report the spec PR when applicable and returned pipeline URL. Check the
   run before claiming generation completed or an SDK PR exists; do not
   queue a duplicate run just to check progress.

This branch uses pipeline **8473**, **SDK Generation - .NET Provisioning Libraries**.
Its definition selects `TypeSpecEmitter=@azure-typespec/http-client-csharp-provisioning`
and `SdkRepoUrl=https://github.com/Azure/azure-sdk-for-net` for the specs repo's
`eng/pipelines/spec-gen-sdk.yml`. `TypeSpecEmitter` is not a YAML queue parameter.
The tool validates the remote config, uses an open PR's `refs/pull/<number>/merge`
(merged PRs use their base branch), and returns queue errors without switching
to `main`. No release plan is required or modified.

Do not substitute ordinary .NET pipeline 7412, the generator-PR regeneration
pipeline, `azsdk_run_generate_sdk`, or `azsdk_release_sdk`. Do not trigger a real
run without an identified target and the user's generation request. This branch
does not require local SDK generation or raw tsp-client.

## Standard generation rules (management/data-plane)

- **Always call `azure-sdk-mcp:azsdk_run_generate_sdk`** to generate. Do **not** use `azure-sdk-mcp:azsdk_release_sdk` (that publishes an already-generated package, it does not generate) or `azure-sdk-mcp:azsdk_get_sdk_pull_request_link` / `azure-sdk-mcp:azsdk_get_pull_request` (those only retrieve links) to generate an SDK.
- `azure-sdk-mcp:azsdk_run_generate_sdk` generates **one language per call**. To generate for **all languages**, call it **once per language** the release plan targets.
- A **release plan work item ID (or release plan ID)** is required. `azsdk_run_generate_sdk` **also** requires the **TypeSpec project path** and the **SDK release type** (`beta` or `stable`) as explicit inputs, and it validates them — it does **not** read them from the release plan. First call `azure-sdk-mcp:azsdk_get_release_plan` to obtain the TypeSpec project path, SDK release type, and target languages, then pass those into each generation call along with the work item ID.
- Generation uses the release plan’s saved spec commit when present. Plans without a saved commit keep the existing branch/PR behavior.
- Requires the `azure-sdk-mcp` server; there is no CLI fallback for the pipeline generation workflow.
- Private Preview release plans cannot generate SDKs via the pipeline — only the API spec PR needs to merge. If needed for validation, direct the user to generate locally via `azsdk-common-generate-sdk-locally`.

## MCP Tools

| Tool                                            | Purpose                                                        |
| ----------------------------------------------- | -------------------------------------------------------------- |
| `azure-sdk-mcp:azsdk_get_release_plan`          | Fetch the release plan to determine target languages / details |
| `azure-sdk-mcp:azsdk_run_generate_sdk`          | Run the generation pipeline (once per language)                |
| `azure-sdk-mcp:azsdk_get_sdk_pull_request_link` | Retrieve the generated SDK pull request link after generation  |
| `azure-sdk-mcp:azsdk_prepare_dotnet_provisioning_config` | Add missing provisioning options to a local TypeSpec config |
| `azure-sdk-mcp:azsdk_create_pull_request` | Create a draft spec PR when provisioning config is missing |
| `azure-sdk-mcp:azsdk_run_generate_dotnet_provisioning` | Queue provisioning generation from a spec PR or configured main |

## Standard generation steps (management/data-plane)

1. **Collect release plan** — Get the release plan work item ID (or release plan ID) from the user, then call `azure-sdk-mcp:azsdk_get_release_plan` to fetch it.
2. **Determine languages** — If the user asked for "all languages", determine the languages the release plan targets (e.g. via `azure-sdk-mcp:azsdk_get_release_plan`). Otherwise use the single language requested.
3. **Generate per language** — For each target language, run `azure-sdk-mcp:azsdk_run_generate_sdk` passing the **TypeSpec project path**, the **SDK release type** (`beta` or `stable`), and the **language** taken from the release plan, plus the release plan **work item ID**. These inputs are required and validated — an invalid TypeSpec project path, or a release type other than `beta`/`stable`, fails the run.
4. **Report pull requests** — After each run completes, retrieve and show the generated SDK pull request link with `azure-sdk-mcp:azsdk_get_sdk_pull_request_link`.

## Examples

- "Run SDK generation for all languages for release 12345"
- "Generate the SDK for release plan 12345"
- "Generate the Python and .NET SDKs for release 12345 using the pipeline"
- "Generate a .NET provisioning library from this spec PR"

## Standard generation troubleshooting

- If the agent tries `azsdk_release_sdk`, get-artifact, or get-service-details tools instead of generating, redirect it to `azure-sdk-mcp:azsdk_run_generate_sdk` — that is the only tool that runs the generation pipeline.
- If the correct tool is not yet available in the session, activate/load the `azure-sdk-mcp` TypeSpec SDK toolset first, then call `azure-sdk-mcp:azsdk_run_generate_sdk`.
- If generation fails with a missing SDK details error, ensure the release plan has SDK details populated for the language before re-running.
- Requires the `azure-sdk-mcp` server. No CLI fallback — prompt the user to configure MCP if unavailable.
