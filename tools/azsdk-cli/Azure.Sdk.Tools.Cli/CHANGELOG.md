# Release History

## 0.6.25 (2026-07-07)

### Features Added

- `azsdk_update_sdk_details_in_release_plan` now marks languages with missing emitter configuration in the TypeSpec project as `MissingEmitterConfig` instead of `Requested` in the release plan work item, so the release plan dashboard shows a distinct "Missing emitter configuration" label instead of the misleading "Exclusion Requested" label.

### Bugs Fixed

- The create release plan tool no longer accepts an `--sdk-type` parameter. The SDK release type is now always derived from the API release type (GA maps to a stable SDK release, preview maps to a beta SDK release), preventing a stable SDK release from a preview API version.

## 0.6.24 (2026-06-30)

### Features Added

- Update package details in release plan using package name in tspconfig.yaml when a new release plan is created.

### Bugs Fixed

- `azsdk_update_sdk_details_in_release_plan` no longer fails for data-plane release plans when the TypeSpec project emits an optional Go package. Go is now accepted as an optional data-plane language and its package name is written to the release plan (Go remains not required, so it is not flagged as an excluded language when absent). Languages the release plan does not track (e.g. Rust, C++) are now skipped instead of causing the update to fail, and are reported in the result message.

- Update SDK details to use explicit output directory param when running TypeSpec emitter.

## 0.6.23 (2026-06-24)

### Features Added

- Release plans now resolve and persist Product Name, Product Type, and Product Lifecycle (copied from a previous release plan on create, or from a matching Triage work item on update via a new `--product-type` / `productType` parameter).

## 0.6.22 (2026-06-22)

### Features Added

- Added an `editScope` parameter (`--edit-scope`) to the `customized-update` command / `azsdk_customized_code_update` MCP tool. It is a flags enum (`CustomCode`, `SpecInputs`, `All`; default `All`). `CustomCode` restricts the tool to custom (non-generated) code: it never edits spec inputs (client.tsp/tspconfig.yaml) or moves the pinned spec commit, and feedback requiring a spec change is reported as out of scope via the `SpecChangeRequired` error code instead of being applied. `SpecInputs` restricts the tool to spec-input edits: it never patches custom code, and feedback requiring a custom-code change is reported as out of scope via the new `CustomCodeChangeRequired` error code instead of being applied. The edit scope is also passed to the feedback classifier so that items addressable either way (e.g. renames doable in spec OR custom code) are biased toward the in-scope axis instead of being reported as out of scope. Regenerating `Generated/` from the unchanged pinned commit is always allowed.
- Made `tspProjectPath` (`--tsp-project-path`) optional on the `customized-update` command / `azsdk_customized_code_update` MCP tool. It is now required only when the edit scope includes spec inputs (`SpecInputs`/`All`). For custom-code-only repair (`editScope CustomCode`) it may be omitted: regeneration then runs `tsp-client update` without `--local-spec-repo`, so the `tsp-client` CLI regenerates from the repo and commit pinned in the package's `tsp-location.yaml` instead of a local checkout. This enables headless custom-code build repair in a language repo where the spec is not checked out.

### Breaking Changes

- Replaced the `package find-work-item` CLI command with `package get-work-item`, which returns the full Azure DevOps package work item, and added `package update-work-item` for patching package work item fields.

## 0.6.21 (2026-06-18)

### Features Added

- Added UX and functionality improvements to `azp` sub-commands for pipeline analysis
- Added --copilot mode to pipeline analysis to call the user's copilot CLI installation for processing
- Enable pipeline analysis commands to take a GitHub PR link in addition to pipeline link or ID

### Breaking Changes

- Removed upstream RAG-based/hosted model pipeline analysis mode via `azsdk azp analyze --agent`

### Other Changes

- Added service ID and product ID as optional when creating a release plan
- Replaced product life cycle property with release plan type when fetching attestation status

## 0.6.20 (2026-06-16)

### Features Added

- Set ADO work item ID as release plan ID for new release plans.

### Bugs Fixed

- Fixed MCP server infinite respawn loop when upgrade or install fails due to rate limiting or network errors.

### Other Changes

- Improved GEPA skill quality scores for all shared skills.
- Updated prepare-release-plan skill with canonical convergence and Release Plan ID documentation.

## 0.6.19 (2026-06-12)

### Features Added

- Release plan is automatically marked as "Finished" when all required language SDKs are either Released or have an Approved exclusion. Management plane checks all 5 languages; data plane checks .NET, Java, Python, and JavaScript only.

### Bugs Fixed

- Release plan tools now accept either the user-facing Release Plan ID or the Azure DevOps work item ID. Tools resolve the supplied number by trying it as a Release Plan ID first, then falling back to a work item ID lookup.
- `GetReleasePlanForWorkItemAsync` now verifies the work item's `System.WorkItemType` is `Release Plan` before mapping, preventing a non-release-plan work item from being mapped to an empty release plan.

