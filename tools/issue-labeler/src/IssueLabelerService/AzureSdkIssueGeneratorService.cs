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
    public class AzureSdkIssueGeneratorService
    {
        private static readonly ActionResult EmptyResult = new JsonResult(new TriageOutput { Labels = [], Answer = null, AnswerType = null });
        private readonly ILogger<AzureSdkIssueLabelerService> _logger;
        private readonly Configuration _configurationService;
        private IssueGeneratorFactory _issueGeneratorServices;

        public AzureSdkIssueGeneratorService(ILogger<AzureSdkIssueLabelerService> logger, TriageRag ragService, Configuration configService, IssueGeneratorFactory issueGeneratorServices)
        {
            _logger = logger;
            _configurationService = configService;
            _issueGeneratorServices = issueGeneratorServices;
        }

        [Function("AzureSdkIssueGeneratorService")]
        public async Task<ActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "POST", Route = null)] HttpRequest request)
        {
            string repositoryName;
            try
            {
                repositoryName = await DeserializeIssuePayloadAsync(request);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unable to deserialize payload: {ex.Message}{Environment.NewLine}\t{ex}{Environment.NewLine}");
                return new BadRequestResult();
            }

            var config = _configurationService.GetDefault();
            TriageOutput result = new TriageOutput {};

            // Enable answers if both the configuration enable answers and the issue predict answers are true

            try
            {
                var generatorService = _issueGeneratorServices.GetIssueGeneratorService(config);
                var answer = await generatorService.GenerateIssue(repositoryName);
                result.Answer = answer;
                Console.WriteLine($"OpenAI answer: {answer}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error generating issue for repository {repositoryName}: {ex.Message}{Environment.NewLine}\t{ex}{Environment.NewLine}");

                result.Answer = null;
                result.AnswerType = null;
            }


            return new JsonResult(result);
        }

        private async Task<string> DeserializeIssuePayloadAsync(HttpRequest request)
        {
            using var bodyReader = new StreamReader(request.Body);
            var requestBody = await bodyReader.ReadToEndAsync();
            return requestBody;
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
