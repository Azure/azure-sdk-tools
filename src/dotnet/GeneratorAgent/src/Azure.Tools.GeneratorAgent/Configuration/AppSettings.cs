using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Azure.Tools.GeneratorAgent.Configuration
{
    internal class AppSettings
    {
        private readonly IConfiguration Configuration;
        private readonly ILogger<AppSettings> Logger;

        public AppSettings(IConfiguration configuration, ILogger<AppSettings> logger)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            ArgumentNullException.ThrowIfNull(logger);
            
            Configuration = configuration;
            Logger = logger;
        }

        // Azure AI Settings
        public string ProjectEndpoint => GetRequiredSetting("AzureSettings:ProjectEndpoint");
        public string Model => Configuration.GetSection("AzureSettings:Model").Value ?? "gpt-4o";
        public string AgentName => Configuration.GetSection("AzureSettings:AgentName").Value ?? "AZC Fixer";
        public int MaxIterations => Configuration.GetSection("AzureSettings:MaxIterations").Get<int>();
        public string AgentInstructions => Configuration.GetSection("AzureSettings:AgentInstructions").Value ?? "";
        public string ErrorAnalysisInstructions => Configuration.GetSection("AzureSettings:ErrorAnalysisInstructions").Value ?? "You are an expert at analyzing compilation errors. Return JSON object with 'errors' array containing objects with 'type' and 'message' fields.";
        public string FixPromptTemplate => Configuration.GetSection("AzureSettings:FixPromptTemplate").Value ?? "";

        // Timeout and polling configurations
        public TimeSpan IndexingMaxWaitTime => TimeSpan.FromSeconds(
            int.Parse(Configuration.GetSection("AzureSettings:IndexingMaxWaitTimeSeconds").Value ?? "180"));
        public TimeSpan IndexingPollingInterval => TimeSpan.FromSeconds(
            int.Parse(Configuration.GetSection("AzureSettings:IndexingPollingIntervalSeconds").Value ?? "5"));
        
        // Concurrency settings
        public int MaxConcurrentUploads => 
            int.Parse(Configuration.GetSection("AzureSettings:MaxConcurrentUploads").Value ?? "10");
        
        // Indexing batch processing settings
        public int IndexingStatusBatchSize => 
            int.Parse(Configuration.GetSection("AzureSettings:IndexingStatusBatchSize").Value ?? "10");
        public int MaxPendingFilesToShowInDebug => 
            int.Parse(Configuration.GetSection("AzureSettings:MaxPendingFilesToShowInDebug").Value ?? "3");
        
        // Vector store settings
        public TimeSpan VectorStoreReadyWaitTime => TimeSpan.FromMilliseconds(
            int.Parse(Configuration.GetSection("AzureSettings:VectorStoreReadyWaitTimeMs").Value ?? "5000"));
        

        // Agent run settings
        public TimeSpan AgentRunMaxWaitTime =>
            TimeSpan.FromSeconds(int.Parse(Configuration.GetSection("AzureSettings:AgentRunMaxWaitTimeSeconds").Value ?? "600"));
        public TimeSpan AgentRunPollingInterval =>
            TimeSpan.FromSeconds(int.Parse(Configuration.GetSection("AzureSettings:AgentRunPollingIntervalSeconds").Value ?? "5"));
        
        // Fix processing settings
        public int DelayBetweenFixesMs => 
            int.Parse(Configuration.GetSection("AzureSettings:DelayBetweenFixesMs").Value ?? "500");
        

        // OpenAI Settings
        public string? OpenAIApiKey => Configuration.GetSection("OpenAI:ApiKey").Value ?? EnvironmentVariables.OpenAIApiKey;
        public string? OpenAIEndpoint => Configuration.GetSection("OpenAI:Endpoint").Value;
        public string OpenAIModel => Configuration.GetSection("OpenAI:Model").Value ?? "gpt-4o";
        
        public string TypespecEmitterPackage => Configuration.GetSection("AzureSettings:TypespecEmitterPackage").Value ?? "@typespec/http-client-csharp";
        public string TypespecCompiler => Configuration.GetSection("AzureSettings:TypespecCompiler").Value ?? "@typespec/compiler";
        public string TypeSpecDirectoryName => "@typespec";
        public string HttpClientCSharpDirectoryName => "http-client-csharp";

        public string AzureSpecRepository => "Azure/azure-rest-api-specs";
        public string AzureSdkDirectoryName => "azure-sdk-for-net";

        // Script Paths
        public string PowerShellScriptPath => "eng/scripts/automation/Invoke-TypeSpecDataPlaneGenerateSDKPackage.ps1";

        // GitHub Settings
        public string? GitHubToken => Configuration.GetSection("GitHubSettings:Token").Value;

        private string GetRequiredSetting(string key)
        {
            string? value = Configuration.GetSection(key).Value;
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"Required configuration setting '{key}' is missing or empty");
            }
            return value;
        }
    }
}

