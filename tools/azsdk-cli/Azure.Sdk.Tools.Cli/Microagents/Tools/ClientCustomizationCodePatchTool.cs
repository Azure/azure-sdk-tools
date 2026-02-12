// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;

namespace Azure.Sdk.Tools.Cli.Microagents.Tools;

public record ClientCustomizationCodePatchInput(
    [property: Description("Path to the customization file (relative to customization root)")] string FilePath,
    [property: Description("Exact text to replace")] string OldContent,
    [property: Description("Replacement text")] string NewContent);

public record ClientCustomizationCodePatchOutput(
    [property: Description("Whether the patch was successfully applied")] bool Success,
    [property: Description("Success or error message describing the result")] string Message);

public class ClientCustomizationCodePatchTool(string baseDir) : AgentTool<ClientCustomizationCodePatchInput, ClientCustomizationCodePatchOutput>
{
    public override string Name { get; init; } = "ClientCustomizationCodePatch";
    public override string Description { get; init; } = "Apply a code patch to a customization file by replacing old content with new content";

    /// <summary>
    /// Tracks all patches successfully applied by this tool instance.
    /// </summary>
    public List<AppliedPatch> AppliedPatches { get; } = [];

    /// <summary>
    /// Callback invoked after a successful patch. Used to cancel the agent to prevent wasted tokens.
    /// </summary>
    public Action? OnPatchApplied { get; set; }

    public override async Task<ClientCustomizationCodePatchOutput> Invoke(ClientCustomizationCodePatchInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.FilePath))
        {
            return new ClientCustomizationCodePatchOutput(false, "File path cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(input.OldContent))
        {
            return new ClientCustomizationCodePatchOutput(false, "Old content cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(input.NewContent))
        {
            return new ClientCustomizationCodePatchOutput(false, "New content cannot be empty");
        }

        if (!ToolHelpers.TryGetSafeFullPath(baseDir, input.FilePath, out var safeFilePath))
        {
            return new ClientCustomizationCodePatchOutput(false, "File path is outside the customization directory");
        }

        try
        {
            if (!File.Exists(safeFilePath))
            {
                return new ClientCustomizationCodePatchOutput(false, $"File not found: {input.FilePath}");
            }

            var fileContent = await File.ReadAllTextAsync(safeFilePath, ct);

            if (!fileContent.Contains(input.OldContent))
            {
                return new ClientCustomizationCodePatchOutput(false, "Old content not found in file");
            }

            var occurrences = CountOccurrences(fileContent, input.OldContent);
            var updatedContent = fileContent.Replace(input.OldContent, input.NewContent);
            await File.WriteAllTextAsync(safeFilePath, updatedContent, ct);

            var description = GeneratePatchDescription(input.OldContent, input.NewContent);
            AppliedPatches.Add(new AppliedPatch(input.FilePath, description, occurrences, input.OldContent, input.NewContent));

            // Signal to the caller that a patch was applied - stop the agent to prevent wasted tokens
            OnPatchApplied?.Invoke();

            return new ClientCustomizationCodePatchOutput(true, $"Patch applied to {input.FilePath} ({occurrences} replacement(s))");
        }
        catch (Exception ex)
        {
            return new ClientCustomizationCodePatchOutput(false, $"Failed: {ex.Message}");
        }
    }

    private static int CountOccurrences(string text, string pattern)
    {
        if (string.IsNullOrEmpty(pattern))
        {
            return 0;
        }

        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }

    /// <summary>
    /// Generates a brief human-readable description of the patch.
    /// </summary>
    private static string GeneratePatchDescription(string oldContent, string newContent)
    {
        // Extract first meaningful line from each for comparison
        var oldLine = GetFirstMeaningfulLine(oldContent);
        var newLine = GetFirstMeaningfulLine(newContent);

        if (oldLine.Length > 50)
        {
            oldLine = oldLine[..47] + "...";
        }
        if (newLine.Length > 50)
        {
            newLine = newLine[..47] + "...";
        }

        return $"Changed '{oldLine}' to '{newLine}'";
    }

    private static string GetFirstMeaningfulLine(string content)
    {
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed) && !trimmed.StartsWith("//") && !trimmed.StartsWith("/*"))
            {
                return trimmed;
            }
        }
        return content.Trim().Split('\n')[0].Trim();
    }
}
