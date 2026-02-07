# TypeSpec New Project - Detailed Guide

## Prerequisites

Before creating a TypeSpec project:
1. Clone the `azure-rest-api-specs` or `azure-rest-api-specs-pr` repository
2. Ensure Node.js is installed (for TypeSpec compiler)
3. Determine your service type (ARM or Data Plane)

## Template Selection Deep Dive

### Choose `azure-arm` template when:
- Building a **Resource Manager (ARM)** service
- Creating Azure resources that appear in the Azure Portal
- Defining resources with standard ARM lifecycle (PUT, GET, DELETE)
- Service namespace starts with `Microsoft.` (e.g., `Microsoft.Compute`)

### Choose `azure-core` template when:
- Building a **Data Plane** service
- Creating client APIs that don't manage Azure resources
- Building services like Azure AI, Azure Storage data operations, Azure Communication Services
- Service is accessed via service-specific endpoints, not `management.azure.com`

## Detailed Execution Steps

### Step 1: Gather Information

Ask the user for:
1. **Service namespace**: Pascal case name (e.g., `Contoso`, `WidgetManager`)
   - For ARM services: Exclude the `Microsoft.` prefix (it's added automatically)
   - For Data Plane: Use the full service name
2. **Service type**: ARM (resource management) or Data Plane
3. **Output directory**: Must be under `azure-rest-api-specs/specification/`

### Step 2: Initialize the Project

Call the `azsdk_typespec_init_project` MCP tool with:
- `outputDirectory`: Full path under the specs repo (e.g., `/path/to/azure-rest-api-specs/specification/contoso/Contoso.Management`)
- `template`: `azure-arm` or `azure-core`
- `serviceNamespace`: The Pascal case service name

### Step 3: Post-Initialization

After the project is created, guide the user to:
1. Navigate to the project directory
2. Install dependencies: `npm ci`
3. Edit the generated `.tsp` files to define their API
4. Compile: `npx tsp compile .`
5. Validate: Use `azsdk_run_typespec_validation` tool

## Common Scenarios

### Scenario: New ARM Resource Provider

```
User: "I want to create a new ARM resource provider for managing widgets"

1. Clarify service namespace: "What's your service name? (e.g., WidgetManager)"
2. Determine output path: /azure-rest-api-specs/specification/widgetmanager/WidgetManager.Management
3. Call azsdk_typespec_init_project:
   - template: azure-arm
   - serviceNamespace: WidgetManager
   - outputDirectory: <full path>
```

### Scenario: New Data Plane Service

```
User: "Set up TypeSpec for a data plane API for my AI service"

1. Clarify service namespace: "What's your service name? (e.g., ContosoAI)"
2. Determine output path: /azure-rest-api-specs/specification/contosoai/ContosoAI
3. Call azsdk_typespec_init_project:
   - template: azure-core
   - serviceNamespace: ContosoAI
   - outputDirectory: <full path>
```

## Directory Structure Requirements

The output directory MUST be:
- Under the `azure-rest-api-specs` or `azure-rest-api-specs-pr` repository
- Inside the `/specification/` folder
- An empty directory (or not exist yet)

Example valid paths:
```
azure-rest-api-specs/specification/contoso/Contoso.Management/
azure-rest-api-specs/specification/widgetmanager/WidgetManager/
```

## Troubleshooting

### "Invalid --output-directory, must be under azure-rest-api-specs"
- Ensure you've cloned the specs repo
- Provide the full absolute path to a directory under `/specification/`

### "Invalid --template"
- Use exactly `azure-arm` or `azure-core` (case-sensitive)

### "Directory not empty"
- The output directory must be empty
- Either clear it or choose a different path

## Next Steps After Project Creation

1. **Define your API**: Edit the main `.tsp` file to add your operations and models
2. **Add documentation**: Include `@doc` decorators for all operations
3. **Validate**: Run `azsdk_run_typespec_validation` before committing
4. **Generate SDK**: Use `azsdk_package_generate_code` to create client libraries
