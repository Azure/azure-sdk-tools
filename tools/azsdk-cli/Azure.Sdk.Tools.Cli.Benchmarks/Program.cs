// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.CommandLine;
using Azure.Sdk.Tools.Cli.Benchmarks.Infrastructure;
using Azure.Sdk.Tools.Cli.Benchmarks.Models;
using Azure.Sdk.Tools.Cli.Benchmarks.Scenarios;

namespace Azure.Sdk.Tools.Cli.Benchmarks;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Azure SDK Benchmarks - run and manage benchmark scenarios");

        // list command
        var listCommand = new Command("list", "List all available scenarios");
        var listTagOption = new Option<string[]>("--tag") { Description = "Filter scenarios by tag (can be specified multiple times)", AllowMultipleArgumentsPerToken = true };
        var listRepoOption = new Option<string?>("--repo") { Description = "Filter scenarios by repository (e.g., Azure/azure-rest-api-specs)" };
        listCommand.Options.Add(listTagOption);
        listCommand.Options.Add(listRepoOption);
        listCommand.SetAction((parseResult, _) =>
        {
            var tags = parseResult.GetValue(listTagOption);
            var repo = parseResult.GetValue(listRepoOption);
            HandleListCommand(tags, repo);
            return Task.FromResult(0);
        });
        rootCommand.Subcommands.Add(listCommand);

        // run command
        var runCommand = new Command("run", "Run benchmark scenario(s)");

        var nameArgument = new Argument<string?>("name") { Description = "Name of the scenario to run", Arity = ArgumentArity.ZeroOrOne };
        var allOption = new Option<bool>("--all") { Description = "Run all scenarios" };
        var modelOption = new Option<string?>("--model") { Description = $"Model to use (default: {BenchmarkDefaults.DefaultModel})" };
        var cleanupOption = new Option<CleanupPolicy>("--cleanup")
        {
            Description = "Cleanup policy for workspaces (always, never, on-success)",
            DefaultValueFactory = _ => CleanupPolicy.OnSuccess
        };
        var verboseOption = new Option<bool>("--verbose") { Description = "Show agent activity during execution" };
        var parallelOption = new Option<int>("--parallel") { Description = $"Maximum number of scenarios to run concurrently (default: {BenchmarkDefaults.DefaultMaxParallelism})", DefaultValueFactory = _ => BenchmarkDefaults.DefaultMaxParallelism };
        var tagOption = new Option<string[]>("--tag") { Description = "Filter scenarios by tag (can be specified multiple times)", AllowMultipleArgumentsPerToken = true };
        var repoOption = new Option<string?>("--repo") { Description = "Filter scenarios by repository (e.g., Azure/azure-rest-api-specs)" };
        var authoringSpecRepoOption = new Option<string?>("--authoring-spec-repo") { Description = "The repository containing the authoring skill to override (e.g. Azure/azure-rest-api-specs or Azure/azure-rest-api-specs:<branch>)" };
        var authoringSkillPathOption = new Option<string?>("--authoring-skill-path") { Description = "The filesystem path of the authoring skill directory to be used." };

        runCommand.Arguments.Add(nameArgument);
        runCommand.Options.Add(allOption);
        runCommand.Options.Add(modelOption);
        runCommand.Options.Add(cleanupOption);
        runCommand.Options.Add(verboseOption);
        runCommand.Options.Add(parallelOption);
        runCommand.Options.Add(tagOption);
        runCommand.Options.Add(repoOption);
        runCommand.Options.Add(authoringSpecRepoOption);
        runCommand.Options.Add(authoringSkillPathOption);

        runCommand.SetAction(async (parseResult, _) =>
        {
            var name = parseResult.GetValue(nameArgument);
            var all = parseResult.GetValue(allOption);
            var model = parseResult.GetValue(modelOption);
            var cleanup = parseResult.GetValue(cleanupOption);
            var verbose = parseResult.GetValue(verboseOption);
            var parallel = parseResult.GetValue(parallelOption);
            var tags = parseResult.GetValue(tagOption);
            var repo = parseResult.GetValue(repoOption);
            var authoringSpecRepo = parseResult.GetValue(authoringSpecRepoOption);
            var authoringSkillPath = parseResult.GetValue(authoringSkillPathOption);
            return await HandleRunCommand(name, all, tags, repo, model, cleanup, verbose, parallel, authoringSpecRepo, authoringSkillPath);
        });
        rootCommand.Subcommands.Add(runCommand);

        return await rootCommand.Parse(args).InvokeAsync();
    }

    private static void HandleListCommand(string[]? tags, string? repo)
    {
        var scenarios = FilterScenarios(ScenarioDiscovery.DiscoverAll(), tags, repo).ToList();

        if (scenarios.Count == 0)
        {
            Console.WriteLine("No scenarios found.");
            return;
        }

        // Calculate column widths
        var nameWidth = Math.Max("Name".Length, scenarios.Max(s => s.Name.Length));
        var descWidth = Math.Max("Description".Length, scenarios.Max(s => s.Description.Length));
        var tagsWidth = Math.Max("Tags".Length, scenarios.Max(s => string.Join(", ", s.Tags).Length));

        // Print header
        Console.WriteLine($"{"Name".PadRight(nameWidth)} | {"Description".PadRight(descWidth)} | {"Tags".PadRight(tagsWidth)}");
        Console.WriteLine(new string('-', nameWidth + descWidth + tagsWidth + 6));

        // Print rows
        foreach (var scenario in scenarios)
        {
            var tagDisplay = string.Join(", ", scenario.Tags);
            Console.WriteLine($"{scenario.Name.PadRight(nameWidth)} | {scenario.Description.PadRight(descWidth)} | {tagDisplay.PadRight(tagsWidth)}");
        }

        Console.WriteLine($"\nTotal: {scenarios.Count} scenario(s)");
    }

    private static async Task<int> HandleRunCommand(string? name, bool all, string[]? tags, string? repo, string? model, CleanupPolicy cleanup, bool verbose, int parallel, string? authoringSpecRepo, string? authoringSkillPath)
    {
        if (string.IsNullOrEmpty(name) && !all)
        {
            Console.WriteLine("Error: Must specify either <name> or --all");
            Console.WriteLine("Usage: run <name>     Run a specific scenario");
            Console.WriteLine("       run --all      Run all scenarios");
            return 1;
        }

        if (!string.IsNullOrEmpty(name) && all)
        {
            Console.WriteLine("Error: Cannot specify both <name> and --all");
            return 1;
        }

        if ((tags is { Length: > 0 } || repo != null) && !all)
        {
            Console.WriteLine("Error: --tag and --repo can only be used with --all");
            return 1;
        }

        if (parallel < 1)
        {
            Console.WriteLine("Error: --parallel must be at least 1");
            return 1;
        }

        var scenariosToRun = new List<BenchmarkScenario>();

        if (all)
        {
            scenariosToRun.AddRange(FilterScenarios(ScenarioDiscovery.DiscoverAll(authoringSpecRepo, authoringSkillPath), tags, repo));
            if (scenariosToRun.Count == 0)
            {
                var filters = new List<string>();
                if (tags is { Length: > 0 }) filters.Add($"tag(s): {string.Join(", ", tags)}");
                if (repo != null) filters.Add($"repo: {repo}");
                var message = filters.Count > 0
                    ? $"No scenarios found matching {string.Join(" and ", filters)}"
                    : "No scenarios found.";
                Console.WriteLine(message);
                return 1;
            }
        }
        else
        {
            var scenario = ScenarioDiscovery.FindByName(name!, authoringSpecRepo, authoringSkillPath);
            if (scenario == null)
            {
                Console.WriteLine($"Error: Scenario '{name}' not found.");
                Console.WriteLine("\nAvailable scenarios:");
                foreach (var s in ScenarioDiscovery.DiscoverAll(authoringSpecRepo, authoringSkillPath))
                {
                    Console.WriteLine($"  - {s.Name}");
                }
                return 1;
            }
            scenariosToRun.Add(scenario);
        }

        Console.WriteLine("=== Azure SDK Benchmarks ===\n");

        if (!File.Exists("/.dockerenv"))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("⚠ Warning: Not running inside a container.");
            Console.WriteLine("  During benchmark runs, Copilot CLI is executed with all permissions, and automatically approves permission prompts, which can affect the state of your local machine.");
            Console.WriteLine("  For full isolation, consider running inside a container.");
            Console.ResetColor();
            Console.WriteLine();
        }

        var effectiveModel = model ?? BenchmarkDefaults.DefaultModel;
        Console.WriteLine($"Model: {effectiveModel}");
        if (model != null)
        {
            Console.WriteLine("  (overridden via --model flag)");
        }
        Console.WriteLine($"Parallelism: {parallel}");
        Console.WriteLine();

        var options = new BenchmarkOptions
        {
            CleanupPolicy = cleanup,
            Model = model,
            Verbose = verbose
        };

        var results = new ConcurrentBag<(BenchmarkScenario Scenario, BenchmarkResult Result)>();
        using var runner = new BenchmarkRunner();
        var consoleLock = new object();

        await Parallel.ForEachAsync(scenariosToRun, new ParallelOptions { MaxDegreeOfParallelism = parallel }, async (scenario, ct) =>
        {
            lock (consoleLock)
            {
                Console.WriteLine($"Running scenario: {scenario.Name}");
                Console.WriteLine($"Description: {scenario.Description}");
                Console.WriteLine($"Target repo: {scenario.Repo.CloneUrl}");
                Console.WriteLine();
            }

            var result = await runner.RunAsync(scenario, options);
            results.Add((scenario, result));

            lock (consoleLock)
            {
                PrintResult(result);
            }
        });

        // Summary for multiple scenarios
        var resultsList = results.ToList();
        if (resultsList.Count > 0)
        {
            Console.WriteLine("\n=== Summary ===");
            var passed = resultsList.Count(r => r.Result.Passed);
            var failed = resultsList.Count - passed;
            Console.WriteLine($"Passed: {passed}, Failed: {failed}, Total: {resultsList.Count}");

            foreach (var (scenario, result) in resultsList)
            {
                var status = result.Passed ? "✓" : "✗";
                Console.WriteLine($"  [{status}] {scenario.Name} ({result.Duration.TotalSeconds:F1}s)");
            }
        }

        return resultsList.All(r => r.Result.Passed) ? 0 : 1;
    }

    private static IEnumerable<BenchmarkScenario> FilterScenarios(IEnumerable<BenchmarkScenario> scenarios, string[]? tags, string? repo)
    {
        if (tags is { Length: > 0 })
            scenarios = scenarios.Where(s => tags.Any(t => s.Tags.Contains(t, StringComparer.OrdinalIgnoreCase)));

        if (!string.IsNullOrEmpty(repo))
            scenarios = scenarios.Where(s => $"{s.Repo.Owner}/{s.Repo.Name}".Equals(repo, StringComparison.OrdinalIgnoreCase));

        return scenarios;
    }

    private static void PrintResult(BenchmarkResult result)
    {
        Console.WriteLine("\n=== Results ===");
        Console.WriteLine($"Status: {(result.Passed ? "PASSED ✓" : "FAILED ✗")}");
        Console.WriteLine($"Duration: {result.Duration.TotalSeconds:F1}s");

        if (result.Error != null)
        {
            Console.WriteLine($"Error: {result.Error}");
        }

        Console.WriteLine($"\nTool calls ({result.ToolCalls.Count}):");
        foreach (var tool in result.ToolCalls)
        {
            Console.WriteLine($"  - {tool}");
        }

        if (result.WorkspacePath != null)
        {
            Console.WriteLine($"\nWorkspace: {result.WorkspacePath}");
            if (result.WorkspaceCleanedUp)
            {
                Console.WriteLine("  Status: cleaned up");
                Console.WriteLine("  Tip: Run with --cleanup never to preserve the workspace and inspect diffs manually.");
            }
            else
            {
                Console.WriteLine("  Status: preserved (available for inspection)");
            }
        }
    }
}
