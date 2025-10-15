using Azure.Core;
using Azure.Identity;
using Azure.Tools.GeneratorAgent.Agent;
using Azure.Tools.GeneratorAgent.Authentication;
using Azure.Tools.GeneratorAgent.Configuration;
using Azure.Tools.GeneratorAgent.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;

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

            // Register configuration as singleton 
            services.AddSingleton(toolConfig);
            
            services.AddSingleton<AppSettings>(provider =>
            {
                var logger = provider.GetRequiredService<ILogger<AppSettings>>();
                var config = provider.GetRequiredService<ToolConfiguration>();
                return config.CreateAppSettings(logger);
            });

            return services
                .AddCredentialServices()
                .AddHttpClientServices()
                .AddAIServices()
                .AddApplicationServices();
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
        /// Adds AI services to the service collection.
        /// </summary>
        private static IServiceCollection AddAIServices(this IServiceCollection services)
        {
            services.AddSingleton<ChatClient>(provider =>
            {
                var appSettings = provider.GetRequiredService<AppSettings>();
                
                if (!string.IsNullOrEmpty(appSettings.OpenAIApiKey) && !string.IsNullOrEmpty(appSettings.OpenAIEndpoint))
                {
                    var apiKeyCredential = new System.ClientModel.ApiKeyCredential(appSettings.OpenAIApiKey);
                    var deploymentName = appSettings.OpenAIModel;
                    
                    return new ChatClient(
                        credential: apiKeyCredential,
                        model: deploymentName,
                        options: new OpenAIClientOptions()
                        {
                            Endpoint = new Uri(appSettings.OpenAIEndpoint),
                        });
                }
                
                throw new InvalidOperationException("OpenAI API key and endpoint not found in configuration. Please set OpenAI:ApiKey and OpenAI:Endpoint in appsettings.json.");
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
            services.AddSingleton<TypeSpecFileVersionManager>();
            services.AddSingleton<TypeSpecPatchApplicator>();

            services.AddScoped<ErrorAnalysisService>();
            services.AddScoped<GenerationOrchestrator>();
            services.AddScoped<BuildWorkflow>();
            services.AddScoped<LocalLibraryGenerationService>();
            services.AddScoped<LibraryBuildService>();
            services.AddScoped<ToolExecutor>();
            services.AddScoped<TypeSpecFileService>();
            services.AddScoped<GitHubFileService>();
            services.AddScoped<ITypeSpecToolHandler, TypeSpecToolHandler>();

            services.AddScoped<OpenAIService>();

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
