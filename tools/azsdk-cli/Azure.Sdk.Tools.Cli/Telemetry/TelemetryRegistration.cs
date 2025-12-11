// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using System.Runtime.InteropServices;
using Azure.Monitor.OpenTelemetry.Exporter;
using Azure.Sdk.Tools.Cli.Configuration;
using Azure.Sdk.Tools.Cli.Telemetry.InformationProvider;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Azure.Sdk.Tools.Cli.Telemetry;

internal enum TelemetryMode
{
    Cli,
    McpServer,
}

internal static class TelemetryRegistration
{
    private const string DefaultAppInsightsConnectionString = "InstrumentationKey=cf8756d3-ef86-4365-9da3-c3df9d28b1d3;IngestionEndpoint=https://eastus-8.in.applicationinsights.azure.com/;LiveEndpoint=https://eastus.livediagnostics.monitor.azure.com/;ApplicationId=9dd94d04-d58f-4d70-b9b2-40682cd7b3e7";

    internal static void AddTelemetry(this IServiceCollection services, bool debug)
    {
        ConfigureTelemetryOptions(services);
        RegisterMachineInformationProvider(services);
        services.AddSingleton<ITelemetryService, TelemetryService>();

        var telemetryEnabled = IsTelemetryEnabled();

        var openTelemetry = services.AddOpenTelemetry()
            .ConfigureResource(ConfigureResource)
            .WithTracing(builder =>
            {
                builder.AddSource(Constants.TOOLS_ACTIVITY_SOURCE)
                    .AddHttpClientInstrumentation()
                    .AddProcessor(new TelemetryProcessor());

                // Not necessary for CLI mode but should be harmless since we won't run asp.net
                builder.AddAspNetCoreInstrumentation();

                if (debug)
                {
                    builder.AddConsoleExporter();
                }

                if (telemetryEnabled)
                {
                    builder.AddAzureMonitorTraceExporter(options =>
                    {
                        options.ConnectionString = GetAppInsightsConnectionString();
                    });
                }
            });
    }

    private static void ConfigureTelemetryOptions(IServiceCollection services)
    {
        services.AddOptions<AzSdkToolsMcpServerConfiguration>()
            .Configure(options =>
            {
                var entryAssembly = Assembly.GetEntryAssembly();
                var assemblyName = entryAssembly?.GetName() ?? new AssemblyName();
                if (assemblyName.Version != null)
                {
                    options.Version = assemblyName.Version.ToString();
                }

                options.IsTelemetryEnabled = IsTelemetryEnabled();
            });
    }

    private static void RegisterMachineInformationProvider(IServiceCollection services)
    {
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
    }

    private static void ConfigureResource(ResourceBuilder resource)
    {
        var version = Assembly.GetExecutingAssembly()?.GetName()?.Version?.ToString();
        resource.AddService(Constants.TOOLS_ACTIVITY_SOURCE, version);
        resource.AddTelemetrySdk();
    }

    private static string GetAppInsightsConnectionString()
    {
        var appInsightsConnectionString = Environment.GetEnvironmentVariable("AZSDKTOOLS_APPLICATIONINSIGHTS_CONNECTION_STRING");
        if (string.IsNullOrEmpty(appInsightsConnectionString))
        {
            return DefaultAppInsightsConnectionString;
        }
        return appInsightsConnectionString;
    }

    private static bool IsTelemetryEnabled()
    {
#if DEBUG
        // Skip telemetry export for local development (dotnet run mode)
        return false;
#else
        var telemetryEnv = Environment.GetEnvironmentVariable("AZSDKTOOLS_COLLECT_TELEMETRY");
        return string.IsNullOrEmpty(telemetryEnv) || (bool.TryParse(telemetryEnv, out var parsed) && parsed);
#endif
    }
}
