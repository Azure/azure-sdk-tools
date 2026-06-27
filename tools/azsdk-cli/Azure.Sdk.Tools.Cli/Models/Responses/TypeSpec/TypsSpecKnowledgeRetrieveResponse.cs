using System.Text;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Responses.TypeSpec
{
    public class TypsSpecKnowledgeRetrieveResponse : CommandResponse
    {
        [JsonPropertyName("context")]
        public string Context { get; set; } = String.Empty;

        protected override string Format()
        {
            if (OperationStatus == Status.Failed)
            {
                return string.Empty;
            }
            var result = new StringBuilder();

            if (!string.IsNullOrEmpty(Context))
            {
                result.AppendLine("\n**Context:**");
                result.AppendLine(Context);
            }

            return result.ToString();
        }
    }
}
