using System;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Azure.Sdk.Tools.NotificationConfiguration;
using Azure.Sdk.Tools.NotificationConfiguration.Helpers;
using Azure.Sdk.Tools.NotificationConfiguration.Services;
using Azure.Sdk.Tools.PipelineOwnersExtractor.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

namespace Azure.Sdk.Tools.PipelineOwnersExtractor
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("Initializing PipelineOwnersExtractor");

            using var host = Host.CreateDefaultBuilder(args)
                .ConfigureServices((context, services) =>
                {
                    services.AddSingleton<TokenCredential, DefaultAzureCredential>();
                    services.AddSingleton<ISecretClientProvider, SecretClientProvider>();
                    services.Configure<PipelineOwnerSettings>(context.Configuration);
                    services.AddSingleton<IPostConfigureOptions<PipelineOwnerSettings>, PostConfigureKeyVaultSettings<PipelineOwnerSettings>>();
                    services.AddSingleton<GitHubService>();
                    services.AddSingleton(CreateGithubAadConverter);
                    services.AddSingleton(CreateAzureDevOpsService);
                    services.AddSingleton<Processor>();
                })
                .Build();

            var processor = host.Services.GetRequiredService<Processor>();

            await processor.ExecuteAsync();
        }

        private static AzureDevOpsService CreateAzureDevOpsService(IServiceProvider provider)
        {
            var logger = provider.GetRequiredService<ILogger<AzureDevOpsService>>();
            var settings = provider.GetRequiredService<IOptions<PipelineOwnerSettings>>().Value;

            var uri = new Uri($"https://dev.azure.com/{settings.Account}");
            var credentials = new VssBasicCredential("pat", settings.AzureDevOpsPat);
            var connection = new VssConnection(uri, credentials);

            return new AzureDevOpsService(connection, logger);
        }

        private static GitHubToAADConverter CreateGithubAadConverter(IServiceProvider provider)
        {
            var logger = provider.GetRequiredService<ILogger<GitHubToAADConverter>>();
            var settings = provider.GetRequiredService<IOptions<PipelineOwnerSettings>>().Value;

            var credential = new ClientSecretCredential(
                settings.OpenSourceAadTenantId, 
                settings.OpenSourceAadAppId,
                settings.OpenSourceAadSecret);

            return new GitHubToAADConverter(credential, logger);
        }
    }
}
