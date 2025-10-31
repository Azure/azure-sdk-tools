// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Azure.Sdk.Tools.Cli.Configuration;
using Azure.Sdk.Tools.Cli.Telemetry.InformationProvider;
using static Azure.Sdk.Tools.Cli.Telemetry.TelemetryConstants;

namespace Azure.Sdk.Tools.Cli.Telemetry;
/// <summary>
/// Provides access to services.
/// </summary>
internal class TelemetryService : ITelemetryService
{
    private readonly bool _isEnabled;
    private readonly List<KeyValuePair<string, object?>> _tagsList;
    private readonly IMachineInformationProvider _informationProvider;
    private readonly TaskCompletionSource _isInitialized = new TaskCompletionSource();

    internal ActivitySource Parent { get; }

    public TelemetryService(IMachineInformationProvider informationProvider, IOptions<AzSdkToolsMcpServerConfiguration> options)
    {
        _isEnabled = options.Value.IsTelemetryEnabled;
        _tagsList = new List<KeyValuePair<string, object?>>()
        {
            new(TagName.AzSdkToolVersion, options.Value.Version),
        };

        Parent = new ActivitySource(options.Value.Name, options.Value.Version, _tagsList);
        _informationProvider = informationProvider;

        Task.Factory.StartNew(InitializeTagList);
    }

    public static TracerProvider RegisterCliTelemetry(bool debug)
    {
        var builder = OpenTelemetry.Sdk.CreateTracerProviderBuilder();
        builder
            .AddSource(Constants.TOOLS_ACTIVITY_SOURCE)
            .SetSampler(new AlwaysOnSampler())
            .AddProcessor(new TelemetryProcessor());

#if !DEBUG
            // Only upload telemetry when not in debug mode
            builder.AddOtlpExporter(otlp =>
                otlp.ExportProcessorType = ExportProcessorType.Simple
            );
#endif

        // output to console when --debug is passed (separate from dotnet debug build/config mode)
        if (debug)
        {
            builder.AddConsoleExporter();
        }

        return builder.Build();
    }

    public static void RegisterServerTelemetry(IServiceCollection services, bool debug = false)
    {
        var builder = services.AddOpenTelemetry()
            .WithTracing(b =>
            {
                b.AddSource(Constants.TOOLS_ACTIVITY_SOURCE)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddProcessor(new TelemetryProcessor());
                if (debug) { b.AddConsoleExporter(); }
            });

#if !DEBUG
            // Only upload telemetry when not in debug mode
            builder.UseOtlpExporter();
#endif
    }

    public ValueTask<Activity?> StartActivity(string activityId) => StartActivity(activityId, null);

    public async ValueTask<Activity?> StartActivity(string activityId, Implementation? clientInfo)
    {
        if (!_isEnabled)
        {
            return null;
        }

        await _isInitialized.Task;

        var activity = Parent.StartActivity(activityId);

        if (activity == null)
        {
            return activity;
        }

        if (clientInfo != null)
        {
            activity.AddTag(TagName.ClientName, clientInfo.Name)
                .AddTag(TagName.ClientVersion, clientInfo.Version);
        }

        activity.AddTag(TagName.EventId, Guid.NewGuid().ToString());

        _tagsList.ForEach(kvp => activity.AddTag(kvp.Key, kvp.Value));

        return activity;
    }

    public void Dispose()
    {
    }

    private async Task InitializeTagList()
    {
        try
        {
            var macAddressHash = await _informationProvider.GetMacAddressHash();
            var deviceId = await _informationProvider.GetOrCreateDeviceId();

            _tagsList.Add(new(TagName.MacAddressHash, macAddressHash));
            _tagsList.Add(new(TagName.DevDeviceId, deviceId));
#if DEBUG
            _tagsList.Add(new(TagName.DebugTag, "true"));
#endif

            _isInitialized.SetResult();
        }
        catch (Exception ex)
        {
            _isInitialized.SetException(ex);
        }
    }
}
