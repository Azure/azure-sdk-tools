using System.Text;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Models.AzureSdkKnowledgeAICompletion;


namespace Azure.Sdk.Tools.Cli.Models.Responses.TypeSpec
{
    public class TypeSpecAuthoringResponse : CommandResponse
    {
        [JsonPropertyName("is_successful")]
        public bool IsSuccessful { get; set; }

        [JsonPropertyName("solution")]
        public string Solution { get; set; } = string.Empty;

        [JsonPropertyName("references")]
        public List<DocumentReference> References { get; set; } = new();

        [JsonPropertyName("full_context")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? FullContext { get; set; }

        [JsonPropertyName("reasoning_progress")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ReasoningProgress { get; set; }

        [JsonPropertyName("query_intention")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public QueryIntention? QueryIntention { get; set; }

        protected override string Format()
        {
            if (!IsSuccessful || !string.IsNullOrEmpty(ResponseError))
            {
                return string.Empty;
            }
            var result = new StringBuilder();
            result.AppendLine($"**Solution:** {Solution}");

            if (References.Any())
            {
                result.AppendLine("\n**References:**");
                foreach (var reference in References)
                {
                    result.AppendLine($"- **{reference.Title}** ({reference.Source})");
                    result.AppendLine($"  {reference.Link}");
                    if (!string.IsNullOrEmpty(reference.Snippet))
                    {
                        result.AppendLine($"  Snippet: {reference.Snippet}");
                    }
                    result.AppendLine();
                }
            }

            if (QueryIntention != null)
            {
                result.AppendLine($"\n**Query Analysis:**");
                result.AppendLine($"- Category: {QueryIntention.Category}");
                if (!string.IsNullOrEmpty(QueryIntention.SpecType))
                {
                    result.AppendLine($"- Spec Type: {QueryIntention.SpecType}");
                }
                if (!string.IsNullOrEmpty(QueryIntention.Scope))
                {
                    result.AppendLine($"- Scope: {QueryIntention.Scope}");
                }
            }

            return result.ToString();
        }
    }
}
