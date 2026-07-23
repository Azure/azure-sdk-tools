// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.Core;

/// <summary>
/// Validates that every MCP tool has test prompts in TestPrompts.json.
/// Runs during PR CI (no MCP server or OpenAI required).
/// </summary>
internal class ToolPromptCoverageTests
{
    // Tools that are only available in DEBUG builds or don't need prompts
    private static readonly HashSet<string> ExemptTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "azsdk_hello_world",
        "azsdk_hello_world_fail",
        "azsdk_example_process_execution",
        "azsdk_example_powershell_execution",
        "azsdk_example_azure_service",
        "azsdk_example_ai_service",
        "azsdk_example_error_handling",
        "azsdk_example_agent_fibonacci",
        "azsdk_example_github_service",
        "azsdk_example_devops_service",
        "azsdk_cleanup_ai_agents",
        "azsdk_upgrade",
        "azsdk_engsys_codeowner_view",
        "azsdk_engsys_codeowner_add_label_owner",
        "azsdk_engsys_codeowner_remove_label_owner",
        "azsdk_engsys_codeowner_add_package_owner",
        "azsdk_engsys_codeowner_remove_package_owner",
        "azsdk_engsys_codeowner_add_package_label",
        "azsdk_engsys_codeowner_remove_package_label"
    };

    [Test]
    public void AllToolsHaveTestPrompts()
    {
        // Discover all MCP tool names via reflection on the CLI assembly
        var cliAssembly = typeof(Azure.Sdk.Tools.Cli.Program).Assembly;
        var allToolNames = cliAssembly.GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            .SelectMany(m => m.GetCustomAttributes<McpServerToolAttribute>())
            .Select(attr => attr.Name)
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => name!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.That(allToolNames, Is.Not.Empty, "No MCP tools found via reflection. Is the CLI assembly loaded?");

        // Load TestPrompts.json
        var testPromptsPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", "TestPrompts.json");
        Assert.That(File.Exists(testPromptsPath), Is.True, $"TestPrompts.json not found at: {testPromptsPath}");

        var json = File.ReadAllText(testPromptsPath);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var file = JsonSerializer.Deserialize<TestPromptsFile>(json, options);
        Assert.That(file?.Prompts, Is.Not.Null, "Failed to deserialize TestPrompts.json");

        var toolsWithPrompts = file!.Prompts
            .Select(p => p.ToolName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingTools = allToolNames
            .Where(t => !ExemptTools.Contains(t))
            .Where(t => !toolsWithPrompts.Contains(t))
            .OrderBy(t => t)
            .ToList();

        if (missingTools.Any())
        {
            Assert.Fail(
                $"Coverage gap: {missingTools.Count} tool(s) have no test prompts in TestPrompts.json. " +
                $"Tool owners must add 2-3 prompt variations for each:\n" +
                $"  - {string.Join("\n  - ", missingTools)}\n\n" +
                $"To add prompts, edit: tools/azsdk-cli/Azure.Sdk.Tools.Cli.Evaluations/TestData/TestPrompts.json");
        }
    }

    private record TestPromptEntry(
        [property: JsonPropertyName("toolName")] string ToolName,
        [property: JsonPropertyName("prompt")] string Prompt,
        [property: JsonPropertyName("category")] string Category);

    private class TestPromptsFile
    {
        [JsonPropertyName("prompts")]
        public List<TestPromptEntry> Prompts { get; set; } = [];
    }
}
