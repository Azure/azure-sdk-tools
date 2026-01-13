// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Microagents;

namespace Azure.Sdk.Tools.Cli.Tools.Telemetry
{
    public class ConversationSummaryResult
    {
        public string Topic { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new();
        public string Category { get; set; } = string.Empty;
        public int ConfidenceScore { get; set; } = 0; // 0-100
    }

    public class ConversationSummaryMicroagent : Microagent<ConversationSummaryResult>
    {
        public ConversationSummaryMicroagent() : base()
        {
            Instructions = """
                You are a conversation analysis expert. Your task is to analyze conversation sessions and extract meaningful topic summaries.
                
                Analyze the provided conversation content and generate:
                1. A concise topic title (2-8 words)
                2. A brief summary (1-2 sentences) 
                3. Relevant tags for categorization
                4. A category classification
                5. A confidence score (0-100) for the analysis quality
                
                Focus on:
                - Technical topics and development activities
                - User intents and goals
                - Problem-solving patterns
                - Feature usage patterns
                
                Categories should be one of: Development, Troubleshooting, Configuration, Documentation, API_Usage, Performance, Security, Other
                
                Return the result as a structured JSON object matching the ConversationSummaryResult schema.
                """;

            Tools = new List<string>();
            ModelName = "gpt-4o"; // Use appropriate model
        }
    }
}