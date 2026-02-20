using System.CommandLine;
using Azure.Sdk.Tools.Cli.Benchmarks.Infrastructure;
using Azure.Sdk.Tools.Cli.Benchmarks.Scenarios;

namespace Azure.Sdk.Tools.Cli.Benchmarks;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Azure SDK Benchmarks - run and manage benchmark scenarios");

        // list command
        var listCommand = new Command("list", "List all available scenarios");
        listCommand.SetHandler(HandleListCommand);
        rootCommand.AddCommand(listCommand);

        // run command
        var runCommand = new Command("run", "Run benchmark scenario(s)");

        var nameArgument = new Argument<string?>("name", () => null, "Name of the scenario to run");
        var allOption = new Option<bool>("--all", "Run all scenarios");
        var modelOption = new Option<string?>("--model", $"Model to use (default: {BenchmarkDefaults.DefaultModel})");
        var cleanupOption = new Option<CleanupPolicy>(
            "--cleanup",
            () => CleanupPolicy.OnSuccess,
            "Cleanup policy for workspaces (always, never, on-success)");
        var verboseOption = new Option<bool>(
            "--verbose",
            "Show agent activity during execution");

        runCommand.AddArgument(nameArgument);
        runCommand.AddOption(allOption);
        runCommand.AddOption(modelOption);
        runCommand.AddOption(cleanupOption);
        runCommand.AddOption(verboseOption);

        runCommand.SetHandler(HandleRunCommand, nameArgument, allOption, modelOption, cleanupOption, verboseOption);
        rootCommand.AddCommand(runCommand);

        return await rootCommand.InvokeAsync(args);
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

    private static async Task<int> HandleRunCommand(string? name, bool all, string? model, CleanupPolicy cleanup, bool verbose)
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
        Console.WriteLine();

        var options = new BenchmarkOptions
        {
            CleanupPolicy = cleanup,
            Model = model,
            Verbose = verbose
        };

        var results = new List<(BenchmarkScenario Scenario, BenchmarkResult Result)>();
        using var runner = new BenchmarkRunner();

        foreach (var scenario in scenariosToRun)
        {
            Console.WriteLine($"Running scenario: {scenario.Name}");
            Console.WriteLine($"Description: {scenario.Description}");
            Console.WriteLine($"Target repo: {scenario.Repo.CloneUrl}");
            Console.WriteLine();

            var result = await runner.RunAsync(scenario, options);
            results.Add((scenario, result));

            PrintResult(result);
        }

        // Summary for multiple scenarios
        if (results.Count > 1)
        {
            Console.WriteLine("\n=== Summary ===");
            var passed = results.Count(r => r.Result.Passed);
            var failed = results.Count - passed;
            Console.WriteLine($"Passed: {passed}, Failed: {failed}, Total: {results.Count}");

            foreach (var (scenario, result) in results)
            {
                var status = result.Passed ? "✓" : "✗";
                Console.WriteLine($"  [{status}] {scenario.Name} ({result.Duration.TotalSeconds:F1}s)");
            }
        }

        return results.All(r => r.Result.Passed) ? 0 : 1;
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
