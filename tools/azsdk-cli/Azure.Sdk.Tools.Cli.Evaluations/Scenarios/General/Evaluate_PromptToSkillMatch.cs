using System.Text.Json;
using Azure.Sdk.Tools.Cli.Evaluations.Evaluators;
using Azure.Sdk.Tools.Cli.Evaluations.Helpers;
using Azure.Sdk.Tools.Cli.Evaluations.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using NUnit.Framework;

namespace Azure.Sdk.Tools.Cli.Evaluations.Scenarios
{
    /// <summary>
    /// Data-driven tests that evaluate skill discoverability using embedding similarity.
    /// Loads skills from .github/skills/*/SKILL.md and test prompts from prompts.json files.
    /// Uses the same evaluation approach as PromptToToolMatchEvaluator.
    /// </summary>
    public partial class Scenario
    {
        private static List<SkillInfo>? s_skills;
        private static Dictionary<string, SkillTestPrompts>? s_skillPrompts;

        /// <summary>
        /// Load skills and their test prompts
        /// </summary>
        private void EnsureSkillsLoaded()
        {
            if (s_skills != null && s_skillPrompts != null)
            {
                return;
            }

            var repoRoot = TestSetup.GetRepositoryRoot();
            s_skills = SkillLoader.LoadAllSkills(repoRoot);
            s_skillPrompts = LoadSkillTestPrompts(repoRoot);
        }

