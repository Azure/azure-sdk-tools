namespace Azure.Sdk.Tools.Cli.Configuration;

public static class APIViewConfiguration
{
    public const string UserAgent = "Azure-SDK-Tools-MCP";

    public static readonly Dictionary<string, string> BaseUrlEndpoints = new()
    {
        { "production", "https://apiview.org" },
        { "staging", "https://apiviewstagingtest.com" },
        { "local", "http://localhost:5000" }
    };

    public static readonly Dictionary<string, string> ApiViewScopes = new()
    {
        { "production", "api://apiview/.default" },
        { "staging", "api://apiviewstaging/.default" },
        { "local", "api://apiviewstaging/.default" }
    };
}
