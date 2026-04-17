// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging.Console;
using Azure.Sdk.Tools.Cli.Formatters;
using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Extensions;

public static class LoggingExtensions
{
    public static void ConfigureMcpLogging(this IServiceCollection services)
    {
        services.AddSingleton<ILoggerProvider, McpServerLoggerProvider>();
        services.AddHostedService<McpLoggingHostedService>();
    }

    // Some dependencies may use the logger instance provided by the host builder (i.e. asp.net)
    // before the MCP server is initialized and can register it's logging provider.
    // For most of these logs we want to swallow them so we don't spam the user with misleading
    // failed to parse message logs from logger stdout. However, in the case of any fatal server
    // startup errors, we wouldn't want to swallow those as it could make debugging difficult.
    // This is a filter to make sure that we hide all non-error logs but show all
    // asp.net server logs even if they can't be sent over the mcp protocol.
    public static void ConfigureMcpConsoleFallbackLogging(this ILoggingBuilder builder, bool isCommandLine, bool debug = false)
    {
        if (!isCommandLine)
        {
            builder.ClearProviders();
            builder.AddConsole(consoleLogOptions =>
            {
                consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Error;
            });
            builder.AddFilter<ConsoleLoggerProvider>((category, level) =>
                level >= LogLevel.Error
                && category != null
                && (category.StartsWith("Microsoft.Hosting", StringComparison.Ordinal)
                    || category.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal)
                    || category.StartsWith("Microsoft.Extensions.Hosting", StringComparison.Ordinal)));
        }
        else
        {
            // In CLI mode, route ALL log levels to stderr so stdout is
            // reserved exclusively for the command response.
            AddCliConsoleLogger(builder, debug);
        }
    }

    public static void ConfigureDefaultLogging(this IServiceCollection services, LogLevel logLevel)
    {
        services.AddLogging(logging =>
        {
            logging.SetMinimumLevel(logLevel);
        });
    }

    private static void AddCliConsoleLogger(ILoggingBuilder builder, bool debug)
    {
        if (debug)
        {
            // Debug mode: use built-in SimpleConsole formatter (shows level + category + message)
            builder.AddSimpleConsole(options =>
            {
                options.ColorBehavior = LoggerColorBehavior.Default;
            });
        }
        else
        {
            // Normal mode: use our custom formatter (message-only with color coding)
            builder.AddConsoleFormatter<SimpleCliConsoleFormatter, ConsoleFormatterOptions>();
        }

        builder.AddConsole(consoleLogOptions =>
        {
            consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
            if (!debug)
            {
                consoleLogOptions.FormatterName = SimpleCliConsoleFormatter.FormatterName;
            }
        });
    }
}
