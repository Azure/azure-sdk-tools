using System.Text.Json;
using GitHub.Copilot;

namespace Azure.Sdk.Tools.Cli.Tests.CopilotAgents;

[TestFixture]
internal class CopilotSdkCompatibilityTests
{
    [TestCase("2026-05-21T08:29:54.042Z", "2026-05-21T08:29:54.042+00:00")]
    [TestCase("2026-05-21T01:29:54.042-07:00", "2026-05-21T01:29:54.042-07:00")]
    public void PingResponse_DeserializesIso8601Timestamp(string timestamp, string expectedTimestamp)
    {
        var json = $$"""
            {
              "message": "pong",
              "timestamp": "{{timestamp}}",
              "protocolVersion": 3
            }
            """;

        var response = JsonSerializer.Deserialize<PingResponse>(
            json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.That(response, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(response.Message, Is.EqualTo("pong"));
            Assert.That(response.Timestamp, Is.EqualTo(DateTimeOffset.Parse(expectedTimestamp)));
            Assert.That(response.ProtocolVersion, Is.EqualTo(3));
        });
    }
}