## 0.6.18 (2026-06-08)

### Other Changes

- Updated the release plan response to include the link to release plan dashboard.
- Added release plan type check in SDK generation and inform the agent that SDK generation is not required for private preview. 

## 0.6.17 (2026-06-02)

### Bugs Fixed

- Fixed issues in the MCP tool to lookup service details using TypeSpec project path.

## 0.6.16 (2026-06-01)

### Bugs Fixed

- Fixed DevOps work item creation for empty DateTime fields (#15795)

## 0.6.15 (2026-05-29)

### Bugs Fixed

- Fixed the Update SDK Details MCP tool to read package names from the TypeSpec metadata emitter output (typespec-metadata.yaml).

## 0.6.14 (2026-05-27)

### Features Added

- Added pre-build step for the .NET plugin during SDK generation
- Added `apiReleaseType` required parameter to `CreateReleasePlan` (options: Private Preview, Public Preview, GA) to set `Custom.ReleasePlanType` in ADO work items.
- Spec PR validation against release type: Private Preview requires `azure-rest-api-specs-pr`; Public Preview/GA requires `azure-rest-api-specs`.
- SDK release type now defaults automatically (beta for preview, stable for GA) when not provided.
- Duplicate release plan check now considers API release type, allowing separate plans for different release stages.
- Release plan title format updated to include release type (e.g., "Private Preview release plan for Contoso.Management").

### Breaking Changes

- Removed `userEmail` parameter from `CreateReleasePlan`; email is resolved automatically.
- `sdkReleaseType` parameter in `CreateReleasePlan` is now optional (was required).

## 0.6.13 (2026-05-18)

### Features Added

- Added optional `--release-plan-id` parameter to `update-release-status` CLI command. When provided, it is used as an additional filter on top of the package name search to select the correct release plan. Returns a message if the specified release plan ID is not found among matching plans.
- Get release plan returns the link to new release planner dashboard https://aka.ms/azsdk/releaseplan-dashboard

### Bugs Fixed

- `azsdk_release_sdk` now passes a `release_<safeName>=true` template parameter when triggering Java release pipelines so per-package selection works (azure-sdk-for-java#48465). Previously, manually queued Java releases failed fast because no package was selected. (#14832)
- Removed the check requiring Java package names to include group name in `groupName:packageName` format when updating release status.

### Other Changes

- Set `TriggerSource` when running SDK generation so PRs open as ready for review.

## 0.6.12 (2026-05-04)

### Other Changes

- Resolve npm exec binaries directly from node_modules for NpmOptions when `.npmrc` is in user context

## 0.6.11 (2026-05-01)

### Features Added

- Added `AZSDK_COPILOT_CLI_PATH` environment variable to provide a custom path to the Copilot CLI executable (`copilot`/`copilot.exe`) for the GitHub Copilot SDK when the bundled binary is unavailable in standalone builds.

### Bugs Fixed

- Fixed misleading "No feedback items to process" error when Copilot CLI is missing. Now surfaces the actual error with installation instructions and env var workaround.
- Introduced `CopilotCliUnavailableException` to distinguish Copilot CLI issues from other failures across all copilot-dependent tools.

### Other Changes

- Bumped `GitHub.Copilot.SDK` from 0.1.32 to 0.2.2.
- Audit reads data from the cache to reduce GitHub API use

## 0.6.10 (2026-04-27)

### Features Added

- Added Rust language support for `setup`, `generate`, `build`, and `pack` tools.
- Added `azsdk_get_kpi_attestation_status` MCP tool to check KPI attestation status for a release plan given product ID and lifecycle.
- Added CODEOWNERS Audit command (CLI only) that brings data model to a valid state.
- Added optional package version argument for `azsdk release-plan update-release-status` CLI.

### Other Changes

- Surface APIView link in `azsdk_release_sdk` when APIView approval is missing

### Bugs Fixed

- Release plan ID and work item ID in `azsdk_get_release_plan` were being confused by agent. Reordered arguments and updated description to enforce release plan ID as main argument to provide. 

## 0.6.9 (2026-04-16)

### Features Added

- Added MCP tool for updating the CODEOWNERS cache

### Other Changes

- Made spec PR optional parameter for both `azsdk_create_release_plan` and `azsdk_get_release_plan`

## 0.6.8 (2026-04-15)

### Features Added

- Added CODEOWNERS validation for paths, useful for release and PR checks

## 0.6.7 (2026-04-13)

### Features Added

- Added support to collect telemetry for sanitized user prompts
- Added `azsdk_apiview_get_review_url` MCP tool and `azsdk apiview get-review-url` CLI command to retrieve the APIView review URL for a package by name and language
- Added recorded and live test support for Python, JavaScript, Java and .NET
- Enhanced `azsdk_customized_code_update` with a two-phase AI-assisted customization workflow: Phase A applies TypeSpec decorators (`client.tsp`) to fix ~80% of issues, Phase B applies targeted code-level patches to customization files when the build still fails after regeneration
- `azsdk_customized_code_update` now accepts APIView review URLs as input in addition to plain-text customization requests and build error output, enabling direct resolution of API review feedback
- `azsdk_customized_code_update` customization flow is now enabled for .NET, Java, JavaScript, and Python
- Added support for updating `ci.yml` in SDK projects
- Implemented version update for Java, Python, and .NET language services

### Other Changes

- Updated `azsdk_typespec_delegate_apiview_feedback` to split the feedback summary table into addressed/not-addressed sections for easier review

## 0.6.6 (2026-04-01)

### Features Added

- Added support for CODEOWNERS "Section" in Label Owners (defaults to "Client Libraries")

### Bugs Fixed

- Fixed sample translation to preserve source directory structure when writing translated files

### Other Changes

- CODEOWNERS generator supports file paths and doesn't assume all paths are directories

## 0.6.5 (2026-03-27)

### Features Added

- Implemented version update for Go language service
- Added team support for CODEOWNERS add/remove tools

## 0.6.4 (2026-03-25)

### Features Added

- Added a new CLI command to ingest telemetry events from Copilot hooks

## 0.6.3 (2026-03-12)

### Bugs Fixed

- Fixed a bug that caused the update release status CLI command to fail when a release plan was not found for a package.

## 0.6.2 (2026-03-11)

### Features Added

- Added MCP progress reporting to long running tools including SDK generation, build, and pack tools

### Bugs Fixed

- Fixed a bug in get release plan CLI when release plan is fetched using api spec pull request.

## 0.6.1 (2026-03-05)

### Features Added

- Added `azsdk_package_pack` tool to create package artifacts
- Improved `azsdk_typespec_delegate_apiview_feedback` tool description to better recognize intent expressed as "address", "fix", or "resolve" APIView feedback
- Added a CLI command `azsdk release-plan update` and MCP tool `azsdk_update_release_plan` to update release plan.
- Updated CLI command `azsdk release-plan get` to get release plan using API spec pull request or spec project path.

### Bugs Fixed

- Filter out downvoted `azure-sdk` bot comments from APIView feedback to reduce noise in delegated issues

## 0.6.0 (2026-02-27)

### Features Added

- Added auto-install to `azsdk verify setup` MCP and CLI tool, enabling auto-installing of supported missing requirements
- Changed the CLI interface for verifying setup to `azsdk verify setup check` for non-install mode, and `azsdk verify setup install`
- Added `azsdk eng package-info` command for CI pipeline package manifest generation
- Switch Go language service to use C# native package info generation
- Customized code update tool now uses copilot sdk

## 0.5.19 (2026-02-25)

### Bugs Fixed

- Override path extension variable to support running commands on Windows without extension.

## 0.5.18 (2026-02-24)

### Bugs Fixed

- Fix process running issues on Windows when MCP server is used in copilot CLI

## 0.5.17 (2026-02-18)

### Features Added

- Added `azsdk upgrade` command and `#azsdk_upgrade` mcp tool to perform a self-upgrade to the latest (or specified) version
- The CLI and MCP server will proactively check for new updates and notify the user on a 1 and 3 day TTL, respectively
- Add TypeSpec project path in package release status telemetry when release plan exists
- Add WorkloadIdentityCredential in identity chain when running on GitHub action


## 0.5.16 (2026-02-09)

### Features Added

- Added support for version number and release date update in the CHANGELOG.md for data plane package.
- Add an MCP tool to abandon release plans.
- Make agentic search configurable and disable agentic search for TypeSpec authoring.
- Add release-plan find-product command to retrieve product details from a TypeSpec project path.
- Add a CLI command to address APIView feedback via coding agent (creates issues from APIView feedback).

## 0.5.15 (2026-01-30)

### Features Added

- Added new MCP tools azsdk_package_generate_samples and azsdk_package_translate_samples for end-to-end sample workflows.

### Bugs Fixed

- Disabled response file handling for command line to avoid considering JavaScript package name with '@' as response file name.

## 0.5.14 (2026-01-27)

### Features Added

- Added a new CLI command to update the package release status in release plan.

## 0.5.13 (2026-01-23)

### Features Added

- Improved error message when GitHub authentication fails to include GitHub CLI installation and authentication instructions
- Added TypeSpecProject to the telemetry data for the `azsdk_package_generate_code` tool
- Added email notification support for overdue release plan owners.
- Added support for GitHub URLs in TypeSpecHelper methods to accept URLs like `https://github.com/Azure/azure-rest-api-specs/blob/main/specification/...` in addition to local paths
- MCP server now forwards log and subprocess output to MCP logging notifications instead of stdout
- Added `APISpecProjectPath` property to Release Plan Work Item to track the TypeSpec project path in release plans
- Added CLI mode telemetry, app insights endpoint determined by debug vs. release builds

### Breaking Changes

- Removed ability to set custom telemetry endpoint via environment variable

### Bugs Fixed

- Fixed case insensitivity with ward ScanPaths in package validation readme tool

## 0.5.11 (2026-01-05)

### Features Added

- Updated `azsdk_verify_setup` to check that `core.longpaths` in git config is set to true on Windows
- Removed `azsdk pkg release-readiness` and replace it with `azsdk pkg release --check-ready`
- Add MCP Tool `azsdk_update_api_spec_pull_request_in_release_plan` & CLI command to update the TypeSpec pull request link in release plan

### Bugs Fixed

- Fixed test failures being reported as a success to the agent.
- Test result output is now made available to the agent.
- Fixed an APIView approval failure bug in `azsdk_release_sdk` tool for management plane packages.
- Fixed issue in `azsdk_link_sdk_pull_request_to_release_plan` and `azsdk_get_sdk_pull_request_link` tools where language was set as unknown in response.
- Fixed issue in `azsdk_get_release_plan_for_spec_pr` where tool call status was set as successful in telemetry but actually failed.

## 0.5.10 (2025-12-08)

### Features Added

- Add CLI command to identify in progress release plans with past due date
- Allow non-exact matches for package name in `azsdk_release_sdk` tool
- Moved `azsdk_check_api_spec_ready_for_sdk` and `azsdk_link_sdk_pull_request_to_release_plan` under release plan command hierarchy.
- Added APIView tools to expose APIView functionality to MCP agents (`get-comments`) and CLI (`get-content`)

### Bugs Fixed

- .NET validation GeneratedCode check had scriptPath passed in twice
- Fixed invalid language error in `azsdk_link_sdk_pull_request_to_release_plan` tool
- Fixed issues in package and release plan responses so package_type, language and TypeSpec project path are set in the telemetry.

## 0.5.9 (2025-11-24)

### Features Added

- Added a new command to list all MCP tools and its CLI command.

## 0.5.8 (2025-11-13)

### Features Added

- Validate package names before adding to release plan
- Allow forced creation when a release plan already exists
- Add support for sample translation from one language to another
- Updated CLI commands hierarchy to move all package related commands under `package` command group, all TypeSpec based commands under `tsp` command and similarly all release plan related commands under `release-plan`.
- Added ChangelogContentUpdateTool, MetadataUpdateTool, and VersionUpdateTool

### Bugs Fixed

- Sample generator: Fix integer overflow when calculating file sizes
- Sample generator: Fix stopping loading when an empty file is encountered
- Sample generator: Fix not giving enough priority to files in the input package folder

### Other Changes

- Added a PythonOptions that allows the user to use an env var to declare a python venv

## 0.5.7 (2025-11-05)

### Features Added

- Updated responses for release plan, TypeSpec, generate and build SDK tools to include language, TypeSpec path and package name.
- Added verify setup tool

### Bugs Fixed

- Validation workflow fixes
- Fix issue in telemetry service when setting string properties

## 0.5.6 (2025-11-03)

### Features Added

- Sample generator: Add support for user-defined additional context
- Sample generator: Add support for input prompts with local links
- Updated responses to include language and package name in telemetry.

## 0.5.5 (2025-10-28)

### Features Added

- Add support for generating samples for Azure client libraries across all languages
- Add tool status in response
- Disable telemetry in debug mode.

### Bugs Fixed

- Fixed issue when linking .NET PR to release plan

## 0.5.4 (2025-10-21)

### Features

- None

### Bugs Fixed

- Fix in create release plan tool to use Active spec PR URL field in the query to resolve the failure in DevOps side.

## 0.5.3 (2025-10-17)

### Features

- Updated System.CommandLine dependency to 2.5 beta
### Bugs Fixed

- Added a language specific way to get package name for validation checks, to account for different language naming (JS uses package.json name)

## 0.5.2 (2025-10-13)

### Features

- Added new tool to update language exclusion justification and also to mark as language as excluded from release.

### Breaking Changes

- None

### Bugs Fixed

- None

## 0.5.1 (2025-10-07)

### Features

- None

### Breaking Changes

- None

### Bugs Fixed

- Create release plan tool failure
- Use existing release plan instead of failing when a release plan exists for spec PR.

## 0.5.0 (2025-09-25)

### Features

- Swap `azsdkcli` to manually versioned package, dropping auto-dev-versioning.
- Adding changelog + changelog enforcement

### Breaking Changes

- None

### Bugs Fixed

- None

## 0.4.x and Earlier

See previous releases in git history.
