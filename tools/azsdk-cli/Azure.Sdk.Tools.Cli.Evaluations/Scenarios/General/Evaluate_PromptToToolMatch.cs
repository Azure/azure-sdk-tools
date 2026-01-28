using Azure.Sdk.Tools.Cli.Evaluations.Evaluators;
using Azure.Sdk.Tools.Cli.Evaluations.Helpers;
using Azure.Sdk.Tools.Cli.Evaluations.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using NUnit.Framework;

namespace Azure.Sdk.Tools.Cli.Evaluations.Scenarios
{
    /// <summary>
    /// Data-driven tests that evaluate tool discoverability using embedding similarity.
    /// Loads test prompts from TestPrompts.json and validates that each prompt matches
    /// its expected tool with sufficient confidence.
    /// </summary>
    public partial class Scenario
    {
        private static TestPromptRegistry? s_promptRegistry;
        private static IReadOnlyList<AIFunction>? s_tools;

        /// <summary>
        /// Load test prompts and tools for data-driven tests
        /// </summary>
        private async Task EnsurePromptRegistryLoadedAsync()
        {
            s_promptRegistry ??= await TestPromptRegistry.LoadFromDefaultPathAsync();
            s_tools ??= (await s_mcpClient!.ListToolsAsync()).ToList();
        }

        /// <summary>
        /// Evaluates a single prompt against its expected tool using embedding similarity.
        /// The test passes if the expected tool ranks in top 3 with ≥40% confidence.
        /// </summary>
        [Test]
        [TestCaseSource(nameof(GetPromptTestCases))]
        public async Task Evaluate_PromptToToolMatch(string toolName, string prompt)
        {
            await EnsurePromptRegistryLoadedAsync();

            var evaluator = new PromptToToolMatchEvaluator();

            // Use a simple sanitized scenario name that won't have invalid path characters
            var promptHash = Math.Abs(prompt.GetHashCode()).ToString();
            var sanitizedScenarioName = $"PromptToToolMatch_{toolName}_{promptHash}";

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
                        expectedToolNames: [toolName],
                        availableTools: s_tools!)
                ]);

            EvaluationHelper.ValidatePromptToToolMatchEvaluator(result);
        }

        /// <summary>
        /// Validates that all MCP tools have at least one test prompt defined.
        /// This ensures tool owners add prompts for their tools.
        /// </summary>
        [Test]
        public async Task Evaluate_AllToolsHaveTestPrompts()
        {
            await EnsurePromptRegistryLoadedAsync();

            var allToolNames = s_tools!.Select(t => t.Name).ToList();
            var toolsWithoutPrompts = s_promptRegistry!.GetToolsWithoutPrompts(allToolNames).ToList();

            // Filter out example/test tools that don't need prompts
            var exemptTools = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "azsdk_hello_world",
                "azsdk_hello_world_fail",
                "azsdk_example_process_execution",
                "azsdk_example_powershell_execution",
                "azsdk_example_azure_service",
                "azsdk_example_ai_service",
                "azsdk_example_error_handling",
                "azsdk_example_microagent_fibonacci",
                "azsdk_example_github_service",
                "azsdk_example_devops_service"
            };

            var missingTools = toolsWithoutPrompts
                .Where(t => !exemptTools.Contains(t))
                .ToList();

            // Log coverage statistics first (always useful to see)
            var promptCounts = s_promptRegistry.GetPromptCountsByTool();
            var totalPrompts = s_promptRegistry.Prompts.Count;
            var toolsWithPrompts = promptCounts.Count;
            var totalTools = allToolNames.Count - exemptTools.Count(t => allToolNames.Contains(t));

            TestContext.WriteLine($"\n=== Test Prompt Coverage ===");
            if (totalTools == 0)
            {
                TestContext.WriteLine("No non-exempt tools found; skipping coverage statistics.");
                return;
            }

            TestContext.WriteLine($"Total prompts: {totalPrompts}");
            TestContext.WriteLine($"Tools with prompts: {toolsWithPrompts}/{totalTools} ({(double)toolsWithPrompts / totalTools:P0})");
            TestContext.WriteLine($"Tools without prompts: {missingTools.Count}");
            TestContext.WriteLine($"Average prompts per tool: {(toolsWithPrompts == 0 ? "N/A" : $"{(double)totalPrompts / toolsWithPrompts:F1}")}");

            // FAIL if any tools are missing prompts - enforces tool owners to add prompts
            if (missingTools.Any())
            {
                Assert.Fail($"Coverage gap: {missingTools.Count} tool(s) have no test prompts in TestPrompts.json. " +
                    $"Tool owners must add 2-3 prompt variations for each:\n" +
                    $"  - {string.Join("\n  - ", missingTools)}\n\n" +
                    $"To add prompts, edit: tools/azsdk-cli/Azure.Sdk.Tools.Cli.Evaluations/TestData/TestPrompts.json");
            }
        }

        /// <summary>
        /// Provides test cases from TestPrompts.json for the data-driven test.
        /// Each test case is a (toolName, prompt) pair.
        /// </summary>
        public static IEnumerable<TestCaseData> GetPromptTestCases()
        {
            // Load synchronously for test case source (NUnit requirement)
            TestPromptRegistry registry;
            try
            {
                registry = TestPromptRegistry.LoadFromDefaultPathAsync().GetAwaiter().GetResult();
            }
            catch (FileNotFoundException)
            {
                // Return empty if file not found - test will be skipped
                yield break;
            }

            foreach (var entry in registry.Prompts)
            {
                var truncatedPrompt = entry.Prompt.Length > 50
                    ? entry.Prompt.Substring(0, 47) + "..."
                    : entry.Prompt;

                yield return new TestCaseData(entry.ToolName, entry.Prompt)
                    .SetName($"{entry.ToolName}: {truncatedPrompt}")
                    .SetCategory(entry.Category);
            }
        }
    }
}
