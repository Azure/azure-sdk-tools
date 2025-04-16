using System;
using System.Threading.Tasks;
using IssueLabeler.Shared;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Collections.Generic;

namespace IssueLabelerService
{
    public class OpenAiAnswerService : IAnswerService
    {
        private RepositoryConfiguration _config;
        private TriageRag _ragService;
        private ILogger<AnswerFactory> _logger;
        public OpenAiAnswerService(ILogger<AnswerFactory> logger, RepositoryConfiguration config, TriageRag ragService)
        {
            _config = config;
            _ragService = ragService;
            _logger = logger;
        }
        public async Task<AnswerOutput> AnswerQuery(IssuePayload issue, string[] labels)
        {
            // Configuration for Azure services
            var modelName = _config.AnswerModelName;
            var issueIndexName = _config.IssueIndexName;
            var documentIndexName = _config.DocumentIndexName;

            // Issue specific configurations
            var issueSemanticName = _config.IssueSemanticName;
            string issueFieldName = _config.IssueIndexFieldName;

            // Document specific configurations
            var documentSemanticName = _config.DocumentSemanticName;
            string documentFieldName = _config.DocumentIndexFieldName;

            // Query + Filtering configurations
            string query = $"{issue.Title} {issue.Body}";
            int top = int.Parse(_config.SourceCount);
            double scoreThreshold = double.Parse(_config.ScoreThreshold);
            double solutionThreshold = double.Parse(_config.SolutionThreshold);

            var issues = await _ragService.SearchIssuesAsync(issueIndexName, issueSemanticName, issueFieldName, query, top, scoreThreshold, labels);

            // TODO: Add labels once dotnet has Service and Category fields in Document Index
            var docs = await _ragService.SearchDocumentsAsync(documentIndexName, documentSemanticName, documentFieldName, query, top, scoreThreshold);

            // Filtered out all sources for either one then not enough information to answer the issue. 
            if (docs.Count == 0 && issues.Count == 0)
            {
                throw new Exception($"Not enough relevant sources found for {issue.RepositoryName} using the Complete Triage model for issue #{issue.IssueNumber}. Documents: {docs.Count}, Issues: {issues.Count}.");
            }

            double highestScore = _ragService.GetHighestScore(issues, docs, issue.RepositoryName, issue.IssueNumber);
            bool solution = highestScore >= solutionThreshold;

            _logger.LogInformation($"Highest relevance score among the sources: {highestScore}");

            // Makes it nicer for the model to read (Can probably be made more readable but oh well)
            var printableIssues = string.Join("\n", issues.Select(r => JsonConvert.SerializeObject(r)));
            var printableDocs = string.Join("\n", docs.Select(r => JsonConvert.SerializeObject(r)));

            var replacements_UserPrompt = new Dictionary<string, string>
            {
                { "Query", query },
                { "PrintableDocs", printableDocs },
                { "PrintableIssues", printableIssues }
            };

            string instructions, userPrompt;
            if (solution)
            {
                instructions = _config.SolutionInstructions;
                userPrompt = AzureSdkIssueLabelerService.FormatTemplate(_config.SolutionUserPrompt, replacements_UserPrompt, _logger);
            }
            else
            {
                instructions = _config.SuggestionInstructions;
                userPrompt = AzureSdkIssueLabelerService.FormatTemplate(_config.SuggestionUserPrompt, replacements_UserPrompt, _logger);
            }

            var response = await _ragService.SendMessageQnaAsync(instructions, userPrompt, modelName);

            string intro, outro;
            var replacements_intro = new Dictionary<string, string>
            {
                { "IssueUserLogin", issue.IssueUserLogin },
                { "RepositoryName", issue.RepositoryName }
            };

            if (solution)
            {
                intro = AzureSdkIssueLabelerService.FormatTemplate(_config.SolutionResponseIntroduction, replacements_intro, _logger);
                outro = _config.SolutionResponseConclusion;
            }
            else
            {
                intro = AzureSdkIssueLabelerService.FormatTemplate(_config.SuggestionResponseIntroduction, replacements_intro, _logger);
                outro = _config.SuggestionResponseConclusion;
            }

            if (string.IsNullOrEmpty(response))
            {
                throw new Exception($"Open AI Response for {issue.RepositoryName} using the Complete Triage model for issue #{issue.IssueNumber} had an empty response.");
            }

            string formatted_response = intro + response + outro;

            _logger.LogInformation($"Open AI Response for {issue.RepositoryName} using the Complete Triage model for issue #{issue.IssueNumber}.: \n{formatted_response}");

            return new AnswerOutput {
                Answer =  formatted_response, 
                AnswerType = solution ? "solution" : "suggestion" 
            };
        }
    }
}
