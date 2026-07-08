using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.CodeownersUtils.Utils
{
    public interface ICacheValidator
    {
        Task ThrowIfCacheOlderThan(
            string cacheSource,
            DateTime minimumLastModifiedUtc,
            CancellationToken ct = default);
    }

    public class CacheValidator(HttpClient httpClient) : ICacheValidator
    {
        public async Task ThrowIfCacheOlderThan(
            string cacheSource,
            DateTime minimumLastModifiedUtc,
            CancellationToken ct = default)
        {
            if (minimumLastModifiedUtc.Kind != DateTimeKind.Utc)
            {
                minimumLastModifiedUtc = minimumLastModifiedUtc.ToUniversalTime();
            }

            DateTime lastModifiedUtc = await GetLastModifiedUtc(cacheSource, ct);
            if (lastModifiedUtc >= minimumLastModifiedUtc)
            {
                return;
            }

            TimeSpan age = DateTime.UtcNow - lastModifiedUtc;
            throw new InvalidOperationException(
                $"'{cacheSource}' is {age.TotalHours:F1} hours old " +
                $"(last modified {lastModifiedUtc:O}), which is older than the required cutoff " +
                $"{minimumLastModifiedUtc:O}. Refresh the cache using 'azsdk config codeowners update-cache', wait for the build to complete, and try again.");
        }

        private async Task<DateTime> GetLastModifiedUtc(
            string cacheSource,
            CancellationToken ct = default)
        {
            if (Uri.TryCreate(cacheSource, UriKind.Absolute, out Uri cacheUri)
                && (cacheUri.Scheme == Uri.UriSchemeHttp || cacheUri.Scheme == Uri.UriSchemeHttps))
            {
                using var request = new HttpRequestMessage(HttpMethod.Head, cacheUri);
                using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException(
                        $"Failed to fetch metadata for '{cacheSource}'. " +
                        $"HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).");
                }

                if (response.Content?.Headers.LastModified.HasValue == true)
                {
                    return response.Content.Headers.LastModified.Value.UtcDateTime;
                }

                throw new InvalidOperationException(
                    $"Metadata for '{cacheSource}' did not include a parsable Last-Modified header.");
            }

            string fullPath = Path.GetFullPath(cacheSource);
            if (!File.Exists(fullPath))
            {
                throw new InvalidOperationException(
                    $"Path '{cacheSource}' resolved to '{fullPath}', but the file does not exist.");
            }

            return File.GetLastWriteTimeUtc(fullPath);
        }
    }
}
