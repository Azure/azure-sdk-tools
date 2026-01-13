// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Microagents;

namespace Azure.Sdk.Tools.Cli.Tools.Telemetry
{
    public class ConversationAnalysisPromptTemplate : PromptTemplateBase
    {
        public override string BuildPrompt()
        {
            return """
# Conversation Analysis Task

## Context
You are analyzing a user's conversation session to extract meaningful telemetry data for Azure SDK tools.

## Privacy & Ethics
- Focus only on technical content and usage patterns
- Exclude any personally identifiable information
- Redact sensitive data (API keys, secrets, personal names)
- Maintain user privacy while extracting valuable insights

## Analysis Instructions

### Conversation Content:
```
{{conversation_content}}
```

### Analysis Requirements:
1. **Topic Extraction**: Identify the main technical topic or activity
2. **Intent Recognition**: What was the user trying to accomplish?
3. **Context Classification**: What type of development activity occurred?
4. **Tag Generation**: Generate 3-5 relevant tags for categorization
5. **Confidence Assessment**: Rate the analysis quality (0-100)

### Output Format:
Return a JSON object with this exact structure:
```json
{
  "Topic": "concise topic title (2-8 words)",
  "Summary": "brief description of the conversation (1-2 sentences)",
  "Tags": ["tag1", "tag2", "tag3"],
  "Category": "one of: Development|Troubleshooting|Configuration|Documentation|API_Usage|Performance|Security|Other",
  "ConfidenceScore": 85
}
```

### Examples:

**Example 1:**
Conversation: "I'm having trouble with the Azure Storage SDK authentication..."
```json
{
  "Topic": "Azure Storage Authentication Issue",
  "Summary": "User experiencing authentication problems with Azure Storage SDK.",
  "Tags": ["azure-storage", "authentication", "troubleshooting", "sdk"],
  "Category": "Troubleshooting",
  "ConfidenceScore": 90
}
```

**Example 2:** 
Conversation: "How do I generate Python SDK from OpenAPI spec..."
```json
{
  "Topic": "Python SDK Generation from OpenAPI",
  "Summary": "User learning how to generate Python SDK code from OpenAPI specifications.",
  "Tags": ["python", "openapi", "code-generation", "sdk-development"],
  "Category": "Development", 
  "ConfidenceScore": 95
}
```

Now analyze the provided conversation content.
""";
        }
    }
}