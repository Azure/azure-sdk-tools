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
        public async Task<QnaOutput> AnswerQuery(IssuePayload issue)
        {
            // Configuration for Azure services
            var modelName = _config.OpenAIModelName;
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

            var relevantIssues = await _ragService.AzureSearchQueryAsync<Issue>(issueIndexName, issueSemanticName, issueFieldName, query, top);
            var relevantDocuments = await _ragService.AzureSearchQueryAsync<Document>(documentIndexName, documentSemanticName, documentFieldName, query, top);

            // Filter sources under threshold
            var docs = relevantDocuments
                .Where(r => r.Item2 >= scoreThreshold)
                .Select(rd => new
                {
                    rd.Item1.chunk,
                    rd.Item1.Url,
                    Score = rd.Item2
                })
                .ToList();

            var issues = relevantIssues
                .Where(r => r.Item2 >= scoreThreshold)
                .Select(rd => new
                {
                    rd.Item1.Title,
                    rd.Item1.chunk,
                    rd.Item1.Service,
                    rd.Item1.Category,
                    rd.Item1.Url,
                    Score = rd.Item2
                })
                .ToList();

            // Filtered out all sources for either one then not enough information to answer the issue. 
            if (docs.Count == 0 || issues.Count == 0)
            {
                throw new Exception($"Not enough relevant sources found for {issue.RepositoryName} using the Complete Triage model for issue #{issue.IssueNumber}.");
            }

            double highestScore = Math.Max(docs.Max(d => d.Score), issues.Max(d => d.Score));
            bool solution = highestScore >= solutionThreshold;

            _logger.LogInformation($"Highest relevance score among the sources: {highestScore}");

            // Makes it nicer for the model to read (Can probably be made more readable but oh well)
            var printableIssues = issues.Select(r => JsonConvert.SerializeObject(r)).ToList();
            var printableDocs = docs.Select(r => JsonConvert.SerializeObject(r)).ToList();

            string instructions = _config.Instructions;
            string message;
            if (solution)
            {
                message = $"Sources:\nDocumentation:\n{string.Join("\n", printableDocs)}\nGitHub Issues:\n{string.Join("\n", printableIssues)}\nThe user needs a solution to their GitHub Issue:\n{query}";
            }
            else
            {
                message = $"Sources:\nDocumentation:\n{string.Join("\n", printableDocs)}\nGitHub Issues:\n{string.Join("\n", printableIssues)}\nThe user needs suggestions for their GitHub Issue:\n{query}";
            }

            var response = await _ragService.SendMessageQnaAsync(instructions, message);

            string intro, outro;
            var replacements = new Dictionary<string, string>
            {
                { "IssueUserLogin", issue.IssueUserLogin },
                { "RepositoryName", issue.RepositoryName }
            };

            if (solution)
            {
                intro = AzureSdkIssueLabelerService.FormatTemplate(_config.SolutionResponseIntroduction, replacements, _logger);
                outro = _config.SolutionResponseConclusion;
            }
            else
            {
                intro = AzureSdkIssueLabelerService.FormatTemplate(_config.SuggestionResponseIntroduction, replacements, _logger);
                outro = _config.SuggestionResponseConclusion;
            }

            if (string.IsNullOrEmpty(response))
            {
                throw new Exception($"Open AI Response for {issue.RepositoryName} using the Complete Triage model for issue #{issue.IssueNumber} had an empty response.");
            }

            string formatted_response = intro + response + outro;

            _logger.LogInformation($"Open AI Response for {issue.RepositoryName} using the Complete Triage model for issue #{issue.IssueNumber}.: \n{formatted_response}");

            return new QnaOutput {
                Answer =  formatted_response, 
                AnswerType = solution ? "solution" : "suggestion" 
            };
        }
    }
}
