using System;
using System.Collections.Generic;
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using APIViewAI.Interfaces;
using APIViewAI.Model;
using Azure.AI.OpenAI;
using Azure.Identity;
using OpenAI.Chat;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Configuration;
using System.Text;

namespace APIViewAI
{
    public class APIViewCommentProcessor : IAPIViewCommentProcessor
    {
        private string DEPLOYED_MODEL_NAME = "o3-mini";
        private string SystemPrompt = "Azure SDK reviewer has review public API surface in SDK and identifies any issues in the SDK. A comment is added against public API line in API surface. You are an SDK API reviewer and you will help identify any similar issue in Public API surface. I want you to help SDK reviewer by scanning the entire API surface in given input content and identify if there are any more similar issues. Suggest all such instances and a comment that I can associate to each line. You should exclude the exact line that is already passed as input. You can return an empty result if the comment from reviewer is too vague and also look for exactly similar APIs. Sometimes comment is added in the format of a question. for e.g. should this be <xyz>? Each prompt contains API surface and message about review line and comment from reviewer. Generate the list as JSON of array with each element has Comment and ReviewLine.";

        private AzureOpenAIClient? _azureClient;
        private ChatClient? _chatClient;
        private bool _isAIServiceEnabled = true;

        public APIViewCommentProcessor(IConfiguration conf)
        {
            var endpoint = conf["AZURE_OPENAI_ENDPOINT"];
            var modelName = conf["AZURE_OPENAI_MODEL"] ?? DEPLOYED_MODEL_NAME;

            if (string.IsNullOrEmpty(endpoint))
            {
                _isAIServiceEnabled = false;
            }
            else
            {
                _azureClient = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential());
                _chatClient = _azureClient.GetChatClient(modelName);
            }
        }

        // Used by unit test project
        public APIViewCommentProcessor(string endPoint)
        {
            _azureClient = new AzureOpenAIClient(new Uri(endPoint), new DefaultAzureCredential());
            _chatClient = _azureClient.GetChatClient(DEPLOYED_MODEL_NAME);
        }

        public async Task<IList<CommentResponseModel>> DetectSimilarSuggestionsAsync(CommentRequestModel request)
        {
            var result = new List<CommentResponseModel>();
            if (!_isAIServiceEnabled || _chatClient == null)
            {
                return result;
            }

            try
            {
                var response = await _chatClient.CompleteChatAsync(new ChatMessage[] {
                    new SystemChatMessage(SystemPrompt),
                    new UserChatMessage($"Review line: {request.ReviewLine}"),
                    new UserChatMessage($"Comment: {request.Comment}"),
                    new UserChatMessage($"API surface: {request.APISurface}")
                });

                if (response != null && response.Value?.Content?.Count > 0)
                {
                    foreach (var content in response.Value.Content)
                    {
                        var results = JsonSerializer.Deserialize<IEnumerable<CommentResponseModel>>(content.Text);
                        if (results != null)
                        {
                            result.AddRange(results);
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                // todo error reporting to app insights

            }
            return result;
        }

        public async Task<IList<CommentResponseModel>> GenerateAIReviewComments(IList<CommentRequestModel> comments, string reviewText)
        {
            var result = new List<CommentResponseModel>();
            if (!_isAIServiceEnabled || _chatClient == null)
            {
                return result;
            }
            try
            {
                var prompt = GenerateUserPromptFromComments(comments);
                var response = await _chatClient.CompleteChatAsync(new ChatMessage[] {
                    new SystemChatMessage(SystemPrompt),
                    new UserChatMessage(prompt),
                    new UserChatMessage($"Review Text: {reviewText}"),
                });
                if (response != null && response.Value?.Content?.Count > 0)
                {
                    foreach (var content in response.Value.Content)
                    {
                        var results = JsonSerializer.Deserialize<IEnumerable<CommentResponseModel>>(content.Text);
                        if (results != null)
                        {
                            result.AddRange(results);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                // todo error reporting to app insights
            }
            return result;
        }

        private string GenerateUserPromptFromComments(IList<CommentRequestModel> comments)
        {
            var prompt = new StringBuilder();
            var commentsJson = JsonSerializer.Serialize(comments);
            prompt.AppendLine("You are an assistant API reviewer. Here is a lit of comments created by Azure SDK architect in the past and corresponding API line. Your task is to scan the entire API surface given in review text and identify if there are any more similar issues that matches the previous comments from Azure SDK architect. You should exclude the exact line that is already passed as input. You can return an empty result if the comment from reviewer is too vague and also look for exactly similar APIs. Sometimes comment is added in the format of a question. for e.g. should this be <xyz>? Each prompt contains API surface and message about review line and comment from reviewer. Generate the list as JSON of array with each element has Comment and ReviewLine.");
            prompt.AppendLine("Here is the list of comments:");
            prompt.AppendLine(commentsJson);
            return prompt.ToString();

        }
    }
}
