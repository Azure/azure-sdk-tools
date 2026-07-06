using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.Models.ClassifyItems;
using Azure.Sdk.Tools.Cli.Models.Responses;
using Azure.Sdk.Tools.Cli.Prompts;
using Azure.Sdk.Tools.Cli.Prompts.Templates;
using Microsoft.TeamFoundation.Common;

namespace Azure.Sdk.Tools.Cli.Services
{   
    public interface IClassifyService
    {
        Task<ClassifyResponse> ClassifyItemsAsync(ClassificationKind classifyType, ClassifyRequest request, CancellationToken ct);
    }
    public class ClassificationService: IClassifyService
    {
        private readonly ICopilotAgentRunner _agentRunner;
        public ClassificationService(ICopilotAgentRunner agentRunner)
        {
            _agentRunner = agentRunner;
        }

        public async Task<ClassifyResponse> ClassifyItemsAsync(ClassificationKind classifyType, ClassifyRequest request, CancellationToken ct)
        {
            switch (classifyType)
            {
                case ClassificationKind.SdkBreakingChange:
                    if (request is ClassifySdkBreakingChangesRequest sdkBreakingRequest)
                    {
                        if (sdkBreakingRequest.SdkChange.IsNullOrEmpty() || sdkBreakingRequest.SdkBreakingPattern.IsNullOrEmpty())
                        {
                            throw new ArgumentException("No feedback items to classify.");
                        }
                        var classifyTemplate = new SdkBreakingChangeClassificationTemplate(
                            sdkBreakingRequest.SdkBreakingPattern,
                            sdkBreakingRequest.SdkChange,
                            sdkBreakingRequest.Language,
                            sdkBreakingRequest.TspProjectPath
                        );
                        var classifiedResult = await BatchClassifyItems(classifyTemplate, null, ct);
                        return new ClassifyResponse(classifyType, classifiedResult);

                    } else
                    {
                        throw new ArgumentException("Invalid request type for SdkBreakingChange classification.");
                    }
                case ClassificationKind.Customization:
                    if (request is ClassifyCustomizationRequest customizationRequest)
                    {
                        List<FeedbackClassificationResponse.ItemClassificationDetails> allClassifiedResults = new List<FeedbackClassificationResponse.ItemClassificationDetails>();
                        if (customizationRequest.Items == null || customizationRequest.Items.Count == 0)
                        {
                            throw new ArgumentException("No feedback items to classify.");
                        }
                        foreach (var chunk in customizationRequest.Items.Chunk(customizationRequest.BatchSize))
                        {
                            var classifyTemplate = new FeedbackClassificationTemplate(
                                customizationRequest.ServiceName,
                                customizationRequest.Language,
                                customizationRequest.ReferencePatternContent,
                                chunk.ToList(),
                                customizationRequest.GlobalContext,
                                customizationRequest.EditScope
                            );
                            List<object> feedbackItems = chunk.Cast<object>().ToList();
                            var classifyResult = await BatchClassifyItems(classifyTemplate, feedbackItems, ct);
                            allClassifiedResults.AddRange((List<FeedbackClassificationResponse.ItemClassificationDetails>)classifyResult);

                        }
                        return new ClassifyResponse(classifyType, allClassifiedResults);

                    } else
                    {
                        throw new ArgumentException("Invalid request type for Customization classification.");
                    }
                default:
                    throw new ArgumentException($"Unsupported classify type: {classifyType}");
            }
        }

        private async Task<Object> BatchClassifyItems(BasePromptTemplate template, List<object>? items, CancellationToken ct)
        {
            var agent = new CopilotAgent<string>
            {
                Instructions = template.BuildPrompt(),
                Model = "claude-opus-4.5"
            };

            var result = await _agentRunner.RunAsync(agent, ct);
            var classifiedResult = template.ParseClassifyResult(result, items);
            return classifiedResult;
        }
    }
}

