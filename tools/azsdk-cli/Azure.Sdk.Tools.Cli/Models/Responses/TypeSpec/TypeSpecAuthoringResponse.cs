using System.Text;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Models.AzureSdkKnowledgeAICompletion;


namespace Azure.Sdk.Tools.Cli.Models.Responses.TypeSpec
{
    public class TypeSpecAuthoringResponse : TypeSpecBaseResponse
    {
        [JsonPropertyName("solution")]
        public string Solution { get; set; } = string.Empty;

        [JsonPropertyName("references")]
        public List<DocumentReference> References { get; set; } = new();

        [JsonPropertyName("full_context")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? FullContext { get; set; }

        [JsonPropertyName("reasoning")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Reasoning { get; set; }

        [JsonPropertyName("query_intention")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public QueryIntention? QueryIntention { get; set; }

        protected override string Format()
        {
            if (OperationStatus == Status.Failed)
            {
                return string.Empty;
            }
            var result = new StringBuilder();

            if (!string.IsNullOrEmpty(TypeSpecProject))
            {
                result.AppendLine($"TypeSpec project: {TypeSpecProject}");
            }

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

                if (QueryIntention.QuestionScope != null)
                {
                    result.AppendLine($"- Scope: {QueryIntention.QuestionScope}");
                }
                if (QueryIntention.ServiceType != null)
                {
                    result.AppendLine($"- Service Type: {QueryIntention.ServiceType}");
                }
            }

            return result.ToString();
        }
    }
}
