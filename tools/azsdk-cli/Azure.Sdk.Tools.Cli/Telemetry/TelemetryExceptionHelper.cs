// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
namespace Azure.Sdk.Tools.Cli.Telemetry;

internal static class TelemetryExceptionHelper
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

        activity.AddEvent(new ActivityEvent("exception", default, tags));
    }
}
