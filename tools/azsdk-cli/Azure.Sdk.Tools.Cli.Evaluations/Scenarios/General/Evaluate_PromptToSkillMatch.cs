using System.Net.Http;
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
    /// Loads skills from two sources:
    /// 1. Local SKILL.md files in .github/skills/ (for skills that live in this repo)
    /// 2. External skills via runtime fetch from GitHub using the source field in prompts.json
    /// Uses the same evaluation approach as PromptToToolMatchEvaluator.
    /// </summary>
    public partial class Scenario
    {
        private static List<SkillInfo>? s_skills;
        private static Dictionary<string, SkillTestPrompts>? s_skillPrompts;
        private static readonly HttpClient s_httpClient = new();

        /// <summary>
        /// Load skills from local SKILL.md files and merge with external skills.
        /// External skills with a source field are fetched from GitHub at runtime
        /// to ensure the description is always up-to-date.
        /// Falls back to the cached description in prompts.json if the fetch fails.
        /// </summary>
        private async Task EnsureSkillsLoadedAsync()
        {
            if (s_skills != null && s_skillPrompts != null)
            {
                return;
            }

            var repoRoot = TestSetup.GetRepositoryRoot();
            var localSkills = SkillLoader.LoadAllSkills(repoRoot);
            s_skillPrompts = LoadSkillTestPrompts(repoRoot);

            // Merge: start with local skills, then add external skills from prompts.json
            var localSkillNames = localSkills.Select(s => s.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var allSkills = new List<SkillInfo>(localSkills);

            foreach (var (skillName, testPrompts) in s_skillPrompts)
            {
                if (localSkillNames.Contains(skillName))
                {
                    continue;
                }

                // For external skills, try to fetch the live description from GitHub
                var description = await FetchExternalSkillDescriptionAsync(skillName, testPrompts);

                if (!string.IsNullOrWhiteSpace(description))
                {
                    var sourcePath = testPrompts.Source != null
                        ? $"{testPrompts.Source.Repo}/{testPrompts.Source.Path}"
                        : $"tests/{skillName}/prompts.json";
                    allSkills.Add(new SkillInfo(skillName, description, sourcePath));
                }
            }

            s_skills = allSkills;
        }

        /// <summary>
        /// Fetches the skill description from GitHub for external skills.
        /// If the source field is set, downloads the raw SKILL.md and parses the frontmatter.
        /// Fails with a clear error if the source is defined but the fetch fails — no silent fallback.
        /// </summary>
        private static async Task<string?> FetchExternalSkillDescriptionAsync(string skillName, SkillTestPrompts testPrompts)
        {
            if (testPrompts.Source == null ||
                string.IsNullOrEmpty(testPrompts.Source.Repo) ||
                string.IsNullOrEmpty(testPrompts.Source.Path))
            {
                // No source defined — this is only valid for local skills (which are loaded from SKILL.md).
                // External skills without source have no way to get a description.
                return null;
            }

            var rawUrl = $"https://raw.githubusercontent.com/{testPrompts.Source.Repo}/main/{testPrompts.Source.Path}";
            string content;

            try
            {
                content = await s_httpClient.GetStringAsync(rawUrl);
            }
            catch (HttpRequestException ex)
            {
                throw new InvalidOperationException(
                    $"Failed to fetch SKILL.md for skill '{skillName}' from {rawUrl}. " +
                    $"Ensure the source repo/path in tests/{skillName}/prompts.json is correct and accessible.",
                    ex);
            }

            var skillInfo = SkillLoader.ParseSkillContent(content, rawUrl)
                ?? throw new InvalidOperationException(
                    $"Could not parse SKILL.md frontmatter for skill '{skillName}' from {rawUrl}. " +
                    $"Ensure the file has valid YAML frontmatter with name and description fields.");

            TestContext.WriteLine($"  Fetched live description for skill '{skillName}' from {testPrompts.Source.Repo}");
            return skillInfo.Description;
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
            await EnsureSkillsLoadedAsync();

            var skill = s_skills!.FirstOrDefault(s => s.Name.Equals(skillName, StringComparison.OrdinalIgnoreCase));
            Assert.That(skill, Is.Not.Null,
                $"Skill '{skillName}' not found. Ensure it has a SKILL.md in .github/skills/ or a description in tests/{skillName}/prompts.json");

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
            await EnsureSkillsLoadedAsync();

            var skill = s_skills!.FirstOrDefault(s => s.Name.Equals(skillName, StringComparison.OrdinalIgnoreCase));
            Assert.That(skill, Is.Not.Null,
                $"Skill '{skillName}' not found. Ensure it has a SKILL.md in .github/skills/ or a description in tests/{skillName}/prompts.json");

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

        /// <summary>
        /// Validates that all skills have test prompts defined in prompts.json.
        /// This ensures skill authors add prompt variations for discoverability testing.
        /// Mirrors the Evaluate_AllToolsHaveTestPrompts pattern for tools.
        /// </summary>
        [Test]
        [Category("skills")]
        public async Task Evaluate_AllSkillsHaveTestPrompts()
        {
            await EnsureSkillsLoadedAsync();

            var allSkillNames = s_skills!.Select(s => s.Name).ToList();
            var skillsWithPrompts = s_skillPrompts!.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);

            var skillsWithoutPrompts = allSkillNames
                .Where(name => !skillsWithPrompts.Contains(name))
                .ToList();

            // Log coverage statistics
            var totalSkills = allSkillNames.Count;
            var coveredSkills = allSkillNames.Count(name => skillsWithPrompts.Contains(name));

            TestContext.WriteLine($"\n=== Skill Test Prompt Coverage ===");
            if (totalSkills == 0)
            {
                TestContext.WriteLine("No skills found in .github/skills/; skipping coverage statistics.");
                return;
            }

            var totalShouldTrigger = s_skillPrompts!.Values.Sum(p => p.ShouldTrigger?.Count ?? 0);
            var totalShouldNotTrigger = s_skillPrompts!.Values.Sum(p => p.ShouldNotTrigger?.Count ?? 0);

            TestContext.WriteLine($"Total skills: {totalSkills}");
            TestContext.WriteLine($"Skills with prompts: {coveredSkills}/{totalSkills} ({(double)coveredSkills / totalSkills:P0})");
            TestContext.WriteLine($"Skills without prompts: {skillsWithoutPrompts.Count}");
            TestContext.WriteLine($"Total shouldTrigger prompts: {totalShouldTrigger}");
            TestContext.WriteLine($"Total shouldNotTrigger prompts: {totalShouldNotTrigger}");

            if (coveredSkills > 0)
            {
                TestContext.WriteLine($"Average prompts per skill: {(double)(totalShouldTrigger + totalShouldNotTrigger) / coveredSkills:F1}");
            }

            // Validate prompt quality for skills that have prompts
            var skillsWithInsufficientPrompts = new List<string>();
            foreach (var (skillName, testPrompts) in s_skillPrompts!)
            {
                var hasShouldTrigger = testPrompts.ShouldTrigger?.Count > 0;
                var hasShouldNotTrigger = testPrompts.ShouldNotTrigger?.Count > 0;

                if (!hasShouldTrigger || !hasShouldNotTrigger)
                {
                    var missing = new List<string>();
                    if (!hasShouldTrigger) missing.Add("shouldTrigger");
                    if (!hasShouldNotTrigger) missing.Add("shouldNotTrigger");
                    skillsWithInsufficientPrompts.Add($"{skillName} (missing: {string.Join(", ", missing)})");
                }
            }

            if (skillsWithInsufficientPrompts.Any())
            {
                TestContext.WriteLine($"\n⚠️ Skills with incomplete prompts:");
                foreach (var skill in skillsWithInsufficientPrompts)
                {
                    TestContext.WriteLine($"  - {skill}");
                }
            }

            // FAIL if any skills are missing prompts entirely
            if (skillsWithoutPrompts.Any())
            {
                Assert.Fail($"Coverage gap: {skillsWithoutPrompts.Count} skill(s) have no test prompts. " +
                    $"Skill authors must add a prompts.json file with shouldTrigger and shouldNotTrigger arrays:\n" +
                    $"  - {string.Join("\n  - ", skillsWithoutPrompts)}\n\n" +
                    $"To add prompts, create: .github/skills/tests/{{skill-name}}/prompts.json");
            }

            // FAIL if any skills have incomplete prompts (missing shouldTrigger or shouldNotTrigger)
            if (skillsWithInsufficientPrompts.Any())
            {
                Assert.Fail($"Incomplete prompts: {skillsWithInsufficientPrompts.Count} skill(s) are missing required prompt arrays. " +
                    $"Each prompts.json must have both shouldTrigger and shouldNotTrigger:\n" +
                    $"  - {string.Join("\n  - ", skillsWithInsufficientPrompts)}");
            }
        }
    }

    /// <summary>
    /// Model for prompts.json test file
    /// </summary>
    public class SkillTestPrompts
    {
        public string SkillName { get; set; } = "";
        public SkillSource? Source { get; set; }
        public List<string>? ShouldTrigger { get; set; }
        public List<string>? ShouldNotTrigger { get; set; }
    }

    /// <summary>
    /// Points to the SKILL.md source in another repository.
    /// Used to fetch the live description at test time for external skills.
    /// </summary>
    public class SkillSource
    {
        public string Repo { get; set; } = "";
        public string Path { get; set; } = "";
    }
}
