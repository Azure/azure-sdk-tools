using Azure.Sdk.Tools.Cli.Models.AzureSdkKnowledgeAICompletion;

namespace Azure.Sdk.Tools.Cli.Services
{
    /// <summary>
    /// Service for interacting with the AI completion API.
    /// </summary>
    public interface IAzureSdkKnowledgeBaseService
    {
        /// <summary>
        /// Sends an AI chat completion request to the Azure SDK Knowledge Base service.
        /// </summary>
        /// <param name="request">The completion request containing the message and configuration.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the completion response.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when request validation fails.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the API returns an error or the request fails.</exception>
        /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled via <paramref name="cancellationToken"/>.</exception>
        Task<CompletionResponse> SendCompletionRequestAsync(
            CompletionRequest request,
            CancellationToken cancellationToken = default);
    }
}
