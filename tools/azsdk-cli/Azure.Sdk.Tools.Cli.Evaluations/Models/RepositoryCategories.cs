namespace Azure.Sdk.Tools.Cli.Evaluations.Models
{
    /// <summary>
    /// Defines repository category constants for test filtering.
    /// Use these with NUnit's [Category] attribute to specify which repositories a test should run for.
    /// </summary>
    public static class RepositoryCategories
    {
        public const string AzureRestApiSpecs = "azure-rest-api-specs";
        public const string AzureSdkForNet = "azure-sdk-for-net";
        public const string AzureSdkForPython = "azure-sdk-for-python";
        public const string AzureSdkForJava = "azure-sdk-for-java";
        public const string AzureSdkForJs = "azure-sdk-for-js";
        public const string AzureSdkForGo = "azure-sdk-for-go";
    }
}