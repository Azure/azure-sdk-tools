using System.ComponentModel;

namespace Azure.Sdk.Tools.Cli.Microagents.Tools;

public record FetchWebpageInput(
    [property: Description("The URL of the webpage to fetch")] string Url
);

public record FetchWebpageOutput(
    [property: Description("The content of the webpage")] string Content,
    [property: Description("Whether the fetch was successful")] bool Success,
    [property: Description("Error message if fetch failed")] string? ErrorMessage
);

public class FetchWebpageTool : AgentTool<FetchWebpageInput, FetchWebpageOutput>
{
    private static readonly HttpClient _httpClient = new();

    public override string Name { get; init; } = "FetchWebpage";
    public override string Description { get; init; } = "Fetch content from a webpage URL";

    public override async Task<FetchWebpageOutput> Invoke(FetchWebpageInput input, CancellationToken ct)
    {
        if (input is null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        if (string.IsNullOrWhiteSpace(input.Url))
        {
            throw new ArgumentException("URL cannot be empty", nameof(input.Url));
        }

        if (!Uri.TryCreate(input.Url, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException("Invalid URL format", nameof(input.Url));
        }

        // Only allow HTTP/HTTPS schemes
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("Only HTTP and HTTPS URLs are supported", nameof(input.Url));
        }

        try
        {
            var response = await _httpClient.GetAsync(uri, ct);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(ct);

            // Limit content size to avoid overwhelming the LLM
            const int maxContentLength = 50000; // ~50KB
            if (content.Length > maxContentLength)
            {
                content = content.Substring(0, maxContentLength) + "\n\n[Content truncated due to length...]";
            }

            return new FetchWebpageOutput(
                Content: content,
                Success: true,
                ErrorMessage: null
            );
        }
        catch (HttpRequestException ex)
        {
            return new FetchWebpageOutput(
                Content: string.Empty,
                Success: false,
                ErrorMessage: $"HTTP request failed: {ex.Message}"
            );
        }
        catch (TaskCanceledException)
        {
            return new FetchWebpageOutput(
                Content: string.Empty,
                Success: false,
                ErrorMessage: "Request timed out"
            );
        }
        catch (Exception ex)
        {
            return new FetchWebpageOutput(
                Content: string.Empty,
                Success: false,
                ErrorMessage: $"Failed to fetch webpage: {ex.Message}"
            );
        }
    }
}
