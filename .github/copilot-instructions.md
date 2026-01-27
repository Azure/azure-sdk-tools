# Copilot Instructions for Azure SDK Tools Repository

## Overview

This repository contains tools and libraries used by the Azure SDK team's engineering system. Tools are written in multiple languages including C#, Python, JavaScript/TypeScript, PowerShell, and Go.

## General Guidelines

- @azure Rule - Use Azure Best Practices: When generating code for Azure, running terminal commands for Azure, or performing operations related to Azure, invoke your `azure_development-get_best_practices` tool if available.
- Make minimal, surgical changes focused on the specific task at hand
- Follow existing patterns and conventions found in the codebase
- Each tool should be self-contained with its own README, tests, and CI configuration

## Repository Structure

- `/tools/` - Individual tools organized by name, each in its own directory
- `/packages/` - Shared packages (Java and Python)
- `/eng/` - Engineering system files including common scripts and pipeline templates
- `/doc/` - Documentation including development guidelines
- `.github/` - GitHub configuration, workflows, and custom agents

## Tool Development

### Creating a New Tool

1. Create a new folder under `/tools/` with a descriptive name
2. Add a comprehensive README.md explaining:
   - Purpose of the tool
   - Prerequisites
   - How to build, test, and use locally and remotely
   - Where the tool is used
3. Add code owners to `.github/CODEOWNERS`:
   ```
   /tools/<tool-name>/ @owner1 @owner2
   ```
4. Update the index table in the root README.md
5. Provide test cases covering important workflows
6. For tools publishing to public repositories, provide a `ci.yml` using templates from `eng/pipelines/templates`

### Naming Convention for Pipelines

- Public builds: `tools - <tool-name> - ci`
- Internal builds: `tools - <tool-name>`

## Language-Specific Guidelines

### PowerShell

Follow guidelines in `/doc/development/powershell.md`:

**DO:**
- Write scripts that can be run locally
- Write unit tests using Pester
- Handle exit codes from external programs
- Use `Set-StrictMode -Version 4`
- Use `[CmdletBinding()]` at the top of scripts
- Set `$ErrorActionPreference = 'Stop'`
- Support the `-WhatIf` parameter for destructive operations
- Use full CmdLet names (avoid aliases in scripts)
- Declare types for function parameters
- Use explicit `return` statements
- Import common modules from `eng/common/scripts/common.ps1`

**DON'T:**
- Write scripts inline in Azure Pipelines YAML (extract to script files)
- Call functions with parentheses syntax like `myFunc(1, 2, 3)` - use `myFunc 1 2 3`
- Use global state where avoidable
- Use complex pipeline statements with side effects

**Testing:**
- Write Pester tests in a `tests/` directory alongside scripts
- Run tests with `Invoke-Pester`
- See examples in `eng/common/scripts/job-matrix/tests`

### C# / .NET

Follow conventions in `.editorconfig`:
- Use 4 spaces for indentation in C# files
- Sort `using` directives with system directives first
- Use language keywords over BCL types
- Open braces on new lines (Allman style)
- Use PascalCase for constant fields
- Prefix private fields with underscore
- One class per file
- Organize code with explicit accessibility modifiers

### Python

- Follow standard Python conventions
- Use packages under `/packages/python-packages/`

### JavaScript/TypeScript

- Use 4 spaces for indentation in TypeScript files
- Follow existing patterns in `/tools/js-sdk-release-tools/`

## Building and Testing

### .NET Projects

- Use `dotnet build` for building
- Use `dotnet test` for running tests
- Common templates available in `eng/pipelines/templates/stages/archetype-sdk-tool-dotnet.yml`

### PowerShell Projects

- Use `Invoke-Pester` for unit tests
- Common templates available in `eng/pipelines/templates/stages/archetype-sdk-tool-pwsh.yml`

## Code Owners

- Always update `.github/CODEOWNERS` when adding new tools
- Format: `/tools/<tool-name>/ @owner1 @owner2`

## Documentation

- Every tool must have a README.md with clear documentation
- Update the root README.md index when adding tools
- Follow the format shown in existing tool READMEs

## CI/CD

- Use pipeline templates from `eng/pipelines/templates`
- Create `ci.yml` in tool directory for build/test/release
- Use internal builds for release steps with appropriate conditions
- See example: `tools/CreateRuleFabricBot/ci.yml`

## Contributing

- Follow guidelines in `CONTRIBUTING.md`
- Agree to Microsoft CLA
- Follow the Microsoft Open Source Code of Conduct
- Do not add third-party tools directly - use as dependencies
