// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Tools.ReleasePlan;

public interface IReleasePlanTool
{
    Task<DefaultCommandResponse> UpdateSDKDetailsInReleasePlan(int releasePlanWorkItemId, string typeSpecProjectPath, CancellationToken ct);
}
