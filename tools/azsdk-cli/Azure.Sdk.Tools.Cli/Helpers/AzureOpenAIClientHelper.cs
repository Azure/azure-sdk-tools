// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Core;
using OpenAI;
using System.ClientModel;
using System.ClientModel.Primitives;

namespace Azure.Sdk.Tools.Cli.Helpers;

/// <summary>
/// Helper class to create OpenAI clients configured for Azure endpoints with Entra ID authentication
/// </summary>
public static class AzureOpenAIClientHelper
{
    /// <summary>
    /// Creates an OpenAI client configured for Azure OpenAI with TokenCredential (Entra ID) authentication
    /// </summary>
    /// <param name="endpoint">Azure OpenAI endpoint</param>
    /// <param name="credential">Azure TokenCredential for Entra ID authentication</param>
    /// <returns>Configured OpenAIClient instance</returns>
    public static OpenAIClient CreateAzureOpenAIClient(Uri endpoint, TokenCredential credential)
    {
        var options = new OpenAIClientOptions
        {
            Endpoint = endpoint
        };

        // Add Azure Bearer Token authentication policy
        options.AddPolicy(new BearerTokenAuthenticationPolicy(credential, new[] { "https://cognitiveservices.azure.com/.default" }), PipelinePosition.PerCall);

        // Create client with a placeholder API key (required by constructor but not used due to our custom auth policy)
        return new OpenAIClient(new ApiKeyCredential("not-used"), options);
    }

    /// <summary>
    /// Pipeline policy that adds Bearer token authentication for Azure OpenAI
    /// </summary>
    private class BearerTokenAuthenticationPolicy : PipelinePolicy
    {
        private readonly TokenCredential _credential;
        private readonly string[] _scopes;

        public BearerTokenAuthenticationPolicy(TokenCredential credential, string[] scopes)
        {
            _credential = credential;
            _scopes = scopes;
        }

        public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
        {
            ProcessAsync(message, pipeline, currentIndex).GetAwaiter().GetResult();
        }

        public override async ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
        {
            var token = await _credential.GetTokenAsync(new TokenRequestContext(_scopes), message.CancellationToken);
            message.Request.Headers.Set("Authorization", $"Bearer {token.Token}");

            if (currentIndex < pipeline.Count - 1)
            {
                await pipeline[currentIndex + 1].ProcessAsync(message, pipeline, currentIndex + 1);
            }
        }
    }
}
