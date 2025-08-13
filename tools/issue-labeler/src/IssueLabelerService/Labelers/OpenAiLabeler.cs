using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using IssueLabeler.Shared;
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using Azure.Storage.Blobs;
using Newtonsoft.Json;

namespace IssueLabelerService
{
    public class OpenAiLabeler : ILabeler
    {
        private ILogger<LabelerFactory> _logger;
        private RepositoryConfiguration _config;
        private TriageRag _ragService;
        private BlobServiceClient _blobClient;

        public OpenAiLabeler(ILogger<LabelerFactory> logger, RepositoryConfiguration config, TriageRag ragService, BlobServiceClient blobClient) =>
            (_logger, _config, _ragService, _blobClient) = (logger, config, ragService, blobClient);

        public async Task<Dictionary<string, string>> PredictLabels(IssuePayload issue)
        {
            var modelName = _config.LabelModelName;
            double solutionThreshold = double.Parse(_config.SolutionThreshold);

            var searchContentResults = await GetSearchContentResults(issue);

            var labels = await GetLabelsAsync(issue.RepositoryName);

            var categoryLabels = GetCategoryLabelsForPrompt(labels, issue.RepositoryName);
            var printableContent = string.Join("\n\n", searchContentResults.Select(searchContent =>
                $"Title: {searchContent.Title}\nDescription: {searchContent.Chunk}\nURL: {searchContent.Url}\nScore: {searchContent.Score}"));
            var userPrompt = FormatUserPrompt(issue, categoryLabels, printableContent);

            var structure = BuildSearchStructure();
            var result = await _ragService.SendMessageQnaAsync(_config.LabelInstructions, userPrompt, modelName, null, structure);
        
            if (string.IsNullOrEmpty(result))
            {
                _logger.LogInformation($"Open AI Response for {issue.RepositoryName} using the Open AI Labeler for issue #{issue.IssueNumber} had an empty response.");
                return new Dictionary<string, string>();
            }

            var output = JsonConvert.DeserializeObject<Dictionary<string, string>>(result);

            // Filter the output to exclude keys containing "ConfidenceScore"
            var filteredOutput = output
                .Where(kv => !kv.Key.Contains("ConfidenceScore", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            if (!ValidateLabels(labels, filteredOutput))
            {
                _logger.LogInformation($"Open AI Response for {issue.RepositoryName} using the Open AI Labeler for issue #{issue.IssueNumber} provided invalid labels: {string.Join(", ", filteredOutput.Select(kv => $"{kv.Key}: {kv.Value}"))}");
                return new Dictionary<string, string>();
            }

            if (!ValidateConfidenceScores(filteredOutput, output, issue))
            {
                _logger.LogInformation($"Open AI Response for {issue.RepositoryName} using the Open AI Labeler for issue #{issue.IssueNumber} provided invalid confidence scores.");
                return new Dictionary<string, string>();
            }

            _logger.LogInformation($"Open AI Response for {issue.RepositoryName} using the Open AI Labeler for issue #{issue.IssueNumber}: {string.Join(", ", filteredOutput.Select(kv => $"{kv.Key}: {kv.Value}"))}");

            return filteredOutput;
        }

        private BinaryData BuildSearchStructure()
        {
            // Dynamically generate the JSON schema based on the configured labels
            var labelKeys = _config.LabelNames.Split(';', StringSplitOptions.RemoveEmptyEntries);
            var properties = string.Join(", ", labelKeys.Select(key => $"\"{key}\": {{ \"type\": \"string\" }}"));
            var scores = string.Join(", ", labelKeys.Select(key => $"\"{key}ConfidenceScore\": {{ \"type\": \"string\" }}"));
            var required = string.Join(", ", labelKeys.Select(key => $"\"{key}\""));

            return BinaryData.FromString($$"""
            {
              "type": "object",
              "properties": {
                {{properties}},{{scores}}
              },
              "required": [ {{required}} ],
              "additionalProperties": false
            }
            """);
        }

        private async Task<IEnumerable<Label>> GetLabelsAsync(string repositoryName)
        {
            // Initialize BlobServiceClient
            var containerClient = _blobClient.GetBlobContainerClient("labels");

            // Get the blob client for the specific repository
            var blobClient = containerClient.GetBlobClient(repositoryName);

            // Check if the blob exists
            if (!await blobClient.ExistsAsync())
            {
                throw new FileNotFoundException($"Blob for repository '{repositoryName}' not found.");
            }

            // Download the blob content
            var response = await blobClient.DownloadContentAsync();
            var labelsJson = response.Value.Content.ToString();

            // Deserialize the JSON into a list of labels
            var labels = JsonConvert.DeserializeObject<IEnumerable<Label>>(labelsJson);

            if (labels == null)
            {
                throw new InvalidOperationException("Failed to deserialize labels from blob.");
            }

            return labels;
        }

        private string GetCategoryLabelsForPrompt(IEnumerable<Label> labels, string repositoryName)
        {
            // Filter for category labels (color = e99695)
            var categoryLabels = labels
                .Where(label => string.Equals(label.Color, "e99695", StringComparison.OrdinalIgnoreCase))
                .Select(label => label.Name)
                .ToList();

            return "['" + string.Join("', '", categoryLabels) + "'']";
        }

        private bool ValidateLabels(IEnumerable<Label> labels, Dictionary<string, string> labelsToValidate)
        {
            // Get all label names
            var labelNames = labels.Select(label => label.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Validate that all values in the dictionary exist in the label names
            return labelsToValidate.Values.All(label => labelNames.Contains(label));
        }

        private async Task<List<IndexContent>> GetSearchContentResults(IssuePayload issue)
        {
            var indexName = _config.IndexName;
            var semanticName = _config.SemanticName;
            var query = $"{issue.Title} {issue.Body}";
            var top = int.Parse(_config.SourceCount);
            var scoreThreshold = double.Parse(_config.ScoreThreshold);
            var fieldName = _config.IssueIndexFieldName;

            _logger.LogInformation($"Searching content index '{indexName}' with query: {query}");
            var searchContentResults = await _ragService.IssueTriageContentIndexAsync(
                indexName,
                semanticName,
                fieldName,
                query,
                top,
                scoreThreshold
            );

            // If no results are found, throw an exception
            if (searchContentResults.Count == 0)
            {
                throw new InvalidDataException($"Not enough relevant sources found for {issue.RepositoryName} using the Open AI Labeler for issue #{issue.IssueNumber}.");
            }

            _logger.LogInformation($"Found {searchContentResults.Count} issues with score >= {scoreThreshold}");

            return searchContentResults;
        }

        private string FormatUserPrompt(IssuePayload issue, string categoryLabels, string printableContent)
        {
            var replacements = new Dictionary<string, string>
            {
                { "Title", issue.Title },
                { "Description", issue.Body },
                { "PrintableLabels", categoryLabels },
                { "PrintableContent", printableContent }
            };
            var userPrompt = AzureSdkIssueLabelerService.FormatTemplate(_config.LabelPrompt, replacements, _logger);

            return userPrompt;
        }

        private bool ValidateConfidenceScores(Dictionary<string, string> filteredOutput, Dictionary<string, string> output, IssuePayload issue)
        {

            var confidenceThreshold = double.Parse(_config.ConfidenceThreshold);

            foreach (var label in filteredOutput)
            {
                try
                {
                    if (!output.ContainsKey($"{label.Key}ConfidenceScore"))
                    {
                        _logger.LogWarning($"Open AI Response for {issue.RepositoryName} using the Open AI Labeler for issue #{issue.IssueNumber} did not provide a confidence score for label '{label.Key}'.");
                        return false;
                    }

                    var confidence = double.Parse(output[$"{label.Key}ConfidenceScore"]);

                    if (label.Value == "UNKNOWN")
                    {
                        _logger.LogWarning($"Open AI Response for {issue.RepositoryName} using the Open AI Labeler for issue #{issue.IssueNumber} provided an UNKNOWN label.");
                        return false;
                    }

                    if (confidence < confidenceThreshold)
                    {
                        _logger.LogWarning($"Open AI Response for {issue.RepositoryName} using the Open AI Labeler for issue #{issue.IssueNumber} Confidence below threshold: {confidence} < {_config.ConfidenceThreshold}.");
                        return false;
                    }
                }
                catch (FormatException)
                {
                    _logger.LogWarning($"Open AI Response for {issue.RepositoryName} using the Open AI Labeler for issue #{issue.IssueNumber} provided an invalid confidence score or config score threshold not setup: {output[$"{label.Key}ConfidenceScore"]}");
                    return false;
                }
            }
            return true;
        }
    }
}