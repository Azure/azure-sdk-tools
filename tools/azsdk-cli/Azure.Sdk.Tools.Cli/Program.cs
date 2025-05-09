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
        ServerApp = CreateAppBuilder(args).Build();
        var rootCommand = CommandFactory.CreateRootCommand(args, ServerApp.Services);

        var parsedCommands = new CommandLineBuilder(rootCommand)
               .UseDefaults()            // adds help, version, error reporting, suggestions…
               .UseExceptionHandler()    // catches unhandled exceptions and writes them out
               .Build();

        return await parsedCommands.InvokeAsync(args);
    }

    // todo: make this honor subcommands of `start` and the like, instead of simply looking presence of `start` verb
    public static bool IsCLI(string[] args) => !args.Select(x => x.Trim().ToLowerInvariant()).Any(x => x == "start");

    public static WebApplication ServerApp;

    public static WebApplicationBuilder CreateAppBuilder(string[] args)
    {
        var isCLI = IsCLI(args);

        // Any args that ASP.NET doesn't recognize will be _ignored_ by the CreateBuilder, so we don't need to ONLY
        // pass unmatched ASP.NET config values like --ASPNET_URLS to the builder. It'll just quietly ignore everything
        // it doesn't recognize.
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        builder.Logging.AddConsole(consoleLogOptions =>
        {
            consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Error;
        });

        // register common services
        ServiceRegistrations.RegisterCommonServices(builder.Services);

        // add the console logger
        builder.Services.AddLogging(l =>
        {
            l.AddConsole();
            l.SetMinimumLevel(LogLevel.Information);
        });

        if (!isCLI)
        {
            var formatter = new McpFormatter() as ICommandFormatter;
            builder.Services.AddSingleton<IResponseService>(new ResponseService(formatter));
        }
        else
        {
            var outputFormat = SharedOptions.GetOutputFormat(args);
            if (outputFormat == "plain")
            {
                var formatter = new PlainTextFormatter() as ICommandFormatter;
                builder.Services.AddSingleton<IResponseService>(new ResponseService(formatter));
            }
            else if (outputFormat == "json")
            {
                var formatter = new JsonFormatter() as ICommandFormatter;
                builder.Services.AddSingleton<IResponseService>(new ResponseService(formatter));
            }
            else
            {
                throw new ArgumentException($"Invalid output format '{outputFormat}'. Supported formats are: plain, json");
            }
        }

        var toolTypes = SharedOptions.GetFilteredToolTypes(args);
        if (toolTypes.Count == 0)
        {
            builder.Services
                .AddMcpServer()
                .WithStdioServerTransport()
                .WithToolsFromAssembly();
        }
        else
        {

            builder.Services
                .AddMcpServer()
                .WithStdioServerTransport()
                .WithTools(toolTypes);
        }

        return builder;
    }
}