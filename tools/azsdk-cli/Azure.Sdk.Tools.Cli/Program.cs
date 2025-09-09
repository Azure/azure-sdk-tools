// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Core;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Configuration;

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
        var (outputFormat, debug) = SharedOptions.GetGlobalOptionValues(args);
        var logLevel = debug ? LogLevel.Debug : LogLevel.Information;

        // Any args that ASP.NET doesn't recognize will be _ignored_ by the CreateBuilder, so we don't need to ONLY
        // pass unmatched ASP.NET config values like --ASPNET_URLS to the builder. It'll just quietly ignore everything
        // it doesn't recognize.
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        builder.Services.AddOpenTelemetry()
            .WithTracing(b =>
            {
                b.AddSource(Constants.TOOLS_ACTIVITY_SOURCE)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddProcessor(new TelemetryProcessor());
                if (debug) { b.AddConsoleExporter(); }
            })
            .UseOtlpExporter();

        // Log everything to stderr in mcp mode so the client doesn't try to interpret stdout messages that aren't json rpc
        var logErrorThreshold = isCLI ? LogLevel.Error : LogLevel.Debug;

        builder.Logging.AddConsole(consoleLogOptions =>
        {
            consoleLogOptions.LogToStandardErrorThreshold = logErrorThreshold;
        });

        // Skip azure client logging noise
        builder.Logging.AddFilter((category, level) =>
        {
            if (null == category) { return level >= logErrorThreshold; }
            var isAzureClient = category.StartsWith("Azure.", StringComparison.Ordinal);
            var isToolsClient = category.StartsWith("Azure.Sdk.Tools.", StringComparison.Ordinal);
            if (isAzureClient && !isToolsClient) { return level >= LogLevel.Error; }
            return level >= logErrorThreshold;
        });

        // add the console logger
        builder.Services.AddLogging(l =>
        {
            l.AddConsole();
            l.SetMinimumLevel(logLevel);
        });

        var outputMode = !isCLI ? OutputModes.Mcp : outputFormat switch
        {
            "plain" => OutputModes.Plain,
            "json" => OutputModes.Json,
            _ => throw new ArgumentException($"Invalid output format '{outputFormat}'. Supported formats are: plain, json")
        };
        builder.Services.AddSingleton<IOutputHelper>(new OutputHelper(outputMode));

        // register common services
        ServiceRegistrations.RegisterCommonServices(builder.Services);
        // register MCP tools
        ServiceRegistrations.RegisterInstrumentedMcpTools(builder.Services, args);

        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Listen(System.Net.IPAddress.Loopback, 0); // 0 = dynamic port
        });

        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport();

        return builder;
    }
}
