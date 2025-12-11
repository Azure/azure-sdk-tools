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

        var activity = Parent.StartActivity(activityId, ActivityKind.Server);

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
