// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;

namespace Azure.Sdk.Tools.Mock.Handlers.Pipeline;

/// <summary>
/// Reads the build id out of the pipeline tools' shared <c>pipelineIdentifier</c> argument. The real tools
/// accept a build id, a pipeline URL, a GitHub PR link or a PR number; only the first two name a build, so
/// anything else yields null and the caller decides whether to fall back to a fixture or the default response.
/// </summary>
internal static class MockPipelineIdentifier
{
    private static readonly Regex BuildIdInUrl = new(@"[?&]buildId=(\d+)", RegexOptions.IgnoreCase);

    public static string? GetBuildId(Dictionary<string, object?>? arguments)
    {
        var identifier = arguments?.GetValueOrDefault("pipelineIdentifier")?.ToString();
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return null;
        }

        if (int.TryParse(identifier, out var buildId))
        {
            return buildId.ToString();
        }

        var match = BuildIdInUrl.Match(identifier);
        return match.Success ? match.Groups[1].Value : null;
    }
}
