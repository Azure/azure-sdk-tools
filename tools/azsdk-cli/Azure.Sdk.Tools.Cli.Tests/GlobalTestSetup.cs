namespace Azure.Sdk.Tools.Cli.Tests;

[SetUpFixture]
public sealed class GlobalTestSetup
{
    private string? originalTelemetrySetting;

    [OneTimeSetUp]
    public void BeforeAllTests()
    {
        originalTelemetrySetting = Environment.GetEnvironmentVariable("AZSDKTOOLS_COLLECT_TELEMETRY");
        Environment.SetEnvironmentVariable("AZSDKTOOLS_COLLECT_TELEMETRY", "false");
    }

    [OneTimeTearDown]
    public void AfterAllTests()
    {
        Environment.SetEnvironmentVariable("AZSDKTOOLS_COLLECT_TELEMETRY", originalTelemetrySetting);
    }
}
