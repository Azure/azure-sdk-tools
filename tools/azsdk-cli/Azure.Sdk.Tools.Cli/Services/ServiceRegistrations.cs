// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.IO.Enumeration;
using System.Reflection;
using System.Text.Json;
using System.ClientModel;
using Microsoft.Extensions.Azure;
using ModelContextProtocol.Server;
using OpenAI;
using GitHub.Copilot.SDK;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Microagents;
using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Tools.Core;
using Azure.Sdk.Tools.Cli.Services.APIView;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Services.TypeSpec;
using Azure.Sdk.Tools.Cli.Services.Upgrade;
using Azure.Sdk.Tools.Cli.Telemetry;


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
            services.AddSingleton<IAzureSdkKnowledgeBaseService, AzureSdkKnowledgeBaseService>();
            services.AddSingleton<IUpgradeService, UpgradeService>();

            // APIView Services
            services.AddSingleton<IAPIViewAuthenticationService, APIViewAuthenticationService>();
            services.AddSingleton<IAPIViewHttpService, APIViewHttpService>();
            services.AddSingleton<IAPIViewService, APIViewService>();

            services.AddScoped<LanguageService, DotnetLanguageService>();
            services.AddScoped<LanguageService, JavaLanguageService>();
            services.AddScoped<LanguageService, JavaScriptLanguageService>();
            services.AddScoped<LanguageService, PythonLanguageService>();
            services.AddScoped<LanguageService, GoLanguageService>();

            // Helper classes
            services.AddSingleton<IFileHelper, FileHelper>();
            services.AddSingleton<IChangelogHelper, ChangelogHelper>();
            services.AddSingleton<ILogAnalysisHelper, LogAnalysisHelper>();
            services.AddSingleton<IGitHelper, GitHelper>();
            services.AddSingleton<ITestHelper, TestHelper>();
            services.AddSingleton<ITypeSpecHelper, TypeSpecHelper>();
            services.AddSingleton<ISpecPullRequestHelper, SpecPullRequestHelper>();
            services.AddSingleton<IUserHelper, UserHelper>();
            services.AddSingleton<ICodeownersValidatorHelper, CodeownersValidatorHelper>();
            services.AddSingleton<ICodeownersGenerateHelper, CodeownersGenerateHelper>();
            services.AddSingleton<IPackageInfoHelper, PackageInfoHelper>();
            services.AddSingleton<IEnvironmentHelper, EnvironmentHelper>();
            services.AddSingleton<IMcpServerContextAccessor, McpServerContextAccessor>();
            if (outputMode == OutputHelper.OutputModes.Mcp)
            {
                services.AddSingleton<IRawOutputHelper, McpRawOutputHelper>();
            }
            else
            {
                services.AddSingleton<IRawOutputHelper>(_ => new OutputHelper(outputMode));
            }
            services.AddSingleton<ISpecGenSdkConfigHelper, SpecGenSdkConfigHelper>();
            services.AddSingleton<IInputSanitizer, InputSanitizer>();
            services.AddSingleton<ITspClientHelper, TspClientHelper>();
            services.AddSingleton<IAPIViewFeedbackService, APIViewFeedbackService>();

            // Process Helper Classes
            services.AddSingleton<INpxHelper, NpxHelper>();
            services.AddSingleton<INpmHelper, NpmHelper>();
            services.AddSingleton<IPowershellHelper, PowershellHelper>();
            services.AddSingleton<IProcessHelper, ProcessHelper>();
            services.AddSingleton<IMavenHelper, MavenHelper>();
            services.AddSingleton<IPythonHelper, PythonHelper>();
            services.AddSingleton<IGitCommandHelper, GitCommandHelper>();

            // Services that need to be scoped so we can track/update state across services per request
            services.AddScoped<TokenUsageHelper>();
            services.AddScoped<IOutputHelper>(_ => new OutputHelper(outputMode));
            services.AddScoped<ConversationLogger>();
            // Services depending on other scoped services
            services.AddScoped<IMicroagentHostService, MicroagentHostService>();
            services.AddScoped<IAzureAgentServiceFactory, AzureAgentServiceFactory>();
            services.AddScoped<ICommonValidationHelpers, CommonValidationHelpers>();

            // Copilot SDK services for new agents (CopilotAgent<T> pattern)
            // CopilotClient is a singleton because it manages the CLI process connection.
            // Each request creates its own CopilotSession via CreateSessionAsync(), which isolates conversation state.
            services.AddSingleton<CopilotClient>(sp =>
            {
                var logger = sp.GetService<ILogger<CopilotClient>>();
                return new CopilotClient(new CopilotClientOptions
                {
                    UseStdio = true,
                    AutoStart = true,
                    Logger = logger
                });
            });
            services.AddSingleton<ICopilotClientWrapper, CopilotClientWrapper>();
            services.AddScoped<ICopilotAgentRunner, CopilotAgentRunner>();

            // TypeSpec Customization Service (uses Copilot SDK)
            services.AddScoped<ITypeSpecCustomizationService, TypeSpecCustomizationService>();


            services.AddHttpClient();
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

        // Update checking and upgrade management in MCP server mode
        // requires sequencing events at specific points in the Host lifecycle
        // orchestrated via HostedServices
        public static void RegisterUpgradeServices(IServiceCollection services)
        {
            services
                .AddSingleton<UpgradeShutdownCoordinator>()
                .AddHostedService<UpgradeShutdownService>();

#if !DEBUG
            services.AddHostedService<UpgradeNotificationHostedService>();
#endif
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
                        var mcpServerContextAccessor = services.GetRequiredService<IMcpServerContextAccessor>();
                        return new InstrumentedTool(telemetryService, logger, mcpServerContextAccessor, innerTool);
                    }));
                }
            }
        }
    }
}
