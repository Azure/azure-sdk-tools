// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using IssueLabeler.Shared;
using System.Collections.Generic;

namespace IssueLabelerService
{
    public class AzureSdkIssueLabelerService
    {
        private static readonly ActionResult EmptyResult = new JsonResult(new TriageOutput { Labels = [], Answer = null, AnswerType = null });
        private readonly ILogger<AzureSdkIssueLabelerService> _logger;
        private readonly Configuration _configurationService;
        private LabelerFactory _labelers;
        private AnswerFactory _answerServices;

        public AzureSdkIssueLabelerService(ILogger<AzureSdkIssueLabelerService> logger, TriageRag ragService, Configuration configService, LabelerFactory labelers, AnswerFactory answerServices)
        {
            _logger = logger;
            _labelers = labelers;
            _configurationService = configService;
            _answerServices = answerServices;
        }

        [Function("AzureSdkIssueLabelerService")]
        public async Task<ActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "POST", Route = null)] HttpRequest request)
        {
            IssuePayload issue;
            try
            {
                issue = await DeserializeIssuePayloadAsync(request);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unable to deserialize payload: {ex.Message}{Environment.NewLine}\t{ex}{Environment.NewLine}");
                return new BadRequestResult();
            }

            var config = _configurationService.GetForRepository($"{issue.RepositoryOwnerName}/{issue.RepositoryName}");
            Dictionary<string, string> labels;

            // Enable labels if both the configuration enable labels and the issue predict labels are true
            if(bool.Parse(config.EnableLabels) && issue.PredictLabels)
            {
                try
                {
                    // Get the labeler based on the configuration
                    var labeler = _labelers.GetLabeler(config);

                    // Predict labels for the issue
                    labels = await labeler.PredictLabels(issue);

                    // If no labels are returned from ML models, try keyword-based labeling as fallback
                    if (labels?.Count == 0)
                    {
                        _logger.LogInformation($"No labels predicted by ML models for issue #{issue.IssueNumber} in repository {issue.RepositoryName}. Attempting keyword-based labeling.");
                        labels = ApplyKeywordBasedLabels(issue);
                        
                        if (labels?.Count == 0)
                        {
                            _logger.LogInformation($"No labels could be determined for issue #{issue.IssueNumber} in repository {issue.RepositoryName}.");
                            return EmptyResult;
                        }
                    }
                    else
                    {
                        // Supplement ML predictions with keyword-based labels if they're missing basic category labels
                        var keywordLabels = ApplyKeywordBasedLabels(issue);
                        if (keywordLabels?.Count > 0)
                        {
                            foreach (var kvp in keywordLabels)
                            {
                                if (!labels.ContainsKey(kvp.Key))
                                {
                                    labels.Add(kvp.Key, kvp.Value);
                                    _logger.LogInformation($"Added keyword-based label '{kvp.Key}' to supplement ML predictions for issue #{issue.IssueNumber}.");
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error labeling issue #{issue.IssueNumber} in repository {issue.RepositoryName}: {ex.Message}{Environment.NewLine}\t{ex}{Environment.NewLine}");
                    
                    // Try keyword-based labeling as fallback even when ML fails
                    labels = ApplyKeywordBasedLabels(issue);
                    if (labels?.Count == 0)
                    {
                        return EmptyResult;
                    }
                    _logger.LogInformation($"Applied keyword-based labels as fallback after ML error for issue #{issue.IssueNumber}.");
                }

            }
            else
            {
                _logger.LogError($"Labeling is turned off for issue #{issue.IssueNumber} in repository {issue.RepositoryName}. Without Labels, answers will not be suggested.");
                return EmptyResult;
            }

            TriageOutput result = new TriageOutput { Labels = labels.Values };
            
            // Enable answers if both the configuration enable answers and the issue predict answers are true
            if(bool.Parse(config.EnableAnswers) && issue.PredictAnswers)
            {
                try{

                    // Get the Answer Service based on configuration
                    var answerService = _answerServices.GetAnswerService(config);

                    var answer = await answerService.AnswerQuery(issue, labels);

                    result.Answer = answer.Answer;
                    result.AnswerType = answer.AnswerType;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error commenting on issue #{issue.IssueNumber} in repository {issue.RepositoryName}: {ex.Message}{Environment.NewLine}\t{ex}{Environment.NewLine}");

                    result.Answer = null;
                    result.AnswerType = null;
                }
            }

            return new JsonResult(result);
        }

        private async Task<IssuePayload> DeserializeIssuePayloadAsync(HttpRequest request)
        {
            using var bodyReader = new StreamReader(request.Body);
            var requestBody = await bodyReader.ReadToEndAsync();
            return JsonConvert.DeserializeObject<IssuePayload>(requestBody);
        }

        /// <summary>
        /// Apply keyword-based labels as a fallback mechanism when ML models don't predict labels
        /// or to supplement existing predictions with basic category labels.
        /// </summary>
        /// <param name="issue">The issue payload containing title and body</param>
        /// <returns>Dictionary of labels to apply based on keywords</returns>
        private Dictionary<string, string> ApplyKeywordBasedLabels(IssuePayload issue)
        {
            var labels = new Dictionary<string, string>();
            var combinedText = $"{issue.Title} {issue.Body}".ToLowerInvariant();

            // Define keyword patterns for different label types
            var keywordPatterns = new Dictionary<string, string[]>
            {
                ["bug"] = new[] { "bug", "error", "exception", "crash", "failure", "broken", "not working", "issue", "problem" },
                ["feature-request"] = new[] { "feature request", "enhancement", "improve", "add support", "new feature", "would like", "suggestion", "could you add" },
                ["question"] = new[] { "question", "how to", "how do i", "how can i", "help", "clarification", "understand", "explain", "what is", "why does" }
            };

            foreach (var pattern in keywordPatterns)
            {
                foreach (var keyword in pattern.Value)
                {
                    if (combinedText.Contains(keyword))
                    {
                        if (!labels.ContainsKey(pattern.Key))
                        {
                            labels.Add(pattern.Key, pattern.Key);
                            _logger.LogInformation($"Applied keyword-based label '{pattern.Key}' based on keyword '{keyword}' for issue #{issue.IssueNumber}.");
                        }
                        break; // Found a match for this label type, no need to check other keywords
                    }
                }
            }

            return labels;
        }

        public static string FormatTemplate(string template, Dictionary<string, string> replacements, ILogger logger)
        {
            if (string.IsNullOrEmpty(template))
                return string.Empty;

            string result = template;

            foreach (var replacement in replacements)
            {
                if(!result.Contains($"{{{replacement.Key}}}"))
                {
                    logger.LogWarning($"Replacement value for {replacement.Key} does not exist in {template}.");
                }
                result = result.Replace($"{{{replacement.Key}}}", replacement.Value);
            }

            // Replace escaped newlines with actual newlines
            return result.Replace("\\n", "\n");
        }
    }
}
