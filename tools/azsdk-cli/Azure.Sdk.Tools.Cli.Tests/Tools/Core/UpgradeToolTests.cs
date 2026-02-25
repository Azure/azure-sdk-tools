// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#if DEBUG
using Moq;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services.Upgrade;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.CliManagement;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.Core;

internal class UpgradeToolTests
{
    [Test]
    public async Task UpgradeMcp_CheckOnly_UsesSmartPrereleaseSelection()
    {
        var logger = new TestLogger<UpgradeTool>();
        var upgradeService = new Mock<IUpgradeService>();
        var shutdown = new UpgradeShutdownCoordinator();
        var tool = new UpgradeTool(logger, upgradeService.Object, shutdown);

        upgradeService.Setup(s => s.GetCurrentVersion()).Returns("0.5.14");
        upgradeService.Setup(s => s.IsCurrentVersionPrerelease()).Returns(true);
        upgradeService
            .Setup(s => s.CheckLatestVersion(
                true,
                false,
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("0.5.99-dev.1");

        var resp = await tool.UpgradeMcp(targetVersion: null, includePrerelease: false, checkOnly: true, CancellationToken.None);

        Assert.That(resp.ResponseError, Is.Null);
        upgradeService.VerifyAll();
    }

    [Test]
    public async Task UpgradeMcp_WhenUpgradeChangesVersion_RequestsShutdownAndSetsRestartRequired()
    {
        var logger = new TestLogger<UpgradeTool>();
        var upgradeService = new Mock<IUpgradeService>();
        var shutdown = new UpgradeShutdownCoordinator();
        var tool = new UpgradeTool(logger, upgradeService.Object, shutdown);

        upgradeService
            .Setup(s => s.Upgrade(It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpgradeResponse
            {
                OldVersion = "0.5.14",
                NewVersion = "0.5.15",
            });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var watchTask = shutdown.Watch(cts.Token).GetAsyncEnumerator(cts.Token).MoveNextAsync().AsTask();

        var resp = await tool.UpgradeMcp(targetVersion: null, includePrerelease: false, checkOnly: false, CancellationToken.None);

        Assert.That(resp.ResponseError, Is.Null);
        Assert.That(resp.RestartRequired, Is.True);
        Assert.That(resp.Message, Does.Contain("must be restarted"));

        var signaled = await watchTask;
        Assert.That(signaled, Is.True);
    }
}

#endif
