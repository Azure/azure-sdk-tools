// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Extensions;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Telemetry;

namespace Azure.Sdk.Tools.Cli;

public class Program
{
    public static WebApplication ServerApp { get; private set; }

    public static async Task<int> Main(string[] args)
    {
        return await Run(args);
    }

    public static async Task<int> Run(string[] args, LogLevel? logLevel = null)
    {
        var (outputFormat, debug) = SharedOptions.GetGlobalOptionValues(args);
        logLevel ??= debug ? LogLevel.Debug : LogLevel.Information;
        ServerApp = CreateAppBuilder(args, outputFormat, logLevel.Value, debug).Build();
        return await CommandRunner.BuildAndRun(args, ServerApp.Services, debug);
    }

    // todo: make this honor subcommands of `start` and the like, instead of simply looking presence of `start` verb
    public static bool IsCommandLine(string[] args) => !args.Select(x => x.Trim().ToLowerInvariant()).Any(x => x == "start" || x == "mcp");

    public static WebApplicationBuilder CreateAppBuilder(string[] args, string outputFormat, LogLevel logLevel, bool debug = false)
    {
        var isCommandLine = IsCommandLine(args);

        // Any args that ASP.NET doesn't recognize will be _ignored_ by the CreateBuilder, so we don't need to ONLY
        // pass unmatched ASP.NET config values like --ASPNET_URLS to the builder. It'll just quietly ignore everything
        // it doesn't recognize.
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        // Force validation on startup of any lifetime issues with dependency injection
        // service registrations (e.g. Singleton depending on Scoped or Transient).
        // Equivalent to running with DOTNET_ENVIRONMENT=Development
        builder.Host.UseDefaultServiceProvider((context, options) =>
        {
            options.ValidateScopes = true;
            options.ValidateOnBuild = true;
        });

        // In MCP server mode skip console output except for fatal server errors (which may happen
        // before the MCP logging transport is initialized).
        // All other logs will be redirected via the mcp logger over json-rpc to the mcp client only
        builder.Logging.ConfigureMcpConsoleLogging(isCommandLine);

        // Skip azure client logging noise
        builder.Logging.AddFilter((category, level) =>
        {
            if (debug || null == category) { return level >= logLevel; }
            var isAzureClient = category.StartsWith("Azure.", StringComparison.Ordinal);
            var isToolsClient = category.StartsWith("Azure.Sdk.Tools.", StringComparison.Ordinal);
            if (isAzureClient && !isToolsClient) { return level >= LogLevel.Error; }
            return level >= logLevel;
        });

        // add the console logger
        builder.Services.ConfigureDefaultLogging(logLevel, isCommandLine);


        var outputMode = !isCommandLine ? OutputHelper.OutputModes.Mcp : outputFormat switch
        {
            "plain" => OutputHelper.OutputModes.Plain,
            "json" => OutputHelper.OutputModes.Json,
            "hidden" => OutputHelper.OutputModes.Hidden,  // Used for test, don't include in help text
            _ => throw new ArgumentException($"Invalid output format '{outputFormat}'. Supported formats are: plain, json")
        };

        // register common services
        ServiceRegistrations.RegisterCommonServices(builder.Services, outputMode);
        // register MCP tools
        ServiceRegistrations.RegisterInstrumentedMcpTools(builder.Services, args);

        if (isCommandLine)
        {
            return builder;
        }

        builder.Services.ConfigureMcpLogging();

        TelemetryService.RegisterServerTelemetry(builder.Services, debug);

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
