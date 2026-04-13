using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.Models.Responses;
using Microsoft.TeamFoundation.TestManagement.WebApi;

namespace Azure.Sdk.Tools.Cli.Services.TypeSpec
{
    public class TypeSpecSDKbreakingchangeDetectionService : ITypeSpecSDKbreakingchangeDetectionService
    {
        private readonly ILogger<TypeSpecSDKbreakingchangeDetectionService> logger;
        private readonly ICopilotAgentRunner copilotAgentRunner;
        public TypeSpecSDKbreakingchangeDetectionService(ILogger<TypeSpecSDKbreakingchangeDetectionService> logger, ICopilotAgentRunner copilotAgentRunner)
        {
            this.logger = logger;
            this.copilotAgentRunner = copilotAgentRunner;
        }
        public async Task<SDKBreakingChangeDetectionResponse> DetectBreakingChangesAsync(string typespecChanges, string? referenceContent = null, CancellationToken ct = default)
        {
            // Placeholder implementation - in a real implementation, this would analyze the TypeSpec changes to detect breaking changes
            logger.LogInformation("Analyzing TypeSpec changes for breaking changes...");
            if (string.IsNullOrEmpty(referenceContent))
            {
                logger.LogWarning("No reference content provided. The analysis may be less accurate without known SDK breaking change patterns.");
                return null; 
            }
            logger.LogInformation("Using reference content");

            var agent = new CopilotAgent<SDKBreakingChangeDetectionResponse>
            {
                Instructions = this.BuildInstructions(referenceContent, typespecChanges),
                Model = "claude-opus-4.5"
            };
            var result = await copilotAgentRunner.RunAsync(agent, ct);
            logger.LogInformation("copilot agent completed. hasBreakingChange: {hasBreakingChanges}, Breaking Changes: {breakingChanges}", result.HasBreakingChanges, string.Join("\n", result.BreakingChanges));
            // For demonstration purposes, we'll just return a response indicating no breaking changes were found
            return result;
        }

        private string BuildInstructions(string referenceDocContent, string solution)
        {
            return $"""
        # TypeSpec SDK Breaking Change Detector

        You are an expert agent specializing in detecting breaking changes in TypeSpec API specifications that impact SDK generation across multiple programming languages (Java, .NET, Python, JavaScript/TypeScript, Go).

        ## Your task
        Analyze the solution plan. If the planned TypeSpec changes match any of these known SDK breaking change patterns, include SDK IMPACT warnings in the solution with language-specific client.tsp mitigations.

        **SDK Breaking Change Patterns Reference**

        {referenceDocContent}

        **TypeSpec changes to analyze:**
        {solution}
        """;
        }
    }
}
