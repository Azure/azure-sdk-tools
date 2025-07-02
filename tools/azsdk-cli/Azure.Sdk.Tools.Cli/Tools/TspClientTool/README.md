# TspClientTool - MCP Tool for TypeSpec SDK Generation

This MCP tool provides core functionality for generating SDKs using `tsp-client` from TypeSpec projects. It f### Validation and Project Management
```bash
# Validate a TypeSpec project
validate-typespec-project --projectPath "specification/contoso/Contoso.Management"

# Full workflow in one command
tsp-client-full-workflow --tspConfigPath "./tspconfig.yaml" --outputDir "./generated"
```essential workflow commands for TypeSpec-based SDK development.

## Overview

The TspClientTool enables you to:
- Initialize SDK projects from TypeSpec configurations
- Update TypeSpec projects with latest changes
- Sync TypeSpec projects with remote specifications
- Generate client libraries from TypeSpec projects
- Validate TypeSpec projects
- Execute complete SDK generation workflows

## Prerequisites

- [Node.js 18.19 LTS](https://nodejs.org/en/download/) or later
- `tsp-client` installed globally: `npm install -g @azure-tools/typespec-client-generator-cli`

## Available MCP Tools

### Core tsp-client Commands

#### `tsp-client-init`
Initialize an SDK project folder from a tspconfig.yaml file.

**Parameters:**
- `tspConfigPath` (required): Path to tspconfig.yaml file or directory containing it
- `outputDir` (optional): Output directory for generated files
- `skipSyncAndGenerate` (optional): Skip syncing and generating the TypeSpec project
- `localSpecRepo` (optional): Path to local repository with the TypeSpec project
- `commit` (optional): Commit hash to be used
- `repo` (optional): Repository where the project is defined
- `skipInstall` (optional): Skip installing dependencies
- `emitterOptions` (optional): Options to pass to the emitter
- `saveInputs` (optional): Don't clean up temp directory after generation
- `debug` (optional): Enable debug logging

**Example:**
```
tsp-client-init --tspConfigPath "path/to/tspconfig.yaml" --outputDir "./output" --debug true
```

#### `tsp-client-update`
Sync and generate from a TypeSpec project (combines sync and generate).

**Parameters:**
- `outputDir` (optional): Output directory
- `repo` (optional): Repository where the project is defined
- `commit` (optional): Commit hash to be used
- `tspConfig` (optional): Path to tspconfig.yaml
- `localSpecRepo` (optional): Path to local spec repo
- `emitterOptions` (optional): Options to pass to the emitter
- `saveInputs` (optional): Don't clean up temp directory
- `skipInstall` (optional): Skip installing dependencies
- `debug` (optional): Enable debug logging

#### `tsp-client-generate`
Generate a client library from a TypeSpec project.

**Parameters:**
- `outputDir` (optional): Output directory
- `emitterOptions` (optional): Options to pass to the emitter
- `saveInputs` (optional): Don't clean up temp directory
- `skipInstall` (optional): Skip installing dependencies
- `debug` (optional): Enable debug logging

#### `tsp-client-sync`
Sync TypeSpec project specified in tsp-location.yaml.

**Parameters:**
- `outputDir` (optional): Output directory
- `localSpecRepo` (optional): Path to local repository with the TypeSpec project
- `debug` (optional): Enable debug logging

#### `tsp-client-generate`
Generate client library from a TypeSpec project.

**Parameters:**
- `outputDir` (optional): Output directory
- `emitterOptions` (optional): Options to pass to the emitter
- `saveInputs` (optional): Don't clean up temp directory after generation
- `skipInstall` (optional): Skip installing dependencies
- `debug` (optional): Enable debug logging

### Enhanced Features

#### `validate-typespec-project`
Validate if a path contains a valid TypeSpec project and get project information.

**Parameters:**
- `projectPath` (required): Path to the TypeSpec project

**Returns:**
- `IsValid`: Whether the path contains a valid TypeSpec project
- `IsManagementPlane`: Whether it's a management plane project
- `RelativePath`: Relative path within the specification repo
- `ProjectPath`: The provided project path

**Example:**
```
validate-typespec-project --projectPath "specification/contoso/Contoso.Management"
```

#### `tsp-client-full-workflow`
Execute a complete workflow: initialize, sync, and generate for a TypeSpec project.

**Parameters:**
- `tspConfigPath` (required): Path to tspconfig.yaml file
- `outputDir` (optional): Output directory
- `localSpecRepo` (optional): Path to local spec repo
- `commit` (optional): Commit hash to be used
- `repo` (optional): Repository where the project is defined
- `emitterOptions` (optional): Options to pass to the emitter
- `debug` (optional): Enable debug logging

This command performs all three steps in sequence:
1. Initialize the project
2. Sync the TypeSpec files
3. Generate the client library

## Command Hierarchy

The tool supports 4 core commands under the `tsp-client` command group:

```
tsp-client
├── init                 # Initialize SDK project
├── update              # Sync and generate
├── sync                # Sync TypeSpec project
└── generate            # Generate client library
```

## Error Handling

The tool provides enhanced error handling with helpful suggestions:

- **Missing tsp-client**: Suggests installation command
- **Invalid tspconfig.yaml**: Provides formatting guidance
- **Path validation**: Uses TypeSpec project validation
- **Command availability**: Checks if tsp-client is installed before execution

## Integration with Existing Tools

This tool integrates with the existing Azure SDK engineering system:

- Uses `ITypeSpecHelper` for TypeSpec project validation
- Follows existing MCP tool patterns
- Integrates with logging and output services
- Supports the same command hierarchy as other tools

## Examples

### Basic SDK Generation
```bash
# Initialize a project from a remote tspconfig.yaml
tsp-client init --tsp-config "https://github.com/Azure/azure-rest-api-specs/blob/main/specification/contoso/Contoso.Management/tspconfig.yaml"

# Generate from current directory
tsp-client generate

# Full workflow in one command
tsp-client-full-workflow --tspConfigPath "./tspconfig.yaml" --outputDir "./generated"
```

### Validation and Project Management
```bash
# Validate a TypeSpec project
validate-typespec-project --projectPath "specification/contoso/Contoso.Management"

# Generate configuration files
tsp-client-generate-config --packageJsonPath "./package.json"
```

### Converting from Swagger
```bash
# Convert Swagger to TypeSpec
tsp-client-convert --swaggerReadmePath "./swagger/readme.md" --outputDir "./typespec"
```

## See Also

- [tsp-client documentation](https://azure.github.io/typespec-azure/docs/howtos/generate-with-tsp-client/intro_tsp_client/)
- [TypeSpec Azure documentation](https://azure.github.io/typespec-azure/)
- [Azure SDK Engineering System](https://github.com/Azure/azure-sdk-tools)
