# Tools available in Azure SDK MCP server

This document provides a comprehensive list of all MCP (Model Context Protocol) tools and commands supported by the Azure SDK MCP server version 0.5.14.

<style>
table td:nth-child(2),
table th:nth-child(2) {
  white-space: nowrap;
}
</style>

## Tools list

| Name | Command | Description |
|------|---------|-------------|
| azsdk_analyze_log_file | `azsdk azp log analyze` | Analyzes a log file for errors and issues |
| azsdk_analyze_pipeline | `azsdk azp analyze` | Analyze azure pipeline for failures. Set analyzeWithAgent to false unless requested otherwise by the user |
| azsdk_apiview_get_comments | `azsdk apiview get-comments` | Get all the comments of an APIView API using the APIView URL |
| azsdk_abandon_release_plan | `azsdk release-plan abandon` | Abandon a release plan by work item ID or release plan ID. Updates the release plan status to 'Abandoned'. |
| azsdk_check_api_spec_ready_for_sdk | `azsdk release-plan check-api-readiness` | Checks whether a TypeSpec API spec is ready to generate SDK. Provide a pull request number and path to TypeSpec project json as params. |
| azsdk_check_service_label |  | Checks if a service label exists and returns its details |
| azsdk_convert_swagger_to_typespec | `azsdk tsp convert` | Converts an existing Azure service swagger definition to a TypeSpec project. Returns path to the created project. |
| azsdk_create_pull_request |  | Create pull request for repository changes. Provide title, description and path to repository root. Creates a pull request for committed changes in the current branch. |
| azsdk_create_release_plan | `azsdk release-plan create` | Create Release Plan |
| azsdk_create_service_label |  | Creates a pull request to add a new service label |
| azsdk_customized_code_update | `azsdk tsp client customized-update` | Update customized TypeSpec-generated client code |
| azsdk_engsys_codeowner_update |  | Adds or deletes codeowners for a given service label or path in a repo. When isAdding is false, the inputted users will be removed. |
| azsdk_engsys_validate_codeowners_entry_for_service |  | Validates codeowners in a specific repository for a given service or repo path. |
| azsdk_get_failed_test_case_data |  | Get detailed information (error messages, stack traces, output) for a specific failed test case by title from a TRX file. Use this to debug a particular test failure. |
| azsdk_get_failed_test_cases | `azsdk pkg test results` | Get list of all failed test case titles (names only) from a TRX file. Use this to quickly see which tests failed without details. |
| azsdk_get_failed_test_run_data |  | Get complete details for all failed test cases from a TRX file. Returns full data including error messages, stack traces, and output for every failed test. Use this for comprehensive analysis. |
| azsdk_get_github_user_details |  | Connect to GitHub using personal access token. |
| azsdk_get_modified_typespec_projects | `azsdk tsp project modified-projects` | This tool returns list of TypeSpec projects modified in current branch |
| azsdk_get_pipeline_llm_artifacts | `azsdk azp test-results` | Downloads artifacts intended for LLM analysis from a pipeline run |
| azsdk_get_pipeline_status | `azsdk azp status` | Get pipeline status for a given pipeline build ID |
| azsdk_get_pull_request |  | This tool gets pull request details, status, comments, checks, next action details, links to APIView reviews. |
| azsdk_get_pull_request_link_for_current_branch |  | Get pull request link for current branch in the repo. Provide absolute path to repository root as param. This tool call GetPullRequest to get pull request details. |
| azsdk_get_release_plan | `azsdk release-plan get` | Get Release Plan: Get release plan work item details for a given work item id or release plan Id. |
| azsdk_get_release_plan_for_spec_pr |  | Get release plan for API spec pull request. This tool should be used only if work item Id is unknown. |
| azsdk_get_sdk_pull_request_link | `azsdk spec-workflow get-sdk-pr` | Get SDK pull request link from SDK generation pipeline run or from work item. Build ID of pipeline run is required to query pull request link from SDK generation pipeline. This tool can get SDK pull request details if present in a work item. |
| azsdk_link_namespace_approval_issue | `azsdk release-plan link-namespace-approval` | Link package namespace approval issue to release plan(required only for management plan). This requires GitHub issue URL for the namespace approval request and release plan work item id. |
| azsdk_link_sdk_pull_request_to_release_plan | `azsdk release-plan link-sdk-pr` | Link SDK pull request to release plan work item |
| azsdk_package_build_code | `azsdk pkg build` | Build/compile SDK code for a specified project locally. |
| azsdk_package_generate_code | `azsdk pkg generate` | Generates SDK code for a specified language using either 'tspconfig.yaml' or 'tsp-location.yaml'. Runs locally. |
| azsdk_package_run_check | `azsdk pkg validate` | Run validation checks for SDK packages. Provide package path, check type (All, Changelog, Dependency, Readme, Cspell, Snippets), and whether to fix errors. |
| azsdk_package_run_tests | `azsdk pkg test run` | Run tests for the specified SDK package. Provide package path. |
| azsdk_package_update_changelog_content | `azsdk pkg update-changelog-content` | Updates the changelog content for a specified package. |
| azsdk_package_update_metadata | `azsdk pkg update-metadata` | Updates the package metadata content for a specified package. |
| azsdk_package_update_version | `azsdk pkg update-version` | Updates the version and release date for a specified package. |
| azsdk_release_sdk | `azsdk pkg release` | Releases the specified SDK package for a language. This includes checking if the package is ready for release and triggering the release pipeline. To ONLY check package release readiness pass checkReady as true. |
| azsdk_run_generate_sdk | `azsdk spec-workflow generate-sdk` | Generate SDK from a TypeSpec project using pipeline. |
| azsdk_run_typespec_validation | `azsdk tsp validate` | Run TypeSpec validation. Provide absolute path to TypeSpec project root as param. This tool runs TypeSpec validation and TypeSpec configuration validation. |
| azsdk_typespec_check_project_in_public_repo | `azsdk tsp check-public-repo` | Check if TypeSpec project is in public spec repo. Provide absolute path to TypeSpec project root as param. |
| azsdk_typespec_init_project | `azsdk tsp init` | Use this tool to initialize a new TypeSpec project. Returns the path to the created project. |
| azsdk_update_api_spec_pull_request_in_release_plan | `azsdk release-plan update-spec-pr` | Update TypeSpec pull request URL in a release plan using work item id or release plan id. |
| azsdk_update_language_exclusion_justification |  | Update language exclusion justification in release plan work item. This tool is called to update justification for excluded languages in the release plan. Optionally pass a language name to explicitly request exclusion for a specific language. |
| azsdk_update_sdk_details_in_release_plan |  | Update the SDK details in the release plan work item. This tool is called to update SDK language and package name in the release plan work item. sdkDetails parameter is a JSON of list of SDKInfo and each SDKInfo contains Language and PackageName as properties. |
| azsdk_verify_setup | `azsdk verify setup check/install` | Verifies the developer environment for MCP release tool requirements. Use 'check' for verification-only mode or 'install' to auto-install missing requirements. Accepts a list of supported languages to check requirements for, and the packagePath of the repo to check. |
|  | `azsdk mcp` | Starts the MCP server (stdio mode) |
|  | `azsdk start` | Starts the MCP server (stdio mode) |
|  | `azsdk config codeowners update` | Update codeowners in a repository |
|  | `azsdk config codeowners validate` | Validate codeowners for an existing service entry |
|  | `azsdk config github-label check` | Check if a service label exists in the common labels CSV |
|  | `azsdk config github-label create` | Creates a PR for a new label given a proposed label and brand documentation |
|  | `azsdk pkg readme generate` | Generate README content for a package |
|  | `azsdk pkg samples generate` | Generates sample files |
|  | `azsdk pkg samples translate` | Translates sample files from source language to target package language |
|  | `azsdk quokka` |  |
|  | `azsdk release-plan list-overdue` |  |
|  | `azsdk release-plan update-release-status` |  |
|  | `azsdk apiview get-content` | Get content by APIView URL |
|  | `azsdk list` |  |

