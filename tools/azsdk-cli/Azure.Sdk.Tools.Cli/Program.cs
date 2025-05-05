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
        // todo: parse a command bundle here. pass it to CreateHostBuilder once we have an actual type
        // todo: can we have a "start" verb and a <tool> verb? EG if someone calls <server.exe> HelloWorld
        //   "This is a hello world input" -> we invoke just that tool
        //   "<server.exe> start" -> runs server responding to vscode copilot chat
        ServiceCollection services = new();
        ConfigureServices(services, targetedTools: null);
        var serviceProvider = services.BuildServiceProvider();

        var commandFactory = serviceProvider.GetRequiredService<CommandFactory>();
        var rootCommand = commandFactory.CreateRootCommand();

        // should probably swap over to returning a CommandResponse object similar to 
        // azure-mcp...but at the same time we could just structured log the result in the
        // HandleCommand function of each tool, then maybe a ConsoleFormatter to turn the structured
        // log into something easy to read?
        return await rootCommand.InvokeAsync(args);
    }

    public static void ConfigureServices(IServiceCollection services, List<MCPTool>? targetedTools)
    {
        if (targetedTools == null)
        {
            services.AddMcpServer()
                .WithStdioServerTransport()
                .WithToolsFromAssembly();
        }
    }

}
