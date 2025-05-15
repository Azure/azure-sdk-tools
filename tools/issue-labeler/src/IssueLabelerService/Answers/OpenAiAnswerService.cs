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

            //Subquery generation configurations
            int subqueryCount = query.Length < 50 ? 3 : query.Length < 150 ? 5 : 7;
            string countString = subqueryCount.ToString();
            var replacementSubqueriesPrompt = new Dictionary<string, string>
            {
                { "subqueryCount", countString },
                { "query", query }
            };

            string subqueriesPrompt = AzureSdkIssueLabelerService.FormatTemplate(_config.SubqueriesGenerationPrompt, replacementSubqueriesPrompt, _logger);

            //Step 1: Generate sub-queries
            var subqueries = await _ragService.GenerateSubqueriesAsync(subqueriesPrompt, query, modelName);

            //Step 2: Retrieve results for sub-queries
            var aggregatedIssues = new List<Issue>();
            var aggregatedDocs = new List<Document>();
            var uniqueIssues = new HashSet<string>();
            var uniqueDocs = new HashSet<string>();

            var subqueryTasks = subqueries.Select(async subquery =>
            {
                var subqueryIssues = await _ragService.SearchIssuesAsync(issueIndexName, issueSemanticName, issueFieldName, subquery, top, scoreThreshold, labels);
                var subqueryDocs = await _ragService.SearchDocumentsAsync(documentIndexName, documentSemanticName, documentFieldName, subquery, top, scoreThreshold, labels);
                AddUniqueItems(subqueryIssues, uniqueIssues, aggregatedIssues, issue => issue.Id);
                AddUniqueItems(subqueryDocs, uniqueDocs, aggregatedDocs, doc => doc.Url);
                return (Issues: subqueryIssues, Docs: subqueryDocs);
            });

            var subqueryResults = await Task.WhenAll(subqueryTasks);

            // Step 3: Retrieve results for the original query
            var originalIssues = await _ragService.SearchIssuesAsync(issueIndexName, issueSemanticName, issueFieldName, query, top, scoreThreshold, labels);
            var originalDocs = await _ragService.SearchDocumentsAsync(documentIndexName, documentSemanticName, documentFieldName, query, top, scoreThreshold, labels);


            // Step 4: Deduplicate original query results as they are added
            AddUniqueItems(originalIssues,uniqueIssues,aggregatedIssues, issue => issue.Id);
            AddUniqueItems(originalDocs, uniqueDocs, aggregatedDocs, doc => doc.Url);
            var allIssues = aggregatedIssues.ToList();
            var allDocs = aggregatedDocs.ToList();

            // Step 5: Check if there are any results
            if (allDocs.Count == 0 && allIssues.Count == 0)
            {
                throw new Exception($"Not enough relevant sources found for {issue.RepositoryName} using the Complete Triage model for issue #{issue.IssueNumber}. Documents: {allDocs.Count}, Issues: {allIssues.Count}.");
            }

            double highestScore = _ragService.GetHighestScore(allIssues, allDocs, issue.RepositoryName, issue.IssueNumber);
            bool solution = highestScore >= solutionThreshold;

            _logger.LogInformation($"Highest relevance score among the sources: {highestScore}");

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

            return new AnswerOutput
            {
                Answer = formatted_response,
                AnswerType = solution ? "solution" : "suggestion"
            };
        }

        private void AddUniqueItems<T, TKey>(
        IEnumerable<T> items,
        HashSet<TKey> uniqueSet,
        List<T> aggregatedList,
        Func<T, TKey> keySelector)
        {
            foreach (var item in items)
            {
                if (uniqueSet.Add(keySelector(item)))
                {
                    aggregatedList.Add(item);
                }
            }
        }
    }
}
