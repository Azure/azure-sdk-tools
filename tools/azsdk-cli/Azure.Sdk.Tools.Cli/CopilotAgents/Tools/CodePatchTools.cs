// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.ComponentModel;
using System.Text.RegularExpressions;
using Azure.Sdk.Tools.Cli.Microagents;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Microsoft.Extensions.AI;

namespace Azure.Sdk.Tools.Cli.CopilotAgents.Tools;

/// <summary>
/// Factory methods for creating code patch AIFunction tools for copilot agents.
/// </summary>
public static partial class CodePatchTools
{
    // Compiled regex patterns for performance
    private static readonly Regex LineNumberPrefixRegex = MyRegex();
    private static readonly Regex WhitespaceRegex = WhitespaceRegex1();
    private static readonly Regex EscapedQuoteRegex = EscapedQuoteRegex1();
    private static readonly Regex UnescapedQuoteRegex = UnescapedQuoteRegex1();
    private static readonly Regex InternalQuoteRegex = InternalQuoteRegex1();
    private static readonly Regex QuotePairRegex = QuotePairRegex1();

    /// <summary>
    /// Creates a surgical code patch tool that combines line-based targeting with text replacement.
    /// </summary>
    /// <param name="baseDir">The base directory for resolving file paths.</param>
    /// <param name="description">Optional custom description for the tool.</param>
    /// <returns>An AIFunction that applies code patches.</returns>
    /// <remarks>
    /// The approach solves two problems:
    /// <list type="number">
    /// <item><description>Line numbers narrow down WHERE to patch (avoiding "content not found" across entire file)</description></item>
    /// <item><description>OldText/NewText provide SURGICAL precision (avoiding multi-line syntax corruption)</description></item>
    /// </list>
    /// </remarks>
    public static AIFunction CreateCodePatchTool(
        string baseDir,
        string description = "Code patch: finds OldText within lines StartLine-EndLine and replaces with NewText. Use for precise edits that preserve surrounding syntax.",
        Action<AppliedPatch>? onPatchApplied = null)
    {
        return AIFunctionFactory.Create(
            async (
                [Description("Path to the customization file (relative to customization root or just filename)")]
                string filePath,

                [Description("1-based start line number of the region containing the text to replace")]
                int startLine,

                [Description("1-based end line number of the region (inclusive). Use same as startLine for single-line edits.")]
                int endLine,

                [Description("The EXACT text fragment to find and replace within the specified line range. Must match exactly.")]
                string oldText,

                [Description("The replacement text. Only oldText is replaced, preserving surrounding code.")]
                string newText,

                CancellationToken cancellationToken) =>
            {
                var result = await ApplyPatchAsync(baseDir, filePath, startLine, endLine, oldText, newText, cancellationToken);
                if (result.Success && onPatchApplied is not null)
                {
                    onPatchApplied(new AppliedPatch(filePath, result.Message, 1));
                }
                return result;
            },
            "ClientCustomizationCodePatch",
            description);
    }

    /// <summary>
    /// Applies a code patch to a file.
    /// </summary>
    public static async Task<CodePatchResult> ApplyPatchAsync(
        string baseDir,
        string filePath,
        int startLine,
        int endLine,
        string oldText,
        string newText,
        CancellationToken ct)
    {
        // Validate inputs
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return new CodePatchResult(false, "FilePath cannot be empty");
        }

        if (startLine < 1)
        {
            return new CodePatchResult(false, "StartLine must be >= 1");
        }

        if (endLine < startLine)
        {
            return new CodePatchResult(false, "EndLine must be >= StartLine");
        }

        if (string.IsNullOrEmpty(oldText))
        {
            return new CodePatchResult(false, "OldText cannot be empty - specify the exact text to replace");
        }

        // NewText can be empty (for deletion)
        newText ??= "";

        // Resolve file path
        if (!ToolHelpers.TryGetSafeFullPath(baseDir, filePath, out var safeFilePath))
        {
            return new CodePatchResult(false, "File path is outside the customization directory");
        }

