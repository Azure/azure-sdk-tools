using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Azure.Sdk.Tools.NotificationConfiguration;
using Azure.Sdk.Tools.NotificationConfiguration.Helpers;
using Azure.Sdk.Tools.NotificationConfiguration.Services;
using Azure.Sdk.Tools.PipelineOwnersExtractor.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.Services.Client;
using Microsoft.VisualStudio.Services.WebApi;

namespace Azure.Sdk.Tools.PipelineOwnersExtractor
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("Initializing PipelineOwnersExtractor");

            using var host = Host.CreateDefaultBuilder(args)
                // This affects config file loading and defaults to Directory.GetCurrentDirectory()
                .UseContentRoot(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location))
                .ConfigureServices((context, services) =>
                {
                    services.AddSingleton<TokenCredential>(BuildAzureCredential);
                    services.Configure<PipelineOwnerSettings>(context.Configuration);
                    services.AddSingleton<GitHubService>();
                    services.AddSingleton(CreateGithubAadConverter);
                    services.AddSingleton(CreateAzureDevOpsService);
                    services.AddSingleton<Processor>();
                })
                .Build();

            var processor = host.Services.GetRequiredService<Processor>();

            await processor.ExecuteAsync();
        }

        /// <summary>
        /// Build a TokenCredential supporting <see cref="AzurePowerShellCredential"/> and <see cref="AzureCliCredential"/>
        /// </summary>
        /// <remarks>
        /// Full context provided here, on internal Azure SDK Engineering System Teams channel:
        /// https://teams.microsoft.com/l/message/19:59dbfadafb5e41c4890e2cd3d74cc7ba@thread.skype/1675713800408?tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47&groupId=3e17dcb0-4257-4a30-b843-77f47f1d4121&parentMessageId=1675713800408&teamName=Azure%20SDK&channelName=Engineering%20System%20%F0%9F%9B%A0%EF%B8%8F&createdTime=1675713800408
        ///
        /// [1] https://learn.microsoft.com/en-us/dotnet/api/overview/azure/identity-readme?view=azure-dotnet#define-a-custom-authentication-flow-with-chainedtokencredential
        /// [2] https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/identity/Azure.Identity/README.md#logging
        /// </remarks>
        private static TokenCredential BuildAzureCredential(IServiceProvider provider) {
            return new ChainedTokenCredential(
                new AzureCliCredential(),
                new AzurePowerShellCredential()
            );
        }

        private static AzureDevOpsService CreateAzureDevOpsService(IServiceProvider provider)
        {
            var logger = provider.GetRequiredService<ILogger<AzureDevOpsService>>();
            var settings = provider.GetRequiredService<IOptions<PipelineOwnerSettings>>().Value;

            var uri = new Uri($"https://dev.azure.com/{settings.Account}");
            
            var azureCredential = provider.GetRequiredService<TokenCredential>();
            var devopsCredential = new VssAzureIdentityCredential(azureCredential);
            var connection = new VssConnection(uri, devopsCredential);

            return new AzureDevOpsService(connection, logger);
        }

        private static GitHubToAADConverter CreateGithubAadConverter(IServiceProvider provider)
        {
            var logger = provider.GetRequiredService<ILogger<GitHubToAADConverter>>();
            var azureCredential = provider.GetRequiredService<TokenCredential>();

            return new GitHubToAADConverter(azureCredential, logger);
        }
    }
}
