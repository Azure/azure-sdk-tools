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

    }
}