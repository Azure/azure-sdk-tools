using System.Text.Json;
using GitHub.Copilot;

namespace Azure.Sdk.Tools.Cli.Tests.CopilotAgents;

[TestFixture]
internal class CopilotSdkCompatibilityTests
{
    [Test]
    public void PingResponse_DeserializesIso8601Timestamp()
    {
        const string json = """
            {
              "message": "pong",
              "timestamp": "2026-05-21T08:29:54.042Z",
              "protocolVersion": 3
            }
            """;

        var response = JsonSerializer.Deserialize<PingResponse>(
            json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.That(response, Is.Not.Null);
        Assert.That(response.Timestamp, Is.EqualTo(DateTimeOffset.Parse("2026-05-21T08:29:54.042Z")));
    }
}
