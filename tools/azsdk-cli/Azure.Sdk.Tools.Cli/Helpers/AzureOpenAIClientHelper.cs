// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Core;
using OpenAI;
using System.ClientModel;
using System.ClientModel.Primitives;

namespace Azure.Sdk.Tools.Cli.Helpers;

/// <summary>
/// Helper class to create OpenAI clients configured for Azure endpoints with authentication
/// </summary>
public static class AzureOpenAIClientHelper
{
    private const string PlaceholderApiKey = "not-used";

    /// <summary>
    /// Creates an OpenAI client configured for Azure OpenAI with API key or TokenCredential (Entra ID) authentication
    /// </summary>
    /// <param name="endpoint">Azure OpenAI endpoint</param>
    /// <param name="credential">Azure TokenCredential for Entra ID authentication (used if no API key is available)</param>
    /// <returns>Configured OpenAIClient instance</returns>
    public static OpenAIClient CreateAzureOpenAIClient(Uri endpoint, TokenCredential credential)
    {
        var options = new OpenAIClientOptions
        {
            Endpoint = endpoint
        };

        // Check for API key from environment variable first
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            // Use API key authentication if available
            return new OpenAIClient(new ApiKeyCredential(apiKey), options);
        }

        // Fall back to bearer token (Entra ID) authentication
        BearerTokenPolicy tokenPolicy = new(credential, "https://cognitiveservices.azure.com/.default");
        options.AddPolicy(tokenPolicy, PipelinePosition.BeforeTransport);

        // Create client with a placeholder API key (required by constructor but not used due to our bearer token policy)
        return new OpenAIClient(new ApiKeyCredential(PlaceholderApiKey), options);
    }
}
