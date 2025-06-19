using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.Search.Documents.Agents;
using Azure.Search.Documents.Agents.Models;
using IssueLabeler.Shared;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace IssueLabelerService
{
    public class OpenAiAnswerService : IAnswerService
    {
        private const string SolutionAnswerType = "solution";
        private const string SuggestionsAnswerType = "suggestions";
        private RepositoryConfiguration _config;
        private TriageRag _ragService;
        private ILogger<AnswerFactory> _logger;
        private readonly KnowledgeAgentRetrievalClient _retrievalClient;

        public OpenAiAnswerService(ILogger<AnswerFactory> logger, RepositoryConfiguration config, TriageRag ragService)
        {
            _config = config;
            _ragService = ragService;
            _logger = logger;
            _retrievalClient = new KnowledgeAgentRetrievalClient(
                endpoint: new Uri(_config.SearchEndpoint),
                agentName: _config.KnowledgeAgentName,
                tokenCredential: new DefaultAzureCredential()
            );
        }

        public async Task<AnswerOutput> AnswerQuery(IssuePayload issue, Dictionary<string, string> labels)
        {
            var modelName = _config.AnswerModelName;
            var instructions = _config.KnowledgeAgentInstruction;
            var replacements = new Dictionary<string, string>
            {
                { "Title", issue.Title },
                { "Body", issue.Body },
            };
            var message = AzureSdkIssueLabelerService.FormatTemplate(_config.KnowledgeAgentMessage, replacements, _logger);

            KnowledgeAgentRetrievalRequest retrievalRequest = BuildRetrievalRequest(instructions, message);

            var retrievalResult = await _retrievalClient.RetrieveAsync(retrievalRequest);

            var contextBlock = GetContextBlock(retrievalResult);

            var response = await _ragService.SendMessageQnaAsync(instructions, message, modelName, contextBlock);

            var formattedResponse = FormatResponse(SuggestionsAnswerType, issue, response);

            _logger.LogInformation($"Open AI Response for {issue.RepositoryName} using the Complete Triage model for issue #{issue.IssueNumber}.: \n{formattedResponse}");

            return new AnswerOutput
            {
                Answer = formattedResponse,
                AnswerType = SuggestionsAnswerType
            };
        }

        private KnowledgeAgentRetrievalRequest BuildRetrievalRequest(string instructions, string message)
        {
            var agentMessages = new[]
            {
                new KnowledgeAgentMessage("assistant", new KnowledgeAgentMessageContent[] { new KnowledgeAgentMessageTextContent(instructions) }),
                new KnowledgeAgentMessage("user", new KnowledgeAgentMessageContent[] { new KnowledgeAgentMessageTextContent(message) }),
            };


            var retrievalRequest = new KnowledgeAgentRetrievalRequest(agentMessages)
            {
                TargetIndexParams =
                {
                    new KnowledgeAgentIndexParams
                    {
                        IndexName = _config.IndexName,
                        RerankerThreshold = 1.0f,
                        MaxDocsForReranker = 200,
                    }
                }
            };
            return retrievalRequest;
        }

        private string GetContextBlock(KnowledgeAgentRetrievalResponse retrievalResult)
        {
            if (retrievalResult.Response.Count == 0)
            {
                return null;
            }

            var snippets = retrievalResult.Response[0].Content
                .OfType<KnowledgeAgentMessageTextContent>()
                .Select(content => content.Text)
                .ToList();

            var allSources = new List<JObject>();

            foreach (var snippet in snippets)
            {
                try
                {
                    var arr = JArray.Parse(snippet);
                    foreach (var obj in arr)
                    {
                        if (obj is JObject jobj)
                            allSources.Add(jobj);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to parse snippet as JSON array: {ex.Message}");
                }
            }

            _logger.LogInformation($"Number of sources retrieved: {allSources.Count}");

            return string.Join(
                "\n\n",
                allSources.Select((obj, i) =>
                    $"Title: {obj["title"]}\nDescription: {obj["content"]}\nTerms: {obj["terms"]}")
            );
        }

        private string FormatResponse(string answerType, IssuePayload issue, string response)
        {
            string intro;
            string outro;

            var replacementsIntro = new Dictionary<string, string>
            {
                { "IssueUserLogin", issue.IssueUserLogin },
                { "RepositoryName", issue.RepositoryName }
            };

            if (answerType == SolutionAnswerType)
            {
                intro = AzureSdkIssueLabelerService.FormatTemplate(_config.SolutionResponseIntroduction, replacementsIntro, _logger);
                outro = _config.SolutionResponseConclusion;
            }
            else
            {
                intro = AzureSdkIssueLabelerService.FormatTemplate(_config.SuggestionResponseIntroduction, replacementsIntro, _logger);
                outro = _config.SuggestionResponseConclusion;
            }

            if (string.IsNullOrEmpty(response))
            {
                throw new Exception($"Open AI Response for {issue.RepositoryName} using the Complete Triage model for issue #{issue.IssueNumber} had an empty response.");
            }

            return intro + response + outro;
        }
    }
}
