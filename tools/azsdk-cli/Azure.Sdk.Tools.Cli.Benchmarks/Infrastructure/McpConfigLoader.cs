// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;
using GitHub.Copilot.SDK;

namespace Azure.Sdk.Tools.Cli.Benchmarks.Infrastructure;

/// <summary>
/// Loads MCP server configuration from VS Code's .vscode/mcp.json format
/// and converts it to the Copilot SDK format.
/// </summary>
public static class McpConfigLoader
{
    private const string VsCodeMcpConfigPath = ".vscode/mcp.json";

    /// <summary>
    /// Loads MCP server configuration from a workspace directory.
    /// Looks for .vscode/mcp.json and translates it to SDK format.
    /// </summary>
    /// <param name="workspaceRoot">The root directory of the workspace.</param>
    /// <returns>
    /// A dictionary of MCP server configurations suitable for SessionConfig.McpServers,
    /// or null if no configuration file is found.
    /// </returns>
    public static async Task<Dictionary<string, object>?> LoadFromWorkspaceAsync(string workspaceRoot)
    {
        var configPath = Path.Combine(workspaceRoot, VsCodeMcpConfigPath);
        
        if (!File.Exists(configPath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(configPath);
            var vsCodeConfig = JsonSerializer.Deserialize<VsCodeMcpConfig>(json, JsonOptions);

            if (vsCodeConfig?.Servers == null || vsCodeConfig.Servers.Count == 0)
            {
                return null;
            }

            return TranslateToSdkFormat(vsCodeConfig.Servers, workspaceRoot);
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"Warning: Failed to parse {configPath}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Translates VS Code MCP server configs to Copilot SDK format.
    /// </summary>
    private static Dictionary<string, object> TranslateToSdkFormat(
        Dictionary<string, VsCodeMcpServerConfig> vsCodeServers,
        string workspaceRoot)
    {
        var sdkServers = new Dictionary<string, object>();

        foreach (var (name, vsCodeServer) in vsCodeServers)
        {
            var sdkServer = TranslateServer(vsCodeServer, workspaceRoot);
            if (sdkServer != null)
            {
                sdkServers[name] = sdkServer;
            }
        }

        return sdkServers;
    }

    /// <summary>
    /// Translates a single VS Code MCP server config to SDK format.
    /// </summary>
    private static object? TranslateServer(VsCodeMcpServerConfig vsCodeServer, string workspaceRoot)
    {
        var serverType = vsCodeServer.Type?.ToLowerInvariant() ?? "stdio";

        // Handle remote server types
        if (serverType is "http" or "sse")
        {
            if (string.IsNullOrEmpty(vsCodeServer.Url))
            {
                Console.Error.WriteLine($"Warning: Remote MCP server missing 'url' property");
                return null;
            }

            return new McpRemoteServerConfig
            {
                Type = serverType,
                Url = vsCodeServer.Url,
                Headers = vsCodeServer.Headers,
                Tools = ["*"], // Include all tools by default
                Timeout = vsCodeServer.Timeout
            };
        }

        // Handle local server types (stdio, local, or default)
        if (string.IsNullOrEmpty(vsCodeServer.Command))
        {
            Console.Error.WriteLine($"Warning: Local MCP server missing 'command' property");
            return null;
        }

        // Expand ${workspaceFolder} variable in args
        var expandedArgs = vsCodeServer.Args?
            .Select(arg => ExpandVariables(arg, workspaceRoot))
            .ToList() ?? [];

        // Expand ${workspaceFolder} in cwd if present
        var expandedCwd = vsCodeServer.Cwd != null 
            ? ExpandVariables(vsCodeServer.Cwd, workspaceRoot) 
            : null;

        // Expand ${workspaceFolder} in env values if present
        var expandedEnv = vsCodeServer.Env?
            .ToDictionary(
                kvp => kvp.Key,
                kvp => ExpandVariables(kvp.Value, workspaceRoot));

        return new McpLocalServerConfig
        {
            Type = "local", // SDK uses "local" not "stdio"
            Command = vsCodeServer.Command,
            Args = expandedArgs,
            Env = expandedEnv,
            Cwd = expandedCwd,
            Tools = ["*"], // Include all tools by default
            Timeout = vsCodeServer.Timeout
        };
    }

    /// <summary>
    /// Expands VS Code variables like ${workspaceFolder}.
    /// </summary>
    private static string ExpandVariables(string value, string workspaceRoot)
    {
        return value.Replace("${workspaceFolder}", workspaceRoot);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };
}

/// <summary>
/// Represents the VS Code .vscode/mcp.json file structure.
/// </summary>
internal class VsCodeMcpConfig
{
    [JsonPropertyName("servers")]
    public Dictionary<string, VsCodeMcpServerConfig>? Servers { get; set; }
}

/// <summary>
/// Represents a single MCP server configuration in VS Code format.
/// </summary>
internal class VsCodeMcpServerConfig
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("command")]
    public string? Command { get; set; }

    [JsonPropertyName("args")]
    public List<string>? Args { get; set; }

    [JsonPropertyName("env")]
    public Dictionary<string, string>? Env { get; set; }

    [JsonPropertyName("cwd")]
    public string? Cwd { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("headers")]
    public Dictionary<string, string>? Headers { get; set; }

    [JsonPropertyName("timeout")]
    public int? Timeout { get; set; }
}
