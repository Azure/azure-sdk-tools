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
                .Where(toolOrSkill => !triggeredToolsAndSkills.Any(triggered => triggered.Contains(toolOrSkill, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            if (missingTriggers.Count == 0)
            {
                return Task.FromResult(ValidationResult.Pass(Name,
                    $"All specified tools and skills were triggered: {string.Join(", ", ToolAndSkillToCheck)}"));
            }
            return Task.FromResult(ValidationResult.Fail(Name,
                $"The following tools or skills were expected to be triggered but were not: {string.Join(", ", missingTriggers)}"));
        }
    }
}
