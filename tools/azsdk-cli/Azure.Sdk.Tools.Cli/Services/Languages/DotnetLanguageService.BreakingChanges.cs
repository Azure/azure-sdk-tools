// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models.SdkBreakingChangeDetection;

namespace Azure.Sdk.Tools.Cli.Services.Languages;

public sealed partial class DotnetLanguageService
{
    public override string? ValidateBreakingChangeClassification(SdkBreakingChangeDetectionResult classification)
    {
        var error = base.ValidateBreakingChangeClassification(classification);
        if (error != null)
        {
            return error;
        }

        if (classification.BreakingChanges.Any(change =>
            change.Mitigation == null || !Enum.IsDefined(change.Mitigation.Value)))
        {
            return "The .NET classification must provide a supported mitigation route for every breaking change.";
        }

        return null;
    }

    public override async Task<string> GetSdkBreakingPattern(string sdkRepoRoot, CancellationToken ct)
    {
        var configuredPath = await specGenSdkConfigHelper.GetSdkBreakingChangePatternFileConfigurationAsync(sdkRepoRoot, ct);
        var path = Path.Combine(sdkRepoRoot, configuredPath == string.Empty
            ? Path.Combine("doc", "dev", "SDKBreakingChanges.md")
            : configuredPath);
        logger.LogInformation("Loading .NET SDK breaking change patterns from {Path}", path);
        var patterns = await File.ReadAllTextAsync(path, ct);
        if (string.IsNullOrWhiteSpace(patterns))
        {
            throw new InvalidOperationException($"The .NET SDK breaking change pattern catalog is empty: {path}");
        }
        return patterns;
    }
}
