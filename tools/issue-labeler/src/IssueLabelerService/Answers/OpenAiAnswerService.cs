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
        private const string SolutionAnswerType = "solution";
        private const string SuggestionsAnswerType = "suggestions";
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
            var modelName = _config.AnswerModelName;

            var solutionThreshold = double.Parse(_config.SolutionThreshold);

            var searchContentResults = await GetSearchContentResults(issue, labels);

            var printableContent = string.Join("\n\n", searchContentResults.Select(searchContent =>
                $"Title: {searchContent.Title}\nDescription: {searchContent.Chunk}\nURL: {searchContent.Url}\nScore: {searchContent.Score}"));

            var highestScore = _ragService.GetHighestScoreForContent(searchContentResults, issue.RepositoryName, issue.IssueNumber);
            _logger.LogInformation($"Highest relevance score among the sources: {highestScore}");

            var answerType = highestScore >= solutionThreshold ? SolutionAnswerType : SuggestionsAnswerType;
            _logger.LogInformation($"Solution status for {issue.RepositoryName} using the Complete Triage model for issue #{issue.IssueNumber}: {answerType}");

            var userPrompt = FormatUserPrompt(issue, answerType, printableContent);

            var response = await _ragService.SendMessageQnaAsync(_config.Instructions, userPrompt, modelName);

            var formattedResponse = FormatResponse(answerType, issue, response);

            _logger.LogInformation($"Open AI Response for {issue.RepositoryName} using the Complete Triage model for issue #{issue.IssueNumber}.: \n{formattedResponse}");

            return new AnswerOutput
            {
                Answer = formattedResponse,
                AnswerType = answerType
            };
        }

        private async Task<List<IndexContent>> GetSearchContentResults(IssuePayload issue, Dictionary<string, string> labels)
        {
            var indexName = _config.IndexName;
            var semanticName = _config.SemanticName;
            var fieldName = _config.IssueIndexFieldName;
            var top = int.Parse(_config.SourceCount);
            var scoreThreshold = double.Parse(_config.ScoreThreshold);
            var query = $"{issue.Title} {issue.Body}";

            _logger.LogInformation($"Searching content index '{indexName}' with query: {query}");
            var searchContentResults = await _ragService.IssueTriageContentIndexAsync(
                indexName,
                semanticName,
                fieldName,
                query,
                top,
                scoreThreshold,
                labels
            );

            if (searchContentResults.Count == 0)
            {
                throw new Exception($"Not enough relevant sources found for {issue.RepositoryName} using the Complete Triage model for issue #{issue.IssueNumber}.");
            }

            _logger.LogInformation($"Found {searchContentResults.Count} relevant issues for {issue.RepositoryName} using the Complete Triage model for issue #{issue.IssueNumber}.");

            return searchContentResults;
        }

        private string FormatUserPrompt(IssuePayload issue, string answerType, string printableContent)
        {
            var replacements = new Dictionary<string, string>
            {
                { "Title", issue.Title },
                { "Description", issue.Body },
                { "AnswerType", answerType },
                { "PrintableContent", printableContent }
            };

            return AzureSdkIssueLabelerService.FormatTemplate(
                _config.Prompt,
                replacements,
                _logger
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