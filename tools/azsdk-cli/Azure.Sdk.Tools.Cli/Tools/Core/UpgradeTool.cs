// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.ComponentModel;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services.Upgrade;
using Azure.Sdk.Tools.Cli.Tools.Core;

namespace Azure.Sdk.Tools.Cli.Tools.CliManagement;

[McpServerToolType, Description("Manage azsdk CLI version and upgrades")]
public class UpgradeTool(
    ILogger<UpgradeTool> logger,
    IUpgradeService upgradeService,
    UpgradeShutdownCoordinator shutdownCoordinator
) : MCPTool
{
    // No command hierarchy - this is a top-level command
    public override CommandGroup[] CommandHierarchy { get; set; } = [];

    // Copy here so the mcp name analyzer can pass
    private const string UpgradeToolName = SharedCommandNames.UpgradeToolName;
    private const string UpgradeCommandName = SharedCommandNames.UpgradeCommandName;

    private readonly Option<string?> versionOption = new("--version-override")
    {
        Description = "Target version to upgrade to (default: latest)",
        Required = false,
    };

    private readonly Option<bool> prereleaseOption = new("--prerelease")
    {
        Description = "Include prerelease versions when checking for updates",
        Required = false,
        DefaultValueFactory = _ => false,
    };

    private readonly Option<bool> checkOnlyOption = new("--check", "-c")
    {
        Description = "Only check for updates, don't perform upgrade",
        Required = false,
        DefaultValueFactory = _ => false,
    };

    // Hidden option for two-step upgrade (not shown in help).
    // Meant to be called internally after the user-facing upgrade command.
    private readonly Option<string?> completeUpgradeOption = new("--complete-upgrade")
    {
        Description = "[FOR INTERNAL USE ONLY] Complete upgrade by replacing the specified target path",
        Required = false,
        Hidden = true
    };

    protected override Command GetCommand() =>
        new McpCommand(UpgradeCommandName, "Check for and install azsdk CLI updates", UpgradeToolName)
        {
            versionOption,
            prereleaseOption,
            checkOnlyOption,
            completeUpgradeOption
        };

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        var targetVersion = parseResult.GetValue(versionOption);
        var includePrerelease = parseResult.GetValue(prereleaseOption);
        var checkOnly = parseResult.GetValue(checkOnlyOption);
        var completeUpgradePath = parseResult.GetValue(completeUpgradeOption);

        // Two-step upgrade flow:
        // The running executable cannot be replaced while it's running (file lock).
        // To work around this, we use a two-step process:
        // 1. The original azsdk binary downloads the new version to a temp directory
        // 2. The original azsdk binary spawns the NEW exe with --complete-upgrade <original-path>
        // 3. The original azsdk binary exits immediately (releasing the file lock)
        // 4. The new exe waits for the original file to be unlocked (up to 30 seconds, polling)
        // 5. Once unlocked, the new exe copies itself over the original path
        // This allows the upgrade to complete even though the OS locks running executables.
        if (!string.IsNullOrEmpty(completeUpgradePath))
        {
            return await upgradeService.CompleteUpgrade(completeUpgradePath, ct);
        }

        if (checkOnly)
        {
            return await CheckForUpdates(includePrerelease, ct);
        }

        return await UpgradeCli(targetVersion, includePrerelease, ct);
    }

    public async Task<UpgradeResponse> UpgradeCli(
        string? targetVersion = null,
        bool includePrerelease = false,
        CancellationToken ct = default)
    {
        try
        {
            logger.LogInformation("Starting upgrade (target: {Target}, prerelease: {Prerelease})",
                targetVersion ?? "latest", includePrerelease);
            return await upgradeService.Upgrade(targetVersion, includePrerelease, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during upgrade");
            return new UpgradeResponse
            {
                ResponseError = $"Upgrade failed: {ex.Message}"
            };
        }
    }

    [McpServerTool(Name = UpgradeToolName)]
    [Description("Upgrade the MCP server to the latest version. IMPORTANT: After upgrade completes, the MCP server must be restarted to use the new version.")]
    public async Task<UpgradeResponse> UpgradeMcp(
        string? targetVersion = null,
        bool includePrerelease = false,
        bool checkOnly = false,
        CancellationToken ct = default)
    {
        try
        {
            if (checkOnly)
            {
                return await CheckForUpdates(includePrerelease, ct);
            }

            logger.LogInformation("Starting upgrade (target: {Target}, prerelease: {Prerelease})",
                targetVersion ?? "latest", includePrerelease);

            var result = await upgradeService.Upgrade(targetVersion, includePrerelease, ct);

            // Always set restart required for MCP mode
            if (result.OperationStatus == Status.Succeeded && result.OldVersion != result.NewVersion)
            {
                result.RestartRequired = true;
                result.Message = "The MCP server must be restarted to use the new version. Shutting down...";

                // Trigger server shutdown after the "restart required" response is sent back to the MCP client.
                // This is so that the background process we spawn to replace the executable can complete
                // without the current server locking the process until it times out.
                await shutdownCoordinator.RequestShutdown();
            }

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during upgrade");
            return new UpgradeResponse
            {
                ResponseError = $"Upgrade failed: {ex.Message}"
            };
        }
    }

    private async Task<UpgradeResponse> CheckForUpdates(bool includePrerelease, CancellationToken ct)
    {
        try
        {
            var currentVersion = upgradeService.GetCurrentVersion();

            // Use smart prerelease matching unless explicitly requested
            var usePrerelease = includePrerelease || upgradeService.IsCurrentVersionPrerelease();

            var latestVersion = await upgradeService.CheckLatestVersion(usePrerelease, failSilently: false, ignoreCacheTtl: true, ct);

            if (string.IsNullOrEmpty(latestVersion))
            {
                return new UpgradeResponse
                {
                    ResponseError = "Failed to find latest version"
                };
            }

            var isNewer = VersionHelper.IsNewer(latestVersion, currentVersion);

            return new UpgradeResponse
            {
                OldVersion = currentVersion,
                NewVersion = isNewer ? latestVersion : currentVersion,
                Message = isNewer
                    ? $"A new version of azsdk is available ({currentVersion} -> {latestVersion})"
                        + Environment.NewLine
                        + $"Release notes: {upgradeService.GetReleaseNotesUrl(latestVersion)}"
                    : $"You are up to date (version {currentVersion})."
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking for updates");
            return new UpgradeResponse
            {
                ResponseError = $"Failed to check for updates: {ex.Message}"
            };
        }
    }
}
