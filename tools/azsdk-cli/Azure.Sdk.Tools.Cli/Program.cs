using System.CommandLine;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Services;

namespace Azure.Sdk.Tools.Cli;

public class Program
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
            builder.SetMinimumLevel(LogLevel.Information);
        });
        services.AddSingleton<CommandFactory>();
        ServiceRegistrations.RegisterCommonServices(services);

        var serviceProvider = services.BuildServiceProvider();
        var commandFactory = serviceProvider.GetRequiredService<CommandFactory>();
        var rootCommand = commandFactory.CreateRootCommand(args);

        return await rootCommand.InvokeAsync(args);
    }
}
