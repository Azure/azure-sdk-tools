using Azure.Sdk.Tools.Cli.Benchmarks.Infrastructure;
using Azure.Sdk.Tools.Cli.Benchmarks.Scenarios;

namespace Azure.Sdk.Tools.Cli.Benchmarks;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("=== Azure SDK Benchmarks POC ===\n");
        
        var scenario = new RenameClientPropertyScenario();
        var options = new BenchmarkOptions
        {
            // Keep workspace on failure for debugging
            CleanupPolicy = CleanupPolicy.OnSuccess
        };
        
        Console.WriteLine($"Running scenario: {scenario.Name}");
        Console.WriteLine($"Description: {scenario.Description}");
        Console.WriteLine($"Target repo: {scenario.Repo.CloneUrl}");
        Console.WriteLine();
        
        using var runner = new BenchmarkRunner();
        var result = await runner.RunAsync(scenario, options);
        
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
        
        Console.WriteLine("\n=== Git Diff ===");
        if (string.IsNullOrWhiteSpace(result.GitDiff))
        {
            Console.WriteLine("(no changes)");
        }
        else
        {
            Console.WriteLine(result.GitDiff);
        }
        
        if (!result.Passed && result.WorkspacePath != null)
        {
            Console.WriteLine($"\nWorkspace preserved at: {result.WorkspacePath}");
        }
        
        return result.Passed ? 0 : 1;
    }
}
