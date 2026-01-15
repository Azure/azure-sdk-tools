// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using OpenTelemetry.Logs;
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
    private readonly TracerProvider _tracerProvider;
    private readonly IMachineInformationProvider _informationProvider;
    private readonly ILogger<TelemetryService> _logger;
    private readonly TaskCompletionSource _isInitialized = new TaskCompletionSource();

    internal ActivitySource Parent { get; }

    public TelemetryService(
        TracerProvider tracerProvider,
        ILogger<TelemetryService> logger,
        IMachineInformationProvider informationProvider,
        IOptions<AzSdkToolsMcpServerConfiguration> options
    )
    {
        _isEnabled = options.Value.IsTelemetryEnabled;
        _tagsList = [
            new(TagName.AzSdkToolVersion, options.Value.Version),
        ];

        Parent = new ActivitySource(options.Value.Name, options.Value.Version, _tagsList);

        _tracerProvider = tracerProvider;
        _logger = logger;
        _informationProvider = informationProvider;

        Task.Factory.StartNew(InitializeTagList);
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
        // TODO: switch above ActivityKind.Server so telemetry ends up in
        // the more appropriate requests table in azure monitor.
        // Currently the data pipeline is set up to read from the
        // dependencies table, so a dashboard migration needs to happen first.
        // var activity = Parent.StartActivity(activityId, ActivityKind.Server);

        if (activity == null)
        {
#if DEBUG
            // Fail fast if we're generating null activities so we can catch silent issues in development
            throw new Exception($"Failed to start activity '{activityId}' as there is no listener registered");
#else
            return activity;
#endif
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

    public void Flush()
    {
        // Do not block too long before exiting the CLI at the end of command execution,
        // even if the telemetry upload is not complete.
        // 2 seconds seems like a reasonable balance but most of the time the
        // upload delay should be very quick and not noticeable to users.
        _tracerProvider?.ForceFlush(2000);
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
