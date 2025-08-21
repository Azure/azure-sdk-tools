// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.AI.OpenAI;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Microagents;
using Microsoft.Extensions.Azure;

namespace Azure.Sdk.Tools.Cli.Services
{
    public static class ServiceRegistrations
    {    /// <summary>
         /// This is the function that defines all of the services available to any of the MCPTool instantiations. This
         /// same collection modification is run within the HostServerTool::CreateAppBuilder.
         /// </summary>
         /// <param name="services"></param>
         /// todo: make this use reflection to populate itself with all of our services and helpers
        public static void RegisterCommonServices(IServiceCollection services)
        {
            // Services
            services.AddSingleton<IAzureService, AzureService>();
            services.AddSingleton<IAzureAgentServiceFactory, AzureAgentServiceFactory>();
            services.AddSingleton<IDevOpsConnection, DevOpsConnection>();
            services.AddSingleton<IDevOpsService, DevOpsService>();
            services.AddSingleton<IGitHubService, GitHubService>();
            services.AddSingleton<ILanguageRepoServiceFactory, LanguageRepoServiceFactory>();

            // Language Services
            services.AddSingleton<LanguageRepoService>();
            services.AddSingleton<PythonLanguageRepoService>();
            services.AddSingleton<JavaScriptLanguageRepoService>();
            services.AddSingleton<DotNetLanguageRepoService>();
            services.AddSingleton<GoLanguageRepoService>();
            services.AddSingleton<JavaLanguageRepoService>();

            // Update language services
            // Register concrete update implementations so ActivatorUtilities can create them with DI
            services.AddSingleton<Update.JavaUpdateLanguageService>();
            // services.AddSingleton<Update.PythonUpdateLanguageService>();
            // services.AddSingleton<Update.JavaScriptUpdateLanguageService>();
            // services.AddSingleton<Update.DotNetUpdateLanguageService>();
            // services.AddSingleton<Update.GoUpdateLanguageService>();

            // Self-contained Func that detects repo language and returns the correct IUpdateLanguageService
            services.AddSingleton<Func<string, Update.IUpdateLanguageService>>(sp => (packagePath) =>
            {
                var repoService = sp.GetRequiredService<ILanguageRepoServiceFactory>().GetService(packagePath);
                return repoService.CreateUpdateService(sp);
            });
            // concrete update implementations - register each concrete UpdateLanguageService so
            // the factory can resolve them via DI when creating per-repo instances.
            // Example registrations (copy-paste ready):
            services.AddSingleton<Update.JavaUpdateLanguageService>();
            // services.AddSingleton<Update.PythonUpdateLanguageService>();
            // services.AddSingleton<Update.JavaScriptUpdateLanguageService>();
            // services.AddSingleton<Update.DotNetUpdateLanguageService>();
            // services.AddSingleton<Update.GoUpdateLanguageService>();
           
            // Helper classes
            services.AddSingleton<ILogAnalysisHelper, LogAnalysisHelper>();
            services.AddSingleton<IGitHelper, GitHelper>();
            services.AddSingleton<ITestHelper, TestHelper>();
            services.AddSingleton<ITypeSpecHelper, TypeSpecHelper>();
            services.AddSingleton<ISpecPullRequestHelper, SpecPullRequestHelper>();
            services.AddSingleton<IUserHelper, UserHelper>();
            services.AddSingleton<IEnvironmentHelper, EnvironmentHelper>();

            // Process Helper Classes
            services.AddSingleton<INpxHelper, NpxHelper>();
            services.AddSingleton<IPowershellHelper, PowershellHelper>();
            services.AddSingleton<IProcessHelper, ProcessHelper>();

            services.AddSingleton<IMicroagentHostService, MicroagentHostService>();

            services.AddAzureClients(clientBuilder =>
            {
                // For more information about this pattern: https://learn.microsoft.com/en-us/dotnet/azure/sdk/dependency-injection
                var service = new AzureService();
                clientBuilder.UseCredential(service.GetCredential());

                // Azure OpenAI client does not, for some reason, have an
                // in-package facade for this, so register manually.
                clientBuilder.AddClient<AzureOpenAIClient, AzureOpenAIClientOptions>(
                    (options, credential, _) =>
                    {
                        var endpointEnvVar = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
                        var ep = string.IsNullOrWhiteSpace(endpointEnvVar) ?
                            "https://openai-shared.openai.azure.com"
                            : endpointEnvVar;

                        return new AzureOpenAIClient(new Uri(ep), credential, options);
                    });
            });
        }
    }
}
