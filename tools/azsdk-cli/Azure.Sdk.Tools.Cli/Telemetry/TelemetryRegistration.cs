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
    // Used for runs of the released executable.
    // App insights owned by datax team
    private const string DefaultAppInsightsConnectionString = "InstrumentationKey=61976f7a-4734-47a1-9fa0-0d5dcfda7f11;IngestionEndpoint=https://centralus-2.in.applicationinsights.azure.com/;LiveEndpoint=https://centralus.livediagnostics.monitor.azure.com/;ApplicationId=b22875b9-495e-4a5f-925a-a8b3b28ab441";

    // Used for development that way if there are regressions to telemetry uploads
    // we can observe them before shipping a release executable.
    // AzSdkToolsMcpAppInsights in the 'Azure SDK Engineering System' subscription
    private const string DebugAppInsightsConnectionString = "InstrumentationKey=30dc97fc-a02e-4ac2-b42b-3984be8e8617;IngestionEndpoint=https://westus3-1.in.applicationinsights.azure.com/;LiveEndpoint=https://westus3.livediagnostics.monitor.azure.com/;ApplicationId=6852648c-9710-45ec-8470-24738c518ae6";

    internal static void AddTelemetry(this IServiceCollection services, bool debug)
    {
        ConfigureTelemetryOptions(services);
        RegisterMachineInformationProvider(services);
        services.AddSingleton<ITelemetryService, TelemetryService>();

        var telemetryEnabled = IsTelemetryEnabled();

        services
            .AddOpenTelemetry()
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
        services
            .AddOptions<AzSdkToolsMcpServerConfiguration>()
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
#if DEBUG
        // In development mode with dotnet run, upload telemetry to our testing app insights
        return DebugAppInsightsConnectionString;
#else
        return DefaultAppInsightsConnectionString;
#endif
    }

    private static bool IsTelemetryEnabled()
    {
        var telemetryEnv = Environment.GetEnvironmentVariable("AZSDKTOOLS_COLLECT_TELEMETRY");
        return string.IsNullOrEmpty(telemetryEnv) || (bool.TryParse(telemetryEnv, out var parsed) && parsed);
    }
}
