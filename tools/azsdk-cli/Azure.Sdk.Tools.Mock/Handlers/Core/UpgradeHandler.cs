// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Mock.Handlers.Core;

/// <summary>
/// Mock handler for azsdk_upgrade. Always reports the current "mock" version is up to date so
/// callers exercising the upgrade flow don't trigger a real download.
/// </summary>
public class UpgradeHandler : IMockToolHandler
{
    public string ToolName => "azsdk_upgrade";

    public CommandResponse Handle(Dictionary<string, object?>? arguments) => new UpgradeResponse
    {
        OldVersion = "0.0.0-mock",
        NewVersion = "0.0.0-mock",
        Message = "azsdk is already up to date (mock).",
        RestartRequired = false
    };
}
