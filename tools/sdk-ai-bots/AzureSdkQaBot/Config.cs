namespace AzureSdkQaBot
{
    public class ConfigOptions
    {
        public string? BOT_ID { get; set; }
        public string? BOT_PASSWORD { get; set; }
        public string? GITHUB_TOKEN { get; set; }
        public string? KeyVaultUrl { get; set; }
        public string? CertificateName { get; set; }
        public OpenAIConfigOptions? OpenAI { get; set; }
        public AzureConfigOptions? Azure { get; set; }
        public CognitiveSearchOptions? Search { get; set; }
    }

    /// <summary>
    /// Options for Open AI
    /// </summary>
    public class OpenAIConfigOptions
    {
        public string? ApiKey { get; set; }
    }

    /// <summary>
    /// Options for Azure OpenAI and Azure Content Safety
    /// </summary>
    public class AzureConfigOptions
    {
        public required string OpenAIApiKey { get; set; }
        public required string OpenAIEndpoint { get; set; }
        public required string EmbeddingModelDeploymentName { get; set; }
        public required string ChatModelDeploymentName { get; set; }
        public string? ContentSafetyApiKey { get; set; }
        public string? ContentSafetyEndpoint { get; set; }
    }

    /// <summary>
    /// Options for Cognitive Search
    /// </summary>
    public class CognitiveSearchOptions
    {
        public required string SearchServiceUrl { get; set; }
        public required string SearchServiceApiKey { get; set; }
    }
}
