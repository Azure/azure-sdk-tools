# Azure SDK Tools End-to-End Test Prompts

This file contains prompts used for end-to-end testing to ensure each tool is
invoked properly by MCP clients. The tables are organized by tool category,
with tool names sorted alphabetically within each table.

**Usage:** The `PromptToToolMatchEvaluator` uses these prompts to measure tool
discoverability via embedding similarity. Each tool should have 2-3 prompt
variations showing how users might invoke it.

**Adding prompts:** Tool owners should add rows to the appropriate table. Format:
- `Tool Name`: The exact MCP tool name (e.g., `azsdk_get_pipeline_status`)
- `Test Prompt`: A natural language prompt a user might ask
- `Category`: Repository context where this applies (`all`, `azure-rest-api-specs`, `azure-sdk-for-*`)

---

## APIView Tools

| Tool Name | Test Prompt | Category |
|:----------|:------------|:---------|
| azsdk_apiview_get_comments | Get all the APIView comments for my package | all |
| azsdk_apiview_get_comments | Show me the API review feedback for this package | all |
| azsdk_apiview_get_comments | What comments did the API reviewers leave? | all |

## Config Tools

| Tool Name | Test Prompt | Category |
|:----------|:------------|:---------|
| azsdk_check_service_label | Check if a service label exists for my service | all |
| azsdk_check_service_label | Does the service label for Contoso already exist? | all |
| azsdk_create_service_label | Create a new service label for my service | all |
| azsdk_create_service_label | Add a service label for Contoso Widget Manager | all |
| azsdk_engsys_codeowner_update | Update the CODEOWNERS file for my service | all |
| azsdk_engsys_codeowner_update | Add myself as a codeowner for the storage service | all |
| azsdk_engsys_validate_codeowners_entry_for_service | Validate the codeowners entry for my service | all |
| azsdk_engsys_validate_codeowners_entry_for_service | Check if codeowners are correctly configured for storage | all |

## EngSys Tools

| Tool Name | Test Prompt | Category |
|:----------|:------------|:---------|
| azsdk_analyze_log_file | Analyze this log file for errors | all |
| azsdk_analyze_log_file | What errors are in this build log? | all |
| azsdk_cleanup_ai_agents | Clean up AI agents in my project | all |
| azsdk_get_failed_test_case_data | Get detailed information about a specific failed test | all |
| azsdk_get_failed_test_case_data | Show me the error message and stack trace for the failed test TestAuthentication | all |
| azsdk_get_failed_test_cases | Get the list of failed test cases from my test run | all |
| azsdk_get_failed_test_cases | What tests failed in this TRX file? | all |
| azsdk_get_failed_test_cases | Show me which tests failed | all |
| azsdk_get_failed_test_run_data | Get complete details for all failed tests | all |
| azsdk_get_failed_test_run_data | Show me full information about all test failures including stack traces | all |

## GitHub Tools

| Tool Name | Test Prompt | Category |
|:----------|:------------|:---------|
| azsdk_create_pull_request | Create a pull request for my changes | all |
| azsdk_get_github_user_details | Get details for GitHub user octocat | all |
| azsdk_get_github_user_details | Who is the GitHub user johndoe? | all |
| azsdk_get_pull_request | Get the details of my pull request | all |
| azsdk_get_pull_request | Show me the status and comments on PR #1234 | all |
| azsdk_get_pull_request_link_for_current_branch | Get the PR link for my current branch | all |
| azsdk_get_pull_request_link_for_current_branch | What's the pull request URL for this branch? | all |

## Package Tools

| Tool Name | Test Prompt | Category |
|:----------|:------------|:---------|
| azsdk_package_build_code | Build my SDK package | all |
| azsdk_package_build_code | Compile the code for my package | all |
| azsdk_package_generate_code | Generate SDK code from my TypeSpec | all |
| azsdk_package_generate_code | Run code generation for my package | all |
| azsdk_package_run_check | Run the azsdk package check command to validate my SDK | all |
| azsdk_package_run_check | Run validation checks on my SDK package | all |
| azsdk_package_run_check | Validate the changelog and dependencies for my package | all |
| azsdk_package_run_tests | Run tests for my SDK package | all |
| azsdk_package_run_tests | Execute the test suite for my package | all |
| azsdk_package_update_changelog_content | Update the changelog for my package | all |
| azsdk_package_update_changelog_content | Add release notes to the changelog | all |
| azsdk_package_update_metadata | Update the package metadata | all |
| azsdk_package_update_metadata | Change the package description and tags | all |
| azsdk_package_update_version | Update my package version | all |
| azsdk_package_update_version | Bump the version to 1.2.0 | all |
| azsdk_release_sdk | Release my SDK package | all |
| azsdk_release_sdk | Trigger the release pipeline for my package | all |
| azsdk_release_sdk | Start the SDK release process for my package | all |

