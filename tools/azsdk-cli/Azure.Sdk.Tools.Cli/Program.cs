using System.CommandLine;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services;
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
        // todo: what doe sit look like to make this a Lazy<GitHubService>?
        // perhaps we move this to a static function that we can call within HostTool as well.
        services.AddSingleton<IGitHubService, GitHubService>();
        services.AddSingleton<IGitHelper, GitHelper>();
        services.AddSingleton<ITypeSpecHelper, TypeSpecHelper>();
        services.AddSingleton<IDevOpsConnection, DevOpsConnection>();
        services.AddSingleton<IDevOpsService, DevOpsService>();

        var serviceProvider = services.BuildServiceProvider();
        var commandFactory = serviceProvider.GetRequiredService<CommandFactory>();
        var rootCommand = commandFactory.CreateRootCommand();

        return await rootCommand.InvokeAsync(args);
    }
}
