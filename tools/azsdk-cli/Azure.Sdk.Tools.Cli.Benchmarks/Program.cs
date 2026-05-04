// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.CommandLine;
using Azure.Sdk.Tools.Cli.Benchmarks.Infrastructure;
using Azure.Sdk.Tools.Cli.Benchmarks.Models;
using Azure.Sdk.Tools.Cli.Benchmarks.Reporting;
using Azure.Sdk.Tools.Cli.Benchmarks.Scenarios;
using Azure.Sdk.Tools.Cli.Benchmarks.Scenarios.TypeSpec;

namespace Azure.Sdk.Tools.Cli.Benchmarks;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Azure SDK Benchmarks - run and manage benchmark scenarios");

        // list command
        var listCommand = new Command("list", "List all available scenarios");
        var listTagOption = new Option<string[]>("--tag") { Description = "Filter scenarios by tag (can be specified multiple times)", AllowMultipleArgumentsPerToken = true };
        var listRepoOption = new Option<string[]>("--repo") { Description = "Filter scenarios by repository (can be specified multiple times, e.g., Azure/azure-rest-api-specs or Azure/azure-rest-api-specs:branch)", AllowMultipleArgumentsPerToken = true };
        listCommand.Options.Add(listTagOption);
        listCommand.Options.Add(listRepoOption);
        listCommand.SetAction((parseResult, _) =>
        {
            var tags = parseResult.GetValue(listTagOption);
            var repos = parseResult.GetValue(listRepoOption);
            HandleListCommand(tags, repos);
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
        var reportOption = new Option<bool>("--report") { Description = "Generate a markdown report after the run completes" };
        var outputOption = new Option<string?>("--output") { Description = "Output file path for the report (used with --report)" };
        var tagOption = new Option<string[]>("--tag") { Description = "Filter scenarios by tag (can be specified multiple times)", AllowMultipleArgumentsPerToken = true };
        var repoOption = new Option<string[]>("--repo") { Description = "Filter scenarios by repository (can be specified multiple times). Append :ref to override the branch (e.g., Azure/azure-rest-api-specs:my-branch)", AllowMultipleArgumentsPerToken = true };
        var authoringSkillPathOption = new Option<string?>("--authoring-skill-path") { Description = "The filesystem path of the authoring skill directory to be used." };

        runCommand.Arguments.Add(nameArgument);
        runCommand.Options.Add(allOption);
        runCommand.Options.Add(modelOption);
        runCommand.Options.Add(cleanupOption);
        runCommand.Options.Add(verboseOption);
        runCommand.Options.Add(parallelOption);
        runCommand.Options.Add(reportOption);
        runCommand.Options.Add(outputOption);
        runCommand.Options.Add(tagOption);
        runCommand.Options.Add(repoOption);
        runCommand.Options.Add(authoringSkillPathOption);

        runCommand.SetAction(async (parseResult, _) =>
        {
            var name = parseResult.GetValue(nameArgument);
            var all = parseResult.GetValue(allOption);
            var model = parseResult.GetValue(modelOption);
            var cleanup = parseResult.GetValue(cleanupOption);
            var verbose = parseResult.GetValue(verboseOption);
            var parallel = parseResult.GetValue(parallelOption);
            var report = parseResult.GetValue(reportOption);
            var output = parseResult.GetValue(outputOption);
            var tags = parseResult.GetValue(tagOption);
            var repos = parseResult.GetValue(repoOption);
            var authoringSkillPath = parseResult.GetValue(authoringSkillPathOption);
            return await HandleRunCommand(name, all, tags, repos, model, cleanup, verbose, parallel, authoringSkillPath, report, output);
        });
        rootCommand.Subcommands.Add(runCommand);

        // report command
        var reportCommand = new Command("report", "Generate a report from existing benchmark log files");

        var reportPathArgument = new Argument<string>("path") { Description = "Directory containing benchmark-log.json files" };
        var reportOutputOption = new Option<string?>("--output") { Description = "Output file path for the report (default: report.md in the log directory)" };

        reportCommand.Arguments.Add(reportPathArgument);
        reportCommand.Options.Add(reportOutputOption);

        reportCommand.SetAction(async (parseResult, _) =>
        {
            var path = parseResult.GetValue(reportPathArgument)!;
            var output = parseResult.GetValue(reportOutputOption);
            return await HandleReportCommand(path, output);
        });
        rootCommand.Subcommands.Add(reportCommand);

        return await rootCommand.Parse(args).InvokeAsync();
    }

    private static void HandleListCommand(string[]? tags, string[]? repos)
    {
        var repoOptions = ParseRepoOptions(repos);
        var scenarios = FilterScenarios(ScenarioDiscovery.DiscoverAll(), tags, repoOptions).ToList();

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

    private static async Task<int> HandleRunCommand(string? name, bool all, string[]? tags, string[]? repos, string? model, CleanupPolicy cleanup, bool verbose, int parallel, string? authoringSkillPath, bool report, string? output)
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

        if (tags is { Length: > 0 } && !all)
        {
            Console.WriteLine("Error: --tag can only be used with --all");
            return 1;
        }

        if (parallel < 1)
        {
            Console.WriteLine("Error: --parallel must be at least 1");
            return 1;
        }

        // Parse --repo for filtering and optional ref override
        var repoOptions = ParseRepoOptions(repos);

        var scenariosToRun = new List<BenchmarkScenario>();

        if (all)
        {
            scenariosToRun.AddRange(FilterScenarios(ScenarioDiscovery.DiscoverAll(), tags, repoOptions));
            if (scenariosToRun.Count == 0)
            {
                var filters = new List<string>();
                if (tags is { Length: > 0 })
                {
                    filters.Add($"tag(s): {string.Join(", ", tags)}");
                }

                if (repos is { Length: > 0 })
                {
                    filters.Add($"repo(s): {string.Join(", ", repos)}");
                }

                var message = filters.Count > 0
                    ? $"No scenarios found matching {string.Join(" and ", filters)}"
                    : "No scenarios found.";
                Console.WriteLine(message);
                return 1;
            }
        }
        else
        {
            var scenario = ScenarioDiscovery.FindByName(name!);
            if (scenario == null)
            {
                Console.WriteLine($"Error: Scenario '{name}' not found.");
                Console.WriteLine("\nAvailable scenarios:");
                foreach (var s in ScenarioDiscovery.DiscoverAll())
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
        if (repoOptions != null)
        {
            foreach (var (repoKey, gitRef) in repoOptions.Where(kv => kv.Value != null))
            {
                Console.WriteLine($"Ref override: {repoKey} → {gitRef}");
            }
        }
        Console.WriteLine();

        var options = new BenchmarkOptions
        {
            CleanupPolicy = cleanup,
            Model = model,
            Verbose = verbose,
            RefOverrides = repoOptions
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
            // override authoring skill path if specified for authoring scenarios
            if (!string.IsNullOrEmpty(authoringSkillPath) && scenario is AuthoringScenario authoringScenario)
            {
                authoringScenario.AuthoringSkillPath = authoringSkillPath;
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

        // Print total token usage across all scenarios
        var totalUsage = resultsList
            .Where(r => r.Result.TokenUsage != null)
            .Aggregate(new Models.TokenUsage(), (acc, r) => acc + r.Result.TokenUsage!);

        if (totalUsage.TotalTokens > 0)
        {
            Console.WriteLine("\n=== Token Usage (Total) ===");
            Console.WriteLine($"  Input:       {totalUsage.InputTokens,12:N0}");
            Console.WriteLine($"  Output:      {totalUsage.OutputTokens,12:N0}");
            Console.WriteLine($"  Cache Read:  {totalUsage.CacheReadTokens,12:N0}");
            Console.WriteLine($"  Cache Write: {totalUsage.CacheWriteTokens,12:N0}");
            Console.WriteLine($"  Total:       {totalUsage.TotalTokens,12:N0}");
        }

        // Generate report if requested
        if (report)
        {
            Console.WriteLine("\n=== Generating Report ===");
            try
            {
                var reportGenerator = new ReportGenerator();
                var runName = $"benchmark-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
                var reportContent = await reportGenerator.GenerateAsync(resultsList, runName, effectiveModel);

                var reportPath = output ?? Path.Combine(Directory.GetCurrentDirectory(), $"{runName}-report.md");
                await File.WriteAllTextAsync(reportPath, reportContent);
                Console.WriteLine($"Report written to: {reportPath}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Warning: Failed to generate report: {ex.Message}");
            }
        }

        return resultsList.All(r => r.Result.Passed) ? 0 : 1;
    }

    private static IEnumerable<BenchmarkScenario> FilterScenarios(IEnumerable<BenchmarkScenario> scenarios, string[]? tags, Dictionary<string, string?>? repoOptions)
    {
        if (tags is { Length: > 0 })
        {
            scenarios = scenarios.Where(s => tags.Any(t => s.Tags.Contains(t, StringComparer.OrdinalIgnoreCase)));
        }

        if (repoOptions is { Count: > 0 })
        {
            scenarios = scenarios.Where(s => repoOptions.ContainsKey($"{s.Repo.Owner}/{s.Repo.Name}"));
        }

        return scenarios;
    }

    /// <summary>
    /// Parses --repo values into a dictionary keyed by "Owner/Name" with optional ref override values.
    /// </summary>
    private static Dictionary<string, string?>? ParseRepoOptions(string[]? repos)
    {
        if (repos is not { Length: > 0 })
        {
            return null;
        }

        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var repo in repos)
        {
            if (!RepoConfig.TryParse(repo, out var owner, out var name, out var gitRef))
            {
                throw new ArgumentException($"Invalid --repo format: '{repo}'. Expected 'Owner/Name' or 'Owner/Name:Ref'.");
            }

            result[$"{owner}/{name}"] = gitRef;
        }

        return result;
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

        if (result.TokenUsage is { TotalTokens: > 0 } usage)
        {
            Console.WriteLine($"\nToken usage:");
            Console.WriteLine($"  Input: {usage.InputTokens:N0}  Output: {usage.OutputTokens:N0}  Total: {usage.TotalTokens:N0}");
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

    private static async Task<int> HandleReportCommand(string path, string? output)
    {
        if (!Directory.Exists(path))
        {
            Console.Error.WriteLine($"Error: Directory not found: {path}");
            return 1;
        }

        Console.WriteLine($"Generating report from logs in: {path}");

        try
        {
            var reportGenerator = new ReportGenerator();
            var reportContent = await reportGenerator.GenerateFromLogsAsync(path);

            var outputPath = output ?? Path.Combine(path, "report.md");
            await File.WriteAllTextAsync(outputPath, reportContent);
            Console.WriteLine($"Report written to: {outputPath}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error generating report: {ex.Message}");
            return 1;
        }
    }
}
