using Azure.Sdk.Tools.Cli.Telemetry;
using NUnit.Framework;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers;

public class TelemetryPathSanitizerTests
{
    [TestCase("specification/service", "[PATH REDACTED]/specification/service")]
    [TestCase("azure-rest-api-specs/specification/service", "[PATH REDACTED]/azure-rest-api-specs/specification/service")]
    [TestCase("/Users/ben/specification/service", "[PATH REDACTED]/specification/service")]
    [TestCase(@"C:\Users\ben\azure-sdk-for-net\sdk\storage", @"[PATH REDACTED]\azure-sdk-for-net\sdk\storage")]
    public void Sanitize_PreservesAllowlistedSegments(string input, string expected)
    {
        var sanitized = TelemetryPathSanitizer.Sanitize(input);

        Assert.That(sanitized, Is.EqualTo(expected));
    }

    [Test]
    public void Sanitize_RedactsNonAllowlistedPath()
    {
        var input = "/Users/ben/private/file.txt";

        var sanitized = TelemetryPathSanitizer.Sanitize(input);

        Assert.That(sanitized, Is.EqualTo(TelemetryPathSanitizer.Redacted));
    }

    [Test]
    public void Sanitize_DoesNotChangeUrls()
    {
        var input = "https://example.com/specification/service";

        var sanitized = TelemetryPathSanitizer.Sanitize(input);

        Assert.That(sanitized, Is.EqualTo(input));
    }

    [Test]
    public void Sanitize_PreservesPunctuationAroundPaths()
    {
        var input = "Failed to open /Users/ben/private/file.txt, please retry.";

        var sanitized = TelemetryPathSanitizer.Sanitize(input);

        Assert.That(sanitized, Is.EqualTo($"Failed to open {TelemetryPathSanitizer.Redacted}, please retry."));
    }

    [Test]
    public void Sanitize_UsesKnownRootPrefix()
    {
        TelemetryPathSanitizer.AddKnownRoot("/repo/root");
        var input = "/repo/root/specification/service";

        var sanitized = TelemetryPathSanitizer.Sanitize(input);

        Assert.That(sanitized, Is.EqualTo("[PATH REDACTED]/specification/service"));
    }

    [Test]
    public void Sanitize_UsesDynamicAllowlistedSegment()
    {
        TelemetryPathSanitizer.AddAllowlistedSegment("custom-repo");
        var input = "/tmp/custom-repo/src/file.cs";

        var sanitized = TelemetryPathSanitizer.Sanitize(input);

        Assert.That(sanitized, Is.EqualTo("[PATH REDACTED]/custom-repo/src/file.cs"));
    }

    [Test]
    public void Sanitize_IgnoresJsonEscapeSequences()
    {
        var input = "{\"message\":\"RESPONDING TO \\u0027foo\\u0027 with SUCCESS: 0\",\"duration\":1}";

        var sanitized = TelemetryPathSanitizer.Sanitize(input);

        Assert.That(sanitized, Is.EqualTo(input));
    }
}
