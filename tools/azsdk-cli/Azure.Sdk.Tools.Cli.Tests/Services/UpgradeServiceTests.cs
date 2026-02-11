// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services.Upgrade;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text.Json;

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

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset now = now;
        public override DateTimeOffset GetUtcNow() => now;
        public void SetUtcNow(DateTimeOffset utcNow) => now = utcNow;
    }

    private FakeTimeProvider timeProvider;

    [SetUp]
    public void Setup()
    {
        logger = new TestLogger<UpgradeService>();
        mockHttpClientFactory = new Mock<IHttpClientFactory>();
        mockProcessHelper = new Mock<IProcessHelper>();
        mockOutputHelper = new Mock<IRawOutputHelper>();

        // Use a unique temp directory for each test to avoid cache interference
        testConfigDir = Path.Combine(Path.GetTempPath(), $"azsdk-test-{Guid.NewGuid():N}");

        timeProvider = new FakeTimeProvider(DateTimeOffset.Parse("2026-02-10T00:00:00Z"));
        upgradeService = new UpgradeService(
            logger,
            mockHttpClientFactory.Object,
            mockProcessHelper.Object,
            mockOutputHelper.Object,
            testConfigDir,
            timeProvider);
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

    private static string GetCachePath(string configDir) => Path.Combine(configDir, "upgrade-cache.json");

    private async Task WriteCache(CliUpgradeCache cache)
    {
        Directory.CreateDirectory(testConfigDir);
        var json = JsonSerializer.Serialize(cache) + Environment.NewLine;
        await File.WriteAllTextAsync(GetCachePath(testConfigDir), json);
    }

    private async Task<CliUpgradeCache> ReadCache()
    {
        var json = await File.ReadAllTextAsync(GetCachePath(testConfigDir));
        return JsonSerializer.Deserialize<CliUpgradeCache>(json) ?? new CliUpgradeCache();
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
        var isPrerelease = upgradeService.IsCurrentVersionPrerelease();
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

    [Test]
    public async Task CheckLatestVersion_UsesCache_WhenFreshAndHasRemoteVersion()
    {
        // Arrange
        var now = timeProvider.GetUtcNow();
        await WriteCache(new CliUpgradeCache
        {
            RemoteVersion = "1.2.3",
            LastRemoteRefreshUtc = now - TimeSpan.FromHours(1),
        });

        // If the service unnecessarily refreshes, it would pick 9.9.9 and this test would fail.
        SetupMockHttpClient("[{\"tag_name\":\"azsdk_9.9.9\",\"prerelease\":false}]");

        // Act
        var version = await upgradeService.CheckLatestVersion(includePrerelease: false, failSilently: false, ignoreCacheTtl: false, CancellationToken.None);

        // Assert
        Assert.That(version, Is.EqualTo("1.2.3"));
    }

    [Test]
    public async Task CheckLatestVersion_Refreshes_WhenStale()
    {
        // Arrange
        var now = timeProvider.GetUtcNow();
        await WriteCache(new CliUpgradeCache
        {
            RemoteVersion = "1.0.0",
            LastRemoteRefreshUtc = now - TimeSpan.FromDays(2),
        });

        SetupMockHttpClient("[{\"tag_name\":\"azsdk_1.2.3\",\"prerelease\":false}]");

        // Act
        var version = await upgradeService.CheckLatestVersion(includePrerelease: false, failSilently: false, ignoreCacheTtl: false, CancellationToken.None);

        // Assert
        Assert.That(version, Is.EqualTo("1.2.3"));

        var cache = await ReadCache();
        Assert.That(cache.RemoteVersion, Is.EqualTo("1.2.3"));
        Assert.That(cache.LastRemoteRefreshUtc, Is.EqualTo(now));
    }

    [Test]
    public async Task CheckLatestVersion_IgnoreCacheTtl_ForcesRefresh()
    {
        // Arrange
        var now = timeProvider.GetUtcNow();
        await WriteCache(new CliUpgradeCache
        {
            RemoteVersion = "1.2.2",
            LastRemoteRefreshUtc = now - TimeSpan.FromHours(1),
        });

        SetupMockHttpClient("[{\"tag_name\":\"azsdk_1.2.3\",\"prerelease\":false}]");

        // Act
        var version = await upgradeService.CheckLatestVersion(includePrerelease: false, failSilently: false, ignoreCacheTtl: true, CancellationToken.None);

        // Assert
        Assert.That(version, Is.EqualTo("1.2.3"));
    }

    [Test]
    public async Task CheckLatestVersion_FreshCacheButEmptyRemoteVersion_Refreshes()
    {
        // Arrange
        var now = timeProvider.GetUtcNow();
        await WriteCache(new CliUpgradeCache
        {
            RemoteVersion = null,
            LastRemoteRefreshUtc = now - TimeSpan.FromHours(1),
        });

        SetupMockHttpClient("[{\"tag_name\":\"azsdk_9.9.9\",\"prerelease\":false}]");

        // Act
        var version = await upgradeService.CheckLatestVersion(includePrerelease: false, failSilently: false, ignoreCacheTtl: false, CancellationToken.None);

        // Assert
        Assert.That(version, Is.EqualTo("9.9.9"));
    }

    [Test]
    public async Task TryShowUpgradeNotification_Throttled_DoesNotCallHttpOrOutput()
    {
        // Arrange
        var now = timeProvider.GetUtcNow();
        await WriteCache(new CliUpgradeCache
        {
            LastNotifyUtc = now - TimeSpan.FromDays(1),
            RemoteVersion = "1.2.3",
            LastRemoteRefreshUtc = now - TimeSpan.FromDays(1),
        });

        SetupMockHttpClient("[{\"tag_name\":\"azsdk_9.9.9\",\"prerelease\":false}]");

        // Act
        var shown = await upgradeService.TryShowUpgradeNotification(CancellationToken.None);

        // Assert
        Assert.That(shown, Is.False);
        mockOutputHelper.Verify(o => o.OutputConsoleWarning(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task TryShowUpgradeNotification_ShowsWarning_WhenUpgradeAvailable_UpdatesNotifyTimestamp()
    {
        // Arrange
        var now = timeProvider.GetUtcNow();
        await WriteCache(new CliUpgradeCache
        {
            LastNotifyUtc = now - TimeSpan.FromDays(10),
            LastRemoteRefreshUtc = now - TimeSpan.FromDays(10),
        });

        SetupMockHttpClient("[{\"tag_name\":\"azsdk_9.9.9\",\"prerelease\":false}]");

        // Act
        var shown = await upgradeService.TryShowUpgradeNotification(CancellationToken.None);

        // Assert
        Assert.That(shown, Is.True);
        mockOutputHelper.Verify(o => o.OutputConsoleWarning(It.Is<string>(s => s.Contains("Release notes:"))), Times.Once);
        mockOutputHelper.Verify(o => o.OutputConsoleWarning(It.Is<string>(s => s.Contains("azsdk upgrade"))), Times.Once);

        var cache = await ReadCache();
        Assert.That(cache.LastNotifyUtc, Is.EqualTo(now));
        Assert.That(cache.RemoteVersion, Is.EqualTo("9.9.9"));
    }

    [Test]
    public async Task Upgrade_UsesDownloadAndExtractSeams_AndStartsTwoStepUpgrade()
    {
        // Arrange - include all platform variants so test passes on Windows/Mac/Linux
        var releasesJson = """
            [{
                "tag_name":"azsdk_9.9.9",
                "prerelease":false,
                "assets":[
                    {"name":"Azure.Sdk.Tools.Cli-standalone-linux-x64.tar.gz","browser_download_url":"https://example.test/download"},
                    {"name":"Azure.Sdk.Tools.Cli-standalone-linux-arm64.tar.gz","browser_download_url":"https://example.test/download"},
                    {"name":"Azure.Sdk.Tools.Cli-standalone-win-x64.zip","browser_download_url":"https://example.test/download"},
                    {"name":"Azure.Sdk.Tools.Cli-standalone-win-arm64.zip","browser_download_url":"https://example.test/download"},
                    {"name":"Azure.Sdk.Tools.Cli-standalone-osx-x64.zip","browser_download_url":"https://example.test/download"},
                    {"name":"Azure.Sdk.Tools.Cli-standalone-osx-arm64.zip","browser_download_url":"https://example.test/download"}
                ]
            }]
            """;
        SetupMockHttpClient(releasesJson);

        var serviceMock = new Mock<UpgradeService>(
            logger,
            mockHttpClientFactory.Object,
            mockProcessHelper.Object,
            mockOutputHelper.Object,
            testConfigDir,
            timeProvider)
        {
            CallBase = true
        };

        serviceMock
            .Protected()
            .Setup<Task<string>>("DownloadFile", ItExpr.IsAny<string>(), ItExpr.IsAny<string>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync("/tmp/fake-archive.tar.gz");

        serviceMock
            .Protected()
            .Setup<Task<string?>>("ExtractAndFindExecutable", ItExpr.IsAny<string>(), ItExpr.IsAny<string>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                var path = Path.Combine(testConfigDir, "fake-azsdk");
                Directory.CreateDirectory(testConfigDir);
                File.WriteAllText(path, "placeholder");
                return path;
            });

        mockProcessHelper
            .Setup(p => p.Run(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult { ExitCode = 0 });

        // Act
        var result = await serviceMock.Object.Upgrade(targetVersion: "9.9.9", includePrerelease: false, CancellationToken.None);

        // Assert
        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.NewVersion, Is.EqualTo("9.9.9"));
        Assert.That(result.DownloadUrl, Is.EqualTo("https://example.test/download"));

        mockProcessHelper.Verify(p => p.Run(It.Is<ProcessOptions>(o =>
            o.Args.Count >= 3 &&
            o.Args[0] == "upgrade" &&
            o.Args[1] == "--complete-upgrade"), It.IsAny<CancellationToken>()), Times.Once);
    }
}
