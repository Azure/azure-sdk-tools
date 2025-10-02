namespace Azure.Sdk.Tools.Cli.Prompts;

/// <summary>
/// Provides prompts used in validation operations including spelling, changelog, and README checks.
/// </summary>
public static class ValidationPrompts
{
    /// <summary>
    /// Prompt template for microagent-based automated spelling fixes.
    /// This prompt guides a microagent to automatically fix spelling issues by either correcting typos or adding legitimate terms to cspell.json.
    /// </summary>
    public const string MicroagentSpellingFixTemplate = """
        You are an automated spelling assistant for an Azure SDK repository. Analyze cspell lint output and fix spelling issues.

        Tasks:
        1. Analyze each reported spelling issue from the cspell output
        2. For each issue, either:
           - Fix the typo by correcting spelling in the source file
           - Add legitimate technical terms/product names/proper nouns to cspell.json
        3. Apply fixes by reading files, making corrections, and writing them back
        4. Update cspell.json 'words' array with legitimate words (never remove existing words)

        Guidelines:
        - Fix obvious typos in comments, documentation, and non-code text
        - Add technical terms, API names, product names, acronyms to cspell.json
        - Preserve exact casing and formatting

        Return a summary of operations performed.
        """;

    /// <summary>
    /// Gets the microagent spelling fix prompt with the provided cspell output.
    /// </summary>
    /// <param name="cspellOutput">The output from cspell lint command</param>
    /// <returns>Formatted prompt ready for microagent consumption</returns>
    public static string GetMicroagentSpellingFixPrompt(string cspellOutput)
    {
        return MicroagentSpellingFixTemplate + $"""

        cspell lint output to analyze:
        {cspellOutput}
        """;
    }
}