namespace Azure.Tools.GeneratorAgent
{

    internal static class EnvironmentVariables
    {

        public const string Prefix = "AZURE_GENERATOR_";
        public const string GitHubActions = "GITHUB_ACTIONS";
        public const string GitHubWorkflow = "GITHUB_WORKFLOW";
        public static readonly string EnvironmentName = $"{Prefix}ENVIRONMENT";
        public static readonly string AzureTenantId = $"{Prefix}TENANT_ID";
        public static readonly string AzureAuthorityHost = $"{Prefix}AUTHORITY_HOST";
        
        /// <summary>
        /// Gets the GitHub token from environment variables.
        /// Checks AZURE_GENERATOR_GITHUB_TOKEN first, then falls back to GITHUB_TOKEN.
        /// </summary>
        public static string? GitHubToken => Environment.GetEnvironmentVariable($"{Prefix}GITHUB_TOKEN") 
                                           ?? Environment.GetEnvironmentVariable("GITHUB_TOKEN");

        /// <summary>
        /// Gets the OpenAI API key from environment variables.
        /// Checks AZURE_GENERATOR_OPENAI_API_KEY first, then falls back to OPENAI_API_KEY.
        /// </summary>
        public static string? OpenAIApiKey => Environment.GetEnvironmentVariable($"{Prefix}OPENAI_API_KEY") 
                                            ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");

    }
}