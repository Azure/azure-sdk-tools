namespace Azure.Sdk.Tools.Cli.Prompts;

/// <summary>
/// Provides prompts used in validation operations including spelling, changelog, and README checks.
/// </summary>
public static class ValidationPrompts
{
    /// <summary>
    /// Prompt template for automated spelling fix recommendations using LLM analysis.
    /// Analyzes cspell output to determine whether tokens should be fixed or added to dictionary.
    /// Use {0} as placeholder for cspell output.
    /// </summary>
    public const string SpellingFixRecommendationTemplate = """
        You are an automated spelling helper. You will be provided with the output from cspell lint for a code repository.
        For each reported token, decide whether it is a typo that should be corrected, or a legitimate technical/proper term that should be added to the cspell dictionary (the cspell.json 'words' list).

        Requirements:
        1) Output ONLY valid JSON: an array of objects. Each object must contain the following fields:
           - file: path to the file containing the token (relative to repository root)
           - line: the line number where the token appears
           - original: the original token as reported by cspell
           - recommendation: an object with these fields:
               * action: one of 'fix' or 'ignore' ('fix' means propose a replacement, 'ignore' means add to cspell.json words)
               * replacement: (string) present only if action == 'fix' — the corrected token or replacement text
               * justification: (string) a brief one-line justification for the recommendation

        2) If you recommend 'ignore', ensure you explain why this word should be kept (e.g. product name, acronym, code identifier, or language-specific term).
        3) Keep replacements minimal and preserve code formatting and casing.
        """;

    /// <summary>
    /// Gets the spelling fix recommendation prompt with the provided cspell output.
    /// </summary>
    /// <param name="cspellOutput">The output from cspell lint command</param>
    /// <returns>Formatted prompt ready for LLM consumption</returns>
    public static string GetSpellingFixRecommendationPrompt(string cspellOutput)
    {
        return SpellingFixRecommendationTemplate + $"""

        cspell lint output:
        {cspellOutput}

        Return the JSON array now.
        """;
    }
}