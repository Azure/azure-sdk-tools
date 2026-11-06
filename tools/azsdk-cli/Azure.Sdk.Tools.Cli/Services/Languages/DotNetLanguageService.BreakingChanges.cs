// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models.SdkBreakingChangeDetection;

namespace Azure.Sdk.Tools.Cli.Services.Languages;

public sealed partial class DotNetLanguageService
{
    public override bool RequiresBreakingChangePatternCatalog => true;

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

        var remainingEntries = ReadOriginalBreakingEntries(classification.SdkChangeMD);
        if (remainingEntries == null)
        {
            return "Cannot validate .NET originBreaks: the detector report must contain a single nonempty '### Breaking Changes' section with one '- ' bullet per native violation.";
        }
        foreach (var change in classification.BreakingChanges)
        {
            if (change.OriginBreaks == null || change.OriginBreaks.Count == 0)
            {
                return "Every .NET classification must provide nonempty originBreaks containing its exact original detector entries.";
            }
            foreach (var entry in change.OriginBreaks)
            {
                if (string.IsNullOrWhiteSpace(entry) ||
                    !remainingEntries.TryGetValue(entry, out var count) || count == 0)
                {
                    return "The .NET originBreaks contain an empty, fabricated, altered, or duplicated detector entry.";
                }
                remainingEntries[entry] = count - 1;
            }
        }
        if (remainingEntries.Values.Any(count => count != 0))
        {
            return "The .NET originBreaks must cover every original detector entry exactly once; some entries are missing.";
        }

        return null;
    }

    private static Dictionary<string, int>? ReadOriginalBreakingEntries(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return null;
        }
        var entries = new Dictionary<string, int>(StringComparer.Ordinal);
        var foundSection = false;
        var inSection = false;
        using var reader = new StringReader(markdown);
        while (reader.ReadLine() is { } line)
        {
            if (line.TrimEnd() == "### Breaking Changes")
            {
                if (foundSection)
                {
                    return null;
                }
                foundSection = true;
                inSection = true;
            }
            else if (line.StartsWith("### ", StringComparison.Ordinal))
            {
                inSection = false;
            }
            else if (inSection && !string.IsNullOrWhiteSpace(line))
            {
                // The native report emits one single-line "- " bullet per ApiCompat violation.
                if (!line.StartsWith("- ", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(line[2..]))
                {
                    return null;
                }
                var entry = line[2..];
                entries.TryGetValue(entry, out var count);
                entries[entry] = count + 1;
            }
        }
        return entries.Count == 0 ? null : entries;
    }
}
