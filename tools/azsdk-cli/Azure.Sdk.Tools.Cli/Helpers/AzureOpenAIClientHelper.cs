// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.AI.OpenAI;
using Azure.Core;
using OpenAI;
using System.ClientModel;

namespace Azure.Sdk.Tools.Cli.Helpers;

/// <summary>
/// Helper class to create OpenAI clients configured for Azure endpoints with authentication
/// </summary>
public static class AzureOpenAIClientHelper
{
    /// <summary>
    /// Creates an OpenAI client configured for Azure OpenAI with API key or TokenCredential (Entra ID) authentication
    /// </summary>
    /// <param name="endpoint">Azure OpenAI endpoint</param>
    /// <param name="credential">Azure TokenCredential for Entra ID authentication (used if no API key is available)</param>
    /// <returns>Configured OpenAIClient instance</returns>
    public static OpenAIClient CreateAzureOpenAIClient(Uri endpoint, TokenCredential credential)
    {
        // Check for API key from environment variable first
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            // Use API key authentication if available
            return new AzureOpenAIClient(endpoint, new ApiKeyCredential(apiKey));
        }

        // Use Entra ID (DefaultAzureCredential) authentication
        return new AzureOpenAIClient(endpoint, credential);
    }
}
