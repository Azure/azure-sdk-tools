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

public interface ITelemetryService : IDisposable
{
    ValueTask<Activity?> StartActivity(string activityName, CancellationToken ct);

    ValueTask<Activity?> StartActivity(string activityName, Implementation? clientInfo, CancellationToken ct);

    bool? Flush(int timeoutMilliseconds = 2000);
}

internal class TelemetryService : ITelemetryService
{
    private readonly bool _isEnabled;
    private readonly List<KeyValuePair<string, object?>> _tagsList;
    private readonly TracerProvider _tracerProvider;
    private readonly IMachineInformationProvider _informationProvider;
    private readonly ILogger<TelemetryService> _logger;
    private readonly TaskCompletionSource _isInitialized = new TaskCompletionSource();
    private readonly CancellationTokenSource _cts = new();

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

        Task.Factory.StartNew(() => InitializeTagList(_cts.Token));
    }

    public ValueTask<Activity?> StartActivity(string activityId, CancellationToken ct) => StartActivity(activityId, null, ct);

    public async ValueTask<Activity?> StartActivity(string activityId, Implementation? clientInfo, CancellationToken ct)
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

    public bool? Flush(int timeoutMilliseconds = 2000)
    {
        var sw = Stopwatch.StartNew();
        var result = _tracerProvider?.ForceFlush(timeoutMilliseconds);
        sw.Stop();

        if (result == false)
        {
            _logger.LogDebug(
                "Telemetry ForceFlush returned false after {ElapsedMs}ms (timeout: {TimeoutMs}ms). " +
                "Telemetry data may have been dropped because the HTTP export did not complete in time.",
                sw.ElapsedMilliseconds, timeoutMilliseconds);
        }
        else
        {
            _logger.LogDebug("Telemetry ForceFlush completed: result={Result}, elapsed={ElapsedMs}ms",
                result, sw.ElapsedMilliseconds);
        }

        return result;
    }

    public void Dispose()
    {
        _tracerProvider.Dispose();
        _cts.Cancel();
        _cts.Dispose();
    }

    private async Task InitializeTagList(CancellationToken ct)
    {
        try
        {
            var macAddressHash = await _informationProvider.GetMacAddressHash(ct);
            var deviceId = await _informationProvider.GetOrCreateDeviceId(ct);

            _tagsList.Add(new(TagName.MacAddressHash, macAddressHash));
            _tagsList.Add(new(TagName.DevDeviceId, deviceId));
#if DEBUG
            _tagsList.Add(new(TagName.DebugTag, "true"));
#endif

            _isInitialized.SetResult();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to collect machine information for telemetry tags. " +
                "Telemetry will continue with fallback values.");

#if DEBUG
            _tagsList.Add(new(TagName.DebugTag, "true"));
#endif

            // Use fallback values rather than blocking all telemetry
            _tagsList.Add(new(TagName.MacAddressHash, "unknown"));
            _tagsList.Add(new(TagName.DevDeviceId, "unknown"));
            _isInitialized.SetResult();
        }
    }
}
