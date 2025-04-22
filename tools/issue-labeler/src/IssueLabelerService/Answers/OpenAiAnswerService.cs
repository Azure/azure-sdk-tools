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
        public async Task<AnswerOutput> AnswerQuery(IssuePayload issue, Dictionary<string, string> labels)
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
            var docs = await _ragService.SearchDocumentsAsync(documentIndexName, documentSemanticName, documentFieldName, query, top, scoreThreshold, labels);
 
            if (docs.Count == 0 && issues.Count == 0)
            {
                throw new Exception($"Not enough relevant sources found for {issue.RepositoryName} using the Complete Triage model for issue #{issue.IssueNumber}. Documents: {docs.Count}, Issues: {issues.Count}.");
            }

            double highestScore = _ragService.GetHighestScore(issues, docs, issue.RepositoryName, issue.IssueNumber);
            bool solution = highestScore >= solutionThreshold;

            _logger.LogInformation($"Highest relevance score among the sources: {highestScore}");

            // Format issues 
            var printableIssues = string.Join("\n\n", issues.Select(issue =>
                $"Title: {issue.Title}\nDescription: {issue.chunk}\nURL: {issue.Url}\nScore: {issue.Score}"));

            // Format documents 
            var printableDocs = string.Join("\n\n", docs.Select(doc =>
                $"Content: {doc.chunk}\nURL: {doc.Url}\nScore: {doc.Score}"));

            var replacementsUserPrompt = new Dictionary<string, string>
            {
                { "Query", query },
                { "PrintableDocs", printableDocs },
                { "PrintableIssues", printableIssues }
            };

            string instructions, userPrompt;
            if (solution)
            {
                instructions = _config.SolutionInstructions;
                userPrompt = AzureSdkIssueLabelerService.FormatTemplate(_config.SolutionUserPrompt, replacementsUserPrompt, _logger);
            }
            else
            {
                instructions = _config.SuggestionInstructions;
                userPrompt = AzureSdkIssueLabelerService.FormatTemplate(_config.SuggestionUserPrompt, replacementsUserPrompt, _logger);
            }

            var response = await _ragService.SendMessageQnaAsync(instructions, userPrompt, modelName);

            string intro, outro;
            var replacementsIntro = new Dictionary<string, string>
            {
                { "IssueUserLogin", issue.IssueUserLogin },
                { "RepositoryName", issue.RepositoryName }
            };

            if (solution)
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

            string formatted_response = intro + response + outro;

            _logger.LogInformation($"Open AI Response for {issue.RepositoryName} using the Complete Triage model for issue #{issue.IssueNumber}.: \n{formatted_response}");

            return new AnswerOutput {
                Answer =  formatted_response, 
                AnswerType = solution ? "solution" : "suggestion" 
            };
        }
    }
}
