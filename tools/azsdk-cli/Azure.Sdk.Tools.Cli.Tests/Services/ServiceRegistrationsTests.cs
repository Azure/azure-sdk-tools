using Azure.Sdk.Tools.Cli.Services;
using GitHub.Copilot;

namespace Azure.Sdk.Tools.Cli.Tests.Services;

[TestFixture]
[NonParallelizable]
internal class ServiceRegistrationsTests
{
    private readonly Dictionary<string, string?> _savedEnvVars = new();

    [SetUp]
    public void SaveEnvironmentVariables()
    {
        foreach (var envVar in ServiceRegistrations.GitHubTokenEnvironmentVariables)
        {
            _savedEnvVars[envVar] = Environment.GetEnvironmentVariable(envVar);
            Environment.SetEnvironmentVariable(envVar, null);
        }
        _savedEnvVars["AZSDK_COPILOT_CLI_PATH"] = Environment.GetEnvironmentVariable("AZSDK_COPILOT_CLI_PATH");
    }

    [TearDown]
    public void RestoreEnvironmentVariables()
    {
        foreach (var (key, value) in _savedEnvVars)
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }

    [Test]
    public void CreateCopilotClientOptions_ReadsEnvironmentOverrides()
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

    [Test]
    public void ResolveGitHubToken_PrefersAzsdkToken()
    {
        Environment.SetEnvironmentVariable("AZSDK_COPILOT_GITHUB_TOKEN", "azsdk-token");
        Environment.SetEnvironmentVariable("COPILOT_GITHUB_TOKEN", "copilot-token");
        Environment.SetEnvironmentVariable("GH_TOKEN", "gh-token");
        Environment.SetEnvironmentVariable("GITHUB_TOKEN", "github-token");

        var token = ServiceRegistrations.ResolveGitHubToken();

        Assert.That(token, Is.EqualTo("azsdk-token"));
    }

    [Test]
    public void ResolveGitHubToken_FallsThroughToCopilotGithubToken()
    {
        Environment.SetEnvironmentVariable("COPILOT_GITHUB_TOKEN", "copilot-token");
        Environment.SetEnvironmentVariable("GH_TOKEN", "gh-token");

        var token = ServiceRegistrations.ResolveGitHubToken();

        Assert.That(token, Is.EqualTo("copilot-token"));
    }

    [Test]
    public void ResolveGitHubToken_FallsThroughToGhToken()
    {
        Environment.SetEnvironmentVariable("GH_TOKEN", "gh-token");
        Environment.SetEnvironmentVariable("GITHUB_TOKEN", "github-token");

        var token = ServiceRegistrations.ResolveGitHubToken();

        Assert.That(token, Is.EqualTo("gh-token"));
    }

    [Test]
    public void ResolveGitHubToken_FallsThroughToGithubToken()
    {
        Environment.SetEnvironmentVariable("GITHUB_TOKEN", "github-token");

        var token = ServiceRegistrations.ResolveGitHubToken();

        Assert.That(token, Is.EqualTo("github-token"));
    }

    [Test]
    public void ResolveGitHubToken_ReturnsNullWhenNoTokensSet()
    {
        var token = ServiceRegistrations.ResolveGitHubToken();

        Assert.That(token, Is.Null);
    }

    [Test]
    public void ResolveGitHubToken_SkipsWhitespaceOnlyTokens()
    {
        Environment.SetEnvironmentVariable("AZSDK_COPILOT_GITHUB_TOKEN", "   ");
        Environment.SetEnvironmentVariable("COPILOT_GITHUB_TOKEN", "");
        Environment.SetEnvironmentVariable("GH_TOKEN", "  valid-token  ");

        var token = ServiceRegistrations.ResolveGitHubToken();

        Assert.That(token, Is.EqualTo("valid-token"));
    }
}
