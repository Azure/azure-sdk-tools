// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging.Console;
using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Extensions;

public static class LoggingExtensions
{
    public static void ConfigureMcpLogging(this IServiceCollection services)
    {
        services.AddSingleton<ILoggerProvider, McpServerLoggerProvider>();
        services.AddHostedService<McpLoggingHostedService>();
    }

    public static void ConfigureMcpConsoleLogging(this ILoggingBuilder builder, bool isCommandLine)
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
            builder.AddConsole(consoleLogOptions =>
            {
                consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Error;
            });
        }
    }

    public static void ConfigureDefaultLogging(this IServiceCollection services, LogLevel logLevel, bool isCommandLine)
    {
        services.AddLogging(logging =>
        {
            if (isCommandLine)
            {
                logging.AddConsole();
            }
            logging.SetMinimumLevel(logLevel);
        });
    }
}
