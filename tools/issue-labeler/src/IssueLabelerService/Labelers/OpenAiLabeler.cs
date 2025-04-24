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

            // Search for issues and documents
            var issues = await _ragService.SearchIssuesAsync(issueIndexName, issueSemanticName, issueFieldName, query, top, scoreThreshold);
            var docs = await _ragService.SearchDocumentsAsync(documentIndexName, documentSemanticName, documentFieldName, query, top, scoreThreshold);

            // Filtered out all sources for either one then not enough information to answer the issue. 
            if (docs.Count == 0 || issues.Count == 0)
            {
                throw new InvalidDataException($"Not enough relevant sources found for {issue.RepositoryName} using the Open AI Labeler for issue #{issue.IssueNumber}.");
            }

            double highestScore = _ragService.GetHighestScore(issues, docs, issue.RepositoryName, issue.IssueNumber);

            _logger.LogInformation($"Highest relevance score among the sources: {highestScore}");

            // Format issues 
            var printableIssues = string.Join("\n\n", issues.Select(issue =>
                $"Title: {issue.Title}\nDescription: {issue.chunk}\nService: {issue.Service}\nScore: {issue.Score}"));

            // Format documents 
            var printableDocs = string.Join("\n\n", docs.Select(doc =>
                $"Content: {doc.chunk}\nService: {doc.Service}\nScore: {doc.Score}"));

            // Get labels for this repository
            var labels = await GetLabelsAsync(issue.RepositoryName);
            var categoryLabels = GetCategoryLabelsForPrompt(labels, issue.RepositoryName);

            // Will replace variables inside of the user prompt configuration.
            var replacements = new Dictionary<string, string>
            {
                { "Title", issue.Title },
                { "Description", issue.Body },
                { "PrintableDocs", printableDocs },
                { "PrintableIssues", printableIssues },
                { "PrintableLabels", categoryLabels }
            };

            string instructions = _config.LabelInstructions;
            string userPrompt = AzureSdkIssueLabelerService.FormatTemplate(_config.LabelUserPrompt, replacements, _logger);

            var structure = BuildSearchStructure();

            var result = await _ragService.SendMessageQnaAsync(instructions, userPrompt, modelName, structure);
            var output = JsonConvert.DeserializeObject<Dictionary<string, string>>(result);

            if (string.IsNullOrEmpty(result))
            {
                throw new InvalidDataException($"Open AI Response for {issue.RepositoryName} using the Open AI Labeler for issue #{issue.IssueNumber} had an empty response.");
            }

            // Filter the output to exclude keys containing "ConfidenceScore"
            var filteredOutput = output
                .Where(kv => !kv.Key.Contains("ConfidenceScore", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            if(!ValidateLabels(labels, filteredOutput))
            {
                throw new InvalidDataException($"Open AI Response for {issue.RepositoryName} using the Open AI Labeler for issue #{issue.IssueNumber} provided invalid labels: {string.Join(", ", filteredOutput.Select(kv => $"{kv.Key}: {kv.Value}"))}");
            }

            foreach (var label in filteredOutput)
            {
                try
                {
                    var confidence = double.Parse(output[$"{label.Key}ConfidenceScore"]);
                    if(label.Value == "UNKNOWN")
                    {
                        throw new InvalidDataException($"Open AI Response for {issue.RepositoryName} using the Open AI Labeler for issue #{issue.IssueNumber} provided an UNKNOWN label.");
                    }
                    if (confidence < double.Parse(_config.ConfidenceThreshold))
                    {
                        throw new InvalidDataException($"Open AI Response for {issue.RepositoryName} using the Open AI Labeler for issue #{issue.IssueNumber} Confidence below threshold: {confidence} < {_config.ConfidenceThreshold}.");
                    }
                }
                catch(FormatException)
                {
                    throw new InvalidDataException($"Open AI Response for {issue.RepositoryName} using the Open AI Labeler for issue #{issue.IssueNumber} provided an invalid confidence scoreor config score threshold not setup: {output[$"{label.Key}ConfidenceScore"]}");
                }
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
    }
}