## Pipeline Tools

| Tool Name | Test Prompt | Category |
|:----------|:------------|:---------|
| azsdk_analyze_pipeline | Analyze my pipeline run | all |
| azsdk_analyze_pipeline | What happened in this pipeline build? | all |
| azsdk_get_pipeline_llm_artifacts | Get the LLM artifacts from my pipeline | all |
| azsdk_get_pipeline_llm_artifacts | Download the analysis artifacts from the pipeline run | all |
| azsdk_get_pipeline_status | Check the status of my Azure pipeline build 12345678 | all |
| azsdk_get_pipeline_status | What's the status of pipeline run ID 9876543 | all |
| azsdk_get_pipeline_status | Get the pipeline build status for my CI run | all |

## Release Plan Tools

| Tool Name | Test Prompt | Category |
|:----------|:------------|:---------|
| azsdk_check_api_spec_ready_for_sdk | Check if my API spec is ready to generate SDK | azure-rest-api-specs |
| azsdk_check_api_spec_ready_for_sdk | Is my TypeSpec ready for SDK generation? | azure-rest-api-specs |
| azsdk_create_release_plan | Create a release plan for my service | all |
| azsdk_create_release_plan | Start a new release plan for Contoso Widget Manager | all |
| azsdk_get_release_plan | Get the release plan for work item 12345 | all |
| azsdk_get_release_plan | Show me the release plan details | all |
| azsdk_get_release_plan_for_spec_pr | Get the release plan for my spec PR | azure-rest-api-specs |
| azsdk_get_release_plan_for_spec_pr | What release plan is associated with this spec pull request? | azure-rest-api-specs |
| azsdk_get_sdk_pull_request_link | Get the SDK pull request link from the generation pipeline | all |
| azsdk_get_sdk_pull_request_link | Where is the PR created by SDK generation? | all |
| azsdk_link_namespace_approval_issue | Link namespace approval issue to release plan | all |
| azsdk_link_namespace_approval_issue | Associate the namespace approval with my release plan | all |
| azsdk_link_sdk_pull_request_to_release_plan | Link my SDK pull request to the release plan | all |
| azsdk_link_sdk_pull_request_to_release_plan | Connect PR #5678 to release plan 12345 | all |
| azsdk_run_generate_sdk | Generate SDK from my TypeSpec project using the pipeline | azure-rest-api-specs |
| azsdk_run_generate_sdk | Trigger SDK generation for my service | azure-rest-api-specs |
| azsdk_update_api_spec_pull_request_in_release_plan | Update the TypeSpec PR URL in the release plan | all |
| azsdk_update_api_spec_pull_request_in_release_plan | Change the spec PR link in my release plan | all |
| azsdk_update_language_exclusion_justification | Update the language exclusion justification | all |
| azsdk_update_language_exclusion_justification | Explain why Python is excluded from this release | all |
| azsdk_update_sdk_details_in_release_plan | Update SDK details in the release plan | all |
| azsdk_update_sdk_details_in_release_plan | Change the SDK package name in the release plan | all |

## TypeSpec Tools

| Tool Name | Test Prompt | Category |
|:----------|:------------|:---------|
| azsdk_convert_swagger_to_typespec | Convert my swagger to TypeSpec | azure-rest-api-specs |
| azsdk_convert_swagger_to_typespec | Migrate my API from swagger to TypeSpec | azure-rest-api-specs |
| azsdk_customized_code_update | Update my customized SDK code after regeneration | all |
| azsdk_customized_code_update | Help me update the custom code after TypeSpec changes | all |
| azsdk_get_modified_typespec_projects | What TypeSpec projects were modified in my branch? | azure-rest-api-specs |
| azsdk_get_modified_typespec_projects | List the changed TypeSpec projects | azure-rest-api-specs |
| azsdk_run_typespec_validation | Validate my TypeSpec project | azure-rest-api-specs |
| azsdk_run_typespec_validation | Run TypeSpec validation on my project | azure-rest-api-specs |
| azsdk_typespec_check_project_in_public_repo | Check if my TypeSpec project is in the public repo | azure-rest-api-specs |
| azsdk_typespec_check_project_in_public_repo | Is my TypeSpec project in azure-rest-api-specs? | azure-rest-api-specs |
| azsdk_typespec_init_project | Initialize a new TypeSpec project | azure-rest-api-specs |
| azsdk_typespec_init_project | Create a new TypeSpec project for my service | azure-rest-api-specs |

## Verification Tools

| Tool Name | Test Prompt | Category |
|:----------|:------------|:---------|
| azsdk_verify_setup | Verify my environment setup | all |
| azsdk_verify_setup | Check if I have all required tools installed | all |
| azsdk_verify_setup | Verify my MCP release tool setup | all |
