using System.Text.Json;
using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Identity;
using Azure.Sdk.Tools.Cli.Models;
using OpenAI.Chat;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// Interface for Azure OpenAI client to enable dependency injection and testing
/// </summary>
public interface IAzureOpenAIClient
{
    /// <summary>
    /// Gets a chat client for the specified model deployment
    /// </summary>
    /// <param name="model">The model deployment name</param>
    /// <returns>A chat client instance</returns>
    ChatClient GetChatClient(string model);
}


/// <summary>
/// Wrapper implementation of IAzureOpenAIClient that delegates to the actual AzureOpenAIClient
/// </summary>
public class AzureOpenAIClientService : IAzureOpenAIClient
{
    private readonly AzureOpenAIClient _client;

    /// <summary>
    /// Initializes a new instance of AzureOpenAIClientWrapper
    /// </summary>
    /// <param name="endpoint">The Azure OpenAI endpoint URI</param>
    /// <param name="credential">The token credential for authentication</param>
    public AzureOpenAIClientService()
    {
        var ep = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");

        if (!string.IsNullOrEmpty(ep))
        {
            // TODO: there's a pipeline credential that we might also need to support?
            _client = new AzureOpenAIClient(new Uri(ep), new DefaultAzureCredential());
        }
        else
        {
            _client = null;
        }
    }

    /// <inheritdoc />
    public ChatClient GetChatClient(string model)
    {
        if (_client == null)
        {
            throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set, OpenAI related commands will not be available");
        }

        return _client.GetChatClient(model);
    }
}
