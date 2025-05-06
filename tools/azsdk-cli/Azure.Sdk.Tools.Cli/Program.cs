using System.CommandLine;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Services.Azure;
using ModelContextProtocol.Protocol.Types;

namespace Azure.Sdk.Tools.Cli;

public sealed class Program
{
    public static async Task<int> Main(string[] args)
    {
        // create the default service collection which will make logging and commandfactory available for use
        // The command factory itself will walk the assembly and discover any MCPTool implementors, and register them
        // as commands.
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        services.AddSingleton<CommandFactory>();

        // any registrations below here are only necessary for CLI mode
        // the hosttool mode re-inits its own service provider
        services.AddSingleton<IAzureService, AzureService>();

        // 4. Build the provider
        var serviceProvider = services.BuildServiceProvider();
        var commandFactory = serviceProvider.GetRequiredService<CommandFactory>();
        var rootCommand = commandFactory.CreateRootCommand();

        // should probably swap over to returning a CommandResponse object similar to 
        // azure-mcp...but at the same time we could just structured log the result in the
        // HandleCommand function of each tool, then maybe a ConsoleFormatter to turn the structured
        // log into something easy to read?
        return await rootCommand.InvokeAsync(args);
    }
}
