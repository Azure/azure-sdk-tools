// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Helpers;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.Cli.CopilotAgents;

/// <summary>
/// A composite <see cref="ICopilotAgentRunner"/> that selects the best available runner at call time:
///
/// 1. If the MCP server context is available (i.e. the tool was invoked via an MCP client),
///    uses <see cref="SamplingAgentRunner"/> to delegate LLM calls via MCP sampling.
/// 2. Otherwise, falls back to <see cref="CopilotAgentRunner"/> which uses the GitHub Copilot SDK.
///
/// This enables a gradual migration: tools work with both MCP sampling and the Copilot CLI,
/// without requiring callers to know which runner is in use.
/// </summary>
public class CompositeAgentRunner : ICopilotAgentRunner
{
    private readonly SamplingAgentRunner _samplingRunner;
    private readonly CopilotAgentRunner _copilotRunner;
    private readonly IMcpServerContextAccessor _mcpServerContextAccessor;
    private readonly ILogger<CompositeAgentRunner> _logger;

    public CompositeAgentRunner(
        IMcpServerContextAccessor mcpServerContextAccessor,
        ICopilotClientWrapper copilotClientWrapper,
        TokenUsageHelper tokenUsageHelper,
        ILoggerFactory loggerFactory)
    {
        _mcpServerContextAccessor = mcpServerContextAccessor;
        _logger = loggerFactory.CreateLogger<CompositeAgentRunner>();

        _samplingRunner = new SamplingAgentRunner(
            mcpServerContextAccessor,
            tokenUsageHelper,
            loggerFactory.CreateLogger<SamplingAgentRunner>());

        _copilotRunner = new CopilotAgentRunner(
            copilotClientWrapper,
            tokenUsageHelper,
            loggerFactory.CreateLogger<CopilotAgentRunner>());
    }

    public Task<TResult> RunAsync<TResult>(
        CopilotAgent<TResult> agent,
        CancellationToken ct = default) where TResult : notnull
    {
        if (IsSamplingAvailable())
        {
            _logger.LogDebug("Using MCP sampling runner (MCP server context available)");
            return _samplingRunner.RunAsync(agent, ct);
        }

        _logger.LogDebug("Falling back to Copilot SDK runner (no MCP server context)");
        return _copilotRunner.RunAsync(agent, ct);
    }

    private bool IsSamplingAvailable()
    {
        return _mcpServerContextAccessor.IsEnabled
            && _mcpServerContextAccessor.Current != null;
    }
}
