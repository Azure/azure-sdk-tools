using Azure.Sdk.Tools.Cli.Services;
using GitHub.Copilot;

namespace Azure.Sdk.Tools.Cli.Tests.Services;

[TestFixture]
[NonParallelizable]
internal class ServiceRegistrationsTests
{
    [Test]
    public void CreateCopilotClientOptions_ReadsEnvironmentOverrides()
    {
        var originalPath = Environment.GetEnvironmentVariable("AZSDK_COPILOT_CLI_PATH");
        var originalToken = Environment.GetEnvironmentVariable("AZSDK_COPILOT_GITHUB_TOKEN");
        try
        {
            Environment.SetEnvironmentVariable("AZSDK_COPILOT_CLI_PATH", "  test-copilot  ");
            Environment.SetEnvironmentVariable("AZSDK_COPILOT_GITHUB_TOKEN", "  test-token  ");

            var options = ServiceRegistrations.CreateCopilotClientOptions(logger: null);

            Assert.Multiple(() =>
            {
                Assert.That(options.GitHubToken, Is.EqualTo("test-token"));
                Assert.That(options.Connection, Is.TypeOf<StdioRuntimeConnection>());
                Assert.That(((StdioRuntimeConnection)options.Connection!).Path, Is.EqualTo("test-copilot"));
            });
        }
        finally
        {
            Environment.SetEnvironmentVariable("AZSDK_COPILOT_CLI_PATH", originalPath);
            Environment.SetEnvironmentVariable("AZSDK_COPILOT_GITHUB_TOKEN", originalToken);
        }
    }
}
