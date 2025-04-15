using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using IssueLabeler.Shared;
using Microsoft.Extensions.Logging;

namespace IssueLabelerService
{
    public class PromptLabeler : ILabeler
    {
        private readonly ILogger<LabelerFactory> _logger;
        private readonly TriageRag _ragService;
        private RepositoryConfiguration _config;


        public PromptLabeler(ILogger<LabelerFactory> logger, TriageRag ragService, RepositoryConfiguration config)
        {
            _logger = logger;
            _ragService = ragService;
            _config = config;
        }

        public async Task<string[]> PredictLabels(IssuePayload issue)
        {
            _logger.LogInformation($"Predicting Labels using Prompt Labeler for issue #{issue.IssueNumber}");

            BinaryData structure = BinaryData.FromString("""
                {
                  "type": "object",
                  "properties": {
                    "Service": { "type": "string" },
                    "ServiceConfidenceScore": { "type": "string" },
                    "Type": { "type": "string" },
                    "TypeConfidenceScore": { "type": "string" }
                  },
                  "required": [ "Service", "ServiceConfidenceScore", "Type", "TypeConfidenceScore" ],
                  "additionalProperties": false
                }
                """);

            // Format the prompt with issue title and description
            string formattedPrompt = AzureSdkIssueLabelerService.FormatTemplate(
                _config.LabelUserPrompt,
                new Dictionary<string, string>
                {
                    { "Title", issue.Title },
                    { "Description", issue.Body }
                },
                _logger);

            // Call the AI model with the prompt through the RAG service
            var result = await _ragService.SendMessageQnaAsync(
                _config.LabelInstructions,
                formattedPrompt,
                _config.LabelModelName,
                structure);

            _logger.LogInformation($"AI model response: {result}");

            // Deserialize the result
            var output = JsonSerializer.Deserialize<LabelingOutput>(result);

            if (output == null)
            {
                throw new Exception($"Failed to parse AI model response for {issue.RepositoryName} using the Prompt Labeler for issue #{issue.IssueNumber}");
            }

            // Validate Type
            if (IsConfidenceScoreAboveThreshold(output.TypeConfidenceScore, _config.ScoreThreshold) || IsValueUnknown(output.Type))
            {
                throw new Exception($"Invalid AI model Category response for {issue.RepositoryName} using the Prompt Labeler for issue #{issue.IssueNumber}: {result}");
            }

            // Validate Service
            if (IsConfidenceScoreAboveThreshold(output.ServiceConfidenceScore, _config.ScoreThreshold) || IsValueUnknown(output.Service))
            {
                throw new Exception($"Invalid AI model Service response for {issue.RepositoryName} using the Prompt Labeler for issue #{issue.IssueNumber}");
            }

            // Return the predicted labels
            return [output.Service, output.Type];
        }

        private bool IsConfidenceScoreAboveThreshold(string confidenceScore, string threshold) =>
                !string.IsNullOrEmpty(confidenceScore) && double.Parse(confidenceScore) >= double.Parse(threshold) * 100;
        private bool IsValueUnknown(string value) => !string.IsNullOrEmpty(value) && value == "UNKNOWN";


        public class LabelingOutput
        {
            public string Service { get; set; }
            public string ServiceConfidenceScore { get; set; }
            public string Type { get; set; }
            public string TypeConfidenceScore { get; set; }
        }
    }
}
