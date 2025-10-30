# Tools available in Azure SDK MCP server

This document provides a comprehensive list of all MCP (Model Context Protocol) tools supported by the Azure SDK MCP server version 0.5.5.

## Tools list

| Name | Description |
|----------------------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------|
| azsdk_analyze_log_file | Analyzes a log file for errors and issues |
| azsdk_analyze_pipeline | Analyze azure pipeline for failures. Set analyzeWithAgent to false unless requested otherwise by the user |
| azsdk_check_api_spec_ready_for_sdk | Checks whether a TypeSpec API spec is ready to generate SDK. Provide a pull request number and path to TypeSpec project json as params. |
| azsdk_check_package_release_readiness | Checks if SDK package is ready to release (release readiness). This includes checking pipeline status, apiview status, change log status, and namespace approval status. |
| azsdk_check_service_label | Checks if a service label exists and returns its details |
| azsdk_cleanup_ai_agents | Clean up AI agents in an AI foundry project. Leave projectEndpoint empty if not specified |
| azsdk_convert_swagger_to_typespec | Converts an existing Azure service swagger definition to a TypeSpec project. Returns path to the created project. |
| azsdk_create_pull_request | Create pull request for repository changes. Provide title, description and path to repository root. Creates a pull request for committed changes in the current branch. |
| azsdk_create_release_plan | Create Release Plan |
| azsdk_create_service_label | Creates a pull request to add a new service label |
| azsdk_engsys_codeowner_update | Adds or deletes codeowners for a given service label or path in a repo. When isAdding is false, the inputted users will be removed. |
| azsdk_engsys_validate_codeowners_entry_for_service | Validates codeowners in a specific repository for a given service or repo path. |
| azsdk_get_failed_test_case_data | Get details for a failed test from a TRX file |
| azsdk_get_failed_test_cases | Get titles of failed test cases from a TRX file |
| azsdk_get_failed_test_run_data | Get failed test run data from a TRX file |
| azsdk_get_github_user_details | Connect to GitHub using personal access token. |
| azsdk_get_modified_typespec_projects | This tool returns list of TypeSpec projects modified in current branch |
| azsdk_get_pipeline_llm_artifacts | Downloads artifacts intended for LLM analysis from a pipeline run |
| azsdk_get_pipeline_status | Get pipeline status for a given pipeline build ID |
| azsdk_get_pull_request | This tool gets pull request details, status, comments, checks, next action details, links to APIView reviews. |
| azsdk_get_pull_request_link_for_current_branch | Get pull request link for current branch in the repo. Provide absolute path to repository root as param. This tool call GetPullRequest to get pull request details. |
| azsdk_get_release_plan | Get Release Plan: Get release plan work item details for a given work item id or release plan Id. |
| azsdk_get_release_plan_for_spec_pr | Get release plan for API spec pull request. This tool should be used only if work item Id is unknown. |
| azsdk_get_sdk_pull_request_link | Get SDK pull request link from SDK generation pipeline run or from work item. Build ID of pipeline run is required to query pull request link from SDK generation pipeline. This tool can get SDK pull request details if present in a work item. |
| azsdk_init_typespec_project | Use this tool to initialize a new TypeSpec project. Returns the path to the created project. |
| azsdk_link_namespace_approval_issue | Link package namespace approval issue to release plan(required only for management plan). This requires GitHub issue URL for the namespace approval request and release plan work item id. |
| azsdk_link_sdk_pull_request_to_release_plan | Link SDK pull request to release plan work item |
| azsdk_package_build_code | Build/compile SDK code for a specified project locally. |
| azsdk_package_generate_code | Generates SDK code for a specified language using either 'tspconfig.yaml' or 'tsp-location.yaml'. Runs locally. |
| azsdk_package_run_check | Run validation checks for SDK packages. Provide package path, check type (All, Changelog, Dependency, Readme, Cspell, Snippets), and whether to fix errors. |
| azsdk_package_run_tests | Run tests for the specified SDK package. Provide package path. |
| azsdk_release_sdk | Releases the specified SDK package for a language. This includes checking if the package is ready for release and triggering the release pipeline. This tool calls CheckPackageReleaseReadiness |
| azsdk_run_generate_sdk | Generate SDK from a TypeSpec project using pipeline. |
| azsdk_run_typespec_validation | Run TypeSpec validation. Provide absolute path to TypeSpec project root as param. This tool runs TypeSpec validation and TypeSpec configuration validation. |
| azsdk_tsp_update | Update customized TypeSpec-generated client code |
| azsdk_typespec_check_project_in_public_repo | Check if TypeSpec project is in public spec repo. Provide absolute path to TypeSpec project root as param. |
| azsdk_update_language_exclusion_justification | Update language exclusion justification in release plan work item. This tool is called to update justification for excluded languages in the release plan. Optionally pass a language name to explicitly request exclusion for a specific language. |
| azsdk_update_sdk_details_in_release_plan | Update the SDK details in the release plan work item. This tool is called to update SDK language and package name in the release plan work item. sdkDetails parameter is a JSON of list of SDKInfo and each SDKInfo contains Language and PackageName as properties. |