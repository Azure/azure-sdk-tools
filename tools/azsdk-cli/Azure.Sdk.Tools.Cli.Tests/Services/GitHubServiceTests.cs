// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Net;
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

    // Exposes the protected anonymous-first read helper so it can be exercised in isolation.
    private sealed class TestableGitConnection : GitConnection
    {
        public TestableGitConnection(IProcessHelper processHelper) : base(processHelper) { }
        public Task<T> ReadWithAnonymousFallbackForTest<T>(Func<GitHubClient, Task<T>> operation)
            => ReadWithAnonymousFallbackAsync(operation, CancellationToken.None);
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
    public async Task ReadWithAnonymousFallback_UsesAnonymousClient_WhenPublicReadSucceeds()
    {
        var processHelperMock = new Mock<IProcessHelper>();
        var connection = new TestableGitConnection(processHelperMock.Object);

        var usedAuthType = await connection.ReadWithAnonymousFallbackForTest(
            client => Task.FromResult(client.Credentials.AuthenticationType));

        Assert.That(usedAuthType, Is.EqualTo(AuthenticationType.Anonymous));
        // No authentication was attempted for a successful anonymous read.
        processHelperMock.Verify(x => x.Run(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ReadWithAnonymousFallback_RetriesAuthenticated_WhenAnonymousRequiresAuth()
    {
        // A token is available in the environment, so the authenticated fallback can succeed.
        Environment.SetEnvironmentVariable("GITHUB_TOKEN", "fake-token", EnvironmentVariableTarget.Process);
        var processHelperMock = new Mock<IProcessHelper>();
        var connection = new TestableGitConnection(processHelperMock.Object);

        // Simulate GitHub hiding a private resource as Not Found for the anonymous client.
        var usedAuthType = await connection.ReadWithAnonymousFallbackForTest(client =>
        {
            if (client.Credentials.AuthenticationType == AuthenticationType.Anonymous)
            {
                throw new NotFoundException("Not Found", HttpStatusCode.NotFound);
            }
            return Task.FromResult(client.Credentials.AuthenticationType);
        });

        Assert.That(usedAuthType, Is.EqualTo(AuthenticationType.Bearer));
    }

    [Test]
    public void ReadWithAnonymousFallback_PromptsForAuth_WhenRequiredAndNoToken()
    {
        // No token in the environment and `gh auth token` fails, so the authenticated fallback must
        // surface the standard authentication guidance instead of silently swallowing the failure.
        var processHelperMock = new Mock<IProcessHelper>();
        processHelperMock
            .Setup(x => x.Run(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult { ExitCode = 1 });
        var connection = new TestableGitConnection(processHelperMock.Object);

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await connection.ReadWithAnonymousFallbackForTest<int>(client =>
            {
                if (client.Credentials.AuthenticationType == AuthenticationType.Anonymous)
                {
                    throw new NotFoundException("Not Found", HttpStatusCode.NotFound);
                }
                return Task.FromResult(0);
            }));
    }
}
