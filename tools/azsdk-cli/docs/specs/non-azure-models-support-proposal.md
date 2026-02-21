# Proposal: Support for Non-Azure OpenAI Models

## Overview

This proposal documents the approach implemented in [PR #12747](https://github.com/Azure/azure-sdk-tools/pull/12747) for supporting non-Azure OpenAI models in the Azure SDK CLI tool. The implementation switches from the `AzureOpenAIClient` to a more flexible `OpenAIClient`. This new client can work with both Azure-hosted and non-Azure OpenAI-compatible endpoints.

## Background

The Azure SDK CLI (`azsdk`) is used as both a command-line interface and an MCP (Model Context Protocol) server for AI-assisted development workflows. Previously, the tool was tightly coupled to Azure OpenAI through the `Azure.AI.OpenAI` package, which limited its use to Azure-hosted models only.

### Original Implementation

- Used `AzureOpenAIClient` from the `Azure.AI.OpenAI` package
- Required Azure-specific endpoint (`AZURE_OPENAI_ENDPOINT`)
- Only supported Azure Entra ID authentication
- Default endpoint: `https://openai-shared.openai.azure.com`

### New Implementation

- Uses `OpenAIClient` from the standard `OpenAI` package
- Supports flexible endpoint configuration via multiple environment variables
- Supports both API key and Azure Entra ID (bearer token) authentication
- Maintains backward compatibility with existing Azure deployments

## Technical Implementation

### Package Changes

```xml
<!-- Removed -->
<PackageReference Include="Azure.AI.OpenAI" Version="2.3.0-beta.2" />

<!-- Added -->
<PackageReference Include="OpenAI" Version="2.6.0" />
```

### Authentication Priority

The implementation checks for authentication in the following order:

1. **API Key Authentication**: If `OPENAI_API_KEY` environment variable is set, use API key authentication
2. **Entra ID Authentication**: If no API key is found, fall back to bearer token (Azure Entra ID) authentication using `BearerTokenPolicy`

```csharp
// From AzureOpenAIClientHelper.cs
var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");

if (!string.IsNullOrWhiteSpace(apiKey))
{
    // Use API key authentication if available
    return new OpenAIClient(new ApiKeyCredential(apiKey), options);
}

// Fall back to bearer token (Entra ID) authentication
BearerTokenPolicy tokenPolicy = new(credential, "https://cognitiveservices.azure.com/.default");
options.AddPolicy(tokenPolicy, PipelinePosition.BeforeTransport);
```

### Endpoint Configuration Strategy

The endpoint is determined with the following priority:

| Priority | Environment Variable | Behavior |
|----------|---------------------|----------|
| 1 | `OPENAI_BASE_URL` | Used as-is (no modifications) |
| 2 | `AZURE_OPENAI_ENDPOINT` | Automatically appends `/openai/v1` if missing |
| 3 | Neither set, `OPENAI_API_KEY` not set | Default to `https://openai-shared.openai.azure.com/openai/v1` |
| 4 | `OPENAI_API_KEY` only (no endpoint variables) | Use standard OpenAI API (no custom endpoint) |

```csharp
// From ServiceRegistrations.cs
Uri? endpoint = null;

// Priority 1: Use OPENAI_BASE_URL if it exists
if (!string.IsNullOrWhiteSpace(openAiBaseUrl))
{
    endpoint = new Uri(openAiBaseUrl);
}
// Priority 2: Use AZURE_OPENAI_ENDPOINT with /openai/v1 postfix if it exists
else if (!string.IsNullOrWhiteSpace(azureOpenAiEndpoint))
{
    var baseEndpoint = azureOpenAiEndpoint.TrimEnd('/') + "/openai/v1";
    endpoint = new Uri(baseEndpoint);
}
// Priority 3: If no OPENAI_API_KEY but no Azure endpoint, use openai-shared
else if (string.IsNullOrWhiteSpace(openAiApiKey))
{
    endpoint = new Uri("https://openai-shared.openai.azure.com/openai/v1");
}
// Priority 4: OPENAI_API_KEY exists but no Azure endpoint - use standard OpenAI API.
```

## Benefits

### 1. Provider Flexibility
- **Multi-provider support**: Enables use of OpenAI, Azure OpenAI, and OpenAI-compatible services (e.g., local LLM servers, Ollama, LM Studio, vLLM)

### 2. Development Experience
- **Local development**: Developers can use local LLM servers during development without Azure credentials
- **Testing flexibility**: Easier to mock and test with different backends

### 3. Backward Compatibility
- **Seamless migration**: Existing Azure OpenAI users require no configuration changes
- **Default behavior preserved**: Falls back to the shared Azure OpenAI endpoint when no configuration is provided
- **Entra ID support maintained**: Azure authentication continues to work as before

### 4. Simplified Configuration
- **Standard environment variables**: Uses well-known `OPENAI_API_KEY` and `OPENAI_BASE_URL` variables

### 5. Modern SDK Usage
- **Direct OpenAI SDK**: Uses the official OpenAI SDK which is actively maintained

## Decision Matrix: Microsoft Foundry vs. External Providers

Use the following matrix to determine when to use Microsoft Foundry versus external providers when using the Azure SDK CLI/MCP:

| Scenario | Recommended Provider | Rationale |
|----------|---------------------|-----------|
| **Working with Microsoft confidential data** (e.g., unannounced APIs, internal specs) | Microsoft Foundry | Data stays within Microsoft's infrastructure; complies with confidentiality requirements |
| **Production Azure SDK development** | Microsoft Foundry | Enterprise-grade security, compliance, and support; consistent with Azure ecosystem |
| **CI/CD pipelines in Azure DevOps** | Microsoft Foundry | Seamless integration with Azure services; Entra ID authentication |
| **Local development with non-sensitive data** | External Provider (optional) | Flexibility to use local LLM servers; no Azure credentials needed |
| **Applying workflows to non-Azure SDKs** (e.g., OpenAI .NET SDK) | External Provider | Enables using Azure SDK CLI/MCP tools with SDKs outside the Azure ecosystem |
| **Accessing models not available in Microsoft Foundry** | External Provider | Access to specialized or newer models that may not yet be deployed in Microsoft Foundry |

### Key Decision Factors

1. **Data Sensitivity**: If working with any Microsoft confidential information, **always use Microsoft Foundry**
2. **Compliance Requirements**: Microsoft Foundry provides enterprise compliance certifications required for production workloads
3. **Authentication Needs**: Microsoft Foundry integrates with Entra ID for seamless corporate authentication
4. **Network Constraints**: External providers may be necessary in air-gapped or restricted network environments
5. **Model Availability**: Some specialized models may only be available through specific providers

### Default Recommendation

**Use Microsoft Foundry (default configuration)** unless you have a specific need for an external provider AND are certain that no confidential data will be processed.

## Potential Issues and Considerations

### 1. Authentication Scope Hardcoding
- **Issue**: The authentication scope `https://cognitiveservices.azure.com/.default` is hardcoded
- **Risk**: This scope is specific to Azure Cognitive Services and may not be appropriate for all scenarios
- **Consideration**: Future enhancements might need to make this configurable

### 2. Security Considerations
- **API Key Exposure**: Environment variables can be exposed in logs or error messages
- **Mixed Authentication**: When both API key and Azure credentials are available, API key takes precedence
- **Recommendation**: Document security best practices for credential management

### 3. Confidential Data Leakage Risk
- **Issue**: When using non-Microsoft model providers, there is a risk of accidentally sending Microsoft confidential data (e.g., unannounced TypeSpec APIs still in development, internal service specifications, or other proprietary information) to external parties
- **Risk**: Sensitive information could be exposed to third-party model providers who may use the data for training or other purposes, potentially violating confidentiality agreements or revealing unreleased product features
- **Mitigations**:
  - **Explicit opt-in mechanism**: Require users to explicitly opt-in to using non-Azure endpoints via a CLI flag (e.g., `--allow-external-provider`) to ensure intentional use
  - **Warning prompts**: Display a warning message when a non-Azure endpoint is detected, reminding users about data confidentiality considerations
  - **Environment variable safeguards**: Consider requiring a specific environment variable (e.g., `AZSDK_ALLOW_EXTERNAL_AI_PROVIDERS=true`) to enable non-Azure providers
  - **Documentation**: Clearly document the risks of using external providers with confidential data and provide guidance on appropriate use cases

## Scope and Focus: Azure SDK CLI/MCP and Non-Azure SDKs

### Primary Purpose

The Azure SDK CLI/MCP is designed primarily for **Azure SDK development workflows**. Its core functionality, including code generation, API review, testing utilities, and development automation, is tailored specifically for the Azure SDK ecosystem.

### Support for Non-Azure SDKs

While the non-Azure model support enables technical flexibility, the question arises whether supporting non-Azure SDKs (such as the OpenAI .NET SDK) is desirable or meaningful within this tool.

#### Considerations

1. **Shared Technology Stack**: The OpenAI .NET SDK is TypeSpec-based, similar to Azure SDKs, which means many Azure SDK CLI/MCP workflows (TypeSpec generation, code generation patterns) could be applicable
2. **Ownership Context**: Microsoft owns the OpenAI .NET SDK, which creates a unique situation where Azure SDK tooling concepts naturally align
3. **Workflow Overlap**: Due to the shared TypeSpec foundation, a significant subset of workflows (code generation, API reviews, documentation generation, testing patterns) could be applicable to the OpenAI .NET SDK

#### Value Assessment

| Aspect | Azure SDKs | Non-Azure SDKs (e.g., OpenAI .NET) |
|--------|-----------|-----------------------------------|
| TypeSpec/API generation | ✅ Full support | ✅ Applicable (TypeSpec-based) |
| Azure-specific reviews | ✅ Full support | ❌ Not applicable |
| Generic code quality tools | ✅ Full support | ✅ Applicable |
| Documentation workflows | ✅ Full support | ✅ Applicable |
| Testing utilities | ✅ Full support | ⚠️ Partial applicability |

### Alternative: Dedicated OpenAI SDK Tooling

Given that the OpenAI .NET SDK shares the TypeSpec foundation with Azure SDKs, **leveraging existing Azure SDK CLI/MCP infrastructure** may be more efficient than building dedicated tooling.

#### Cost-Effectiveness Analysis

| Factor | Shared Tooling (Current Approach) | Dedicated OpenAI Tooling |
|--------|----------------------------------|-------------------------|
| **Development cost** | Lower (reuses existing infrastructure) | Higher (new tool development) |
| **Maintenance cost** | Lower (shared TypeSpec tooling) | Higher (separate maintenance) |
| **Feature fit** | High overlap due to TypeSpec | Purpose-built but duplicative |
| **User experience** | Familiar for TypeSpec users | Separate learning curve |
| **Long-term scalability** | Benefits from Azure SDK investments | Independent evolution |

#### Recommendation

For non-Azure SDKs like the OpenAI .NET SDK:

1. **Short-term**: The current flexible model support combined with shared TypeSpec infrastructure provides a cost-effective way to support OpenAI SDK workflows
2. **Long-term**: Given the TypeSpec commonality, **extending Azure SDK CLI/MCP to support OpenAI SDK workflows may be more cost-effective** than dedicated tooling
3. **Decision criteria**: Focus on identifying OpenAI-specific requirements that cannot be met by existing TypeSpec-based tooling

## Conclusion

The implementation in PR #12747 provides flexible endpoint configuration and authentication support while maintaining backward compatibility with existing Azure deployments.

## References

- [PR #12747](https://github.com/Azure/azure-sdk-tools/pull/12747) - Original implementation
- [OpenAI SDK](https://github.com/openai/openai-dotnet) - Official .NET SDK
- [Azure OpenAI Documentation](https://learn.microsoft.com/en-us/azure/ai-services/openai/)
- [Model Context Protocol](https://modelcontextprotocol.io/) - MCP specification
