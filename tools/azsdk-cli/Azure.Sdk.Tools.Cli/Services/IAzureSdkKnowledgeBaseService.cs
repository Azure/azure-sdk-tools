using Azure.Sdk.Tools.Cli.Models.AzureSdkKnowledgeAICompletion;

namespace Azure.Sdk.Tools.Cli.Services
{
    /// <summary>
    /// Service for interacting with the AI completion API.
    /// </summary>
    public interface IAzureSdkKnowledgeBaseService
    {
        /// <summary>
        /// Sends a completion request to the AI service.
        /// </summary>
        /// <param name="request">The completion request containing the question and parameters</param>
        /// <param name="apiKey">Optional API key override</param>
        /// <param name="endpoint">Optional endpoint override</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The completion response from the AI service</returns>
        /// <exception cref="ArgumentNullException">Thrown when request is null</exception>
        /// <exception cref="ArgumentException">Thrown when request validation fails</exception>
        /// <exception cref="InvalidOperationException">Thrown when the service call fails</exception>
        Task<CompletionResponse> SendCompletionRequestAsync(
            CompletionRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates a completion request to ensure it meets API requirements.
        /// </summary>
        /// <param name="request">The request to validate</param>
        /// <returns>True if valid, false otherwise</returns>
        bool ValidateRequest(CompletionRequest request);
    }
}
