using System.Net;
using System.Text;

namespace Azure.Sdk.Tools.Cli.Helpers;

internal static class HttpResponseHelper
{
    internal const int MaxEvidenceBytes = 4 * 1024 * 1024;

    internal static async Task<string?> ReadBoundedResponseAsync(HttpClient http, HttpRequestMessage request, CancellationToken ct)
    {
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > MaxEvidenceBytes)
        {
            throw new InvalidOperationException("Response exceeded the evidence size limit.");
        }
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var content = new MemoryStream();
        var buffer = new byte[16 * 1024];
        int count;
        while ((count = await stream.ReadAsync(buffer, ct)) > 0)
        {
            if (content.Length + count > MaxEvidenceBytes)
            {
                throw new InvalidOperationException("Response exceeded the evidence size limit.");
            }
            await content.WriteAsync(buffer.AsMemory(0, count), ct);
        }
        return Encoding.UTF8.GetString(content.ToArray());
    }
}