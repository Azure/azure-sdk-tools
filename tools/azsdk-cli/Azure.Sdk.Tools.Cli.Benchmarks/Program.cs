// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.CommandLine;
using Azure.Sdk.Tools.Cli.Benchmarks.Infrastructure;
using Azure.Sdk.Tools.Cli.Benchmarks.Models;
using Azure.Sdk.Tools.Cli.Benchmarks.Reporting;
using Azure.Sdk.Tools.Cli.Benchmarks.Scenarios;

namespace Azure.Sdk.Tools.Cli.Benchmarks;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Azure SDK Benchmarks - run and manage benchmark scenarios");

        // list command
        var listCommand = new Command("list", "List all available scenarios");
        listCommand.SetAction((_, _) =>
        {
            HandleListCommand();
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

        runCommand.Arguments.Add(nameArgument);
        runCommand.Options.Add(allOption);
        runCommand.Options.Add(modelOption);
        runCommand.Options.Add(cleanupOption);
        runCommand.Options.Add(verboseOption);
        runCommand.Options.Add(parallelOption);
        runCommand.Options.Add(reportOption);

        runCommand.SetAction(async (parseResult, _) =>
        {
            var name = parseResult.GetValue(nameArgument);
            var all = parseResult.GetValue(allOption);
            var model = parseResult.GetValue(modelOption);
            var cleanup = parseResult.GetValue(cleanupOption);
            var verbose = parseResult.GetValue(verboseOption);
            var parallel = parseResult.GetValue(parallelOption);
            var report = parseResult.GetValue(reportOption);
            return await HandleRunCommand(name, all, model, cleanup, verbose, parallel, report);
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

    private static void HandleListCommand()
    {
        var scenarios = ScenarioDiscovery.DiscoverAll().ToList();

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
            var tags = string.Join(", ", scenario.Tags);
            Console.WriteLine($"{scenario.Name.PadRight(nameWidth)} | {scenario.Description.PadRight(descWidth)} | {tags.PadRight(tagsWidth)}");
        }

        Console.WriteLine($"\nTotal: {scenarios.Count} scenario(s)");
    }

    private static async Task<int> HandleRunCommand(string? name, bool all, string? model, CleanupPolicy cleanup, bool verbose, int parallel, bool report)
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

        if (parallel < 1)
        {
            Console.WriteLine("Error: --parallel must be at least 1");
            return 1;
        }

        var scenariosToRun = new List<BenchmarkScenario>();

        if (all)
        {
            scenariosToRun.AddRange(ScenarioDiscovery.DiscoverAll());
            if (scenariosToRun.Count == 0)
            {
                Console.WriteLine("No scenarios found.");
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
        if (resultsList.Count > 1)
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

        // Generate report if requested
        if (report)
        {
            Console.WriteLine("\n=== Generating Report ===");
            try
            {
                var reportGenerator = new ReportGenerator();
                var runName = $"benchmark-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
                var reportContent = await reportGenerator.GenerateAsync(resultsList, runName, effectiveModel);

                var reportPath = Path.Combine(Directory.GetCurrentDirectory(), $"{runName}-report.md");
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
