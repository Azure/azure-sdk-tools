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
using System.Linq;
using IssueLabeler.Shared;
using System.Collections.Generic;

namespace IssueLabelerService
{
    public class AzureSdkIssueLabelerService
    {
        private static readonly ActionResult EmptyResult = new JsonResult(new TriageOutput { Labels = [], Answer = null, AnswerType = null });
        private readonly ILogger<AzureSdkIssueLabelerService> _logger;
        private readonly TriageRag _ragService;
        private readonly Configuration _configurationService;
        private LabelerFactory _labelers;
        private QnaFactory _qnaServices;

        public AzureSdkIssueLabelerService(ILogger<AzureSdkIssueLabelerService> logger, TriageRag ragService, Configuration configService, LabelerFactory labelers, QnaFactory qnaServices)
        {
            _logger = logger;
            _ragService = ragService;
            _labelers = labelers;
            _configurationService = configService;
            _qnaServices = qnaServices;
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

            try
            {
                // Get the labeler based on the configuration
                var labeler = _labelers.GetLabeler(config);

                // Predict labels for the issue
                string[] labels = await labeler.PredictLabels(issue);

                // If no labels are returned, do not generate an answer
                if (labels == null || labels.Length == 0)
                {
                    _logger.LogInformation($"No labels predicted for issue #{issue.IssueNumber} in repository {issue.RepositoryName}.");
                    return EmptyResult;
                }

                // Get the Qna model based on configuration
                var qnaService = _qnaServices.GetQna(config);

                var answer = await qnaService.AnswerQuery(issue);

                TriageOutput result = new TriageOutput
                {
                    Labels = labels,
                    Answer = answer.Answer,
                    AnswerType = answer.AnswerType
                };

                return new JsonResult(result);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing issue #{issue.IssueNumber} in repository {issue.RepositoryName}: {ex.Message}{Environment.NewLine}\t{ex}{Environment.NewLine}");
                return EmptyResult;
            }
        }

        private async Task<IssuePayload> DeserializeIssuePayloadAsync(HttpRequest request)
        {
            using var bodyReader = new StreamReader(request.Body);
            var requestBody = await bodyReader.ReadToEndAsync();
            return JsonConvert.DeserializeObject<IssuePayload>(requestBody);
        }

        public static string FormatTemplate(string template, Dictionary<string, string> replacements)
        {
            if (string.IsNullOrEmpty(template))
                return string.Empty;

            string result = template;

            foreach (var replacement in replacements)
            {
                result = result.Replace($"{{{replacement.Key}}}", replacement.Value);
            }

            // Replace escaped newlines with actual newlines
            return result.Replace("\\n", "\n");
        }
    }
}
