namespace Azure.Sdk.Tools.Cli.Configuration;

public static class APIViewConfiguration
{
    public const string UserAgent = "Azure-SDK-Tools-MCP";

    public static readonly Dictionary<string, string> BaseUrlEndpoints = new()
    {
        { "production", "https://apiview.dev" },
        { "staging", "https://apiviewstagingtest.com" },
        { "local", "http://localhost:5000" }
    };

    public static readonly Dictionary<string, string> ApiViewScopes = new()
    {
        { "production", "api://apiview/.default" },
        { "staging", "api://apiviewstaging/.default" },
        { "local", "api://apiviewstaging/.default" }
    };

    /// <summary>
    /// Returns the environment name ("production", "staging", or "local") inferred from
    /// the host of <paramref name="url"/>, or <see langword="null"/> if unrecognised.
    /// </summary>
    public static string? GetEnvironmentFromUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) { return null; }
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) { return null; }
        var host = uri.Host;
        if (host.EndsWith("apiviewstagingtest.com", StringComparison.OrdinalIgnoreCase)) { return "staging"; }
        if (host.EndsWith("apiview.org", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith("apiview.dev", StringComparison.OrdinalIgnoreCase)) { return "production"; }
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)) { return "local"; }
        return null;
    }
}
