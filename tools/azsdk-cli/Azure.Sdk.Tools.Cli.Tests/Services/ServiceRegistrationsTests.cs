using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;
using GitHub.Copilot;
using Microsoft.Extensions.DependencyInjection;

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

    [TestCase(OutputHelper.OutputModes.Json)]
    [TestCase(OutputHelper.OutputModes.Plain)]
    [TestCase(OutputHelper.OutputModes.Mcp)]
    public void RegisterCommonServices_RegistersDotNetLanguageService(OutputHelper.OutputModes outputMode)
    {
        foreach (var envVar in new[] { "GITHUB_ACTIONS", "SYSTEM_TEAMPROJECTID" })
        {
            _savedEnvVars[envVar] = Environment.GetEnvironmentVariable(envVar);
            Environment.SetEnvironmentVariable(envVar, null);
        }
        var services = new ServiceCollection();

        ServiceRegistrations.RegisterCommonServices(services, outputMode);

        var registration = services.Single(descriptor =>
            descriptor.ServiceType == typeof(LanguageService) &&
            descriptor.ImplementationType == typeof(DotNetLanguageService));
        Assert.That(registration.Lifetime, Is.EqualTo(ServiceLifetime.Scoped));
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
