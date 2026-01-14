# Release History

## 0.5.12 (Unreleased)

### Features Added

- Improved error message when GitHub authentication fails to include GitHub CLI installation and authentication instructions
- Added TypeSpecProject to the telemetry data for the `azsdk_package_generate_code` tool
- MCP server now forwards log and subprocess output to MCP logging notifications instead of stdout

### Breaking Changes

### Bugs Fixed

- Fixed case insensitivity with ward ScanPaths in package validation readme tool

### Other Changes

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
