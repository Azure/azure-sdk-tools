// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Benchmarks.Models;

/// <summary>
/// Default values for benchmark execution.
/// </summary>
public static class BenchmarkDefaults
{
    /// <summary>
    /// The default model to use for benchmark execution.
    /// </summary>
    public const string DefaultModel = "claude-opus-4.5";

    /// <summary>
    /// The default maximum number of scenarios to run concurrently.
    /// </summary>
    public const int DefaultMaxParallelism = 1;

    /// <summary>
    /// The default endpoint for the Azure Knowledge Base.
    /// </summary>
    public const string DefaultAzureKnowledgeBaseEndpoint = "http://localhost:8088";
}
