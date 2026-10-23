// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Mock.Handlers.Verify;

/// <summary>
/// Mock handler for azsdk_verify_setup. Returns an all-good response so downstream tools
/// can proceed without environment prerequisites in mock mode.
/// </summary>
public class VerifySetupHandler : IMockToolHandler
{
    public string ToolName => "azsdk_verify_setup";

    public CommandResponse Handle(Dictionary<string, object?>? arguments) => new VerifySetupResponse
    {
        // Results contains requirement failures, not an inventory of installed tools.
        Results = []
    };
}
