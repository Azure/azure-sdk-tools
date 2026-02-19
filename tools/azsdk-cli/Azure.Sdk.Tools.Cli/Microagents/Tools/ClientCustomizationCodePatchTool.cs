// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.ComponentModel;
using System.Text.RegularExpressions;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;

namespace Azure.Sdk.Tools.Cli.Microagents.Tools;

/// <summary>
/// Input for surgical code patching within customization files.
/// Uses a hybrid approach: line numbers for location + text matching for precision.
/// </summary>
public record ClientCustomizationCodePatchInput(
    [property: Description("Path to the customization file (relative to customization root or just filename)")]
    string FilePath,

    [property: Description("1-based start line number of the region containing the text to replace")]
    int StartLine,

    [property: Description("1-based end line number of the region (inclusive). Use same as StartLine for single-line edits.")]
    int EndLine,

    [property: Description("The EXACT text fragment to find and replace within the specified line range. Must match exactly.")]
    string OldText,

    [property: Description("The replacement text. Only OldText is replaced, preserving surrounding code.")]
    string NewText);

public record ClientCustomizationCodePatchOutput(
    [property: Description("Whether the patch was successfully applied")] bool Success,
    [property: Description("Success or error message describing the result")] string Message);

/// <summary>
/// Code patching tool that combines line-based targeting with text replacement.
/// </summary>
/// <remarks>
/// The approach solves two problems:
/// <list type="number">
/// <item><description>Line numbers narrow down WHERE to patch (avoiding "content not found" across entire file)</description></item>
/// <item><description>OldText/NewText provide SURGICAL precision (avoiding multi-line syntax corruption)</description></item>
/// </list>
/// </remarks>
/// <param name="baseDir">The base directory for resolving file paths. Cannot be null.</param>
/// <exception cref="ArgumentNullException">Thrown when <paramref name="baseDir"/> is null.</exception>
public class ClientCustomizationCodePatchTool(string baseDir) : AgentTool<ClientCustomizationCodePatchInput, ClientCustomizationCodePatchOutput>
{
    private readonly string _baseDir = baseDir ?? throw new ArgumentNullException(nameof(baseDir));

    // Compiled regex patterns for performance
    private static readonly Regex LineNumberPrefixRegex = new(@"^(\s*)\d+:\s?", RegexOptions.Compiled);
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex EscapedQuoteRegex = new(@"\\""", RegexOptions.Compiled);
    private static readonly Regex UnescapedQuoteRegex = new(@"(?<!\\)""", RegexOptions.Compiled);
    private static readonly Regex InternalQuoteRegex = new(@"(?<!\\)""(?![\s]*[+);]|$)", RegexOptions.Compiled);
    private static readonly Regex QuotePairRegex = new(@"(?<!\\)""([^""\\]*?)(?<!\\)""", RegexOptions.Compiled);

    /// <inheritdoc />
    public override string Name { get; init; } = "ClientCustomizationCodePatch";
    /// <inheritdoc />
    public override string Description { get; init; } =
        "Code patch: finds OldText within lines StartLine-EndLine and replaces with NewText. " +
        "Use for precise edits that preserve surrounding syntax.";

    /// <summary>
    /// Gets the list of patches successfully applied by this tool instance.
    /// </summary>
    /// <value>A list of <see cref="AppliedPatch"/> records describing each successful patch.</value>
    public List<AppliedPatch> AppliedPatches { get; } = [];

