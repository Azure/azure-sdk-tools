// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json;

namespace Azure.Sdk.Tools.Cli.Services;

public static class TelemetryService
{
    private const int MaxInstrumentationUploadTime = 5;

    public static void InstrumentationBefore(ILogger logger, string toolName, object? args, CancellationToken ct)
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(MaxInstrumentationUploadTime));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
        Task.Run(() => _instrumentationBefore(logger, toolName, args), linkedCts.Token);
    }

    private static void _instrumentationBefore(ILogger logger, string toolName, object? args)
    {
        // TODO: replace with app insights
        logger.LogDebug("[tool req] {toolName} [args] {args}", toolName, args);
    }

    public static void InstrumentationAfter(ILogger logger, string toolName, object? result, CancellationToken ct)
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(MaxInstrumentationUploadTime));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
        Task.Run(() => _instrumentationAfter(logger, toolName, result), linkedCts.Token);
    }

    private static void _instrumentationAfter(ILogger logger, string toolName, object? result)
    {
        var serialized = "SERIALIZER ERROR";
        try
        {
            serialized = JsonSerializer.Serialize(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error serializing tool response for instrumentation");
        }

        // TODO: replace with app insights
        logger.LogDebug("[tool resp] {toolName} [result] {result}", toolName, serialized);
    }

    public static void InstrumentationError(ILogger logger, string toolName, Exception ex, CancellationToken ct)
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(MaxInstrumentationUploadTime));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
        // TODO: replace with app insights
        Task.Run(() => _instrumentationError(logger, toolName, ex), linkedCts.Token);
    }

    private static void _instrumentationError(ILogger logger, string toolName, Exception ex)
    {
        logger.LogError(ex, "[tool error] {toolName} [error] {error}", toolName, ex.Message);
    }

}
