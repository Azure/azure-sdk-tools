// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using System.Runtime.InteropServices;
using Azure.Sdk.Tools.Cli.Configuration;
using Azure.Sdk.Tools.Cli.Telemetry;
using Azure.Sdk.Tools.Cli.Telemetry.InformationProvider;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Azure.Sdk.Tools.Cli.Extensions;

public static class OpenTelemetryExtensions
{
    private const string DefaultAppInsights = "InstrumentationKey=61976f7a-4734-47a1-9fa0-0d5dcfda7f11;IngestionEndpoint=https://centralus-2.in.applicationinsights.azure.com/;LiveEndpoint=https://centralus.livediagnostics.monitor.azure.com/;ApplicationId=b22875b9-495e-4a5f-925a-a8b3b28ab441";

    public static void ConfigureOpenTelemetry(this IServiceCollection services)
    {
        services.AddOptions<AzSdkToolsMcpServerConfiguration>()
            .Configure(options =>
            {
                var entryAssembly = Assembly.GetEntryAssembly();
                var assemblyName = entryAssembly?.GetName() ?? new AssemblyName();
                if (assemblyName?.Version != null)
                {
                    options.Version = assemblyName.Version.ToString();
                }

                var collectTelemetry = Environment.GetEnvironmentVariable("AZSDKTOOLS_MCP_COLLECT_TELEMETRY");
                options.IsTelemetryEnabled = string.IsNullOrEmpty(collectTelemetry)
                    || (bool.TryParse(collectTelemetry, out var shouldCollect) && shouldCollect);
            });

        services.AddSingleton<ITelemetryService, TelemetryService>();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            services.AddSingleton<IMachineInformationProvider, WindowsMachineInformationProvider>();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            services.AddSingleton<IMachineInformationProvider, MacOSXMachineInformationProvider>();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            services.AddSingleton<IMachineInformationProvider, LinuxMachineInformationProvider>();
        }
        else
        {
            services.AddSingleton<IMachineInformationProvider, DefaultMachineInformationProvider>();
        }

        EnableAzureMonitor(services);
    }

    public static void ConfigureOpenTelemetryLogger(this ILoggingBuilder builder)
    {
        builder.AddOpenTelemetry(logger =>
        {
            logger.AddProcessor(new TelemetryLogRecordEraser());
        });
    }

    private static void EnableAzureMonitor(this IServiceCollection services)
    {
#if DEBUG
        services.AddSingleton(sp =>
        {
            var forwarder = new AzureEventSourceLogForwarder(sp.GetRequiredService<ILoggerFactory>());
            forwarder.Start();
            return forwarder;
        });
#endif

        var appInsightsConnectionString = Environment.GetEnvironmentVariable("AZSDKTOOLS_MCP_APPLICATIONINSIGHTS_CONNECTION_STRING");
        if (string.IsNullOrEmpty(appInsightsConnectionString))
        {
            // Don't use default app insights when running in agent test mode.
            var agentTesting = Environment.GetEnvironmentVariable("AZSDKTOOLS_AGENT_TESTING");
            if (bool.TryParse(agentTesting, out var isAgentTesting) && isAgentTesting)
            {
                return;
            }
            appInsightsConnectionString = DefaultAppInsights;
        }

        services.ConfigureOpenTelemetryTracerProvider((sp, builder) =>
        {
            var serverConfig = sp.GetRequiredService<IOptions<AzSdkToolsMcpServerConfiguration>>();
            if (!serverConfig.Value.IsTelemetryEnabled)
            {
                return;
            }

            builder.AddSource(serverConfig.Value.Name);
        });

        services.AddOpenTelemetry()
            .ConfigureResource(r =>
            {
                var version = Assembly.GetExecutingAssembly()?.GetName()?.Version?.ToString();

                r.AddService(Constants.TOOLS_ACTIVITY_SOURCE, version)
                    .AddTelemetrySdk();
            })
            .UseAzureMonitorExporter(options =>
            {
#if DEBUG
                options.EnableLiveMetrics = true;
                options.Diagnostics.IsLoggingEnabled = true;
                options.Diagnostics.IsLoggingContentEnabled = true;
#endif
                options.ConnectionString = appInsightsConnectionString;
            });
    }
}
