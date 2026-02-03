using System.Text.RegularExpressions;
using Azure.Sdk.Tools.Cli.Evaluations.Models;

namespace Azure.Sdk.Tools.Cli.Evaluations.Helpers
{
    /// <summary>
    /// Loads skills from SKILL.md files in the .github/skills directory.
    /// Parses YAML frontmatter to extract name and description.
    /// </summary>
    public static class SkillLoader
    {
        private static readonly Regex FrontmatterRegex = new(
            @"^---\s*\n(.*?)\n---",
            RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly Regex NameRegex = new(
            @"^name:\s*[""']?([^""'\n]+)[""']?\s*$",
            RegexOptions.Multiline | RegexOptions.Compiled);

        private static readonly Regex DescriptionSingleLineRegex = new(
            @"^description:\s*[""']?([^""'\n]+)[""']?\s*$",
            RegexOptions.Multiline | RegexOptions.Compiled);

        private static readonly Regex DescriptionMultiLineRegex = new(
            @"^description:\s*\|?\s*\n((?:\s{2,}.+\n?)+)",
            RegexOptions.Multiline | RegexOptions.Compiled);

        /// <summary>
        /// Loads all skills from the .github/skills directory.
        /// </summary>
        /// <param name="repoRoot">Root of the repository containing .github/skills/</param>
        /// <returns>List of skills with their names and descriptions</returns>
        public static List<SkillInfo> LoadAllSkills(string repoRoot)
        {
            var skillsDir = Path.Combine(repoRoot, ".github", "skills");
            if (!Directory.Exists(skillsDir))
            {
                return new List<SkillInfo>();
            }

            var skills = new List<SkillInfo>();

            foreach (var dir in Directory.GetDirectories(skillsDir))
            {
                var dirName = Path.GetFileName(dir);
                // Skip special directories
                if (dirName.StartsWith("_") || dirName == "scripts" || dirName == "tests")
                {
                    continue;
                }

                var skillFile = Path.Combine(dir, "SKILL.md");
                if (File.Exists(skillFile))
                {
                    var skill = LoadSkillFromFile(skillFile);
                    if (skill != null)
                    {
                        skills.Add(skill);
                    }
                }
            }

            return skills;
        }

        /// <summary>
        /// Loads a single skill from a SKILL.md file.
        /// </summary>
        public static SkillInfo? LoadSkillFromFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            var content = File.ReadAllText(filePath);
            return ParseSkillContent(content, filePath);
        }

        /// <summary>
        /// Parses SKILL.md content to extract name and description from YAML frontmatter.
        /// </summary>
        public static SkillInfo? ParseSkillContent(string content, string filePath = "")
        {
            var frontmatterMatch = FrontmatterRegex.Match(content);
            if (!frontmatterMatch.Success)
            {
                return null;
            }

            var frontmatter = frontmatterMatch.Groups[1].Value;

            // Extract name
            var nameMatch = NameRegex.Match(frontmatter);
            if (!nameMatch.Success)
            {
                return null;
            }
            var name = nameMatch.Groups[1].Value.Trim();

            // Extract description (try single-line first, then multi-line)
            string? description = null;

            var singleLineMatch = DescriptionSingleLineRegex.Match(frontmatter);
            if (singleLineMatch.Success)
            {
                description = singleLineMatch.Groups[1].Value.Trim();
            }
            else
            {
                var multiLineMatch = DescriptionMultiLineRegex.Match(frontmatter);
                if (multiLineMatch.Success)
                {
                    // Join multi-line description, removing leading whitespace
                    var lines = multiLineMatch.Groups[1].Value
                        .Split('\n')
                        .Select(line => line.TrimStart())
                        .Where(line => !string.IsNullOrWhiteSpace(line));
                    description = string.Join(" ", lines);
                }
            }

            if (string.IsNullOrWhiteSpace(description))
            {
                return null;
            }

            return new SkillInfo(name, description, filePath);
        }
    }
}
