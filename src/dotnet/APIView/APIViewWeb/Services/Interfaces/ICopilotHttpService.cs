using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace APIViewWeb.Services
{
    /// <summary>
    /// Centralized HTTP client for talking to the apiview-copilot service.
    /// Owns endpoint resolution, bearer-token injection, HttpClient lifecycle,
    /// and JSON (de)serialization so individual call sites do not have to
    /// duplicate that plumbing.
    /// </summary>
    public interface ICopilotHttpService
    {
        /// <summary>
        /// Issues a GET to <paramref name="path"/> (relative to the configured
        /// CopilotServiceEndpoint), throws on non-success, and deserializes the
        /// response body into <typeparamref name="TResponse"/>.
        /// </summary>
        Task<TResponse> GetAsync<TResponse>(string path, CancellationToken cancellationToken = default);

        /// <summary>
        /// Issues a POST to <paramref name="path"/> with <paramref name="body"/>
        /// serialized as JSON, throws on non-success, and deserializes the
        /// response body into <typeparamref name="TResponse"/>.
        /// </summary>
        Task<TResponse> PostAsync<TResponse>(string path, object body, CancellationToken cancellationToken = default);

        /// <summary>
        /// Issues a POST to <paramref name="path"/> with <paramref name="body"/>
        /// serialized as JSON and throws on non-success. Use when the response
        /// body is not needed.
        /// </summary>
        Task PostAsync(string path, object body, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends an arbitrary request and returns the raw <see cref="HttpResponseMessage"/>.
        /// The endpoint base is prepended to <paramref name="path"/> and the bearer
        /// token is attached automatically. The caller is responsible for
        /// disposing the returned response and for handling non-success status codes.
        /// </summary>
        Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object body = null, CancellationToken cancellationToken = default);
    }
}
