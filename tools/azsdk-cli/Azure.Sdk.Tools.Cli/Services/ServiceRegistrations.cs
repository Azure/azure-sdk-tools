// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Azure;
using ModelContextProtocol.Server;
using Azure.AI.OpenAI;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Microagents;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Tools;
using Azure.Sdk.Tools.Cli.Languages.Test;
using Azure.Sdk.Tools.Cli.Services.Languages.Test;

namespace Azure.Sdk.Tools.Cli.Services
{
    public static class ServiceRegistrations
    {
        /// <summary>
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

            // Language Check Services (Composition-based)
            services.AddSingleton<ILanguageChecks, LanguageChecks>();
            services.AddSingleton<ILanguageSpecificChecks, PythonLanguageSpecificChecks>();
            services.AddSingleton<ILanguageSpecificChecks, JavaLanguageSpecificChecks>();
            services.AddSingleton<ILanguageSpecificChecks, JavaScriptLanguageSpecificChecks>();
            services.AddSingleton<ILanguageSpecificChecks, DotNetLanguageSpecificChecks>();
            services.AddSingleton<ILanguageSpecificChecks, GoLanguageSpecificChecks>();
            services.AddSingleton<ILanguageSpecificCheckResolver, LanguageSpecificCheckResolver>();

            services.AddSingleton<ITestRunnerResolver, TestRunnerResolver>();
            services.AddSingleton<ITestRunner, JavaScriptTestRunner>();
            // TODO: test runners for other languages.

            // Helper classes
            services.AddSingleton<ILogAnalysisHelper, LogAnalysisHelper>();
            services.AddSingleton<IGitHelper, GitHelper>();
            services.AddSingleton<ITestHelper, TestHelper>();
            services.AddSingleton<ITypeSpecHelper, TypeSpecHelper>();
            services.AddSingleton<ISpecPullRequestHelper, SpecPullRequestHelper>();
            services.AddSingleton<IUserHelper, UserHelper>();
            services.AddSingleton<ICodeownersHelper, CodeownersHelper>();
            services.AddSingleton<ICodeownersValidatorHelper, CodeownersValidatorHelper>();
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

        public static void RegisterInstrumentedMcpTools(IServiceCollection services, string[] args)
        {
            JsonSerializerOptions? serializerOptions = null;
            var toolTypes = SharedOptions.GetFilteredToolTypes(args);

            foreach (var toolType in toolTypes)
            {
                if (toolType is null)
                {
                    continue;
                }

                foreach (var toolMethod in toolType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
                {
                    if (toolMethod.GetCustomAttribute<McpServerToolAttribute>() is not null)
                    {
                        services.AddSingleton((Func<IServiceProvider, McpServerTool>)(services =>
                        {
                            var options = new McpServerToolCreateOptions { Services = services, SerializerOptions = serializerOptions };
                            var innerTool = toolMethod.IsStatic
                                ? McpServerTool.Create(toolMethod, options: options)
                                : McpServerTool.Create(toolMethod, r => ActivatorUtilities.CreateInstance(r.Services, toolType), options);

                            var loggerFactory = services.GetRequiredService<ILoggerFactory>();
                            var logger = loggerFactory.CreateLogger(toolType);
                            return new InstrumentedTool(logger, innerTool, toolMethod.Name);
                        }));
                    }
                }
            }
        }
    }
}
