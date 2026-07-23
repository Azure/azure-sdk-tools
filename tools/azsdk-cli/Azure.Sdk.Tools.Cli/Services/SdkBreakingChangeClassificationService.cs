// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.Models.SdkBreakingChangeDetection;
using Azure.Sdk.Tools.Cli.Prompts.Templates;

namespace Azure.Sdk.Tools.Cli.Services
{   
    public interface ISdkBreakingChangeClassificationService
    {
        Task<SdkBreakingChangeDetectResult?> ClassifySdkBreakingChangesAsync(string sdkchange, string sdkBreakingPattern, string language, string? tspProjectPath, CancellationToken ct);
    }
    public class SdkBreakingChangeClassificationService: ISdkBreakingChangeClassificationService
    {
        private readonly ICopilotAgentRunner _agentRunner;
        private readonly ILogger<SdkBreakingChangeClassificationService> _logger;

        public SdkBreakingChangeClassificationService(ICopilotAgentRunner agentRunner, ILogger<SdkBreakingChangeClassificationService> logger)
        {
            _agentRunner = agentRunner;
            _logger = logger;
        }

        public async Task<SdkBreakingChangeDetectResult?> ClassifySdkBreakingChangesAsync(string sdkchange, string sdkBreakingPattern, string language, string? tspProjectPath, CancellationToken ct)
        {
            var template = new SdkBreakingChangeClassificationTemplate(sdkBreakingPattern, sdkchange, language, tspProjectPath);
            var agent = new CopilotAgent<string>
            {
                Instructions = template.BuildPrompt(),
            };
            var result = await _agentRunner.RunAsync(agent, ct);
            _logger.LogDebug("Use SdkBreakingChangeClassificationTemplate version {Version}, Classification result: {Result}", template.Version, result);
            try
            {
                return JsonSerializer.Deserialize<SdkBreakingChangeDetectResult>(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize classification result: {Result}", result);
                return null;
            }
        }
    }
}

