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

### Files Modified

| File | Change |
|------|--------|
| `Azure.Sdk.Tools.Cli.csproj` | Swapped `Azure.AI.OpenAI` for `OpenAI` package |
| `Helpers/AzureOpenAIClientHelper.cs` | **New file** - Helper for creating OpenAI clients with flexible auth |
| `Services/ServiceRegistrations.cs` | Updated client registration logic for flexible endpoints |
| `Microagents/MicroagentHostService.cs` | Changed from `AzureOpenAIClient` to `OpenAIClient` |
| `Tools/Example/ExampleTool.cs` | Changed from `AzureOpenAIClient` to `OpenAIClient` |
| `Tests/MicroagentHostServiceTests.cs` | Updated mocks for `OpenAIClient` |
| `Tests/TestHelpers/OpenAIMockHelper.cs` | Updated helper for `OpenAIClient` mocks |

## Benefits

### 1. Provider Flexibility
- **Multi-provider support**: Enables use of OpenAI, Azure OpenAI, and OpenAI-compatible services (e.g., local LLM servers, Ollama, LM Studio, vLLM)
- **No vendor lock-in**: Teams can choose their preferred model provider based on requirements
- **Cost optimization**: Allows selection of providers based on pricing for different use cases

### 2. Development Experience
- **Local development**: Developers can use local LLM servers during development without Azure credentials
- **Testing flexibility**: Easier to mock and test with different backends
- **Reduced Azure dependency**: No Azure subscription required for basic usage

### 3. Backward Compatibility
- **Seamless migration**: Existing Azure OpenAI users require no configuration changes
- **Default behavior preserved**: Falls back to the shared Azure OpenAI endpoint when no configuration is provided
- **Entra ID support maintained**: Azure authentication continues to work as before

### 4. Simplified Configuration
- **Standard environment variables**: Uses well-known `OPENAI_API_KEY` and `OPENAI_BASE_URL` variables
- **Automatic endpoint handling**: Smart defaults for Azure endpoints (appending `/openai/v1`)
- **Clear priority system**: Predictable behavior based on which variables are set

### 5. Modern SDK Usage
- **Direct OpenAI SDK**: Uses the official OpenAI SDK which is actively maintained
- **Reduced dependencies**: Single SDK instead of Azure-specific wrapper
- **Better API compatibility**: Direct access to latest OpenAI features

## Potential Issues and Considerations

### 1. Placeholder API Key Workaround
- **Issue**: The implementation uses a placeholder API key (`"not-used"`) when using Entra ID authentication because the `OpenAIClient` constructor requires an API key
- **Risk**: This approach may be fragile if the OpenAI SDK changes its validation logic in future versions
- **Mitigation**: The placeholder is inlined to avoid scanner alerts; however, this should be monitored for SDK updates

```csharp
// Create client with a placeholder API key (required by constructor but not used due to our bearer token policy)
return new OpenAIClient(new ApiKeyCredential("not-used"), options);
```

### 2. Authentication Scope Hardcoding
- **Issue**: The authentication scope `https://cognitiveservices.azure.com/.default` is hardcoded
- **Risk**: This scope is specific to Azure Cognitive Services and may not be appropriate for all scenarios
- **Consideration**: Future enhancements might need to make this configurable

### 3. API Compatibility Concerns
- **Issue**: Non-Azure OpenAI endpoints may have different API versions or feature sets
- **Risk**: Some features available on Azure OpenAI may not work with third-party providers
- **Mitigation**: The `/openai/v1` suffix is only added for Azure endpoints; other endpoints are used as-is

### 4. Security Considerations
- **API Key Exposure**: Environment variables can be exposed in logs or error messages
- **Mixed Authentication**: When both API key and Azure credentials are available, API key takes precedence
- **Recommendation**: Document security best practices for credential management

### 5. Testing Complexity
- **Issue**: The mocking strategy needs to account for the generic `OpenAIClient` instead of Azure-specific client
- **Consideration**: Test infrastructure needs to be maintained for both authentication paths

### 6. Error Handling Differences
- **Issue**: Different providers may return different error formats
- **Risk**: Error handling code optimized for Azure responses may not work correctly with other providers
- **Recommendation**: Implement provider-agnostic error handling

### 7. Feature Parity
- **Issue**: Not all features of `AzureOpenAIClient` may be available through the generic `OpenAIClient`
- **Risk**: Azure-specific features (e.g., content filtering, specific deployment options) may no longer be available when using non-Azure providers
- **Consideration**: Document which features require Azure OpenAI specifically

## Configuration Examples

### Using Azure OpenAI (Default)
```bash
# No configuration needed - uses default Azure endpoint with Entra ID auth
azsdk chat --message "Hello"
```

### Using Azure OpenAI with Custom Endpoint
```bash
export AZURE_OPENAI_ENDPOINT="https://my-resource.openai.azure.com"
azsdk chat --message "Hello"
```

### Using OpenAI (Official)
```bash
export OPENAI_API_KEY="sk-..."
azsdk chat --message "Hello"
```

### Using OpenAI-Compatible Server (e.g., Ollama, vLLM)
```bash
export OPENAI_BASE_URL="http://localhost:11434/v1"
export OPENAI_API_KEY="dummy-key"  # Some servers require any key
azsdk chat --message "Hello"
```

### Using Azure OpenAI with API Key
```bash
export AZURE_OPENAI_ENDPOINT="https://my-resource.openai.azure.com"
export OPENAI_API_KEY="my-azure-api-key"
azsdk chat --message "Hello"
```

## Recommendations

### Short-term
1. **Document the configuration options** in the main README and CLI help text
2. **Add logging** to indicate which authentication method and endpoint is being used
3. **Consider adding a diagnostic command** to verify OpenAI configuration

### Medium-term
1. **Add configuration validation** at startup to catch common misconfigurations
2. **Implement retry logic** that accounts for different provider rate limits
3. **Create integration tests** for different provider configurations

### Long-term
1. **Consider a configuration file** for complex multi-environment setups
2. **Evaluate whether to support multiple providers** simultaneously (e.g., different models from different providers)
3. **Monitor OpenAI SDK updates** for better authentication patterns that eliminate the placeholder key workaround

## Conclusion

The implementation in PR #12747 provides a solid foundation for non-Azure model support while maintaining full backward compatibility. The approach uses well-known environment variables and follows the principle of least surprise. While there are some workarounds (particularly the placeholder API key for Entra ID auth), the benefits of provider flexibility and simplified configuration outweigh these concerns.

The key success factors for this approach are:
- Clear documentation of configuration options
- Monitoring of the OpenAI SDK for updates that may affect the implementation
- Gradual rollout with feedback collection from users trying different providers

## References

- [PR #12747](https://github.com/Azure/azure-sdk-tools/pull/12747) - Original implementation
- [OpenAI SDK](https://github.com/openai/openai-dotnet) - Official .NET SDK
- [Azure OpenAI Documentation](https://learn.microsoft.com/en-us/azure/ai-services/openai/)
- [Model Context Protocol](https://modelcontextprotocol.io/) - MCP specification
