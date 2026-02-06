// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Benchmarks.Infrastructure;
using Azure.Sdk.Tools.Cli.Benchmarks.Validation;

namespace Azure.Sdk.Tools.Cli.Benchmarks.Scenarios;

/// <summary>
/// Base class for all benchmark scenarios.
/// </summary>
public abstract class BenchmarkScenario
{
    // === IDENTITY ===

    /// <summary>
    /// Gets the unique name of the scenario.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Gets the description of the scenario.
    /// </summary>
    public virtual string Description => "";

    /// <summary>
    /// Gets the tags associated with the scenario for filtering and categorization.
    /// </summary>
    public virtual string[] Tags => [];

    // === REPO CONTEXT ===

    /// <summary>
    /// Gets the configuration for the home repository where the agent will run.
    /// </summary>
    public abstract RepoConfig Repo { get; }

    // === MULTI-REPO (optional) ===

    /// <summary>
    /// Gets the target repositories to clone as flat siblings of the home repo.
    /// </summary>
    public virtual IEnumerable<RepoConfig> TargetRepos => [];

    // === MCP OVERRIDE ===

    /// <summary>
    /// Gets the optional path to the azsdk-mcp executable.
    /// When null, uses the repo's config or falls back to the AZSDK_MCP_PATH environment variable.
    /// </summary>
    public virtual string? AzsdkMcpPath => null;

    // === SETUP ===

    /// <summary>
    /// Optional hook for scenario-specific file setup before the agent runs.
    /// </summary>
    /// <param name="workspace">The workspace containing the cloned repository.</param>
    /// <returns>A task representing the asynchronous setup operation.</returns>
    public virtual Task SetupAsync(Workspace workspace) => Task.CompletedTask;

    // === TASK ===

    /// <summary>
    /// Gets the prompt to send to the agent.
    /// </summary>
    public abstract string Prompt { get; }

    /// <summary>
    /// Gets the maximum time allowed for the scenario to complete.
    /// </summary>
    public virtual TimeSpan Timeout => TimeSpan.FromMinutes(5);

    // === VALIDATION ===

    /// <summary>
    /// Gets the validators to run after agent execution.
    /// Empty list means manual validation (POC mode).
    /// </summary>
    public virtual IEnumerable<IValidator> Validators => [];
}
