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
        private readonly ServiceConfiguration _serviceConfiguration;
        private RepositoryConfiguration _config;
        private TriageRag _ragService;
        private ILogger<AnswerFactory> _logger;
        public OpenAiAnswerService(ILogger<AnswerFactory> logger, RepositoryConfiguration config, TriageRag ragService)
        {
            _config = config;
            _ragService = ragService;
            _logger = logger;

            _serviceConfiguration = new ServiceConfiguration(
                ModelName:             config.AnswerModelName,
                IssueIndexName:        config.IssueIndexName,
                IssueSemanticName:     config.IssueSemanticName,
                IssueFieldName:        config.IssueIndexFieldName,
                DocumentIndexName:     config.DocumentIndexName,
                DocumentSemanticName:  config.DocumentSemanticName,
                DocumentFieldName:     config.DocumentIndexFieldName,
                Top:                   int.Parse(config.SourceCount),
                ScoreThreshold:        double.Parse(config.ScoreThreshold),
                SolutionThreshold:     double.Parse(config.SolutionThreshold),
                SubqueriesPromptTemplate: config.SubqueriesGenerationPrompt,
                SolutionInstructions:           config.SolutionInstructions,
                SuggestionInstructions:         config.SuggestionInstructions,
                SolutionUserPrompt:             config.SolutionUserPrompt,
                SuggestionUserPrompt:          config.SuggestionUserPrompt);
        }
        public async Task<AnswerOutput> AnswerQuery(IssuePayload issue, Dictionary<string, string> labels)
        {
            var query = $"{issue.Title} {issue.Body}";
            var uniqueIssues = new HashSet<Issue>(new IssueIdComparer());
            var uniqueDocs = new HashSet<Document>(new DocumentUrlComparer());


            //Step 1: Generate sub-queries
            var subqueries = await _ragService.GenerateSubqueriesAsync(_serviceConfiguration.SubqueriesPromptTemplate, query, _serviceConfiguration.ModelName);

            //Step 2: Retrieve results for sub-queries
            await RetrieveAndAggregateSubqueryResults(subqueries, labels, uniqueIssues, uniqueDocs);

            // Step 3: Retrieve results for the original query
            var originalIssues = await _ragService.SearchIssuesAsync(_serviceConfiguration.IssueIndexName, _serviceConfiguration.IssueSemanticName, _serviceConfiguration.IssueFieldName, query, _serviceConfiguration.Top, _serviceConfiguration.ScoreThreshold, labels);
            var originalDocs = await _ragService.SearchDocumentsAsync(_serviceConfiguration.DocumentIndexName, _serviceConfiguration.DocumentSemanticName, _serviceConfiguration.DocumentFieldName, query, _serviceConfiguration.Top, _serviceConfiguration.ScoreThreshold, labels);

            // Step 4: Deduplicate original query results as they are added
            uniqueIssues.UnionWith(originalIssues);
            uniqueDocs.UnionWith(originalDocs);

            if (uniqueIssues.Count == 0 && uniqueDocs.Count == 0)
            {
                throw new Exception($"Not enough relevant sources found for {issue.RepositoryName} using the Complete Triage model for issue #{issue.IssueNumber}. Documents: {uniqueDocs.Count}, Issues: {uniqueIssues.Count}.");
            }

            var highestScore = _ragService.GetHighestScore(uniqueIssues, uniqueDocs, issue.RepositoryName, issue.IssueNumber);
            var isSolution = highestScore >= _serviceConfiguration.SolutionThreshold;

            _logger.LogInformation($"Highest relevance score among the sources: {highestScore}");

            var replacementsUserPrompt = BuildUserPromptData(uniqueIssues, uniqueDocs, issue);

            var instructions = isSolution ? _serviceConfiguration.SolutionInstructions : _serviceConfiguration.SuggestionInstructions;
            var prompt = isSolution ? _serviceConfiguration.SolutionUserPrompt: _serviceConfiguration.SuggestionUserPrompt;
            var userPrompt = AzureSdkIssueLabelerService.FormatTemplate(prompt, replacementsUserPrompt, _logger);

            var response = await _ragService.SendMessageQnaAsync(instructions, userPrompt, _serviceConfiguration.ModelName);

            var replacementsIntro = new Dictionary<string, string>
            {
                { "IssueUserLogin", issue.IssueUserLogin },
                { "RepositoryName", issue.RepositoryName }
            };
            var responseIntroduction = isSolution ? _config.SolutionResponseIntroduction : _config.SuggestionResponseIntroduction;
            var intro = AzureSdkIssueLabelerService.FormatTemplate(responseIntroduction, replacementsIntro, _logger);
            var outro = isSolution ? _config.SolutionResponseConclusion : _config.SuggestionResponseConclusion;

            if (string.IsNullOrEmpty(response))
            {
                throw new Exception($"Open AI Response for {issue.RepositoryName} using the Complete Triage model for issue #{issue.IssueNumber} had an empty response.");
            }

            var formatted_response = intro + response + outro;

            _logger.LogInformation($"Open AI Response for {issue.RepositoryName} using the Complete Triage model for issue #{issue.IssueNumber}.: \n{formatted_response}");

            return new AnswerOutput
            {
                Answer = formatted_response,
                AnswerType = isSolution ? "solution" : "suggestion"
            };
        }
        private async Task RetrieveAndAggregateSubqueryResults(
            IEnumerable<string> subqueries,
            Dictionary<string, string> labels,
            HashSet<Issue> uniqueIssues,
            HashSet<Document> uniqueDocs)
        {
            var subqueryTasks = subqueries.Select(async subquery =>
            {
                var subqueryIssues = await _ragService.SearchIssuesAsync(_serviceConfiguration.IssueIndexName, _serviceConfiguration.IssueSemanticName, _serviceConfiguration.IssueFieldName, subquery, _serviceConfiguration.Top, _serviceConfiguration.ScoreThreshold, labels);
                var subqueryDocs = await _ragService.SearchDocumentsAsync(_serviceConfiguration.DocumentIndexName, _serviceConfiguration.DocumentSemanticName, _serviceConfiguration.DocumentFieldName, subquery, _serviceConfiguration.Top, _serviceConfiguration.ScoreThreshold, labels);
                uniqueIssues.UnionWith(subqueryIssues);
                uniqueDocs.UnionWith(subqueryDocs);
            });
            await Task.WhenAll(subqueryTasks);
        }

        private Dictionary<string, string> BuildUserPromptData(
            IEnumerable<Issue> allIssues,
            IEnumerable<Document> allDocs,
            IssuePayload issue)
        {
            var printableIssues = string.Join("\n\n", allIssues.Select(issue =>
                $"Title: {issue.Title}\nDescription: {issue.chunk}\nURL: {issue.Url}\nScore: {issue.Score}"));

            var printableDocs = string.Join("\n\n", allDocs.Select(doc =>
                $"Content: {doc.chunk}\nURL: {doc.Url}\nScore: {doc.Score}"));

            var replacementsUserPrompt = new Dictionary<string, string>
            {
                { "Title", issue.Title },
                { "Description", issue.Body },
                { "PrintableDocs", printableDocs },
                { "PrintableIssues", printableIssues }
            };

            return replacementsUserPrompt;
        }

        private class IssueIdComparer : IEqualityComparer<Issue>
        {
            public bool Equals(Issue x, Issue y) => x.Id == y.Id;
            public int GetHashCode(Issue obj) => obj.Id.GetHashCode();
        }

        private class DocumentUrlComparer : IEqualityComparer<Document>
        {
            public bool Equals(Document x, Document y) => x.Url == y.Url;
            public int GetHashCode(Document obj) => obj.Url.GetHashCode();
        }
        private record ServiceConfiguration(
            string ModelName,
            string IssueIndexName,
            string IssueSemanticName,
            string IssueFieldName,
            string DocumentIndexName,
            string DocumentSemanticName,
            string DocumentFieldName,
            int Top,
            double ScoreThreshold,
            double SolutionThreshold,
            string SubqueriesPromptTemplate,
            string SolutionInstructions,
            string SuggestionInstructions,
            string SolutionUserPrompt,
            string SuggestionUserPrompt);
    }
}