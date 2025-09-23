using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;

namespace Azure.Sdk.Tools.Cli.Models.AiCompletion
{
    public class CompletionRequest
    {
        [JsonPropertyName("tenant_id")]
        public TenantId TenantId { get; set; } = TenantId.AzureSDKQaBot;

        [JsonPropertyName("message")]
        [Required]
        public Message Message { get; set; } = new();
    }
}
