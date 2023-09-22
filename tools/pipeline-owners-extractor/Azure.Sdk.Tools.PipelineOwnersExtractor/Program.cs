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
                // This affects config file loading and defaults to Directory.GetCurrentDirectory()
                .UseContentRoot(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location))
                .ConfigureServices((context, services) =>
                {
                    services.AddSingleton<TokenCredential, ChainedTokenCredential>(
                        DefaultAzureCredentialWithoutManagedIdentity);
                    services.AddSingleton<ISecretClientProvider, SecretClientProvider>();
                    services.Configure<PipelineOwnerSettings>(context.Configuration);
                    services
                        .AddSingleton<IPostConfigureOptions<PipelineOwnerSettings>,
                            PostConfigureKeyVaultSettings<PipelineOwnerSettings>>();
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
        /// Instead of using DefaultAzureCredential [1] we use ChainedTokenCredential [2] which works
        /// as DefaultAzureCredential, but most importantly, it excludes ManagedIdentityCredential.
        /// We do so because there is an undesired managed identity available when we run this
        /// code in CI/CD pipelines, which takes priority over the desired AzureCliCredential coming
        /// from the calling AzureCLI@2 task.
        ///
        /// Besides, the returned ChainedTokenCredential also excludes following credentials:
        ///
        /// - SharedTokenCredential, as it appears to fail on linux with following error:
        ///   SharedTokenCacheCredential authentication failed: Persistence check failed. Data was written but it could not be read. Possible cause: on Linux, LibSecret is installed but D-Bus isn't running because it cannot be started over SSH.
        ///
        /// - VisualStudioCodeCredential, as it doesn't work, as explained here:
        ///   https://learn.microsoft.com/en-us/dotnet/api/overview/azure/identity-readme?view=azure-dotnet#defaultazurecredential
        ///
        /// The remaining credentials are in the same order as in DefaultAzureCredential.
        ///
        /// For debugging aids helping determine which credential is used and how,
        /// please see the following tags in azure-sdk-tools repo:
        /// - kojamroz_debug_aid_default_azure_credentials
        ///   Code from @hallipr showing how to get credential data using Microsoft Graph and JwtSecurityToken
        /// - kojamroz_debug_aid_diag_log_on_creds
        ///   Code from kojamroz showing how to use Azure.Identity diagnostic output to get information on which
        ///   credential ends up being in use (additional flags must be set to see the full info [3])
        /// 
        /// Full context provided here, on internal Azure SDK Engineering System Teams channel:
        /// https://teams.microsoft.com/l/message/19:59dbfadafb5e41c4890e2cd3d74cc7ba@thread.skype/1675713800408?tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47&groupId=3e17dcb0-4257-4a30-b843-77f47f1d4121&parentMessageId=1675713800408&teamName=Azure%20SDK&channelName=Engineering%20System%20%F0%9F%9B%A0%EF%B8%8F&createdTime=1675713800408
        ///
        /// [1] https://learn.microsoft.com/en-us/dotnet/api/overview/azure/identity-readme?view=azure-dotnet#defaultazurecredential
        /// [2] https://learn.microsoft.com/en-us/dotnet/api/overview/azure/identity-readme?view=azure-dotnet#define-a-custom-authentication-flow-with-chainedtokencredential
        /// [3] https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/identity/Azure.Identity/README.md#logging
        /// </summary>
        private static Func<IServiceProvider, ChainedTokenCredential> DefaultAzureCredentialWithoutManagedIdentity
            => _
                => new ChainedTokenCredential(
                    new EnvironmentCredential(),
                    new VisualStudioCredential(),
                    new AzureCliCredential(),
                    new AzurePowerShellCredential(),
                    new InteractiveBrowserCredential());

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