        try
        {
            // If the resolved path doesn't exist, try to find the file by name within baseDir
            if (!File.Exists(safeFilePath))
            {
                var fileName = Path.GetFileName(filePath);
                var candidates = Directory.GetFiles(baseDir, fileName, SearchOption.AllDirectories);
                if (candidates.Length == 1)
                {
                    safeFilePath = candidates[0];
                }
                else
                {
                    var hint = candidates.Length == 0
                        ? "No file with that name exists under the customization root."
                        : $"Multiple matches found: {string.Join(", ", candidates.Select(c => Path.GetRelativePath(baseDir, c)))}. Use a more specific path.";
                    return new CodePatchResult(false, $"File not found: {filePath}. {hint}");
                }
            }

            // Read the file
            var allLines = await File.ReadAllLinesAsync(safeFilePath, ct).ConfigureAwait(false);
            var totalLines = allLines.Length;

            // Validate line range
            if (startLine > totalLines)
            {
                return new CodePatchResult(false,
                    $"StartLine {startLine} is beyond end of file ({totalLines} lines)");
            }

            if (endLine > totalLines)
            {
                return new CodePatchResult(false,
                    $"EndLine {endLine} is beyond end of file ({totalLines} lines)");
            }

            // Extract the target line range (convert to 0-based indices)
            var startIdx = startLine - 1;
            var endIdx = endLine - 1;

            // Join the lines in the range to search for OldText
            var targetRegion = string.Join("\n", allLines.Skip(startIdx).Take(endIdx - startIdx + 1));

            // Strip line number prefixes from OldText (LLM might copy from ReadFile output)
            var cleanOldText = StripLineNumberPrefixes(oldText);
            var cleanNewText = StripLineNumberPrefixes(newText);

            // Try exact match first
            int matchIndex = targetRegion.IndexOf(cleanOldText, StringComparison.Ordinal);
            int matchLength = cleanOldText.Length;
            bool usedFuzzyMatch = false;

            // If exact match fails, try with normalized whitespace
            if (matchIndex < 0)
            {
                var normalizedRegion = NormalizeWhitespace(targetRegion);
                var normalizedOldText = NormalizeWhitespace(cleanOldText);

                if (normalizedRegion.Contains(normalizedOldText, StringComparison.Ordinal))
                {
                    // Find actual position and span using fuzzy matching
                    var fuzzyResult = FindFuzzyMatch(targetRegion, cleanOldText);
                    matchIndex = fuzzyResult.Start;
                    matchLength = fuzzyResult.Length;
                    usedFuzzyMatch = true;
                }
            }

            if (matchIndex < 0)
            {
                var regionPreview = targetRegion.Length > 300
                    ? targetRegion[..300] + "..."
                    : targetRegion;
                return new CodePatchResult(false,
                    $"OldText not found in lines {startLine}-{endLine}.\n" +
                    $"Looking for: \"{TruncateForDisplay(cleanOldText, 100)}\"\n" +
                    $"Region content: \"{TruncateForDisplay(regionPreview, 300)}\"");
            }

            // Check for multiple matches (re-check with fuzzy match if that's how we found it)
            int secondMatch;
            if (usedFuzzyMatch)
            {
                // Fuzzy match was used — check for a second fuzzy match after the first span
                var remainder = targetRegion[(matchIndex + matchLength)..];
                var secondFuzzy = FindFuzzyMatch(remainder, cleanOldText);
                secondMatch = secondFuzzy.Start;
            }
            else
            {
                secondMatch = targetRegion.IndexOf(cleanOldText, matchIndex + 1, StringComparison.Ordinal);
            }

            if (secondMatch >= 0)
            {
                return new CodePatchResult(false,
                    $"OldText appears multiple times in lines {startLine}-{endLine}. " +
                    "Include more surrounding context in OldText to make it unique.");
            }

            // Auto-preserve escaping: if OldText has escaped quotes but NewText lost them, restore them
            var preservedNewText = PreserveEscaping(cleanOldText, cleanNewText);

            // Perform the replacement using the actual matched span length
            var updatedRegion = targetRegion[..matchIndex] + preservedNewText + targetRegion[(matchIndex + matchLength)..];

            // Split back into lines and reconstruct the file
            var updatedLines = updatedRegion.Split('\n');
            var newAllLines = new List<string>();

            // Add lines before the region
            newAllLines.AddRange(allLines.Take(startIdx));

            // Add the updated region lines
            newAllLines.AddRange(updatedLines);

            // Add lines after the region
            newAllLines.AddRange(allLines.Skip(endIdx + 1));

            // Write back
            await File.WriteAllLinesAsync(safeFilePath, newAllLines, ct).ConfigureAwait(false);

            var description = $"Replaced \"{TruncateForDisplay(cleanOldText, 50)}\" with \"{TruncateForDisplay(cleanNewText, 50)}\" in lines {startLine}-{endLine}";
            return new CodePatchResult(true, $"Patch applied to {filePath}: {description}");
        }
        catch (Exception ex)
        {
            return new CodePatchResult(false, $"Failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Strips line number prefixes from text that might have been copied from ReadFile output.
    /// </summary>
    private static string StripLineNumberPrefixes(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var lines = text.Split('\n');
        var result = new List<string>();

        foreach (var line in lines)
        {
            var trimmedLine = line.TrimEnd('\r');
            var match = LineNumberPrefixRegex.Match(trimmedLine);
            if (match.Success)
            {
                result.Add(match.Groups[1].Value + trimmedLine[match.Length..]);
            }
            else
            {
                result.Add(trimmedLine);
            }
        }

        return string.Join("\n", result);
    }

    /// <summary>
    /// Normalizes whitespace for flexible matching.
    /// </summary>
    private static string NormalizeWhitespace(string text)
    {
        return WhitespaceRegex.Replace(text, " ").Trim();
    }

    /// <summary>
    /// Finds the position and actual span of oldText in targetRegion using fuzzy whitespace matching.
    /// Returns the start index and length of the actual text in targetRegion that corresponds
    /// to the normalized match, so the correct span is replaced.
    /// </summary>
    private static (int Start, int Length) FindFuzzyMatch(string targetRegion, string oldText)
    {
        var normalizedOld = NormalizeWhitespace(oldText);

        // Sliding window search
        for (int i = 0; i <= targetRegion.Length - oldText.Length / 2; i++)
        {
            // Find the actual end of the match in the original text by expanding
            // character-by-character until we've consumed all normalized content.
            int actualLength = FindActualMatchLength(targetRegion, i, normalizedOld);
            if (actualLength > 0)
            {
                return (i, actualLength);
            }
        }

        return (-1, 0);
    }

    /// <summary>
    /// Given a start position in the source text and a normalized target string,
    /// determines how many characters in the source text correspond to the full
    /// normalized match. Returns 0 if no match.
    /// </summary>
    private static int FindActualMatchLength(string source, int startPos, string normalizedTarget)
    {
        int sourceIdx = startPos;
        int targetIdx = 0;

        // Skip leading whitespace in source at the start position
        while (sourceIdx < source.Length && char.IsWhiteSpace(source[sourceIdx]))
        {
            sourceIdx++;
        }

        int matchStart = sourceIdx;

        while (targetIdx < normalizedTarget.Length && sourceIdx < source.Length)
        {
            if (normalizedTarget[targetIdx] == ' ')
            {
                // Normalized space: consume one or more whitespace chars in source
                if (!char.IsWhiteSpace(source[sourceIdx]))
                {
                    return 0; // expected whitespace but got non-whitespace
                }

                while (sourceIdx < source.Length && char.IsWhiteSpace(source[sourceIdx]))
                {
                    sourceIdx++;
                }

                targetIdx++;
            }
            else
            {
                // Non-space character: must match exactly
                if (source[sourceIdx] != normalizedTarget[targetIdx])
                {
                    return 0;
                }

                sourceIdx++;
                targetIdx++;
            }
        }

        if (targetIdx < normalizedTarget.Length)
        {
            return 0; // didn't consume the entire target
        }

        // Trim trailing whitespace from the matched span
        while (sourceIdx > matchStart && char.IsWhiteSpace(source[sourceIdx - 1]))
        {
            sourceIdx--;
        }

        return sourceIdx - startPos;
    }

    /// <summary>
    /// Truncates a string for display in error messages.
    /// </summary>
    private static string TruncateForDisplay(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var escaped = text
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");

        if (escaped.Length <= maxLength)
        {
            return escaped;
        }

        return escaped[..(maxLength - 3)] + "...";
    }

    /// <summary>
    /// Auto-preserves escaping patterns from OldText to NewText.
    /// </summary>
    private static string PreserveEscaping(string oldText, string newText)
    {
        if (string.IsNullOrEmpty(oldText) || string.IsNullOrEmpty(newText))
        {
            return newText;
        }

        int oldEscapedQuotes = EscapedQuoteRegex.Matches(oldText).Count;

        if (oldEscapedQuotes == 0)
        {
            return newText;
        }

        int newEscapedQuotes = EscapedQuoteRegex.Matches(newText).Count;
        var unescapedQuoteMatches = UnescapedQuoteRegex.Matches(newText);

        if (newEscapedQuotes >= oldEscapedQuotes || unescapedQuoteMatches.Count == 0)
        {
            return newText;
        }

        var result = InternalQuoteRegex.Replace(newText, @"\""");
        result = QuotePairRegex.Replace(result, @"\""$1\""");

        return result;
    }

    [GeneratedRegex(@"^(\s*)\d+:\s?", RegexOptions.Compiled)]
    private static partial Regex MyRegex();
    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex WhitespaceRegex1();
    [GeneratedRegex(@"\\""", RegexOptions.Compiled)]
    private static partial Regex EscapedQuoteRegex1();
    [GeneratedRegex(@"(?<!\\)""", RegexOptions.Compiled)]
    private static partial Regex UnescapedQuoteRegex1();
    [GeneratedRegex(@"(?<!\\)""(?![\s]*[+);]|$)", RegexOptions.Compiled)]
    private static partial Regex InternalQuoteRegex1();
    [GeneratedRegex(@"(?<!\\)""([^""\\]*?)(?<!\\)""", RegexOptions.Compiled)]
    private static partial Regex QuotePairRegex1();
}

/// <summary>
/// Result of a code patch operation.
/// </summary>
public record CodePatchResult(
    [property: Description("Whether the patch was successfully applied")]
    bool Success,
    [property: Description("Success or error message describing the result")]
    string Message);
