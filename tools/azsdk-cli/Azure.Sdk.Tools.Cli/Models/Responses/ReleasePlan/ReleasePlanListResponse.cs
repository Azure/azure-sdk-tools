using System.Text;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlan;

namespace Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlanList
{
    /// <summary>
    /// Represents a response containing multiple release plans and the result of a release plan operation
    /// </summary>
    public class ReleasePlanListResponse : ReleasePlanBaseResponse
    {
        [JsonPropertyName("release_plans")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<ReleasePlanDetails>? ReleasePlanDetailsList { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
        protected override string Format()
        {
            var result = new StringBuilder();
            if (ReleasePlanDetailsList != null && ReleasePlanDetailsList.Count > 0)
            {
                result.AppendLine($"Total Release Plans: {ReleasePlanDetailsList.Count}");
                result.AppendLine(new string('-', 40));

                for (int i = 0; i < ReleasePlanDetailsList.Count; i++)
                {
                    var rp = ReleasePlanDetailsList[i];
                    result.AppendLine($"[{i + 1}] Release Plan ID: {rp.ReleasePlanId}");
                    result.AppendLine($"Title: {rp.Title}");
                    result.AppendLine($"Status: {rp.Status}");
                    result.AppendLine($"Owner: {rp.Owner}");
                    result.AppendLine($"SDK Release Month: {rp.SDKReleaseMonth}");
                    result.AppendLine(new string('-', 40));
                }
            }
            else
            {
                result.AppendLine("No release plan details available.");
            }
            return result.ToString();
        }
    }
}
