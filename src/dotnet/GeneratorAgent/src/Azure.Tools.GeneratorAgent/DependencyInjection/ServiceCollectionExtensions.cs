using Azure.AI.Agents.Persistent;
using Azure.Core;
using Azure.Identity;
using Azure.Tools.GeneratorAgent.Authentication;
using Azure.Tools.GeneratorAgent.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
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

                string? githubToken = EnvironmentVariables.GitHubToken;
                
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
            // All core services are stateless and can be singletons for better performance
            services.AddSingleton<ErrorFixerAgent>();      // Thread-safe, uses threadId for isolation
            services.AddSingleton<ProcessExecutor>();      // Stateless - just executes commands
            services.AddSingleton<BuildErrorAnalyzer>();   // Stateless - just analyzes errors

            // Factory services create ValidationContext-dependent instances, but factories themselves are stateless
            services.AddSingleton<Func<ValidationContext, ISdkGenerationService>>(provider =>
            {
                return validationContext => SdkGenerationServiceFactory.CreateSdkGenerationService(
                    validationContext,
                    provider.GetRequiredService<AppSettings>(),
                    provider.GetRequiredService<ILoggerFactory>(),
                    provider.GetRequiredService<ProcessExecutor>());
            });

            services.AddSingleton<Func<ValidationContext, SdkBuildService>>(provider =>
            {
                return validationContext => new SdkBuildService(
                    provider.GetRequiredService<ILogger<SdkBuildService>>(),
                    provider.GetRequiredService<ProcessExecutor>(),
                    validationContext.ValidatedSdkDir);
            });

            // Register factory method for TypeSpecFileService that requires ValidationContext
            services.AddSingleton<Func<ValidationContext, TypeSpecFileService>>(provider =>
            {
                return validationContext => new TypeSpecFileService(
                    provider.GetRequiredService<AppSettings>(),
                    provider.GetRequiredService<ILogger<TypeSpecFileService>>(),
                    provider.GetRequiredService<ILoggerFactory>(),
                    validationContext,
                    provider.GetRequiredService<Func<ValidationContext, GitHubFileService>>());
            });

            // Register factory method for GitHubFilesService that requires ValidationContext
            services.AddSingleton<Func<ValidationContext, GitHubFileService>>(provider =>
            {
                return validationContext => 
                {
                    var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
                    var httpClient = httpClientFactory.CreateClient(nameof(GitHubFileService));
                    
                    return new GitHubFileService(
                        provider.GetRequiredService<AppSettings>(),
                        provider.GetRequiredService<ILogger<GitHubFileService>>(),
                        validationContext,
                        httpClient);
                };
            });

            return services;
        }

        /// <summary>
        /// Determines the runtime environment based on environment variables.
        /// </summary>
        private static RuntimeEnvironment DetermineRuntimeEnvironment()
        {
            bool isGitHubActions = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(EnvironmentVariables.GitHubActions)) ||
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
            string? tenantId = Environment.GetEnvironmentVariable(EnvironmentVariables.AzureTenantId);
            Uri? authorityHost = null;

            string? authority = Environment.GetEnvironmentVariable(EnvironmentVariables.AzureAuthorityHost);
            if (!string.IsNullOrEmpty(authority) && Uri.TryCreate(authority, UriKind.Absolute, out Uri? parsedAuthority))
            {
                authorityHost = parsedAuthority;
            }

            if (tenantId == null && authorityHost == null)
            {
                return null;
            }

            TokenCredentialOptions options = new TokenCredentialOptions();

            if (authorityHost != null)
            {
                options.AuthorityHost = authorityHost;
            }

            return options;
        }
    }
}
