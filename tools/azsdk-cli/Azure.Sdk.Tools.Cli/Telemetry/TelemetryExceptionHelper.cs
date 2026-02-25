// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
namespace Azure.Sdk.Tools.Cli.Telemetry;

public static class TelemetryExceptionHelper
{
    public static void AddSanitizedException(Activity? activity, Exception ex)
    {
        if (activity == null)
        {
            return;
        }

        var tags = new ActivityTagsCollection
        {
            ["exception.type"] = ex.GetType().FullName,
            ["exception.message"] = TelemetryPathSanitizer.Sanitize(ex.Message ?? string.Empty),
        };

        if (!string.IsNullOrEmpty(ex.StackTrace))
        {
            tags["exception.stacktrace"] = TelemetryPathSanitizer.Sanitize(ex.StackTrace);
        }

        var innerDetails = BuildInnerExceptionDetails(ex.InnerException);
        if (!string.IsNullOrEmpty(innerDetails))
        {
            tags["exception.inner"] = innerDetails;
        }

        activity.AddEvent(new ActivityEvent("exception", default, tags));
    }

    private static string BuildInnerExceptionDetails(Exception? ex)
    {
        if (ex == null)
        {
            return string.Empty;
        }

        var parts = new List<string>();
        var current = ex;
        var depth = 0;
        while (current != null && depth < 5)
        {
            var message = TelemetryPathSanitizer.Sanitize(current.Message ?? string.Empty);
            if (!string.IsNullOrEmpty(message))
            {
                parts.Add($"Message: {message}");
            }

            if (!string.IsNullOrEmpty(current.StackTrace))
            {
                parts.Add($"StackTrace: {TelemetryPathSanitizer.Sanitize(current.StackTrace)}");
            }

            current = current.InnerException;
            depth++;
        }

        return string.Join(" | ", parts);
    }
}
