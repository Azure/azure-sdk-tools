using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using IssueLabeler.Shared;
using System;
using System.Linq;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace IssueLabelerService
{
    public class OpenAiLabeler : ILabeler
    {
        private ILogger<LabelerFactory> _logger;
        private RepositoryConfiguration _config;
        private TriageRag _ragService;

        public OpenAiLabeler(ILogger<LabelerFactory> logger, RepositoryConfiguration config, TriageRag ragService) =>
            (_logger, _config, _ragService) = (logger, config, ragService);

        public async Task<string[]> PredictLabels(IssuePayload issue)
        {
            // Configuration for Azure services
            var modelName = _config.LabelModelName;
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

            // Search for issues and documents
            var issues = await _ragService.SearchIssuesAsync(issueIndexName, issueSemanticName, issueFieldName, query, top, scoreThreshold);
            var docs = await _ragService.SearchDocumentsAsync(documentIndexName, documentSemanticName, documentFieldName, query, top, scoreThreshold);

            // Filtered out all sources for either one then not enough information to answer the issue. 
            if (docs.Count == 0 || issues.Count == 0)
            {
                throw new Exception($"Not enough relevant sources found for {issue.RepositoryName} using the Open AI Labeler for issue #{issue.IssueNumber}.");
            }

            double highestScore = _ragService.GetHighestScore(issues, docs, issue.RepositoryName, issue.IssueNumber);
            bool solution = highestScore >= solutionThreshold;

            _logger.LogInformation($"Highest relevance score among the sources: {highestScore}");

            // Makes it nicer for the model to read (Can probably be made more readable but oh well)
            var printableIssues = string.Join("\n", issues.Select(r => JsonConvert.SerializeObject(r)));
            var printableDocs = string.Join("\n", docs.Select(r => JsonConvert.SerializeObject(r)));

            // Will replace variables inside of the user prompt configuration.
            var replacements = new Dictionary<string, string>
            {
                { "Query", query },
                { "PrintableDocs", printableDocs },
                { "PrintableIssues", printableIssues }
            };

            string instructions, userPrompt;
            if (solution)
            {
                instructions = _config.SolutionInstructions;
                userPrompt = AzureSdkIssueLabelerService.FormatTemplate(_config.SolutionUserPrompt, replacements, _logger);
            }
            else
            {
                instructions = _config.SuggestionInstructions;
                userPrompt = AzureSdkIssueLabelerService.FormatTemplate( _config.SuggestionUserPrompt, replacements, _logger);
            }

            var structure = BinaryData.FromString("""
            {
              "type": "object",
              "properties": {
                "Category": { "type": "string" },
                "Service": { "type": "string" }
              },
              "required": [ "Category", "Service"],
              "additionalProperties": false
            }
            """);

            var result = await _ragService.SendMessageQnaAsync(instructions, userPrompt, modelName, structure);
            var output = JsonConvert.DeserializeObject<LabelOutput>(result);

            if (string.IsNullOrEmpty(result))
            {
                throw new Exception($"Open AI Response for {issue.RepositoryName} using the Open AI Labeler for issue #{issue.IssueNumber} had an empty response.");
            }


            _logger.LogInformation($"Open AI Response for {issue.RepositoryName} using the Open AI Labeler for issue #{issue.IssueNumber} Service: {output.Service} Category: {output.Category}");

            return [output.Service, output.Category];
        }

        private class LabelOutput
        {
            public string Category { get; set; }
            public string Service { get; set; }
        }
    }
}