    /// <summary>
    /// Tracks which text has been replaced in each file to prevent duplicate patches.
    /// Key: normalized file path. Value: set of OldText strings that have been replaced.
    /// </summary>
    private readonly Dictionary<string, HashSet<string>> _appliedTextReplacements = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="input"/> is null.</exception>
    public override async Task<ClientCustomizationCodePatchOutput> Invoke(ClientCustomizationCodePatchInput input, CancellationToken ct)
    {
        // Validate inputs
        if (string.IsNullOrWhiteSpace(input.FilePath))
        {
            return new ClientCustomizationCodePatchOutput(false, "FilePath cannot be empty");
        }

        if (input.StartLine < 1)
        {
            return new ClientCustomizationCodePatchOutput(false, "StartLine must be >= 1");
        }

        if (input.EndLine < input.StartLine)
        {
            return new ClientCustomizationCodePatchOutput(false, "EndLine must be >= StartLine");
        }

        if (string.IsNullOrEmpty(input.OldText))
        {
            return new ClientCustomizationCodePatchOutput(false, "OldText cannot be empty - specify the exact text to replace");
        }

        // NewText can be empty (for deletion)
        var newText = input.NewText ?? "";

        // Resolve file path
        if (!ToolHelpers.TryGetSafeFullPath(_baseDir, input.FilePath, out var safeFilePath))
        {
            return new ClientCustomizationCodePatchOutput(false, "File path is outside the customization directory");
        }

        try
        {
            // If the resolved path doesn't exist, try to find the file by name within baseDir
            if (!File.Exists(safeFilePath))
            {
                var fileName = Path.GetFileName(input.FilePath);
                var candidates = Directory.GetFiles(_baseDir, fileName, SearchOption.AllDirectories);
                if (candidates.Length == 1)
                {
                    safeFilePath = candidates[0];
                }
                else
                {
                    var hint = candidates.Length == 0
                        ? "No file with that name exists under the customization root."
                        : $"Multiple matches found: {string.Join(", ", candidates.Select(c => Path.GetRelativePath(_baseDir, c)))}. Use a more specific path.";
                    return new ClientCustomizationCodePatchOutput(false, $"File not found: {input.FilePath}. {hint}");
                }
            }

            var normalizedFileKey = safeFilePath;

            // Check for duplicate replacement
            if (_appliedTextReplacements.TryGetValue(normalizedFileKey, out var appliedSet) &&
                appliedSet.Contains(input.OldText))
            {
                return new ClientCustomizationCodePatchOutput(false,
                    $"This exact text has already been replaced in {input.FilePath}. " +
                    "Re-read the file to see current state, or specify different OldText.");
            }

            // Read the file
            var allLines = await File.ReadAllLinesAsync(safeFilePath, ct).ConfigureAwait(false);
            var totalLines = allLines.Length;

            // Validate line range
            if (input.StartLine > totalLines)
            {
                return new ClientCustomizationCodePatchOutput(false,
                    $"StartLine {input.StartLine} is beyond end of file ({totalLines} lines)");
            }

            if (input.EndLine > totalLines)
            {
                return new ClientCustomizationCodePatchOutput(false,
                    $"EndLine {input.EndLine} is beyond end of file ({totalLines} lines)");
            }

            // Extract the target line range (convert to 0-based indices)
            var startIdx = input.StartLine - 1;
            var endIdx = input.EndLine - 1;

            // Join the lines in the range to search for OldText
            var targetRegion = string.Join("\n", allLines.Skip(startIdx).Take(endIdx - startIdx + 1));

            // Strip line number prefixes from OldText (LLM might copy from ReadFile output)
            var cleanOldText = StripLineNumberPrefixes(input.OldText);
            var cleanNewText = StripLineNumberPrefixes(newText);

            // Try exact match first
            int matchIndex = targetRegion.IndexOf(cleanOldText, StringComparison.Ordinal);

            // If exact match fails, try with normalized whitespace
            if (matchIndex < 0)
            {
                var normalizedRegion = NormalizeWhitespace(targetRegion);
                var normalizedOldText = NormalizeWhitespace(cleanOldText);

                if (normalizedRegion.Contains(normalizedOldText, StringComparison.Ordinal))
                {
                    // Find actual position using fuzzy matching
                    matchIndex = FindFuzzyMatch(targetRegion, cleanOldText);
                }
            }

            if (matchIndex < 0)
            {
                var regionPreview = targetRegion.Length > 300
                    ? targetRegion[..300] + "..."
                    : targetRegion;
                return new ClientCustomizationCodePatchOutput(false,
                    $"OldText not found in lines {input.StartLine}-{input.EndLine}.\n" +
                    $"Looking for: \"{TruncateForDisplay(cleanOldText, 100)}\"\n" +
                    $"Region content: \"{TruncateForDisplay(regionPreview, 300)}\"");
            }

            // Check for multiple matches 
            var secondMatch = targetRegion.IndexOf(cleanOldText, matchIndex + 1, StringComparison.Ordinal);
            if (secondMatch >= 0)
            {
                return new ClientCustomizationCodePatchOutput(false,
                    $"OldText appears multiple times in lines {input.StartLine}-{input.EndLine}. " +
                    "Include more surrounding context in OldText to make it unique.");
            }

            // Auto-preserve escaping: if OldText has escaped quotes but NewText lost them, restore them
            var preservedNewText = PreserveEscaping(cleanOldText, cleanNewText);

            // Perform the replacement
            var updatedRegion = targetRegion[..matchIndex] + preservedNewText + targetRegion[(matchIndex + cleanOldText.Length)..];

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

            // Record the replacement to prevent duplicates
            if (!_appliedTextReplacements.ContainsKey(normalizedFileKey))
            {
                _appliedTextReplacements[normalizedFileKey] = new HashSet<string>();
            }
            _appliedTextReplacements[normalizedFileKey].Add(input.OldText);

            var description = $"Replaced \"{TruncateForDisplay(cleanOldText, 50)}\" with \"{TruncateForDisplay(cleanNewText, 50)}\" in lines {input.StartLine}-{input.EndLine}";
            AppliedPatches.Add(new AppliedPatch(input.FilePath, description, 1));

            return new ClientCustomizationCodePatchOutput(true, $"Patch applied to {input.FilePath}: {description}");
        }
        catch (Exception ex)
        {
            return new ClientCustomizationCodePatchOutput(false, $"Failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Strips line number prefixes from text that might have been copied from ReadFile output.
    /// </summary>
    /// <param name="text">The text that may contain line number prefixes.</param>
    /// <returns>The text with line number prefixes removed.</returns>
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
    /// <param name="text">The text to normalize.</param>
    /// <returns>The text with all whitespace sequences replaced by single spaces.</returns>
    private static string NormalizeWhitespace(string text)
    {
        return WhitespaceRegex.Replace(text, " ").Trim();
    }

    /// <summary>
    /// Finds the position of oldText in targetRegion using fuzzy whitespace matching.
    /// </summary>
    /// <param name="targetRegion">The region of text to search within.</param>
    /// <param name="oldText">The text to find.</param>
    /// <returns>The start index in the original string, or -1 if not found.</returns>
    private static int FindFuzzyMatch(string targetRegion, string oldText)
    {
        var normalizedOld = NormalizeWhitespace(oldText);

        // Sliding window search
        for (int i = 0; i <= targetRegion.Length - oldText.Length / 2; i++)
        {
            // Extract a candidate region starting at i
            var candidateEnd = Math.Min(i + oldText.Length * 2, targetRegion.Length);
            var candidate = targetRegion[i..candidateEnd];
            var normalizedCandidate = NormalizeWhitespace(candidate);

            if (normalizedCandidate.StartsWith(normalizedOld, StringComparison.Ordinal))
            {
                // Found it - now find the exact end position
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Truncates a string for display in error messages.
    /// </summary>
    /// <param name="text">The text to truncate.</param>
    /// <param name="maxLength">The maximum length of the returned string.</param>
    /// <returns>The truncated text with special characters escaped.</returns>
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
    /// <param name="oldText">The original text containing the expected escaping pattern.</param>
    /// <param name="newText">The replacement text that may have lost escaping.</param>
    /// <returns>The newText with escaping patterns restored to match oldText.</returns>
    /// <remarks>
    /// If OldText contains escaped quotes (\") but NewText has unescaped quotes ("),
    /// this method re-escapes them to maintain Java string literal syntax.
    /// </remarks>
    private static string PreserveEscaping(string oldText, string newText)
    {
        if (string.IsNullOrEmpty(oldText) || string.IsNullOrEmpty(newText))
        {
            return newText;
        }

        // Count escaped quotes in OldText: \"
        int oldEscapedQuotes = EscapedQuoteRegex.Matches(oldText).Count;

        if (oldEscapedQuotes == 0)
        {
            // OldText has no escaped quotes, no preservation needed
            return newText;
        }

        // Count escaped quotes in NewText
        int newEscapedQuotes = EscapedQuoteRegex.Matches(newText).Count;

        // Count unescaped internal quotes in NewText (quotes not preceded by backslash)
        // Exclude quotes at string boundaries (start/end of lines after + ")
        var unescapedQuoteMatches = UnescapedQuoteRegex.Matches(newText);

        if (newEscapedQuotes >= oldEscapedQuotes || unescapedQuoteMatches.Count == 0)
        {
            // NewText has same or more escaped quotes, or no quotes at all - no fix needed
            return newText;
        }

        // NewText has unescaped quotes that should probably be escaped
        // Re-escape internal quotes (not boundary quotes like + " or ");)
        var result = InternalQuoteRegex.Replace(newText, @"\""");

        // Also handle the common case of quotes in method arguments like stringJoin(",", ...)
        // These should be escaped: the comma between quotes
        result = QuotePairRegex.Replace(result, @"\""$1\""");

        return result;
    }
}
