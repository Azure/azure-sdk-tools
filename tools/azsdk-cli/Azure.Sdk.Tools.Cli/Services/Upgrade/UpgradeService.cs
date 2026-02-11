// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services.Upgrade;

public class UpgradeService(
    ILogger<UpgradeService> logger,
    IHttpClientFactory httpClientFactory,
    IProcessHelper processHelper,
    IRawOutputHelper outputHelper,
    string? configDirectoryOverride = null,
    TimeProvider? timeProvider = null) : IUpgradeService
{
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;

    private const string UpgradeFullCommandName = SharedCommandNames.BaseExecutableName + " " + SharedCommandNames.UpgradeCommandName;
    private const string ConfigDirectoryName = "." + SharedCommandNames.BaseExecutableName;
    private const string CacheFileName = "upgrade-cache.json";
    private const string TempDownloadDirectory = "azsdk-upgrade";
    private const string GitHubReleasesUrl = "https://api.github.com/repos/Azure/azure-sdk-tools/releases";
    private const string ReleaseTagPrefix = SharedCommandNames.BaseExecutableName + "_";
    private const string AssetFileNamePrefix = "Azure.Sdk.Tools.Cli-standalone-";

    private static readonly TimeSpan networkTimeout = TimeSpan.FromMilliseconds(3000);
    private static readonly TimeSpan remoteRefreshTtl = TimeSpan.FromDays(1);
    private static readonly TimeSpan notifyThrottleTtl = TimeSpan.FromDays(3);

    private HttpClient? _gitHubHttpClientValue;
    private HttpClient gitHubHttpClient => _gitHubHttpClientValue ??= createGitHubClient();

    private HttpClient createGitHubClient()
    {
        // Use the named overload to avoid reliance on the CreateClient() extension method.
        // This makes the dependency mockable in unit tests.
        var client = httpClientFactory.CreateClient(nameof(UpgradeService));
        client.DefaultRequestHeaders.Add("User-Agent", SharedCommandNames.BaseExecutableName);
        client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
        return client;
    }

    private static readonly JsonSerializerOptions jsonOptions = new()
    {
        WriteIndented = true
    };

    public string GetCurrentVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "0.0.0";
        return version;
    }

    public bool IsCurrentVersionPrerelease()
    {
        return VersionHelper.IsPrerelease(GetCurrentVersion());
    }

    public async Task<string?> CheckLatestVersion(bool includePrerelease, bool failSilently, bool ignoreCacheTtl, CancellationToken ct)
    {
        var configDir = FindOrCreateConfigDirectory();
        if (configDir == null)
        {
            logger.LogWarning("Config directory unavailable, skipping upgrade check");
            return null;
        }

        var cache = await LoadCache(configDir, ct);
        var hasUpgrades = await UpdateVersionCache(cache, includePrerelease, failSilently, ignoreCacheTtl, timeout: null, ct);
        if (hasUpgrades)
        {
            await SaveCache(configDir, cache, ct);
        }

        return cache.RemoteVersion;
    }

    public async Task<bool> TryShowUpgradeNotification(CancellationToken ct)
    {
        try
        {
            var configDir = FindOrCreateConfigDirectory();
            if (configDir == null)
            {
                return false;
            }

            var cache = await LoadCache(configDir, ct);
            var now = timeProvider.GetUtcNow();

            if (cache.LastNotifyUtc.HasValue && now - cache.LastNotifyUtc.Value < notifyThrottleTtl)
            {
                logger.LogDebug("Version upgrade notification throttled, last notified: {LastNotify}", cache.LastNotifyUtc);
                return false;
            }

            var includePrerelease = IsCurrentVersionPrerelease();

            await UpdateVersionCache(cache, includePrerelease, failSilently: true, ignoreCacheTtl: false, timeout: null, ct);

            var latestVersion = cache.RemoteVersion;
            if (string.IsNullOrEmpty(latestVersion))
            {
                // Still save to update LastRemoteRefreshUtc (prevents hammering on failure)
                await SaveCache(configDir, cache, ct);
                return false;
            }

            var currentVersion = GetCurrentVersion();
            if (!VersionHelper.IsNewer(latestVersion, currentVersion))
            {
                logger.LogDebug("Current version {Current} is up to date with {Latest}", currentVersion, latestVersion);
                await SaveCache(configDir, cache, ct);
                return false;
            }

            // Show notification in yellow (output to stderr so it doesn't interfere with command output)
            var releaseNotesUrl = GetReleaseNotesUrl(latestVersion);
            var message = new StringBuilder()
                .AppendLine($"A new version of {SharedCommandNames.BaseExecutableName} is available ({currentVersion} -> {latestVersion})")
                .AppendLine($"Release notes: {releaseNotesUrl}")
                .Append($"Run '{UpgradeFullCommandName}' or invoke the `#{SharedCommandNames.UpgradeToolName}` mcp tool to get it")
                .ToString();

            outputHelper.OutputConsoleWarning(message);

            // Update notify timestamp and save everything in one write
            cache.LastNotifyUtc = now;
            await SaveCache(configDir, cache, ct);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Error showing upgrade notification");
            return false;
        }
    }

    /// <summary>
    /// Refreshes the cache with latest version info from GitHub if the cache is stale.
    /// Updates the cache object in-place but does NOT save it to disk.
    /// </summary>
    /// <returns>True if the cache has updates that need to be persisted</returns>
    private async Task<bool> UpdateVersionCache(
        CliUpgradeCache cache,
        bool includePrerelease,
        bool failSilently,
        bool ignoreCacheTtl,
        TimeSpan? timeout,
        CancellationToken ct
    )
    {
        var now = timeProvider.GetUtcNow();

        // Check if cache is fresh enough
        if (!ignoreCacheTtl
            && cache.LastRemoteRefreshUtc.HasValue
            && now - cache.LastRemoteRefreshUtc.Value < remoteRefreshTtl
            && !string.IsNullOrEmpty(cache.RemoteVersion))
        {
            logger.LogDebug("Using cached remote version: {Version}", cache.RemoteVersion);
            return false;
        }

        // Need to refresh from GitHub
        var (version, isPrerelease) = await TryFetchLatestVersionFromGitHub(includePrerelease, failSilently, timeout, ct);

        // Update cache (even on failure, to avoid retrying too frequently)
        cache.LastRemoteRefreshUtc = now;
        cache.LocalVersion = GetCurrentVersion();
        cache.RemoteSource = GitHubReleasesUrl;

        if (string.IsNullOrEmpty(version))
        {
            logger.LogDebug("Failed to fetch remote version, keeping cached value: {Version}", cache.RemoteVersion);
            return true;
        }

        cache.RemoteVersion = version;
        cache.IsPrerelease = isPrerelease;
        logger.LogDebug("Updated cache with remote version: {Version}", version);

        return true;
    }

    public async Task<UpgradeResponse> Upgrade(string? targetVersion, bool includePrerelease, CancellationToken ct)
    {
        var currentVersion = GetCurrentVersion();

        if (string.IsNullOrEmpty(targetVersion))
        {
            var usePrerelease = includePrerelease || IsCurrentVersionPrerelease();

            var (version, _) = await TryFetchLatestVersionFromGitHub(usePrerelease, failSilently: false, timeout: TimeSpan.FromSeconds(30), ct);
            if (string.IsNullOrEmpty(version))
            {
                return new UpgradeResponse
                {
                    ResponseError = "Failed to fetch latest version from GitHub"
                };
            }

            targetVersion = version;
        }

        if (!VersionHelper.IsNewer(targetVersion, currentVersion))
        {
            return new UpgradeResponse
            {
                NewVersion = currentVersion,
                Message = $"Already up to date (version {currentVersion})."
            };
        }

        var downloadUrl = await GetDownloadUrlForVersion(targetVersion, ct);
        if (string.IsNullOrEmpty(downloadUrl))
        {
            return new UpgradeResponse
            {
                ResponseError = $"Could not find download for version {targetVersion} on your platform."
            };
        }

        logger.LogInformation("Downloading upgrade from {Url}", downloadUrl);

        var tempDir = Path.Combine(Path.GetTempPath(), TempDownloadDirectory, $"{SharedCommandNames.BaseExecutableName}-upgrade-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var downloadPath = await DownloadFile(downloadUrl, tempDir, ct);
        var extractedExePath = await ExtractAndFindExecutable(downloadPath, tempDir, ct);

        if (string.IsNullOrEmpty(extractedExePath))
        {
            return new UpgradeResponse
            {
                ResponseError = $"Failed to find executable in the downloaded archive from {downloadUrl}"
            };
        }

        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            File.SetUnixFileMode(extractedExePath, File.GetUnixFileMode(extractedExePath) | UnixFileMode.UserExecute);
        }

        var currentExePath = GetCurrentExecutablePath();

        StartTwoStepUpgrade(extractedExePath, currentExePath, ct);

        return new UpgradeResponse
        {
            OldVersion = currentVersion,
            NewVersion = targetVersion,
            DownloadUrl = downloadUrl,
            Message = $"Upgrade from {currentVersion} -> {targetVersion} is in progress. The new version will be applied once the upgrade process completes."
        };
    }

    /*
      Two-step upgrade flow:
      The running executable cannot be replaced while it's running (file lock).

      To work around this, we use a two-step process:
        1. The original azsdk executable downloads the new version to a temp directory
        2. The original azsdk executable spawns the NEW exe with --complete-upgrade <original-path>
        3. The original azsdk executable exits immediately (releasing the file lock)
        4. The new exe waits for the original file to be unlocked (up to 10 seconds, polling)
        5. Once unlocked, the new exe copies itself over the original path

      This allows the upgrade to complete even though the OS locks running executables.

      Unix supports moving the in-use executable to a new location, but for simplicity
      we use the same approach on windows and unix systems.
    */
    public async Task<UpgradeResponse> CompleteUpgrade(string targetPath, CancellationToken ct)
    {
        var newVersion = GetCurrentVersion();

        try
        {
            var currentExePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(currentExePath))
            {
                currentExePath = GetCurrentExecutablePath();
            }

            var maxWait = TimeSpan.FromSeconds(30);
            var pollInterval = TimeSpan.FromMilliseconds(100);
            var elapsed = TimeSpan.Zero;

            logger.LogInformation("Waiting for target executable to be replaceable: {Path}", targetPath);

            while (elapsed < maxWait)
            {
                ct.ThrowIfCancellationRequested();

                if (TryReplaceExecutable(currentExePath, targetPath))
                {
                    await UpdateCacheAfterUpgrade(newVersion, ct);
                    logger.LogInformation("Upgrade completed successfully");

                    return new UpgradeResponse
                    {
                        NewVersion = newVersion,
                        Message = $"{SharedCommandNames.BaseExecutableName} upgraded successfully at {targetPath}"
                    };
                }

                await Task.Delay(pollInterval, ct);
                elapsed += pollInterval;
            }

            return new UpgradeResponse
            {
                ResponseError = $"Timed out waiting for target executable to be replaceable after {maxWait.TotalSeconds} seconds."
                                     + $" Exit any running instances of '{targetPath}' and try again."
            };
        }
        catch (OperationCanceledException)
        {
            return new UpgradeResponse
            {
                ResponseError = "Upgrade was canceled."
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error completing upgrade");
            return new UpgradeResponse
            {
                ResponseError = $"Upgrade failed: {ex.Message}"
            };
        }
    }

    private async Task<(string? version, bool isPrerelease)> TryFetchLatestVersionFromGitHub(
        bool includePrerelease,
        bool failSilently,
        TimeSpan? timeout = null,
        CancellationToken ct = default
    )
    {
        try
        {
            // Use provided timeout or default to NetworkTimeout (500ms for background checks)
            var effectiveTimeout = timeout ?? networkTimeout;
            using var timeoutCts = new CancellationTokenSource(effectiveTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            var response = await gitHubHttpClient.GetAsync(GitHubReleasesUrl, linkedCts.Token);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(linkedCts.Token);
            using var doc = JsonDocument.Parse(content);

            foreach (var release in doc.RootElement.EnumerateArray())
            {
                var tagName = release.GetProperty("tag_name").GetString();
                if (string.IsNullOrEmpty(tagName) || !tagName.StartsWith(ReleaseTagPrefix))
                {
                    continue;
                }

                var isPrerelease = release.TryGetProperty("prerelease", out var prereleaseElement) &&
                                   prereleaseElement.GetBoolean();

                // Skip prereleases if not requested
                if (isPrerelease && !includePrerelease)
                {
                    continue;
                }

                var version = tagName[ReleaseTagPrefix.Length..];
                logger.LogDebug("Found release: {Version}, prerelease: {IsPrerelease}", version, isPrerelease);
                return (version, isPrerelease);
            }

            logger.LogWarning("No suitable release found in GitHub Releases");
            return (null, false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            if (!failSilently)
            {
                throw;
            }
            // Keep as debug so we don't muddy user output when running upgrade checks in the background of other commands
            logger.LogDebug("GitHub Releases request timed out");
            return (null, false);
        }
        catch (Exception ex)
        {
            if (!failSilently)
            {
                throw;
            }
            // Keep as debug so we don't muddy user output when running upgrade checks in the background of other commands
            logger.LogDebug(ex, "Failed to fetch releases from GitHub");
            return (null, false);
        }
    }

    private async Task<string?> GetDownloadUrlForVersion(string version, CancellationToken ct)
    {
        try
        {
            var response = await gitHubHttpClient.GetAsync(GitHubReleasesUrl, ct);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(content);

            var expectedTag = $"{ReleaseTagPrefix}{version}";
            var assetName = GetExpectedAssetName();

            foreach (var release in doc.RootElement.EnumerateArray())
            {
                var tagName = release.GetProperty("tag_name").GetString();
                if (tagName != expectedTag)
                {
                    continue;
                }

                if (!release.TryGetProperty("assets", out var assets))
                {
                    continue;
                }

                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.GetProperty("name").GetString();
                    if (name == assetName)
                    {
                        return asset.GetProperty("browser_download_url").GetString();
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get download URL for version {Version}", version);
            return null;
        }
    }

    public string GetReleaseNotesUrl(string version)
    {
        return $"https://github.com/Azure/azure-sdk-tools/releases/tag/{ReleaseTagPrefix}{version}";
    }

    private static string GetExpectedAssetName()
    {
        var os = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win" :
                 RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osx" : "linux";

        var arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => "x64"
        };

        var extension = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "zip" :
                        RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "zip" : "tar.gz";

        return $"{AssetFileNamePrefix}{os}-{arch}.{extension}";
    }

    private async Task ExtractTarGz(string archivePath, string targetDir, CancellationToken ct)
    {
        // Use ProcessHelper with explicit argument list to avoid shell injection
        var options = new ProcessOptions(
            "tar",
            ["-xzf", archivePath, "-C", targetDir],
            logOutputStream: false,
            timeout: TimeSpan.FromMinutes(2)
        );

        var result = await processHelper.Run(options, ct);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"Failed to extract tar.gz archive: exit code {result.ExitCode}, output: {result.Output}");
        }
    }

    private void StartTwoStepUpgrade(string newExePath, string currentExePath, CancellationToken ct)
    {
        var options = new ProcessOptions(
            newExePath,
            ["upgrade", "--complete-upgrade", currentExePath],
            logOutputStream: false
        );

        // Spawn the upgrade completion polling process in the
        // background so it will outlive the current process
        var _ = processHelper.Run(options, ct);
        logger.LogDebug("Two-step upgrade initiated");
    }

    private string? FindOrCreateConfigDirectory()
    {
        try
        {
            // Use override if provided (primarily for testing)
            if (!string.IsNullOrEmpty(configDirectoryOverride))
            {
                Directory.CreateDirectory(configDirectoryOverride);
                return configDirectoryOverride;
            }

            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrEmpty(homeDir))
            {
                return null;
            }

            var configDir = Path.Combine(homeDir, ConfigDirectoryName);
            Directory.CreateDirectory(configDir);
            return configDir;
        }
        catch
        {
            return null;
        }
    }

    private string GetCurrentExecutablePath()
    {
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var defaultPath = Path.Combine(homeDir, "bin", SharedCommandNames.BaseExecutableName);

#if DEBUG
        // In debug mode, upgrade the installed binary at $HOME/bin/azsdk, not the debug build
        var debug = true;
#else
        var debug = false;
#endif

        if (debug)
        {
            return defaultPath;
        }

        if (Environment.ProcessPath == null)
        {
            logger.LogWarning("Process path could not be found, falling back to default location for current executable path ({defaultPath})", defaultPath);
            return defaultPath;
        }

        return Environment.ProcessPath;
    }

    private static async Task<CliUpgradeCache> LoadCache(string configDir, CancellationToken ct)
    {
        var cachePath = Path.Combine(configDir, CacheFileName);

        try
        {
            if (File.Exists(cachePath))
            {
                var json = await File.ReadAllTextAsync(cachePath, ct);
                var cache = JsonSerializer.Deserialize<CliUpgradeCache>(json);
                if (cache != null)
                {
                    return cache;
                }
            }
        }
        catch
        {
            // Ignore cache read errors, return fresh cache
        }

        return new CliUpgradeCache();
    }

    private static async Task SaveCache(string configDir, CliUpgradeCache cache, CancellationToken ct)
    {
        var cachePath = Path.Combine(configDir, CacheFileName);
        // Use unique temp file name to avoid race conditions with concurrent processes
        var tempPath = $"{cachePath}.{Guid.NewGuid():N}.tmp";

        try
        {
            var json = JsonSerializer.Serialize(cache, jsonOptions) + Environment.NewLine;
            await File.WriteAllTextAsync(tempPath, json, ct);
            File.Move(tempPath, cachePath, overwrite: true);
        }
        // Ignore cache write errors
        catch
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    /// <summary>
    /// Updates the cache after a successful upgrade to reflect the new local version.
    /// This prevents stale "upgrade available" notifications after upgrading.
    /// </summary>
    private async Task UpdateCacheAfterUpgrade(string? newVersion, CancellationToken ct)
    {
        try
        {
            var configDir = FindOrCreateConfigDirectory();
            if (configDir == null || string.IsNullOrEmpty(newVersion))
            {
                return;
            }

            var cache = await LoadCache(configDir, ct);
            var now = timeProvider.GetUtcNow();

            cache.RemoteSource = GitHubReleasesUrl;
            cache.LocalVersion = newVersion;
            cache.LastRemoteRefreshUtc = now;
            if (cache.RemoteVersion == newVersion)
            {
                cache.LastNotifyUtc = now;
            }

            await SaveCache(configDir, cache, ct);
            logger.LogDebug("Updated version cache after upgrade to {Version}", newVersion);
        }
        catch (Exception ex)
        {
            // Don't fail the upgrade if cache update fails
            logger.LogDebug(ex, "Failed to update version cache after upgrade");
        }
    }

    protected virtual async Task<string> DownloadFile(string url, string targetDir, CancellationToken ct)
    {
        var fileName = Path.GetFileName(new Uri(url).LocalPath);
        var filePath = Path.Combine(targetDir, fileName);

        using var response = await gitHubHttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        await using var fileStream = File.Create(filePath);
        await response.Content.CopyToAsync(fileStream, ct);

        return filePath;
    }

    protected virtual async Task<string?> ExtractAndFindExecutable(string archivePath, string extractDir, CancellationToken ct)
    {
        var extractTarget = Path.Combine(extractDir, "extracted");
        Directory.CreateDirectory(extractTarget);

        if (archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            ZipFile.ExtractToDirectory(archivePath, extractTarget);
        }
        else if (archivePath.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
        {
            await ExtractTarGz(archivePath, extractTarget, ct);
        }
        else
        {
            return null;
        }

        var executableName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                                ? $"{SharedCommandNames.BaseExecutableName}.exe"
                                : SharedCommandNames.BaseExecutableName;
        var executablePath = Directory.GetFiles(extractTarget, executableName, SearchOption.AllDirectories).FirstOrDefault();

        return executablePath;
    }

    protected virtual bool TryReplaceExecutable(string sourceExePath, string targetPath)
    {
        try
        {
            File.Copy(sourceExePath, targetPath, overwrite: true);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }
}
