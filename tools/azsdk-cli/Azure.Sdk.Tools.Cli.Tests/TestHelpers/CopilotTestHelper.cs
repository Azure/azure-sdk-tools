// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.CopilotAgents;
using GitHub.Copilot.SDK;

namespace Azure.Sdk.Tools.Cli.Tests.TestHelpers;

/// <summary>
/// Helper for testing with the GitHub Copilot SDK.
/// </summary>
public static class CopilotTestHelper
{
    /// <summary>
    /// Checks if the GitHub Copilot CLI is installed and authenticated.
    /// </summary>
    /// <returns>True if Copilot is available and authenticated, false otherwise.</returns>
    public static async Task<bool> IsCopilotAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            var copilotClient = new CopilotClient(new CopilotClientOptions
            {
                UseStdio = true,
                AutoStart = true
            });
            var wrapper = new CopilotClientWrapper(copilotClient);
            var authStatus = await wrapper.GetAuthStatusAsync(ct);
            return authStatus.IsAuthenticated;
        }
        catch
        {
            // CLI not installed or failed to start
            return false;
        }
    }
}
