using System.Diagnostics;
using Azure.Sdk.Tools.Cli.Telemetry;
using NUnit.Framework;

namespace Azure.Sdk.Tools.Cli.Tests.Telemetry;

public class TelemetryExceptionHelperTests
{
    [Test]
    public void AddSanitizedException_RedactsPathsIncludingInnerExceptions()
    {
        using var activity = new Activity("test");
        activity.Start();

        var inner = new InvalidOperationException("Inner at /Users/ben/private/inner.txt");
        var ex = new Exception("Outer at /Users/ben/private/outer.txt", inner);

        TelemetryExceptionHelper.AddSanitizedException(activity, ex);

        var exceptionEvent = activity.Events.Single(e => e.Name == "exception");
        var tags = exceptionEvent.Tags.ToDictionary(t => t.Key, t => t.Value?.ToString());

        Assert.That(tags["exception.message"], Does.Contain(TelemetryPathSanitizer.Redacted));
        Assert.That(tags["exception.inner"], Does.Contain(TelemetryPathSanitizer.Redacted));
    }
}
