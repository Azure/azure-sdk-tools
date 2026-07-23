// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using ModelContextProtocol.Server;
using YamlDotNet.RepresentationModel;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.Core;

/// <summary>
/// Validates that every MCP tool has at least one "required" tool-calls stimulus under
/// evals/tools/*.eval.yaml (the Vally prompt-to-tool suite). Runs during PR CI -- no MCP
/// server, no LLM, and no vally install required, since it only parses the eval YAML.
/// </summary>
internal class ToolPromptCoverageTests
{
    // Tools that are only available in DEBUG builds or don't need eval coverage
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
    public void AllToolsHaveEvalCoverage()
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

        // Discover the repo root by walking up from the test binary's directory, then read
        // every stimulus's tool-calls.required[].name across evals/tools/*.eval.yaml.
        var evalsToolsDir = Path.Combine(FindRepoRoot(), "evals", "tools");
        Assert.That(Directory.Exists(evalsToolsDir), Is.True, $"evals/tools directory not found at: {evalsToolsDir}");

        var toolsWithCoverage = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var evalFile in Directory.GetFiles(evalsToolsDir, "*.eval.yaml"))
        {
            foreach (var name in ExtractRequiredToolNames(evalFile))
            {
                toolsWithCoverage.Add(name);
            }
        }

        var missingTools = allToolNames
            .Where(t => !ExemptTools.Contains(t))
            .Where(t => !toolsWithCoverage.Contains(t))
            .OrderBy(t => t)
            .ToList();

        if (missingTools.Any())
        {
            Assert.Fail(
                $"Coverage gap: {missingTools.Count} tool(s) have no eval coverage under evals/tools/. " +
                $"Tool owners must add 2-3 prompt variations (as tool-calls.required stimuli) for each:\n" +
                $"  - {string.Join("\n  - ", missingTools)}\n\n" +
                $"To add coverage, edit or add: evals/tools/prompt-to-tool-<area>.eval.yaml");
        }
    }

    /// <summary>
    /// Parses one evals/tools/*.eval.yaml file and yields every tool name referenced in a
    /// tool-calls grader's "required" list, across all stimuli. Uses YamlDotNet's low-level
    /// representation model rather than a strict typed model, since eval files legitimately
    /// vary in which grader types/fields they use.
    /// </summary>
    private static IEnumerable<string> ExtractRequiredToolNames(string evalFilePath)
    {
        using var reader = new StreamReader(evalFilePath);
        var yaml = new YamlStream();
        yaml.Load(reader);
        if (yaml.Documents.Count == 0 || yaml.Documents[0].RootNode is not YamlMappingNode root)
        {
            yield break;
        }

        if (!root.Children.TryGetValue(new YamlScalarNode("stimuli"), out var stimuliNode) || stimuliNode is not YamlSequenceNode stimuli)
        {
            yield break;
        }

        foreach (var stimulusNode in stimuli.Children)
        {
            if (stimulusNode is not YamlMappingNode stimulus)
            {
                continue;
            }
            if (!stimulus.Children.TryGetValue(new YamlScalarNode("graders"), out var gradersNode) || gradersNode is not YamlSequenceNode graders)
            {
                continue;
            }

            foreach (var graderNode in graders.Children)
            {
                if (graderNode is not YamlMappingNode grader)
                {
                    continue;
                }
                if (!grader.Children.TryGetValue(new YamlScalarNode("type"), out var typeNode) ||
                    typeNode is not YamlScalarNode typeScalar ||
                    typeScalar.Value != "tool-calls")
                {
                    continue;
                }
                if (!grader.Children.TryGetValue(new YamlScalarNode("config"), out var configNode) || configNode is not YamlMappingNode config)
                {
                    continue;
                }
                if (!config.Children.TryGetValue(new YamlScalarNode("required"), out var requiredNode) || requiredNode is not YamlSequenceNode required)
                {
                    continue;
                }

                foreach (var requiredItem in required.Children)
                {
                    if (requiredItem is YamlMappingNode requiredMapping &&
                        requiredMapping.Children.TryGetValue(new YamlScalarNode("name"), out var nameNode) &&
                        nameNode is YamlScalarNode nameScalar &&
                        !string.IsNullOrEmpty(nameScalar.Value))
                    {
                        yield return nameScalar.Value;
                    }
                }
            }
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var gitPath = Path.Combine(dir.FullName, ".git");
            if ((Directory.Exists(gitPath) || File.Exists(gitPath)) && Directory.Exists(Path.Combine(dir.FullName, "evals")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }

        throw new InvalidOperationException($"Could not locate repository root (looked for .git + evals/ upward from {AppContext.BaseDirectory}).");
    }
}

