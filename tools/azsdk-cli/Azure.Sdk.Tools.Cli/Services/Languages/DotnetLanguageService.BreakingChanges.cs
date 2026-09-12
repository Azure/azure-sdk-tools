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
            change.MitigationStrategy == null || !Enum.IsDefined(change.MitigationStrategy.Value)))
        {
            return "The .NET classification must provide a supported mitigation route for every breaking change.";
        }

        return null;
    }
}
