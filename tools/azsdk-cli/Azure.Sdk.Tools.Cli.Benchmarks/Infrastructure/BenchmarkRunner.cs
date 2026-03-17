// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Linq;
using Azure.Sdk.Tools.Cli.Benchmarks.Models;
using Azure.Sdk.Tools.Cli.Benchmarks.Scenarios;
using Azure.Sdk.Tools.Cli.Benchmarks.Validation;

namespace Azure.Sdk.Tools.Cli.Benchmarks.Infrastructure;

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

            // Track activity line length for clearing
            var lastActivityLength = 0;
            Action<string>? onActivity = options.Verbose ? (activity) =>
            {
                // Clear previous line and write new activity
                var clearPadding = lastActivityLength > activity.Length
                    ? new string(' ', lastActivityLength - activity.Length)
                    : "";
                Console.Write($"\r  {activity}{clearPadding}");
                lastActivityLength = activity.Length;
            } : null;

            var execConfig = new ExecutionConfig
            {
                WorkingDirectory = workspace.RepoPath,
                Prompt = scenario.Prompt,
                Timeout = scenario.Timeout,
                AzsdkMcpPath = options.AzsdkMcpPath ?? scenario.AzsdkMcpPath,
                Model = options.Model ?? BenchmarkDefaults.DefaultModel,
                OnActivity = onActivity,
                Verbose = options.Verbose
            };
            var execResult = await executor.ExecuteAsync(execConfig);

            // Clear activity line if verbose was enabled
            if (options.Verbose && lastActivityLength > 0)
            {
                Console.Write($"\r{new string(' ', lastActivityLength + 2)}\r");
            }

            // 4. Capture git diff
            Console.WriteLine($"[Benchmark] Capturing git diff...");
            var gitDiff = await workspace.GetGitDiffAsync();

            // 5. Validation
            ValidationSummary? validation = null;
            var validators = scenario.Validators.ToList();

            if (validators.Count > 0)
            {
                Console.WriteLine($"[Benchmark] Running {validators.Count} validator(s)...");

                var validationContext = new ValidationContext
                {
                    Workspace = workspace,
                    GitDiff = gitDiff,
                    ToolCalls = execResult.ToolCalls,
                    Messages = execResult.Messages,
                    ExecutionCompleted = execResult.Completed,
                    ExecutionError = execResult.Error,
                    ScenarioName = scenario.Name
                };

                var validatorRunner = new ValidatorRunner();
                validation = await validatorRunner.RunAsync(validators, validationContext);
            }

            stopwatch.Stop();

            // Determine pass/fail based on validation (or POC logic if no validators)
            var passed = validators.Count > 0
                ? validation!.Passed
                : execResult.Completed && !string.IsNullOrWhiteSpace(gitDiff); // POC fallback

            // 6. Write execution log to workspace (after validation)
            Console.WriteLine($"[Benchmark] Writing execution log...");
            await workspace.WriteExecutionLogAsync(
                scenario.Name,
                execResult.Messages,
                execResult.ToolCalls,
                gitDiff,
                stopwatch.Elapsed,
                passed,
                validation,
                execResult.Error);

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
                WorkspaceCleanedUp = willCleanup,
                Validation = validation
            };

            // 7. Cleanup
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
                WorkspaceCleanedUp = willCleanup,
                Validation = null
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
