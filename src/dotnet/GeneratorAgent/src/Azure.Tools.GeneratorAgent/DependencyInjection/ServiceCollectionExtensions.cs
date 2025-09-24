using Azure.AI.Agents.Persistent;
using Azure.Core;
using Azure.Identity;
using Azure.Tools.GeneratorAgent.Agent;
using Azure.Tools.GeneratorAgent.Authentication;
using Azure.Tools.GeneratorAgent.Configuration;
using Azure.Tools.GeneratorAgent.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Azure.Tools.GeneratorAgent.DependencyInjection
{
    internal static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds all required services for the GeneratorAgent application.
        /// </summary>
        /// <param name="services">The service collection to add services to</param>
        /// <param name="toolConfig">The tool configuration</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddGeneratorAgentServices(this IServiceCollection services, ToolConfiguration toolConfig)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(toolConfig);

            // Register configuration
            services.AddSingleton(toolConfig);
            services.AddSingleton(provider =>
            {
                var logger = provider.GetRequiredService<ILogger<AppSettings>>();
                return toolConfig.CreateAppSettings(logger);
            });

            // Register credential management services
            services.AddCredentialServices();

            // Register HttpClient management
            services.AddHttpClientServices();

            // Register Azure AI services
            services.AddAzureAIServices();

            // Register application services
            services.AddApplicationServices();

            return services;
        }

        /// <summary>
        /// Adds credential management services to the service collection.
        /// </summary>
        private static IServiceCollection AddCredentialServices(this IServiceCollection services)
        {
            services.AddSingleton<CredentialFactory>();
            services.AddSingleton<TokenCredential>(provider =>
            {
                var credentialFactory = provider.GetRequiredService<CredentialFactory>();
                var environment = DetermineRuntimeEnvironment();
                var options = CreateCredentialOptions();
                return credentialFactory.CreateCredential(environment, options);
            });

            return services;
        }

        /// <summary>
        /// Adds HttpClient services with proper configuration for GitHub API and other HTTP operations.
        /// </summary>
        private static IServiceCollection AddHttpClientServices(this IServiceCollection services)
        {
            // Named HttpClient for GitHub API operations
            services.AddHttpClient<GitHubFileService>((serviceProvider, client) =>
            {
                client.Timeout = TimeSpan.FromMinutes(2);
                client.DefaultRequestHeaders.UserAgent.Clear();
                client.DefaultRequestHeaders.UserAgent.ParseAdd("AzureSDK-TypeSpecGenerator/1.0");

                var githubToken = EnvironmentVariables.GitHubToken;
                
                if (string.IsNullOrEmpty(githubToken))
                {
                    var appSettings = serviceProvider.GetRequiredService<AppSettings>();
                    githubToken = appSettings?.GitHubToken;
                }
                
                if (!string.IsNullOrEmpty(githubToken))
                {
                    client.DefaultRequestHeaders.Authorization = 
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", githubToken);
                }
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler());

            return services;
        }

        /// <summary>
        /// Adds Azure AI services to the service collection.
        /// </summary>
        private static IServiceCollection AddAzureAIServices(this IServiceCollection services)
        {
            services.AddSingleton<PersistentAgentsClient>(provider =>
            {
                var appSettings = provider.GetRequiredService<AppSettings>();
                var credential = provider.GetRequiredService<TokenCredential>();
                return new PersistentAgentsClient(appSettings.ProjectEndpoint, credential);
            });

            return services;
        }

        /// <summary>
        /// Adds application-specific services to the service collection.
        /// </summary>
        private static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            services.AddSingleton<ProcessExecutionService>();
            services.AddSingleton<FormatPromptService>();

            // Step 1: Add ToolExecutor for clean tool-based approach
            services.AddSingleton<Func<ValidationContext, ToolExecutor>>(provider =>
            {
                return validationContext =>
                {
                    var toolHandlerFactory = provider.GetRequiredService<Func<ValidationContext, ITypeSpecToolHandler>>();
                    var toolHandler = toolHandlerFactory(validationContext);
                    return new ToolExecutor(
                        toolHandler,
                        provider.GetRequiredService<ILogger<ToolExecutor>>());
                };
            });

            // Step 2: Add ConversationManager factory for agent conversations
            services.AddSingleton<Func<ValidationContext, ConversationManager>>(provider =>
            {
                return validationContext =>
                {
                    var toolExecutorFactory = provider.GetRequiredService<Func<ValidationContext, ToolExecutor>>();
                    var toolExecutor = toolExecutorFactory(validationContext);
                    
                    return new ConversationManager(
                        provider.GetRequiredService<PersistentAgentsClient>(),
                        toolExecutor,
                        provider.GetRequiredService<AppSettings>(),
                        provider.GetRequiredService<ILogger<ConversationManager>>());
                };
            });

            // Step 3: Add ToolBasedAgent for complete workflow orchestration
            services.AddSingleton<Func<ValidationContext, ToolBasedAgent>>(provider =>
            {
                return validationContext =>
                {
                    var conversationManagerFactory = provider.GetRequiredService<Func<ValidationContext, ConversationManager>>();
                    var conversationManager = conversationManagerFactory(validationContext);
                    
                    return new ToolBasedAgent(
                        conversationManager,
                        provider.GetRequiredService<FormatPromptService>(),
                        provider.GetRequiredService<AppSettings>(),
                        provider.GetRequiredService<PersistentAgentsClient>(),
                        provider.GetRequiredService<ILogger<ToolBasedAgent>>());
                };
            });



            // Register ErrorAnalysisService with ToolBasedAgent
            services.AddSingleton<Func<ValidationContext, ErrorAnalysisService>>(provider =>
            {
                return validationContext =>
                {
                    var toolBasedAgentFactory = provider.GetRequiredService<Func<ValidationContext, ToolBasedAgent>>();
                    var toolBasedAgent = toolBasedAgentFactory(validationContext);
                    
                    return new ErrorAnalysisService(
                        toolBasedAgent,
                        provider.GetRequiredService<ILogger<ErrorAnalysisService>>());
                };
            });

            // Add new tool-based services
            services.AddSingleton<TypeSpecFileVersionManager>();

            services.AddSingleton<Func<ValidationContext, LocalLibraryGenerationService>>(provider =>
            {
                return validationContext => new LocalLibraryGenerationService(
                    provider.GetRequiredService<AppSettings>(),
                    provider.GetRequiredService<ILoggerFactory>().CreateLogger<LocalLibraryGenerationService>(),
                    provider.GetRequiredService<ProcessExecutionService>(),
                    validationContext);
            });

            services.AddSingleton<Func<ValidationContext, LibraryBuildService>>(provider =>
            {
                return validationContext => new LibraryBuildService(
                    provider.GetRequiredService<ILogger<LibraryBuildService>>(),
                    provider.GetRequiredService<ProcessExecutionService>(),
                    validationContext.ValidatedSdkDir);
            });

            services.AddSingleton<Func<ValidationContext, TypeSpecFileService>>(provider =>
            {
                return validationContext => new TypeSpecFileService(
                    provider.GetRequiredService<ILogger<TypeSpecFileService>>(),
                    validationContext,
                    provider.GetRequiredService<Func<ValidationContext, GitHubFileService>>());
            });

            services.AddSingleton<Func<ValidationContext, ITypeSpecToolHandler>>(provider =>
            {
                return validationContext =>
                {
                    var fileServiceFactory = provider.GetRequiredService<Func<ValidationContext, TypeSpecFileService>>();
                    var fileService = fileServiceFactory(validationContext);
                    var versionManager = provider.GetRequiredService<TypeSpecFileVersionManager>();
                    

                    return new TypeSpecToolHandler(
                        fileService,
                        versionManager,
                        provider.GetRequiredService<ILogger<TypeSpecToolHandler>>());
                };
            });

            services.AddSingleton<Func<ValidationContext, GitHubFileService>>(provider =>
            {
                return validationContext => new GitHubFileService(
                    provider.GetRequiredService<AppSettings>(),
                    provider.GetRequiredService<ILogger<GitHubFileService>>(),
                    validationContext,
                    provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(GitHubFileService)));
            });

            return services;
        }

        /// <summary>
        /// Determines the runtime environment based on environment variables.
        /// </summary>
        private static RuntimeEnvironment DetermineRuntimeEnvironment()
        {
            var isGitHubActions = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(EnvironmentVariables.GitHubActions)) ||
                                 !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(EnvironmentVariables.GitHubWorkflow));

            if (isGitHubActions)
            {
                return RuntimeEnvironment.DevOpsPipeline;
            }

            return RuntimeEnvironment.LocalDevelopment;
        }

        /// <summary>
        /// Creates credential options from environment variables.
        /// </summary>
        private static TokenCredentialOptions? CreateCredentialOptions()
        {
            var tenantId = Environment.GetEnvironmentVariable(EnvironmentVariables.AzureTenantId);
            Uri? authorityHost = null;

            var authority = Environment.GetEnvironmentVariable(EnvironmentVariables.AzureAuthorityHost);
            if (!string.IsNullOrEmpty(authority) && Uri.TryCreate(authority, UriKind.Absolute, out Uri? parsedAuthority))
            {
                authorityHost = parsedAuthority;
            }

            if (tenantId == null && authorityHost == null)
            {
                return null;
            }

            var options = new TokenCredentialOptions();

            if (authorityHost != null)
            {
                options.AuthorityHost = authorityHost;
            }

            return options;
        }
    }
}
