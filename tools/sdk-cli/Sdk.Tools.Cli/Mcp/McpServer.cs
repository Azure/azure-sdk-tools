using ModelContextProtocol.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sdk.Tools.Cli.Helpers;
using Sdk.Tools.Cli.Models;
using Sdk.Tools.Cli.Services;
using Sdk.Tools.Cli.Services.Languages;

namespace Sdk.Tools.Cli.Mcp;

/// <summary>
/// MCP server implementation for AI agent integration.
/// Exposes sdk-cli tools to VS Code Copilot, Claude Desktop, etc.
/// </summary>
public static class McpServer
{
    public static async Task RunAsync(string transport, int port, string logLevel)
    {
        var builder = Host.CreateApplicationBuilder();
        
        // Configure logging
        builder.Logging.AddConsole().SetMinimumLevel(ParseLogLevel(logLevel));
        
        // Configure services
        builder.Services.AddSingleton(AiProviderSettings.FromEnvironment());
        builder.Services.AddSingleton<AiDebugLogger>();
        builder.Services.AddSingleton<AiService>();
        builder.Services.AddSingleton<FileHelper>();
        builder.Services.AddSingleton<ConfigurationHelper>();
        
        // Configure MCP server
        builder.Services.AddMcpServer(options =>
        {
            options.ServerInfo = new() { Name = "sdk-cli", Version = "1.0.0" };
        })
        .WithStdioServerTransport()
        .WithToolsFromAssembly();
        
        var host = builder.Build();
        await host.RunAsync();
    }
    
    private static LogLevel ParseLogLevel(string level) => level.ToLowerInvariant() switch
    {
        "debug" => LogLevel.Debug,
        "warning" => LogLevel.Warning,
        "error" => LogLevel.Error,
        _ => LogLevel.Information
    };
}
