using AgentClientProtocol.Sdk;
using AgentClientProtocol.Sdk.Stream;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sdk.Tools.Cli.Helpers;
using Sdk.Tools.Cli.Models;
using Sdk.Tools.Cli.Services;
using Sdk.Tools.Cli.Services.Languages;

namespace Sdk.Tools.Cli.Acp;

/// <summary>
/// Entry point that hosts the SampleGeneratorAgent over stdio using ACP protocol.
/// </summary>
public static class SampleGeneratorAgentHost
{
    public static async Task RunAsync(string logLevel)
    {
        var services = ConfigureServices(logLevel);
        var logger = services.GetRequiredService<ILogger<SampleGeneratorAgent>>();
        
        // Create the agent
        var agent = new SampleGeneratorAgent(services, logger);
        
        // Create stdio transport using ACP SDK
        var reader = new StreamReader(Console.OpenStandardInput());
        var writer = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
        var stream = new NdJsonStream(reader, writer);
        
        // Create agent-side connection and run
        var connection = new AgentSideConnection(agent, stream);
        agent.SetConnection(connection);
        
        logger.LogDebug("ACP agent starting on stdio");
        
        await connection.RunAsync();
        
        logger.LogDebug("ACP agent exited");
    }
    
    private static IServiceProvider ConfigureServices(string logLevel)
    {
        var services = new ServiceCollection();
        
        var level = logLevel.ToLowerInvariant() switch
        {
            "debug" => LogLevel.Debug,
            "warning" => LogLevel.Warning,
            "error" => LogLevel.Error,
            _ => LogLevel.Information
        };
        
        services.AddLogging(b => b.AddConsole().SetMinimumLevel(level));
        services.AddSingleton(AiProviderSettings.FromEnvironment());
        services.AddSingleton<AiDebugLogger>();
        services.AddSingleton<AiService>();
        services.AddSingleton<FileHelper>();
        services.AddSingleton<ConfigurationHelper>();
        
        return services.BuildServiceProvider();
    }
}
