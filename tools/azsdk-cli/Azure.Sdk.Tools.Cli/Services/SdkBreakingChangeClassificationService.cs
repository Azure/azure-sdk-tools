// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Linq;
using System.Text.Json;
using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.CopilotAgents.Tools;
using Azure.Sdk.Tools.Cli.Models.SdkBreakingChangeDetection;
using Azure.Sdk.Tools.Cli.Prompts.Templates;
using Microsoft.Extensions.AI;

namespace Azure.Sdk.Tools.Cli.Services
{   
    public interface ISdkBreakingChangeClassificationService
    {
        Task<SdkBreakingChangeDetectionResult?> ClassifySdkBreakingChangesAsync(string sdkchange, string sdkBreakingPattern, string language, string? tspProjectPath, CancellationToken ct);
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

        public async Task<SdkBreakingChangeDetectionResult?> ClassifySdkBreakingChangesAsync(string sdkchange, string sdkBreakingPattern, string language, string? tspProjectPath, CancellationToken ct)
        {
            try
            {
                var template = new SdkBreakingChangeClassificationTemplate(sdkBreakingPattern, sdkchange, language, tspProjectPath);
                List<AIFunction>? specTools = null;
                if (!string.IsNullOrEmpty(tspProjectPath) && Directory.Exists(tspProjectPath))
                {
                    // Tools scoped to spec repo for TypeSpec project inspection
                    specTools = new List<AIFunction>
                    {
                        FileTools.CreateReadFileTool(tspProjectPath),
                        FileTools.CreateListFilesTool(tspProjectPath),
                        FileTools.CreateGrepSearchTool(tspProjectPath)
                    };
                }
                var agent = new CopilotAgent<string>
                {
                    Instructions = template.BuildPrompt(),
                    Tools = specTools ?? new List<AIFunction>(),
                };
                
                var result = await _agentRunner.RunAsync(agent, ct);
                _logger.LogDebug("Use SdkBreakingChangeClassificationTemplate version {Version}, Classification result: {Result}", template.Version, result);
                try
                {
                    return JsonSerializer.Deserialize<SdkBreakingChangeDetectionResult>(result);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to deserialize classification result: {Result}, SdkBreakingChangeClassificationTemplate version: {Version}", result, template.Version);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occur when run copilot agent");
                return null;
            }
        }
    }
}

