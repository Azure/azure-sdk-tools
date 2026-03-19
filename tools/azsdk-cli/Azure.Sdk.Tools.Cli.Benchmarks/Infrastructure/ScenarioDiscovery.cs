// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Reflection;
using Azure.Sdk.Tools.Cli.Benchmarks.Scenarios;
using Azure.Sdk.Tools.Cli.Benchmarks.Scenarios.Typespec;

namespace Azure.Sdk.Tools.Cli.Benchmarks.Infrastructure;

/// <summary>
/// Provides methods to discover benchmark scenarios using reflection.
/// </summary>
public static class ScenarioDiscovery
{
    /// <summary>
    /// Discovers all non-abstract classes that inherit from <see cref="BenchmarkScenario"/>
    /// and returns instances of them.
    /// </summary>
    public static IEnumerable<BenchmarkScenario> DiscoverAll(string? authoringSpecRepo = null, string? authoringSkillPath = null)
    {
        var scenarioType = typeof(BenchmarkScenario);
        var assembly = Assembly.GetExecutingAssembly();

        var scenarioTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && scenarioType.IsAssignableFrom(t) && t != typeof(AuthoringScenario));

        foreach (var type in scenarioTypes)
        {
            BenchmarkScenario? scenario = null;
            try
            {
                scenario = Activator.CreateInstance(type) as BenchmarkScenario;
            }
            catch (Exception ex)
            {
                // Skip scenarios that fail to instantiate, but warn the user
                Console.Error.WriteLine($"Warning: Failed to load scenario '{type.Name}': {ex.Message}");
            }

            if (scenario != null)
            {
                yield return scenario;
            }
        }

        // discovery all AuthoringScenarios from json files
        var authoringScenarios = AuthoringScenarioLoader.LoadFromJsonFiles(authoringSpecRepo, authoringSkillPath);
        foreach (var scenario in authoringScenarios)
        {
            yield return scenario;
        }
    }

    /// <summary>
    /// Finds a scenario by name (case-insensitive).
    /// </summary>
    /// <param name="name">The name of the scenario to find.</param>
    /// <param name="authoringSkillRepo">The repository containing the authoring skill to override (optional).</param>
    /// <param name="authoringSkillPath">The path of the authoring skill to use (optional).</param>         
    /// <returns>The scenario if found, null otherwise.</returns>
    public static BenchmarkScenario? FindByName(string name, string? authoringSpecRepo = null, string? authoringSkillPath = null)
    {
        return DiscoverAll(authoringSpecRepo, authoringSkillPath)
            .FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }
}
