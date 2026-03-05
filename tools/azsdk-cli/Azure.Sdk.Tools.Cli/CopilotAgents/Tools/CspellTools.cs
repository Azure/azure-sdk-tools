// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Azure.Sdk.Tools.Cli.CopilotAgents.Tools;

/// <summary>
/// Factory methods for creating cspell-related AIFunction tools for copilot agents.
/// </summary>
public static class CspellTools
{
    /// <summary>
    /// Creates an UpdateCspellWords tool that adds words to the cspell.json words list.
    /// </summary>
    /// <param name="baseDir">The base directory (repository root) for resolving cspell.json.</param>
    /// <param name="description">Optional custom description for the tool.</param>
    /// <returns>An AIFunction that updates cspell.json.</returns>
    public static AIFunction CreateUpdateCspellWordsTool(
        string baseDir,
        string description = "Add words to the cspell.json words list")
    {
        return AIFunctionFactory.Create(
            async ([Description("List of words to add to the cspell.json words list")] List<string> words) =>
            {
                var cspellRelativePath = Path.Combine(".vscode", "cspell.json");

                if (!ToolHelpers.TryGetSafeFullPath(baseDir, cspellRelativePath, out var cspellPath))
                {
                    throw new ArgumentException("The cspell.json path is invalid or outside the allowed base directory.");
                }

                if (!File.Exists(cspellPath))
                {
                    throw new FileNotFoundException($"cspell.json not found at {cspellPath}");
                }

                var cspellContent = await File.ReadAllTextAsync(cspellPath);
                var configDict = JsonSerializer.Deserialize<Dictionary<string, object>>(cspellContent)
                    ?? new Dictionary<string, object>();

                var existingWords = GetExistingWords(configDict);
                var wordsToAdd = words.Where(w => !existingWords.Contains(w, StringComparer.OrdinalIgnoreCase)).ToList();

                existingWords.AddRange(wordsToAdd);
                configDict["words"] = existingWords;

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                var updatedContent = JsonSerializer.Serialize(configDict, options);
                await File.WriteAllTextAsync(cspellPath, updatedContent);

                return $"Successfully added {wordsToAdd.Count} words to cspell.json";
            },
            "UpdateCspellWords",
            description);
    }

    private static List<string> GetExistingWords(Dictionary<string, object> configDict)
    {
        if (!configDict.ContainsKey("words"))
        {
            return new List<string>();
        }

        var wordsElement = (JsonElement)configDict["words"];
        return wordsElement.ValueKind == JsonValueKind.Array
            ? wordsElement.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!).ToList()
            : new List<string>();
    }
}
