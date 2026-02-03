namespace Azure.Sdk.Tools.Cli.Evaluations.Models
{
    /// <summary>
    /// Represents a skill loaded from a SKILL.md file.
    /// Used for testing skill discoverability with the same evaluator as MCP tools.
    /// </summary>
    public record SkillInfo(
        /// <summary>
        /// The skill name from the YAML frontmatter.
        /// </summary>
        string Name,

        /// <summary>
        /// The skill description from the YAML frontmatter.
        /// This is the text that gets matched against user prompts.
        /// </summary>
        string Description,

        /// <summary>
        /// Path to the SKILL.md file (for diagnostics).
        /// </summary>
        string FilePath
    );
}
