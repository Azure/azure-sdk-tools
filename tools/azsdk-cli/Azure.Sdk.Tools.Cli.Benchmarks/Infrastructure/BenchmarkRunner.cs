// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using Azure.Sdk.Tools.Cli.Benchmarks.Scenarios;

namespace Azure.Sdk.Tools.Cli.Benchmarks.Infrastructure;

/// <summary>
/// Options for running a benchmark.
/// </summary>
public class BenchmarkOptions
{
    /// <summary>
    /// Override path to azsdk MCP server.
    /// </summary>
    public string? AzsdkMcpPath { get; init; }

    /// <summary>
    /// Cleanup policy after run.
    /// </summary>
    public CleanupPolicy CleanupPolicy { get; init; } = CleanupPolicy.OnSuccess;

    /// <summary>
    /// Override model to use (for future use).
    /// </summary>
    public string? Model { get; init; }
}

/// <summary>
/// Result of running a benchmark scenario.
/// </summary>
public class BenchmarkResult
{
    /// <summary>
    /// Gets the name of the scenario that was executed.
    /// </summary>
    public required string ScenarioName { get; init; }

    /// <summary>
    /// Gets whether the benchmark passed.
    /// </summary>
    public bool Passed { get; init; }

    /// <summary>
    /// Gets the error message if the benchmark failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Gets the duration of the benchmark execution.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets the git diff of changes made during the benchmark.
    /// </summary>
    public string? GitDiff { get; init; }

    /// <summary>
    /// Gets the list of tool calls made during execution.
    /// </summary>
    public IReadOnlyList<string> ToolCalls { get; init; } = [];

    /// <summary>
    /// Gets the path to the workspace where the benchmark was executed.
    /// </summary>
    public string? WorkspacePath { get; init; }

    /// <summary>
    /// Gets whether the workspace was cleaned up after execution.
    /// </summary>
    public bool WorkspaceCleanedUp { get; init; }
}

/// <summary>
/// Orchestrates benchmark execution by coordinating workspace setup,
/// agent execution, and result capture.
/// </summary>
public class BenchmarkRunner : IDisposable
{
    private readonly WorkspaceManager _workspaceManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="BenchmarkRunner"/> class.
    /// </summary>
    public BenchmarkRunner()
    {
        _workspaceManager = new WorkspaceManager();
    }

    /// <summary>
    /// Runs a benchmark scenario and returns the result.
    /// </summary>
    /// <param name="scenario">The benchmark scenario to run.</param>
    /// <param name="options">Optional benchmark options.</param>
    /// <returns>The result of running the benchmark.</returns>
    public async Task<BenchmarkResult> RunAsync(BenchmarkScenario scenario, BenchmarkOptions? options = null)
    {
        options ??= new BenchmarkOptions();
        Workspace? workspace = null;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // 1. Environment Setup - prepare workspace
            Console.WriteLine($"[Benchmark] Preparing workspace for {scenario.Name}...");
            workspace = await _workspaceManager.PrepareAsync(scenario.Repo, scenario.Name);

            // 2. Run scenario setup (if any)
            Console.WriteLine($"[Benchmark] Running scenario setup...");
            await scenario.SetupAsync(workspace);

            // 3. Execution - run agent
            Console.WriteLine($"[Benchmark] Executing scenario prompt...");
            using var executor = new SessionExecutor();
            var execConfig = new ExecutionConfig
            {
                WorkingDirectory = workspace.RepoPath,
                Prompt = scenario.Prompt,
                Timeout = scenario.Timeout,
                AzsdkMcpPath = options.AzsdkMcpPath ?? scenario.AzsdkMcpPath,
                Model = options.Model ?? BenchmarkDefaults.DefaultModel
            };
            var execResult = await executor.ExecuteAsync(execConfig);

            // 4. Capture git diff
            Console.WriteLine($"[Benchmark] Capturing git diff...");
            var gitDiff = await workspace.GetGitDiffAsync();

            stopwatch.Stop();

            // For POC, we consider it passed if execution completed and there's a diff
            var passed = execResult.Completed && !string.IsNullOrWhiteSpace(gitDiff);

            // Determine if cleanup will happen based on policy
            var willCleanup = options.CleanupPolicy switch
            {
                CleanupPolicy.Always => true,
                CleanupPolicy.Never => false,
                CleanupPolicy.OnSuccess => passed,
                _ => false
            };

            var result = new BenchmarkResult
            {
                ScenarioName = scenario.Name,
                Passed = passed,
                Error = execResult.Error,
                Duration = stopwatch.Elapsed,
                GitDiff = gitDiff,
                ToolCalls = execResult.ToolCalls,
                WorkspacePath = workspace.RootPath,
                WorkspaceCleanedUp = willCleanup
            };

            // 5. Cleanup
            await _workspaceManager.CleanupAsync(workspace, options.CleanupPolicy, passed);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            // Determine if cleanup will happen (passed=false in exception case)
            var willCleanup = options.CleanupPolicy switch
            {
                CleanupPolicy.Always => true,
                CleanupPolicy.Never => false,
                CleanupPolicy.OnSuccess => false, // Failed, so won't cleanup on-success
                _ => false
            };

            if (workspace != null)
            {
                await _workspaceManager.CleanupAsync(workspace, options.CleanupPolicy, false);
            }

            return new BenchmarkResult
            {
                ScenarioName = scenario.Name,
                Passed = false,
                Error = ex.Message,
                Duration = stopwatch.Elapsed,
                WorkspacePath = workspace?.RootPath,
                WorkspaceCleanedUp = willCleanup
            };
        }
    }

    /// <summary>
    /// Disposes of resources used by the benchmark runner.
    /// </summary>
    public void Dispose()
    {
        // WorkspaceManager handles its own cleanup
        GC.SuppressFinalize(this);
    }
}
