# Tools available in Azure SDK MCP server

This document provides a comprehensive list of all MCP (Model Context Protocol) tool commands supported by the Azure SDK MCP server version 0.5.9.

## Tools list

| Command | Description |
|---------|-------------|
| azsdk azp analyze | Analyze azure pipeline for failures. Set analyzeWithAgent to false unless requested otherwise by the user |
| azsdk azp log analyze | Analyzes a log file for errors and issues |
| azsdk azp status | Get pipeline status for a given pipeline build ID |
| azsdk azp test-results | Downloads artifacts intended for LLM analysis from a pipeline run |
| azsdk pkg build | Build/compile SDK code for a specified project locally. |
| azsdk pkg generate | Generates SDK code for a specified language using either 'tspconfig.yaml' or 'tsp-location.yaml'. Runs locally. |
| azsdk pkg release | Releases the specified SDK package for a language. This includes checking if the package is ready for release and triggering the release pipeline. This tool calls CheckPackageReleaseReadiness |
| azsdk pkg release-readiness | Checks if SDK package is ready to release (release readiness). This includes checking pipeline status, apiview status, change log status, and namespace approval status. |
| azsdk pkg test results | Get titles of failed test cases from a TRX file |
| azsdk pkg test run | Run tests for the specified SDK package. Provide package path. |
| azsdk pkg update-changelog-content | Updates the changelog content for a specified package. |
| azsdk pkg update-metadata | Updates the package metadata content for a specified package. |
| azsdk pkg update-version | Updates the version and release date for a specified package. |
| azsdk pkg validate | Run validation checks for SDK packages. Provide package path, check type (All, Changelog, Dependency, Readme, Cspell, Snippets), and whether to fix errors. |
| azsdk release-plan create | Create Release Plan |
| azsdk release-plan get | Get Release Plan: Get release plan work item details for a given work item id or release plan Id. |
| azsdk release-plan link-namespace-approval | Link package namespace approval issue to release plan(required only for management plan). This requires GitHub issue URL for the namespace approval request and release plan work item id. |
| azsdk spec-workflow check-api-readiness | Checks whether a TypeSpec API spec is ready to generate SDK. Provide a pull request number and path to TypeSpec project json as params. |
| azsdk spec-workflow generate-sdk | Generate SDK from a TypeSpec project using pipeline. |
| azsdk spec-workflow get-sdk-pr | Get SDK pull request link from SDK generation pipeline run or from work item. Build ID of pipeline run is required to query pull request link from SDK generation pipeline. This tool can get SDK pull request details if present in a work item. |
| azsdk spec-workflow link-sdk-pr | Link SDK pull request to release plan work item |
| azsdk tsp check-public-repo | Check if TypeSpec project is in public spec repo. Provide absolute path to TypeSpec project root as param. |
| azsdk tsp client customized-update | Update customized TypeSpec-generated client code |
| azsdk tsp convert | Converts an existing Azure service swagger definition to a TypeSpec project. Returns path to the created project. |
| azsdk tsp init | Use this tool to initialize a new TypeSpec project. Returns the path to the created project. |
| azsdk tsp project modified-projects | This tool returns list of TypeSpec projects modified in current branch |
| azsdk tsp validate | Run TypeSpec validation. Provide absolute path to TypeSpec project root as param. This tool runs TypeSpec validation and TypeSpec configuration validation. |
| azsdk verify setup | Verifies the developer environment for MCP release tool requirements. Accepts a list of supported languages to check requirements for, and the packagePath of the repo to check. |

