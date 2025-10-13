# Azure SDK Tools CLI Command Guidelines

This document provides comprehensive guidelines for creating CLI commands in the `Azure.Sdk.Tools.Cli` project. All CLI commands must follow these established patterns and conventions to ensure consistency, maintainability, and proper integration with both CLI and MCP (Model Context Protocol) server functionality.
Azure SDK developers and service team can use these CLI commands to generate, build, test and release Azure SDK.

## Namespace Organization

### Directory Structure

```
Tools/
├── Package/          # Package-level operations 
├── ReleasePlan/      # Release planning operations
├── TypeSpec/         # TypeSpec operations
```

## Command Hierarchy

All CLI commands must follow a predefined top-level command hierarchy. Commands are organized into the following categories:

### 1. **package** - Package Operations

**Namespace:** `Azure.Sdk.Tools.Cli.Tools.Package`  
**Command Group:** `SharedCommandGroups.Package`  
**Verb:** `package`  or `pkg`

For operations at the SDK package level. The package group has further sub-grouping for better organization:

#### Core Package Operations:

- Build/compile SDK code
- Generate SDKs
- Release packages
- Update version information
- Validate packages

#### Sub-Groups:

##### **readme** - README Operations
 
For generating and updating README files:

- Generate README
- Update README 

##### **sample** - Sample Operations

For generating and updating SDK samples:

- Create samples
- Update samples

##### **test** - Test Operations

For creating, updating, and running tests for SDK packages:

- Create tests
- Update tests
- Run tests for specific packages

**Examples:**

- `package build --package-path ./sdk/storage`
- `package generate --typespec-project ./specification/storage`
- `package readme generate --package-path ./sdk/compute`
- `package readme update --package-path ./sdk/keyvault --section getting-started`
- `package release --package-name azure-core`
- `package sample generate --package-path ./sdk/compute`
- `package sample update --package-path ./sdk/storage --sample-name basic-usage`
- `package test generate --package-path ./sdk/storage --test-type <type>`
- `package test run --package-path ./sdk/keyvault --test-suite integration`
- `package validate --package-path ./sdk/keyvault`

### 2. **release-plan** - Release Plan

**Namespace:** `Azure.Sdk.Tools.Cli.Tools.ReleasePlan`  
**Command Group:** Custom (no predefined group)  
**Verb:** `release-plan` 

For release planning and SDK coordination:

- Create release plans
- Link SDK pull requests to release plan
- Get release plan details

**Examples:**

- `release-plan get --workitem-id 456`
- `release-plan link-sdk-pr --release-plan-id 123 --pr 789`

### 3. **typespec** - TypeSpec Operations and TypeSpec Client Operations

**Namespace:** `Azure.Sdk.Tools.Cli.Tools.TypeSpec`  
**Command Group:** `SharedCommandGroups.TypeSpec`  
**Verb:** `tsp`  

For TypeSpec-related operations:

- Create TypeSpec projects
- Convert Swagger to TypeSpec
- Validate TypeSpec projects
- Common TypeSpec workflows

**Examples:**

- `tsp convert --swagger-file ./swagger.json`
- `tsp init --name MyService`
- `tsp validate --project-path ./typespec`
