# Copilot Instructions for Azure SDK Tools Repository

## Overview

This repository contains tools and libraries used by the Azure SDK team's engineering system. Tools are written in multiple languages including C#, Python, JavaScript/TypeScript, PowerShell, and Go.

## General Guidelines

- @azure Rule - Use Azure Best Practices: When generating code for Azure, running terminal commands for Azure, or performing operations related to Azure, invoke your `azure_development-get_best_practices` tool if available.
- Base all assumptions and conclusions on data from authoritative sources. Do research as needed to gather data. If you lack sufficient data for validation, stop and tell me what you don't know.
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
- Common templates available in `eng/common/pipelines/templates/stages/archetype-sdk-tool-pwsh.yml`

## Dependency Updates (package-lock.json)

When updating a dependency (for example to resolve a security advisory), keep the
change as small as possible and follow these rules:

### 1. Prefer updating only `package-lock.json`; leave `package.json` untouched

- If the existing semver range in `package.json` already allows the target
  version, update **only** `package-lock.json`. Regenerate it with
  `npm install --package-lock-only` (run from the directory that contains the
  `package.json`/`package-lock.json`).
- Only modify `package.json` (by adding an `overrides` entry or a direct
  dependency) when it is required to keep `package-lock.json` in sync and valid.
- A common case that *requires* a `package.json` change: a parent package pins a
  transitive dependency to an **exact** version. For example, `@angular/build`
  pins `vite`, `undici`, and `piscina` to exact versions, so a lock-only bump is
  reverted on the next `npm install` and fails `npm ci` (e.g.
  `Invalid: lock file's vite@7.3.5 does not satisfy vite@7.3.2`). In that case an
  `overrides` entry is needed to make the new version "stick".

### 2. When an override is needed, override to `^X.Y.Z` (roll-forward), not exact `X.Y.Z`

- Prefer `^X.Y.Z` so the dependency rolls forward to future minor and patch
  releases, rather than pinning the exact `X.Y.Z`.
- Add the override consistent with the existing structure in that
  `package.json`. In `src/dotnet/APIView/ClientSPA`, overrides for packages
  pinned by `@angular/build` are nested under the `@angular/build` key (next to
  the existing `vitest`/`vite` entries), not at the top level.

### 3. Validate the result

- After any change, run `npm ci` in the affected directory to confirm
  `package-lock.json` is consistent with `package.json`. `npm ci` failing with an
  "Invalid: lock file's ... does not satisfy ..." error means an override (or a
  `package.json` change) is still required.
- Verify the intended package resolves to the target version in
  `package-lock.json`. Note that an override does not necessarily remove every
  reference to the old version: a parent's own dependency list may still record
  its original pinned version even though the resolved package was overridden.
  Make PR descriptions accurate about which references remain.

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
- See examples: `tools/pipeline-witness/ci.yml`, `tools/http-fault-injector/ci.yml`

## Contributing

- Follow guidelines in `CONTRIBUTING.md`
- Agree to Microsoft CLA
- Follow the Microsoft Open Source Code of Conduct
- Do not add third-party tools directly - use as dependencies
