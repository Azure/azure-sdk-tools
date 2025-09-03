using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;

namespace Azure.Sdk.Tools.Cli.Models.AiCompletion
{
    public class CompletionRequest
    {
        [JsonPropertyName("tenant_id")]
        public TenantId TenantId { get; set; } = TenantId.AzureSDKQaBot;

        [JsonPropertyName("prompt_template")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [StringLength(5000)]
        public string? PromptTemplate { get; set; }

        [JsonPropertyName("intension_prompt_template")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [StringLength(5000)]
        public string? IntensionPromptTemplate { get; set; }

        [JsonPropertyName("prompt_template_arguments")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [StringLength(2000)]
        public string? PromptTemplateArguments { get; set; }

        [JsonPropertyName("top_k")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [Range(1, 100, ErrorMessage = "TopK must be between 1 and 100")]
        public int? TopK { get; set; }

        [JsonPropertyName("sources")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [MaxLength(20, ErrorMessage = "Maximum 20 sources allowed")]
        public List<Source>? Sources { get; set; }

        [JsonPropertyName("message")]
        [Required]
        public Message Message { get; set; } = new();

        [JsonPropertyName("history")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [MaxLength(50, ErrorMessage = "Maximum 50 history messages allowed")]
        public List<Message>? History { get; set; }

        [JsonPropertyName("with_full_context")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? WithFullContext { get; set; }

        [JsonPropertyName("with_preprocess")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? WithPreprocess { get; set; }

        [JsonPropertyName("additional_infos")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [MaxLength(10, ErrorMessage = "Maximum 10 additional info items allowed")]
        public List<AdditionalInfo>? AdditionalInfos { get; set; }
    }
}
