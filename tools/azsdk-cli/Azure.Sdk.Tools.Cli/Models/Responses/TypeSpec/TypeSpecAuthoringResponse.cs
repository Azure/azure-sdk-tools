using System.Text;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Models.AiCompletion;
using Azure.Sdk.Tools.Cli.Tools.TypeSpec;

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

        [JsonPropertyName("query_intension")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public QueryIntension? QueryIntension { get; set; }

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

            if (QueryIntension != null)
            {
                result.AppendLine($"\n**Query Analysis:**");
                result.AppendLine($"- Category: {QueryIntension.Category}");
                if (!string.IsNullOrEmpty(QueryIntension.SpecType))
                {
                    result.AppendLine($"- Spec Type: {QueryIntension.SpecType}");
                }
                if (!string.IsNullOrEmpty(QueryIntension.Scope))
                {
                    result.AppendLine($"- Scope: {QueryIntension.Scope}");
                }
            }

            return result.ToString();
        }
    }
}
