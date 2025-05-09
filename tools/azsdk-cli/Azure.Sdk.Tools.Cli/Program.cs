using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Services;

namespace Azure.Sdk.Tools.Cli;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
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

        var outputFormat = SharedOptions.GetOutputFormat(args);
        if (args[0] == "server")
        {
            services.AddScoped<ICommandFormatter, McpFormatter>();
        }
        else if (outputFormat == "plain")
        {
            services.AddScoped<ICommandFormatter, PlainTextFormatter>();
        }
        else if (outputFormat == "json")
        {
            services.AddScoped<ICommandFormatter, JsonFormatter>();
        }
        else
        {
            throw new ArgumentException($"Invalid output format '{outputFormat}'. Supported formats are: plain, json");
        }

        var parsedCommands = new CommandLineBuilder(rootCommand)
               .UseDefaults()            // adds help, version, error reporting, suggestions…
               .UseExceptionHandler()    // catches unhandled exceptions and writes them out
               .Build();

        return await parsedCommands.InvokeAsync(args);
    }
}
