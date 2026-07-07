// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services;
using Moq;
using Octokit;

namespace Azure.Sdk.Tools.Cli.Tests.Services;

[TestFixture]
public class GitHubServiceTests
{
    private string? originalGitHubToken;
    private string? originalGitHubPat;

    // Exposes the protected read-only client selection so it can be exercised in isolation.
    private sealed class TestableGitConnection : GitConnection
    {
        public TestableGitConnection(IProcessHelper processHelper) : base(processHelper) { }
        public GitHubClient GetReadOnlyClientForTest() => GetReadOnlyClient();
    }

    [SetUp]
    public void SetUp()
    {
        // Preserve and clear the auth environment variables so tests control token availability.
        originalGitHubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        originalGitHubPat = Environment.GetEnvironmentVariable("GITHUB_PERSONAL_ACCESS_TOKEN");
        Environment.SetEnvironmentVariable("GITHUB_TOKEN", null, EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable("GITHUB_PERSONAL_ACCESS_TOKEN", null, EnvironmentVariableTarget.Process);
    }

    [TearDown]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable("GITHUB_TOKEN", originalGitHubToken, EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable("GITHUB_PERSONAL_ACCESS_TOKEN", originalGitHubPat, EnvironmentVariableTarget.Process);
    }

    [Test]
    public void GetReadOnlyClient_returns_anonymous_client_when_no_auth_token_available()
    {
        // Simulate `gh auth token` failing (user not authenticated).
        var processHelperMock = new Mock<IProcessHelper>();
        processHelperMock
            .Setup(x => x.Run(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult { ExitCode = 1 });

        var connection = new TestableGitConnection(processHelperMock.Object);

        var client = connection.GetReadOnlyClientForTest();

        Assert.That(client.Credentials.AuthenticationType, Is.EqualTo(AuthenticationType.Anonymous));
    }

    [Test]
    public void GetReadOnlyClient_returns_authenticated_client_when_token_available()
    {
        Environment.SetEnvironmentVariable("GITHUB_TOKEN", "fake-token", EnvironmentVariableTarget.Process);
        var processHelperMock = new Mock<IProcessHelper>();

        var connection = new TestableGitConnection(processHelperMock.Object);

        var client = connection.GetReadOnlyClientForTest();

        Assert.That(client.Credentials.AuthenticationType, Is.EqualTo(AuthenticationType.Bearer));
        // `gh auth token` should not be invoked when a token is already present in the environment.
        processHelperMock.Verify(x => x.Run(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