        /// <summary>
        /// Loads test prompts from .github/skills/tests/{skill-name}/prompts.json
        /// </summary>
        private static Dictionary<string, SkillTestPrompts> LoadSkillTestPrompts(string repoRoot)
        {
            var result = new Dictionary<string, SkillTestPrompts>(StringComparer.OrdinalIgnoreCase);
            var testsDir = Path.Combine(repoRoot, ".github", "skills", "tests");

            if (!Directory.Exists(testsDir))
            {
                return result;
            }

            foreach (var skillTestDir in Directory.GetDirectories(testsDir))
            {
                var promptsFile = Path.Combine(skillTestDir, "prompts.json");
                if (File.Exists(promptsFile))
                {
                    try
                    {
                        var json = File.ReadAllText(promptsFile);
                        var prompts = JsonSerializer.Deserialize<SkillTestPrompts>(json, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                        if (prompts != null && !string.IsNullOrEmpty(prompts.SkillName))
                        {
                            result[prompts.SkillName] = prompts;
                        }
                    }
                    catch (JsonException)
                    {
                        // Skip malformed files
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Evaluates that prompts correctly trigger the expected skill.
        /// Uses embedding similarity to compare prompts against skill descriptions.
        /// </summary>
        [Test]
        [TestCaseSource(nameof(GetSkillShouldTriggerTestCases))]
        public async Task Evaluate_PromptToSkillMatch_ShouldTrigger(string skillName, string prompt)
        {
            EnsureSkillsLoaded();

            var skill = s_skills!.FirstOrDefault(s => s.Name.Equals(skillName, StringComparison.OrdinalIgnoreCase));
            Assert.That(skill, Is.Not.Null, $"Skill '{skillName}' not found in .github/skills/");

            // Convert skills to AIFunctions for the evaluator
            var skillsAsTools = s_skills!.Select(s => AIFunctionFactory.Create(
                () => { },
                name: s.Name,
                description: s.Description
            )).ToList();

            var evaluator = new PromptToToolMatchEvaluator();
            var promptHash = Math.Abs(prompt.GetHashCode()).ToString();
            var sanitizedScenarioName = $"PromptToSkillMatch_{skillName}_{promptHash}";

            var result = await EvaluationHelper.RunScenarioAsync(
                messages: [],
                response: new ChatResponse(),
                scenarioName: sanitizedScenarioName,
                chatConfig: s_chatConfig!,
                executionName: s_executionName,
                reportingPath: ReportingPath,
                evaluators: [evaluator],
                enableResponseCaching: false,
                additionalContexts:
                [
                    new PromptToToolMatchEvaluatorContext(
                        prompt: prompt,
                        expectedToolNames: [skillName],
                        availableTools: skillsAsTools)
                ]);

            EvaluationHelper.ValidatePromptToToolMatchEvaluator(result);
        }

        /// <summary>
        /// Evaluates that prompts do NOT incorrectly trigger a skill.
        /// These are negative test cases - prompts that should match OTHER skills or nothing.
        /// </summary>
        [Test]
        [TestCaseSource(nameof(GetSkillShouldNotTriggerTestCases))]
        public async Task Evaluate_PromptToSkillMatch_ShouldNotTrigger(string skillName, string prompt)
        {
            EnsureSkillsLoaded();

            var skill = s_skills!.FirstOrDefault(s => s.Name.Equals(skillName, StringComparison.OrdinalIgnoreCase));
            Assert.That(skill, Is.Not.Null, $"Skill '{skillName}' not found in .github/skills/");

            // Convert skills to AIFunctions for the evaluator
            var skillsAsTools = s_skills!.Select(s => AIFunctionFactory.Create(
                () => { },
                name: s.Name,
                description: s.Description
            )).ToList();

            var evaluator = new PromptToToolMatchEvaluator();
            var promptHash = Math.Abs(prompt.GetHashCode()).ToString();
            var sanitizedScenarioName = $"PromptToSkillNotMatch_{skillName}_{promptHash}";

            var result = await EvaluationHelper.RunScenarioAsync(
                messages: [],
                response: new ChatResponse(),
                scenarioName: sanitizedScenarioName,
                chatConfig: s_chatConfig!,
                executionName: s_executionName,
                reportingPath: ReportingPath,
                evaluators: [evaluator],
                enableResponseCaching: false,
                additionalContexts:
                [
                    new PromptToToolMatchEvaluatorContext(
                        prompt: prompt,
                        expectedToolNames: [skillName],
                        availableTools: skillsAsTools)
                ]);

            // For shouldNotTrigger, we expect the evaluator to FAIL (skill should NOT match)
            var metric = result.Get<BooleanMetric>(PromptToToolMatchEvaluator.MatchMetricName);
            Assert.That(metric, Is.Not.Null, "Expected BooleanMetric from evaluator");

            // The test passes if the skill did NOT match the prompt
            // (i.e., the evaluator returned false or failed to find the skill in top K)
            Assert.That(metric.Value, Is.False,
                $"Skill '{skillName}' should NOT match prompt: \"{prompt}\". " +
                "If this is a valid match, move the prompt to shouldTrigger in prompts.json.");
        }

        /// <summary>
        /// Provides shouldTrigger test cases from prompts.json files.
        /// </summary>
        public static IEnumerable<TestCaseData> GetSkillShouldTriggerTestCases()
        {
            var repoRoot = TestSetup.GetRepositoryRoot();
            var prompts = LoadSkillTestPrompts(repoRoot);

            foreach (var (skillName, testPrompts) in prompts)
            {
                if (testPrompts.ShouldTrigger == null) continue;

                foreach (var prompt in testPrompts.ShouldTrigger)
                {
                    var truncated = prompt.Length > 50 ? prompt[..47] + "..." : prompt;
                    yield return new TestCaseData(skillName, prompt)
                        .SetName($"Skill_{skillName}_ShouldTrigger: {truncated}")
                        .SetCategory("skills");
                }
            }
        }

        /// <summary>
        /// Provides shouldNotTrigger test cases from prompts.json files.
        /// </summary>
        public static IEnumerable<TestCaseData> GetSkillShouldNotTriggerTestCases()
        {
            var repoRoot = TestSetup.GetRepositoryRoot();
            var prompts = LoadSkillTestPrompts(repoRoot);

            foreach (var (skillName, testPrompts) in prompts)
            {
                if (testPrompts.ShouldNotTrigger == null) continue;

                foreach (var prompt in testPrompts.ShouldNotTrigger)
                {
                    var truncated = prompt.Length > 50 ? prompt[..47] + "..." : prompt;
                    yield return new TestCaseData(skillName, prompt)
                        .SetName($"Skill_{skillName}_ShouldNotTrigger: {truncated}")
                        .SetCategory("skills");
                }
            }
        }
    }

    /// <summary>
    /// Model for prompts.json test file
    /// </summary>
    public class SkillTestPrompts
    {
        public string SkillName { get; set; } = "";
        public string? Description { get; set; }
        public List<string>? ShouldTrigger { get; set; }
        public List<string>? ShouldNotTrigger { get; set; }
    }
}
