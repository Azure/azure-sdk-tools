// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.ClassifyItems;
using Azure.Sdk.Tools.Cli.Prompts.Templates;
using Azure.Sdk.Tools.Cli.Tools.Package;

namespace Azure.Sdk.Tools.Cli.Services
{   
    public interface IClassifyService
    {
        public Task<TResponse> ClassifyItemsAsync<TResponse>(ClassificationKind classifyType, ClassifyRequest request, CancellationToken ct) where TResponse : ClassifyResponse;
    }
    public class ClassificationService: IClassifyService
    {
        private readonly ICopilotAgentRunner _agentRunner;
        private static readonly string _defaultCopilotAgentModel = "claude-opus-4.5";
        public const string SdkBreakingChangeClassifierModelVariable = "AZURE_SDK_BREAKING_CLASSIFIER_MODEL";
        /// <summary>
        /// The model that this agent will use. Defaults to "claude-opus-4.5".
        /// </summary>
        public string CopilotAgentModel { get; set; } = Environment.GetEnvironmentVariable(SdkBreakingChangeClassifierModelVariable) ?? _defaultCopilotAgentModel;
        public ClassificationService(ICopilotAgentRunner agentRunner)
        {
            _agentRunner = agentRunner;
        }
        public async Task<TResponse> ClassifyItemsAsync<TResponse>(ClassificationKind classifyType, ClassifyRequest request, CancellationToken ct) where TResponse : ClassifyResponse
        {
            var response = await ClassifyItemsAsync(classifyType, request, ct);
            if (response is TResponse typedResponse)
            {
                return typedResponse;
            }
            else
            {
                throw new InvalidOperationException($"Classification kind '{classifyType}' returned '{response.GetType().Name}', but caller expected '{typeof(TResponse).Name}'.");
            }
        }
        public async Task<ClassifyResponse> ClassifyItemsAsync(ClassificationKind classifyType, ClassifyRequest request, CancellationToken ct)
        {
            switch (classifyType)
            {
                case ClassificationKind.SdkBreakingChange:
                    if (request is ClassifySdkBreakingChangesRequest sdkBreakingRequest)
                    {
                        if (string.IsNullOrEmpty(sdkBreakingRequest.SdkChange) || string.IsNullOrEmpty(sdkBreakingRequest.SdkBreakingPattern))
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
                        return new ClassifySdkBreakingChangesResponse(classifiedResult);

                    } else
                    {
                        throw new ArgumentException("Invalid request type for SdkBreakingChange classification.");
                    }
                case ClassificationKind.Customization:
                    if (request is ClassifyCustomizationRequest customizationRequest)
                    {
                        List<FeedbackItemClassificationDetails> allClassifiedResults = new List<FeedbackItemClassificationDetails>();
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
                            List<FeedbackItem> feedbackItems = chunk.Cast<FeedbackItem>().ToList();
                            var classifyResult = await BatchClassifyItems(classifyTemplate, feedbackItems, ct);
                            allClassifiedResults.AddRange(classifyResult);

                        }
                        return new ClassifyCustomizationResponse(allClassifiedResults);

                    } else
                    {
                        throw new ArgumentException("Invalid request type for Customization classification.");
                    }
                default:
                    throw new ArgumentException($"Unsupported classify type: {classifyType}");
            }
        }

        private async Task<List<T>> BatchClassifyItems<T, I>(ClassificationBaseTemplate<T, I> template, List<I>? items, CancellationToken ct)
        {
            var agent = new CopilotAgent<string>
            {
                Instructions = template.BuildPrompt(),
                Model = this.CopilotAgentModel,
            };

            var result = await _agentRunner.RunAsync(agent, ct);
            var classifiedResult = template.ParseClassifyResult(result, items);
            return classifiedResult;
        }
    }
}

