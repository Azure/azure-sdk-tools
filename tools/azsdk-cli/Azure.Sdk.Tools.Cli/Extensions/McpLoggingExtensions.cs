// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Extensions;

public static class McpLoggingExtensions
{
    public static void ConfigureMcpLogging(this IServiceCollection services)
    {
        services.AddSingleton<ILoggerProvider, McpServerLoggerProvider>();
        services.AddHostedService<McpLoggingHostedService>();
    }
}
