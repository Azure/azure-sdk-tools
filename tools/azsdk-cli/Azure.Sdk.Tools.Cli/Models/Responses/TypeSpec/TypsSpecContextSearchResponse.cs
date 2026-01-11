using System.Text;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Models.AiCompletion;
using Azure.Sdk.Tools.Cli.Models.AzureSDKKnowledgeAICompletion;
using Octokit;

namespace Azure.Sdk.Tools.Cli.Models.Responses.TypeSpec
{
    public class TypsSpecContextSearchResponse : CommandResponse
    {
        [JsonPropertyName("is_successful")]
        public bool IsSuccessful { get; set; }

        //[JsonPropertyName("context")]
        //public List<Knowledge> Contexts { get; set; } = new();
        [JsonPropertyName("context")]
        public string Context { get; set; } = String.Empty;

        [JsonPropertyName("intention")]
        public string Intention { get; set; } = String.Empty;

        protected override string Format()
        {
            if (!IsSuccessful || !string.IsNullOrEmpty(ResponseError))
            {
                return string.Empty;
            }
            var result = new StringBuilder();

            //if (Contexts.Any())
            //{
            //    result.AppendLine("\n**Context:**");
            //    foreach (var context in Contexts)
            //    {
            //        result.AppendLine($"- **{context.Title}** ({reference.Source})");
            //        result.AppendLine($"  {context.Link}");
            //        if (!string.IsNullOrEmpty(context.Content))
            //        {
            //            result.AppendLine($"  Content: {context.Content}");
            //        }
            //        result.AppendLine();
            //    }
            //}

            if (!string.IsNullOrEmpty(Context))
            {
                result.AppendLine("\n**Context:**");
                result.AppendLine(Context);
            }
            if (!string.IsNullOrEmpty(Intention))
            {
                result.AppendLine("\n**Intention:**");
                result.AppendLine(Intention);
            }

            return result.ToString();
        }
    }
}
