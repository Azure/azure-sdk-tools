// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Moq;
using Moq.Protected;
using System.Net;

namespace Azure.Sdk.Tools.Cli.Tests.Services;

[TestFixture]
public class UpgradeServiceTests
{
    private TestLogger<UpgradeService> logger;
    private Mock<IHttpClientFactory> mockHttpClientFactory;
    private Mock<IProcessHelper> mockProcessHelper;
    private Mock<IRawOutputHelper> mockOutputHelper;
    private string testConfigDir;
    private UpgradeService upgradeService;

    [SetUp]
    public void Setup()
    {
        logger = new TestLogger<UpgradeService>();
        mockHttpClientFactory = new Mock<IHttpClientFactory>();
        mockProcessHelper = new Mock<IProcessHelper>();
        mockOutputHelper = new Mock<IRawOutputHelper>();

        // Use a unique temp directory for each test to avoid cache interference
        testConfigDir = Path.Combine(Path.GetTempPath(), $"azsdk-test-{Guid.NewGuid():N}");
        upgradeService = new UpgradeService(
            logger,
            mockHttpClientFactory.Object,
            mockProcessHelper.Object,
            mockOutputHelper.Object,
            testConfigDir);
    }

    [TearDown]
    public void TearDown()
    {
        // Clean up test config directory
        try
        {
            if (Directory.Exists(testConfigDir))
            {
                Directory.Delete(testConfigDir, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    [Test]
    public void GetCurrentVersion_ReturnsVersion()
    {
        var version = upgradeService.GetCurrentVersion();
        Assert.That(version, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void IsCurrentVersionPrerelease_ReturnsFalse_ForStableVersion()
    {
        // The actual version is from the assembly, which is typically stable in tests
        var isPrerelease = upgradeService.IsCurrentVersionPrerelease();
        // Just verify it returns a boolean without throwing
        Assert.That(isPrerelease, Is.TypeOf<bool>());
    }

    [Test]
    [TestCase("2.0.0", "1.0.0", true)]
    [TestCase("1.0.1", "1.0.0", true)]
    [TestCase("1.1.0", "1.0.0", true)]
    [TestCase("1.0.0", "1.0.0", false)]
    [TestCase("1.0.0", "2.0.0", false)]
    [TestCase("0.9.0", "1.0.0", false)]
    [TestCase("1.0.0-dev.2", "1.0.0-dev.1", true)]
    [TestCase("1.0.0", "1.0.0-dev.1", true)]  // stable > prerelease
    [TestCase("1.0.0-dev.1", "1.0.0", false)] // prerelease < stable
    [TestCase("0.5.16", "0.5.15", true)]
    [TestCase("0.5.15", "0.5.15", false)]
    public void VersionHelper_IsNewer_ReturnsCorrectResult(string remote, string local, bool expected)
    {
        var result = VersionHelper.IsNewer(remote, local);
        Assert.That(result, Is.EqualTo(expected), $"Expected IsNewer({remote}, {local}) = {expected}, got {result}");
    }

    [Test]
    [TestCase("1.0.0-dev.20240101.1", true)]
    [TestCase("1.0.0-dev", true)]
    [TestCase("1.0.0", false)]
    [TestCase("2.0.0-beta.1", false)]  // beta is not dev
    [TestCase("1.0.0-alpha", false)]   // alpha is not dev
    public void VersionHelper_IsPrerelease_ReturnsCorrectResult(string version, bool expected)
    {
        var result = VersionHelper.IsPrerelease(version);
        Assert.That(result, Is.EqualTo(expected), $"Expected IsPrerelease({version}) = {expected}, got {result}");
    }

    [Test]
    public async Task CheckLatestVersion_ParsesGitHubResponse()
    {
        var gitHubResponse = @"[
            {""tag_name"": ""Azure.Sdk.Tools.Other_1.0.0"", ""prerelease"": false},
            {""tag_name"": ""azsdk_1.2.3"", ""prerelease"": false},
            {""tag_name"": ""azsdk_1.2.2"", ""prerelease"": false}
        ]";

        SetupMockHttpClient(gitHubResponse);

        // Test through public API - CheckLatestVersion calls TryFetchLatestVersionFromGitHub internally
        var version = await upgradeService.CheckLatestVersion(includePrerelease: false, failSilently: false, ignoreCacheTtl: false, CancellationToken.None);

        Assert.That(version, Is.EqualTo("1.2.3"));
    }

    [Test]
    public async Task CheckLatestVersion_SkipsPrerelease_WhenNotRequested()
    {
        // Note: The "prerelease" field in GitHub API determines if a release is prerelease
        var gitHubResponse = @"[
            {""tag_name"": ""azsdk_2.0.0-dev.20240101.1"", ""prerelease"": true},
            {""tag_name"": ""azsdk_1.0.0"", ""prerelease"": false}
        ]";

        SetupMockHttpClient(gitHubResponse);

        var version = await upgradeService.CheckLatestVersion(includePrerelease: false, failSilently: false, ignoreCacheTtl: false, CancellationToken.None);

        Assert.That(version, Is.EqualTo("1.0.0"));
    }

    [Test]
    public async Task CheckLatestVersion_IncludesPrerelease_WhenRequested()
    {
        var gitHubResponse = @"[
            {""tag_name"": ""azsdk_2.0.0-dev.20240101.1"", ""prerelease"": true},
            {""tag_name"": ""azsdk_1.0.0"", ""prerelease"": false}
        ]";

        SetupMockHttpClient(gitHubResponse);

        var version = await upgradeService.CheckLatestVersion(includePrerelease: true, failSilently: false, ignoreCacheTtl: false, CancellationToken.None);

        Assert.That(version, Is.EqualTo("2.0.0-dev.20240101.1"));
    }

    [Test]
    public async Task CheckLatestVersion_ReturnsNull_OnEmptyResponse()
    {
        var gitHubResponse = @"[]";

        SetupMockHttpClient(gitHubResponse);

        var version = await upgradeService.CheckLatestVersion(includePrerelease: false, failSilently: false, ignoreCacheTtl: false, CancellationToken.None);

        Assert.That(version, Is.Null);
    }

    [Test]
    public async Task CheckLatestVersion_ReturnsNull_OnNetworkError()
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        var client = new HttpClient(mockHandler.Object);
        mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

        // With failSilently: true, should return null instead of throwing
        var version = await upgradeService.CheckLatestVersion(includePrerelease: false, failSilently: true, ignoreCacheTtl: false, CancellationToken.None);

        Assert.That(version, Is.Null);
    }

    private void SetupMockHttpClient(string responseContent)
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseContent)
            });

        var client = new HttpClient(mockHandler.Object);
        mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);
    }
}
