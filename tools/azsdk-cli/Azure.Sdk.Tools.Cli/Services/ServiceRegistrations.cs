// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Azure;
using ModelContextProtocol.Server;
using Azure.AI.OpenAI;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Extensions;
using Azure.Sdk.Tools.Cli.Microagents;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services.ClientUpdate;
using Azure.Sdk.Tools.Cli.Telemetry;
using Azure.Sdk.Tools.Cli.Tools;
using Azure.Sdk.Tools.Cli.Services.Tests;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services
{
    public static class ServiceRegistrations
    {
        /// <summary>
        /// This is the function that defines all of the services available to any tool instantiations
        /// </summary>
        /// <param name="services"></param>
        /// todo: make this use reflection to populate itself with all of our services and helpers
        public static void RegisterCommonServices(IServiceCollection services, OutputHelper.OutputModes outputMode)
        {
            // Services
            services.AddSingleton<IAzureService, AzureService>();
            services.AddSingleton<IDevOpsConnection, DevOpsConnection>();
            services.AddSingleton<IDevOpsService, DevOpsService>();
            services.AddSingleton<IGitHubService, GitHubService>();

            // Language Check Services (Composition-based)
            services.AddScoped<ILanguageChecks, LanguageChecks>();
            services.AddLanguageSpecific<ILanguageSpecificChecks>(new LanguageSpecificImplementations
            {
                Python = typeof(PythonLanguageSpecificChecks),
                Java = typeof(JavaLanguageSpecificChecks),
                JavaScript = typeof(JavaScriptLanguageSpecificChecks),
                DotNet = typeof(DotNetLanguageSpecificChecks),
                Go = typeof(GoLanguageSpecificChecks),
            });

            // Client update language services
            services.AddLanguageSpecific<IClientUpdateLanguageService>(new LanguageSpecificImplementations
            {
                Java = typeof(JavaUpdateLanguageService),
                // Future: Python = typeof(PythonUpdateLanguageService), etc
            });

            services.AddLanguageSpecific<ITestRunner>(new LanguageSpecificImplementations
            {
                JavaScript = typeof(JavaScriptTestRunner),
                Python = typeof(PythonTestRunner),
            });

            // Helper classes
            services.AddSingleton<ILogAnalysisHelper, LogAnalysisHelper>();
            services.AddSingleton<IGitHelper, GitHelper>();
            services.AddSingleton<ITestHelper, TestHelper>();
            services.AddSingleton<ITypeSpecHelper, TypeSpecHelper>();
            services.AddSingleton<ISpecPullRequestHelper, SpecPullRequestHelper>();
            services.AddSingleton<IUserHelper, UserHelper>();
            services.AddSingleton<ICodeownersValidatorHelper, CodeownersValidatorHelper>();
            services.AddSingleton<IEnvironmentHelper, EnvironmentHelper>();
            services.AddSingleton<IRawOutputHelper>(_ => new OutputHelper(outputMode));
            services.AddSingleton<ISpecGenSdkConfigHelper, SpecGenSdkConfigHelper>();
            services.AddSingleton<IInputSanitizer, InputSanitizer>();
            services.AddSingleton<ITspClientHelper, TspClientHelper>();

            // Process Helper Classes
            services.AddSingleton<INpxHelper, NpxHelper>();
            services.AddSingleton<IPowershellHelper, PowershellHelper>();
            services.AddSingleton<IProcessHelper, ProcessHelper>();

            // Services that need to be scoped so we can track/update state across services per request
            services.AddScoped<TokenUsageHelper>();
            services.AddScoped<IOutputHelper>(_ => new OutputHelper(outputMode));
            // Services depending on other scoped services
            services.AddScoped<IMicroagentHostService, MicroagentHostService>();
            services.AddScoped<IAzureAgentServiceFactory, AzureAgentServiceFactory>();


            // Telemetry
            services.AddSingleton<ITelemetryService, TelemetryService>();
            services.ConfigureOpenTelemetry();

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

        // Once middleware support is added to the MCP SDK this should be replaced
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
                            var telemetryService = services.GetRequiredService<ITelemetryService>();
                            return new InstrumentedTool(telemetryService, logger, innerTool);
                        }));
                    }
                }
            }
        }
    }
}
