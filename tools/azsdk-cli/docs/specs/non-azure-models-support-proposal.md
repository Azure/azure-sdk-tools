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

## Potential Issues and Considerations

### 1. Authentication Scope Hardcoding
- **Issue**: The authentication scope `https://cognitiveservices.azure.com/.default` is hardcoded
- **Risk**: This scope is specific to Azure Cognitive Services and may not be appropriate for all scenarios
- **Consideration**: Future enhancements might need to make this configurable

### 2. Security Considerations
- **API Key Exposure**: Environment variables can be exposed in logs or error messages
- **Mixed Authentication**: When both API key and Azure credentials are available, API key takes precedence
- **Recommendation**: Document security best practices for credential management

## Conclusion

The implementation in PR #12747 provides flexible endpoint configuration and authentication support while maintaining backward compatibility with existing Azure deployments.

## References

- [PR #12747](https://github.com/Azure/azure-sdk-tools/pull/12747) - Original implementation
- [OpenAI SDK](https://github.com/openai/openai-dotnet) - Official .NET SDK
- [Azure OpenAI Documentation](https://learn.microsoft.com/en-us/azure/ai-services/openai/)
- [Model Context Protocol](https://modelcontextprotocol.io/) - MCP specification
