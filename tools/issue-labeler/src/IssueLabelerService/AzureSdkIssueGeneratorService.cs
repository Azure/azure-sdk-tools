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
using Octokit;

namespace IssueLabelerService
{
    public class AzureSdkIssueGeneratorService
    {
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
            IssueGeneratorPayload payload;
            try
            {
                payload = await DeserializeIssuePayloadAsync(request);
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
                var answer = await generatorService.GenerateIssue(payload);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error generating issue for repository {payload.RepositoryName}: {ex.Message}{Environment.NewLine}\t{ex}{Environment.NewLine}");

                result.Answer = null;
                result.AnswerType = null;
            }


            return new JsonResult(result);
        }

        private async Task<IssueGeneratorPayload> DeserializeIssuePayloadAsync(HttpRequest request)
        {
            using var bodyReader = new StreamReader(request.Body);
            var requestBody = await bodyReader.ReadToEndAsync();
            return JsonConvert.DeserializeObject<IssueGeneratorPayload>(requestBody);
        }
    }
}
