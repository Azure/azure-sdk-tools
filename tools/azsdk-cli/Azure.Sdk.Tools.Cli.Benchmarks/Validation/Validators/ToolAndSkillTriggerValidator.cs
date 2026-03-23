using Azure.Sdk.Tools.Cli.Benchmarks.Models;

namespace Azure.Sdk.Tools.Cli.Benchmarks.Validation.Validators
{
    public class ToolAndSkillTriggerValidator : IValidator
    {
        public string Name { get; }
        public IReadOnlyList<string> ToolAndSkillToCheck { get; }

        public ToolAndSkillTriggerValidator(string name, IEnumerable<string> toolAndSkillToCheck)
        {
            Name = name;
            ToolAndSkillToCheck = toolAndSkillToCheck.ToList();
        }

        public Task<ValidationResult> ValidateAsync(
        ValidationContext context,
        CancellationToken cancellationToken = default)
        {
            var triggeredToolsAndSkills = context.ToolCalls;
            var missingTriggers = ToolAndSkillToCheck
                .Where(toolOrSkill => !triggeredToolsAndSkills.Any(triggered => MatchesToolOrSkill(triggered, toolOrSkill)))
                .ToList();
            if (missingTriggers.Count == 0)
            {
                return Task.FromResult(ValidationResult.Pass(Name,
                    $"All specified tools and skills were triggered: {string.Join(", ", ToolAndSkillToCheck)}"));
            }
            return Task.FromResult(ValidationResult.Fail(Name,
                $"The following tools or skills were expected to be triggered but were not: {string.Join(", ", missingTriggers)}"));
        }
        
        private static bool MatchesToolOrSkill(ToolCallRecord record, string expected)
        {
            if (record.ToolName.Contains(expected, StringComparison.OrdinalIgnoreCase))
            { 
                return true;
            }

            // For skill invocations, the ToolName is "skill" and the actual skill name is in ToolArgs
            if (record.ToolName.Equals("skill", StringComparison.OrdinalIgnoreCase) && record.ToolArgs != null)
            {
                var argsString = record.ToolArgs.ToString();
                if (argsString != null && argsString.Contains(expected, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
