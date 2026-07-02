# Tools available in Azure SDK MCP server

This document provides a comprehensive list of all MCP (Model Context Protocol) tools and commands supported by the Azure SDK MCP server version 0.6.24.

## Tools list

| Name | Command | Description |
|------|---------|-------------|
| azsdk_abandon_release_plan | `azsdk release-plan abandon` | Abandon a release plan by work item ID or release plan ID. Updates the release plan status to 'Abandoned'. |
| azsdk_analyze_log_file | `azsdk ci log analyze` | Analyzes a log file for errors and issues |
| azsdk_analyze_pipeline | `azsdk ci analyze` | Analyze what happened in an Azure pipeline build. Investigates pipeline runs, identifies failures, and explains build issues. Accepts an Azure Pipeline link, Build ID, GitHub Pull Request link, or Pull Request number. |
| azsdk_apiview_get_comments | `azsdk apiview get-comments` | Get API review comments and feedback from APIView for a package. Retrieves all reviewer comments left on the API review. |
| azsdk_apiview_get_copilot_review | `azsdk apiview get-copilot-review` | Get the status and results of a Copilot review job. When complete, the response includes all generated review comments. |
| azsdk_apiview_get_review_url | `azsdk apiview get-review-url` | Get the APIView review URL for a package by name and language. Returns the direct link to the API review page for the specified package. |
| azsdk_apiview_request_copilot_review | `azsdk apiview request-copilot-review` | Submit an API surface text for automated Copilot review. Provide the text directly via 'api-text' (raw or markdown-fenced), or supply an APIView URL to have the text fetched automatically. Returns a job ID — use get-copilot-review to poll for results and comments. |
| azsdk_check_api_spec_ready_for_sdk | `azsdk release-plan check-api-readiness` | Checks whether a TypeSpec API spec is ready to generate SDK. Provide a pull request number and path to TypeSpec project json as params. |
| azsdk_check_service_label |  | Checks if a service label exists and returns its details |
| azsdk_convert_swagger_to_typespec | `azsdk tsp convert` | Converts an existing Azure service swagger definition to a TypeSpec project. Returns path to the created project. |
| azsdk_create_pull_request |  | Create pull request for repository changes. Provide title, description and path to repository root. Creates a pull request for committed changes in the current branch. |
| azsdk_create_release_plan | `azsdk release-plan create` | Create Release Plan for a TypeSpec project and API release type. API release types support Private Preview, Public Preview, and GA. Service ID and product ID are optional and will be resolved from existing release plans when available. |
| azsdk_create_service_label |  | Creates a pull request to add a new service label |
| azsdk_customized_code_update | `azsdk tsp client customized-update` | Applies patches to customization files based on build errors, regenerates code if needed (Java), builds, and returns success/failure with build result. |
| azsdk_engsys_codeowner_add_label_owner |  | Add owner(s) to a label with an optional path in CODEOWNERS work items. Valid ownerType values: service-owner, azsdk-owner, pr-label. |
| azsdk_engsys_codeowner_add_package_label |  | Add PR label(s) to a package in CODEOWNERS work items. |
| azsdk_engsys_codeowner_add_package_owner |  | Add source owner(s) to a package in CODEOWNERS work items. |
| azsdk_engsys_codeowner_check_package |  | Check that a package has sufficient owners, PR labels, and service owners from a CODEOWNERS cache file. |
| azsdk_engsys_codeowner_remove_label_owner |  | Remove owner(s) from a label with an optional path in CODEOWNERS work items. Valid ownerType values: service-owner, azsdk-owner, pr-label. |
| azsdk_engsys_codeowner_remove_package_label |  | Remove PR label(s) from a package in CODEOWNERS work items. |
| azsdk_engsys_codeowner_remove_package_owner |  | Remove source owner(s) from a package in CODEOWNERS work items. |
| azsdk_engsys_codeowner_update_cache | `azsdk config codeowners update-cache` | Run the CODEOWNERS cache update pipeline. Use this after making changes to ownership information to unblock releases or other pipelines. |
| azsdk_engsys_codeowner_view |  | View CODEOWNERS associations for a user, label(s), package, or path. Exactly one axis (githubUser, label, package, or path) must be specified. Multiple labels are treated as AND. |
| azsdk_get_failed_test_case_data |  | Get detailed information (error messages, stack traces, output) for a specific failed test case by title from a test result file. Use this to debug a particular test failure. |
| azsdk_get_failed_test_cases | `azsdk pkg test results` | Get list of all failed test case titles (names only) from a test result file. Use this to quickly see which tests failed without details. |
| azsdk_get_failed_test_run_data |  | Get complete details for all failed test cases from a test result file. Returns full data including error messages, stack traces, and output for every failed test. Use this for comprehensive analysis. |
| azsdk_get_github_user_details |  | Get GitHub user details and profile information. Find out who a GitHub user is by their username. |
| azsdk_get_kpi_attestation_status | `azsdk release-plan get-kpi-attestation` | Get KPI attestation status for release plans with given product tree ID and release plan type. If product ID and release plan type are not provided, a TypeSpec project path can be used to resolve them. |
| azsdk_get_modified_typespec_projects | `azsdk tsp project modified-projects` | This tool returns list of TypeSpec projects modified in current branch |
| azsdk_get_pipeline_llm_artifacts | `azsdk ci test-results` | Downloads artifacts intended for LLM analysis from a pipeline run. Accepts an Azure Pipeline link, Build ID, GitHub Pull Request link, or PR number. |
| azsdk_get_pipeline_status | `azsdk ci status` | Get pipeline status for a given Azure Pipeline link, Build ID, GitHub Pull Request link, or PR number |
| azsdk_get_pr_checks | `azsdk ci checks` | Get pipeline and CI check results from a GitHub Pull Request link or PR number |
| azsdk_get_pull_request |  | This tool gets pull request details, status, comments, checks, next action details, links to APIView reviews. |
| azsdk_get_pull_request_link_for_current_branch |  | Get pull request link for current branch in the repo. Provide absolute path to repository root as param. This tool call GetPullRequest to get pull request details. |
| azsdk_get_release_plan | `azsdk release-plan get` | Get Release Plan: Get release plan work item details for a given release plan number/Id or work item id. If neither is provided, finds the active release plan by TypeSpec project path or spec PR URL. Optionally filter by API release type (allowed values: Private Preview, Public Preview, GA). |
| azsdk_get_sdk_pull_request_link | `azsdk spec-workflow get-sdk-pr` | Get SDK pull request link from SDK generation pipeline run or from work item. Build ID of pipeline run is required to query pull request link from SDK generation pipeline. This tool can get SDK pull request details if present in a work item. |
| azsdk_get_service_details_by_typespec_path | `azsdk release-plan get-service-details` | Get service and service tree product details for a product using TypeSpec project path: Get service tree product details (service tree ID, service ID, package display name, product service tree link). |
| azsdk_link_namespace_approval_issue | `azsdk release-plan link-namespace-approval` | Link package namespace approval issue to release plan(required only for management plan). This requires GitHub issue URL for the namespace approval request and release plan work item id. |
| azsdk_link_sdk_pull_request_to_release_plan | `azsdk release-plan link-sdk-pr` | Link SDK pull request to release plan work item |
| azsdk_package_build_code | `azsdk pkg build` | Build/compile SDK code for a specified project locally. |
| azsdk_package_generate_code | `azsdk pkg generate` | Generate SDK code locally or run code generation for a package from TypeSpec. Creates client library code for Azure services. Runs locally, not via pipeline. |
| azsdk_package_generate_samples |  | Generates sample code for a specified package based on a prompt describing sample scenarios. |
| azsdk_package_pack | `azsdk pkg pack` | Create distributable artifacts for the specified SDK package. |
| azsdk_package_run_check | `azsdk pkg validate` | Run validation checks for SDK packages. Provide package path, check type (All, Changelog, Dependency, Readme, Cspell, Snippets), and whether to fix errors. |
| azsdk_package_run_tests | `azsdk pkg test run` | Run tests for the specified SDK package. Provide package path. |
| azsdk_package_translate_samples |  | Translates sample code files from a source package to a target package in a different programming language. Takes samples from the source package's samples directory, understands the functionality being demonstrated, and generates equivalent idiomatic code for the target language using the target package's APIs. Preserves the sample's intent and structure while adapting authentication patterns, error handling, and async conventions to match the target language's best practices. |
| azsdk_package_update_changelog_content | `azsdk pkg update-changelog-content` | Updates the changelog content for a specified package. |
| azsdk_package_update_metadata | `azsdk pkg update-metadata` | Updates the package metadata content for a specified package. |
| azsdk_package_update_version | `azsdk pkg update-version` | Update or bump the version number for an SDK package. Sets the package version and release date in project files. |
| azsdk_release_sdk | `azsdk pkg release` | Releases the specified SDK package for a language. This includes checking if the package is ready for release and triggering the release pipeline. To ONLY check package release readiness pass checkReady as true. |
| azsdk_run_generate_sdk | `azsdk spec-workflow generate-sdk` | Generate SDK from a TypeSpec project using pipeline. Release plan work item ID is required to run SDK generation. |
| azsdk_run_typespec_validation | `azsdk tsp validate` | Run TypeSpec validation. Provide absolute path to TypeSpec project root as param. This tool runs TypeSpec validation and TypeSpec configuration validation. |
| azsdk_typespec_check_project_in_public_repo | `azsdk tsp check-public-repo` | Check if TypeSpec project is in public spec repo. Provide absolute path to TypeSpec project root as param. |
| azsdk_typespec_delegate_apiview_feedback | `azsdk tsp delegate-apiview-feedback` | Address, fix, resolve, or delegate APIView feedback/comments from an APIView URL. Use this tool instead of making code changes directly: it reads the reviewer comments, creates a GitHub issue with the feedback, and assigns GitHub Copilot to determine and implement the required TypeSpec client customizations. |
| azsdk_typespec_generate_authoring_plan |  | Generate solutions or execution plans for TypeSpec‑related tasks, such as defining and updating TypeSpec‑based API specifications for an Azure service.<br>This tool applies to all tasks involving **TypeSpec**:<br>- Writing new TypeSpec definitions: service, api version, resource, models, operations<br>- Editing or refactoring existing TypeSpec files, editing api version, service, resource, models, operations, or properties.<br>- Versioning evolution:<br>  - Make a **preview** API **stable (GA)**.<br>  - Replace an existing **preview** with a **new preview**.<br>  - Replace a **preview** with a **stable**<br>  - Replacing a preview API with a stable API and a new preview API.<br>  - **Add** a preview or **add** a stable API version.<br>- Resolving TypeSpec-related issues<br>Pass in a `request` to get an AI-generated response with references.<br>Returns an answer with supporting references and documentation links<br> |
| azsdk_typespec_init_project | `azsdk tsp init` | Use this tool to initialize a new TypeSpec project. Returns the path to the created project. |
| azsdk_update_api_spec_pull_request_in_release_plan | `azsdk release-plan update-spec-pr` | Update TypeSpec pull request URL in a release plan using work item id or release plan id. |
| azsdk_update_language_exclusion_justification |  | Update language exclusion justification in release plan work item. This tool is called to update justification for excluded languages in the release plan. Optionally pass a language name to explicitly request exclusion for a specific language. |
| azsdk_update_release_plan | `azsdk release-plan update` | Update an existing release plan. Updates spec PR URL, TypeSpec project path, SDK release type, and optionally service/product IDs. When a product ID is provided, product name, product lifecycle and product type are resolved from a matching triage work item in Azure DevOps. If the product type cannot be determined, provide it via productType (allowed values: Offering, Feature, Sku). Runs TypeSpec metadata emitter to resolve package names and updates SDK details. If work item ID is not provided, finds the active release plan by TypeSpec project path or spec PR URL. |
| azsdk_update_release_plan_target | `azsdk release-plan update-release-target` | Update the SDK release target month on an existing release plan. |
| azsdk_update_sdk_details_in_release_plan |  | Update the SDK details in the release plan work item. This tool is called to update SDK language and package name in the release plan work item. Provide path to typespec project. |
| azsdk_upgrade | `azsdk upgrade` | Upgrade the MCP server to the latest version. IMPORTANT: After upgrade completes, the MCP server must be restarted to use the new version. |
| azsdk_verify_setup | `azsdk verify setup check` | Verifies the developer environment for MCP release tool requirements. Accepts a list of supported languages to check requirements for, the packagePath of the repo to check, and an optional list of requirement names to try installing. To auto-install, call with `requirementsToInstall` containing the exact requirement names the user wants to install. |
|  | `azsdk apiview create-pull-request-revision` | Create an API revision if API changes are detected in a pull request (PR pipeline usage) |
|  | `azsdk apiview create-ci-revision` | Create an API revision from Azure DevOps pipeline artifacts (CI/release pipeline usage) |
|  | `azsdk apiview get-content` | Get content by APIView URL |
|  | `azsdk tsp generate-authoring-plan` | Generate a solution or execution plan for defining and updating a TypeSpec-based API specification for an Azure service. |
|  | `azsdk release-plan update-release-status` |  |
|  | `azsdk release-plan list-overdue` |  |
|  | `azsdk quokka` |  |
|  | `azsdk pkg samples translate` | Translates sample files from source language to target package language |
|  | `azsdk pkg samples generate` | Generates sample files |
|  | `azsdk pkg readme generate` | Generate README content for a package |
|  | `azsdk pkg update-work-item` | Update the Azure DevOps package work item |
|  | `azsdk pkg get-work-item` | Get the Azure DevOps package work item |
|  | `azsdk eng package-info` | Generate PackageInfo JSON files for CI pipelines |
|  | `azsdk ingest-telemetry` |  |
|  | `azsdk config github-label sync-ado` | Synchronize service labels from the GitHub CSV to Azure DevOps Work Items |
|  | `azsdk config codeowners remove-package-label` | Remove PR label(s) from a package |
|  | `azsdk config github-label check` | Check if a service label exists in the common labels CSV |
|  | `azsdk config codeowners audit` | Audit CODEOWNERS work items for violations and optionally fix them. You MUST update the CODEOWNERS cache before running this command. |
|  | `azsdk config codeowners check-package` | Check that a package has sufficient owners, PR labels, and service owners from a CODEOWNERS cache file |
|  | `azsdk config codeowners export-section` | Export one or more named sections from a CODEOWNERS file |
|  | `azsdk config codeowners remove-label-owner` | Remove owner(s) from a label and optional path |
|  | `azsdk verify setup install` | Install missing environment requirements. Exit codes: 0 = all requirements met, 1 = blocking (manual intervention needed).  |
|  | `azsdk config codeowners remove-package-owner` | Remove source owner(s) from a package |
|  | `azsdk config codeowners add-label-owner` | Add owner(s) to a label and optional path |
|  | `azsdk config codeowners add-package-label` | Add PR label(s) to a package |
|  | `azsdk config codeowners add-package-owner` | Add source owner(s) to a package |
|  | `azsdk config codeowners view` | View CODEOWNERS associations for a user, label, package, or path |
|  | `azsdk config codeowners generate` | Generate CODEOWNERS file from Azure DevOps work items |
|  | `azsdk start` | Starts the MCP server (stdio mode) |
|  | `azsdk mcp` | Starts the MCP server (stdio mode) |
|  | `azsdk config github-label create` | Creates a PR for a new label given a proposed label and brand documentation |
|  | `azsdk list` |  |

