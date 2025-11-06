// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.IO.Enumeration;
using System.Reflection;
using System.Text.Json;
using Azure.AI.OpenAI;
using System.ClientModel;
using Microsoft.Extensions.Azure;
using ModelContextProtocol.Server;
using OpenAI;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Extensions;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Microagents;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Samples;
using Azure.Sdk.Tools.Cli.Services.ClientUpdate;
using Azure.Sdk.Tools.Cli.Services.Tests;
using Azure.Sdk.Tools.Cli.Services.VerifySetup;
using Azure.Sdk.Tools.Cli.Telemetry;
using Azure.Sdk.Tools.Cli.Tools;
using Microsoft.Extensions.Azure;
using ModelContextProtocol.Server;


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
            services.AddLanguageSpecific<ILanguageSpecificChecks>(
                python: typeof(PythonLanguageSpecificChecks),
                java: typeof(JavaLanguageSpecificChecks),
                javascript: typeof(JavaLanguageSpecificChecks),
                dotnet: typeof(DotNetLanguageSpecificChecks),
                go: typeof(GoLanguageSpecificChecks)
            );

            // Client update language services
            services.AddLanguageSpecific<IClientUpdateLanguageService>(
                java: typeof(JavaUpdateLanguageService)
                // Future: python: typeof(PythonUpdateLanguageService), etc
            );

            services.AddLanguageSpecific<IPackageInfoHelper>(
                dotnet: typeof(DotNetPackageInfoHelper),
                java: typeof(JavaPackageInfoHelper),
                python: typeof(PythonPackageInfoHelper),
                javascript: typeof(JavaScriptPackageInfoHelper),
                go: typeof(GoPackageInfoHelper)
            );

            services.AddLanguageSpecific<SampleLanguageContext>(
                dotnet: typeof(DotNetSampleLanguageContext),
                java: typeof(JavaSampleLanguageContext),
                python: typeof(PythonSampleLanguageContext),
                javascript: typeof(TypeScriptSampleLanguageContext),
                go: typeof(GoSampleLanguageContext)
            );

            services.AddLanguageSpecific<ITestRunner>(
                java: typeof(JavaTestRunner),
                javascript: typeof(JavaScriptTestRunner),
                go: typeof(GoLanguageSpecificChecks),
                python: typeof(PythonTestRunner),
                dotnet: typeof(DotNetTestRunner)
            );

            services.AddLanguageSpecific<IEnvRequirementsCheck>(
                python: typeof(PythonRequirementsCheck),
                java: typeof(JavaRequirementsCheck),
                javascript: typeof(JavaScriptRequirementsCheck),
                dotnet: typeof(DotNetRequirementsCheck),
                go: typeof(GoRequirementsCheck)
            );

            // Helper classes
            services.AddSingleton<IFileHelper, FileHelper>();
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
            services.AddScoped<ConversationLogger>();
            // Services depending on other scoped services
            services.AddScoped<IMicroagentHostService, MicroagentHostService>();
            services.AddScoped<IAzureAgentServiceFactory, AzureAgentServiceFactory>();
            services.AddScoped<ICommonValidationHelpers, CommonValidationHelpers>();


            // Telemetry
            services.AddSingleton<ITelemetryService, TelemetryService>();
            services.ConfigureOpenTelemetry();

            services.AddAzureClients(clientBuilder =>
            {
                // For more information about this pattern: https://learn.microsoft.com/en-us/dotnet/azure/sdk/dependency-injection
                var service = new AzureService();
                clientBuilder.UseCredential(service.GetCredential());
            });

            // Register OpenAI client with endpoint and authentication
            services.AddSingleton<OpenAIClient>(sp =>
            {
                var azureService = sp.GetRequiredService<IAzureService>();
                var credential = azureService.GetCredential();

                var openAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
                var openAiBaseUrl = Environment.GetEnvironmentVariable("OPENAI_BASE_URL");
                var azureOpenAiEndpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");

                Uri? endpoint = null;

                // Priority 1: Use OPENAI_BASE_URL if it exists
                if (!string.IsNullOrWhiteSpace(openAiBaseUrl))
                {
                    endpoint = new Uri(openAiBaseUrl);
                }
                // Priority 2: Use AZURE_OPENAI_ENDPOINT with /openai/v1 postfix if it exists
                else if (!string.IsNullOrWhiteSpace(azureOpenAiEndpoint))
                {
                    var baseEndpoint = azureOpenAiEndpoint.TrimEnd('/') + "/openai/v1";
                    endpoint = new Uri(baseEndpoint);
                }
                // Priority 3: If no OPENAI_API_KEY but no Azure endpoint, use openai-shared
                else if (string.IsNullOrWhiteSpace(openAiApiKey))
                {
                    endpoint = new Uri("https://openai-shared.openai.azure.com/openai/v1");
                }
                // Priority 4: OPENAI_API_KEY exists but no Azure endpoint - use standard OpenAI (no endpoint)

                // If we have an endpoint, use the Azure helper which handles bearer token vs API key
                if (endpoint != null)
                {
                    return AzureOpenAIClientHelper.CreateAzureOpenAIClient(endpoint, credential);
                }

                // For standard OpenAI (OPENAI_API_KEY exists, no Azure endpoint)
                return new OpenAIClient(new ApiKeyCredential(openAiApiKey!));
            });
        }

        // Once middleware support is added to the MCP SDK this should be replaced
        public static void RegisterInstrumentedMcpTools(IServiceCollection services, string[] args)
        {
            JsonSerializerOptions? serializerOptions = null;
            var toolTypes = SharedOptions.ToolsList;
            var toolMatchList = SharedOptions.GetToolsFromArgs(args);

            foreach (var toolType in toolTypes)
            {
                if (toolType is null)
                {
                    continue;
                }

                var toolMethods = toolType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
                                    .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() is not null);

                if (toolMatchList.Length > 0)
                {
                    toolMethods = toolMethods.Where(m =>
                    {
                        var attr = m.GetCustomAttribute<McpServerToolAttribute>();
                        return attr?.Name is not null && toolMatchList.Any(glob => FileSystemName.MatchesSimpleExpression(glob, attr.Name));
                    });
                }

                foreach (var toolMethod in toolMethods)
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

        public static void AddLanguageSpecific<T>(this IServiceCollection services,
            Type dotnet = default,
            Type go = default,
            Type java = default,
            Type javascript = default,
            Type python = default) where T : class
        {
            services.AddScoped<ILanguageSpecificResolver<T>, LanguageSpecificResolver<T>>();
            if (dotnet != null)
            {
                services.AddKeyedScoped(typeof(T), SdkLanguage.DotNet, dotnet);
            }
            if (go != null)
            {
                services.AddKeyedScoped(typeof(T), SdkLanguage.Go, go);
            }
            if (java != null)
            {
                services.AddKeyedScoped(typeof(T), SdkLanguage.Java, java);
            }
            if (javascript != null)
            {
                services.AddKeyedScoped(typeof(T), SdkLanguage.JavaScript, javascript);
            }
            if (python != null)
            {
                services.AddKeyedScoped(typeof(T), SdkLanguage.Python, python);
            }
        }
    }
}
