using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using Azure.AI.OpenAI;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Microsoft.Extensions.Azure;

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

        var (outputFormat, debug) = SharedOptions.GetGlobalOptionValues(args);

        // Log everything to stderr in mcp mode so the client doesn't try to interpret stdout messages that aren't json rpc
        var logErrorThreshold = isCLI ? LogLevel.Error : LogLevel.Debug;

        builder.Logging.AddConsole(consoleLogOptions =>
        {
            consoleLogOptions.LogToStandardErrorThreshold = logErrorThreshold;
        });

        // register common services
        ServiceRegistrations.RegisterCommonServices(builder.Services);

        // add the console logger
        var logLevel = debug ? LogLevel.Debug : LogLevel.Information;

        builder.Services.AddLogging(l =>
        {
            l.AddConsole();
            l.SetMinimumLevel(logLevel);
        });

        if (!isCLI)
        {
            builder.Services.AddSingleton<IOutputService>(new OutputService(OutputModes.Mcp));
        }
        else if (outputFormat == "plain")
        {
            builder.Services.AddSingleton<IOutputService>(new OutputService(OutputModes.Plain));
        }
        else if (outputFormat == "json")
        {
            builder.Services.AddSingleton<IOutputService>(new OutputService(OutputModes.Json));
        }
        else
        {
            throw new ArgumentException($"Invalid output format '{outputFormat}'. Supported formats are: plain, json");
        }

        builder.Services.AddAzureClients(clientBuilder =>
        {
            // Azure OpenAI client does not, for some reason, have an
            // in-package facade for this, so register manually.
            clientBuilder.AddClient<AzureOpenAIClient, AzureOpenAIClientOptions>(
                (options, credential, _) =>
                {
                    var ep = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT") ?? "https://<need to know how to have a default>";

                    if (string.IsNullOrEmpty(ep))
                    {
                        throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set, OpenAI related commands will not be available");
                    }

                    return new AzureOpenAIClient(new Uri(ep), credential, options);
                });
        });

        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Listen(System.Net.IPAddress.Loopback, 0); // 0 = dynamic port
        });

        var toolTypes = SharedOptions.GetFilteredToolTypes(args);

        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithTools(toolTypes);

        return builder;
    }
}
