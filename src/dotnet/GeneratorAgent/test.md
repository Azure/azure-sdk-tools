# Azure SDK Generator Agent - Testing Instructions

## Overview
This document provides comprehensive instructions for testing the Azure SDK Generator Agent, which helps generate Azure SDKs from TypeSpec specifications and fix Azure analyzer failures through AI-powered customizations.

## Prerequisites

### Required Tools
- .NET 9.0 SDK or later
- Node.js (for TypeSpec compiler)
- Git
- Azure CLI (for authentication if needed)

### Environment Setup
1. Ensure you have access to the following repositories:
   - `azure-sdk-tools` (this repository)
   - `azure-sdk-for-net` 
   - `azure-rest-api-specs`

2. Required environment variables (if using Azure OpenAI):
   ```powershell
   $env:AZURE_OPENAI_ENDPOINT = "your-endpoint"
   $env:AZURE_OPENAI_API_KEY = "your-api-key" 
   $env:AZURE_OPENAI_DEPLOYMENT_NAME = "your-deployment"
   ```

## Test Scenario: EventGrid Library Generation

### Step 1: Select a TypeSpec-based Library

1. Navigate to the [Azure SDK for .NET Library Inventory](https://github.com/Azure/azure-sdk-for-net/blob/main/doc/GeneratorMigration/Library_Inventory.md)

2. Under "Data Plane Libraries using TypeSpec (@azure-typespec/http-client-csharp)", select a library for testing. 

**For example test library: Azure.Messaging.EventGrid.Namespaces**
- Specification path: `specification/eventgrid/Azure.Messaging.EventGrid`
- SDK path: `sdk/eventgrid/Azure.Messaging.EventGrid.Namespaces`

### Step 2: Clone Required Repositories

```powershell
# Clone azure-rest-api-specs (if not already cloned)
cd C:\Users\radhgupta\Desktop
git clone https://github.com/Azure/azure-rest-api-specs.git

# Clone azure-sdk-for-net (if not already cloned)  
git clone https://github.com/Azure/azure-sdk-for-net.git
```

### Step 3: Prepare the Test Environment

#### 3.1 Locate the TypeSpec Specification

#### 3.2 Remove Existing Generated SDK so that we can re-generate the library

#### 3.3 Remove C# Customizations from client.tsp


### Step 4: Build the Generator Agent

# Navigate to the Generator Agent directory
cd C:\Users\radhgupta\Desktop\azure-sdk-tools\src\dotnet\GeneratorAgent

# Clean and build the project
- dotnet clean
- dotnet build

### Step 5: Run the Generator Agent

```powershell
# Navigate to the Generator Agent source directory
cd C:\Users\radhgupta\Desktop\azure-sdk-tools\src\dotnet\GeneratorAgent\src\Azure.Tools.GeneratorAgent

# Run the generator with EventGrid specification
dotnet run -- -t "C:\Users\radhgupta\Desktop\azure-rest-api-specs\specification\eventgrid\Azure.Messaging.EventGrid" -o "C:\Users\radhgupta\Desktop\azure-sdk-for-net\sdk\eventgrid\Azure.Messaging.EventGrid.Namespaces"
```

### Step 6: Verify the Generation Process

The tool should:
1. Install TypeSpec dependencies globally
2. Compile the TypeSpec specification
3. Generate the initial SDK
4. Run Azure analyzer to detect failures
5. Use AI to generate fixes in `client.tsp`
6. Retry compilation until successful or max attempts reached



## Expected Test Results

### Success Indicators
- ✅ TypeSpec compilation succeeds
- ✅ SDK files are generated in the output directory
- ✅ Azure analyzer passes without errors
- ✅ `client.tsp` contains appropriate customizations
- ✅ Generated C# code compiles successfully

### Common Issues and Troubleshooting

#### Issue: "Failed to install TypeSpec dependencies"
**Solution:**
```powershell
# Install dependencies manually
npm install -g @typespec/compiler @azure-tools/typespec-azure-core @azure-tools/typespec-client-generator-core
```

#### Issue: "GitHub API rate limit exceeded"
**Solution:**
- Set up GitHub authentication with a personal access token
- Or use local TypeSpec files only (avoid GitHub API calls)

#### Issue: "Azure analyzer errors persist"
**Solution:**
- Check the generated `client.tsp` for syntax errors
- Verify all required imports are present
- Review the analyzer output for specific error details

#### Issue: "TypeSpec compilation fails"
**Solution:**
```powershell
# Test compilation manually
cd "C:\Users\radhgupta\Desktop\azure-rest-api-specs\specification\eventgrid\Azure.Messaging.EventGrid"
npx tsp compile . --emit @azure-tools/typespec-client-generator-core
```

## Test Validation Steps

### 1. Verify Generated SDK Quality
```powershell
# Build the generated SDK
cd C:\Users\radhgupta\Desktop\azure-sdk-for-net\sdk\eventgrid\Azure.Messaging.EventGrid.Namespaces
dotnet build

# Run basic tests if available
dotnet test --no-build
```

### 2. Check TypeSpec Customizations
Review the generated `client.tsp` to ensure:
- Proper import statements
- Valid TypeSpec syntax
- Appropriate use of `@@` decorators
- Meaningful client names and structure

### 3. Analyze Logs
Check the console output for:
- Successful dependency installation
- TypeSpec compilation success
- Analyzer error detection and fixes
- Final success confirmation

## Command Line Options Reference

```powershell
dotnet run -- --help

# Available options:
# -t, --typespec-dir    Path to TypeSpec specification directory (required)
# -o, --output-dir      Output directory for generated SDK files (required)  
# -c, --commit-id       GitHub commit ID (optional, for GitHub-based generation)
```

## Conclusion

This testing framework ensures the Azure SDK Generator Agent works correctly across different scenarios and library types. Regular testing with these instructions helps maintain the tool's reliability and effectiveness in generating high-quality Azure SDKs from TypeSpec specifications.

## Notes

- Always backup important files before testing
- The tool is designed to be non-destructive to `main.tsp` files
- Generated customizations in `client.tsp` should be reviewed by domain experts
- Test results may vary based on the complexity of the TypeSpec specification
- Ensure you have appropriate access rights to the target repositories and Azure services