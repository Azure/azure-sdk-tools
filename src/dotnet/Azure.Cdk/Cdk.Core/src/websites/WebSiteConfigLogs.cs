using Azure.Core;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.AppService.Models;
using Cdk.Core;

namespace Cdk.AppService
{
    public class WebSiteConfigLogs : Resource<SiteLogsConfigData>
    {
        private const string ResourceTypeName = "Microsoft.Web/sites/config";

        private static string GetName(string? name) => name is null ? $"logs-{Infrastructure.Seed}" : $"{name}-{Infrastructure.Seed}";

        internal WebSiteConfigLogs(WebSite scope, string resourceName, string version = "2021-02-01", AzureLocation? location = default)
            : base(scope, resourceName, ResourceTypeName, version, ArmAppServiceModelFactory.SiteLogsConfigData(
                name: resourceName,
                applicationLogs: new ApplicationLogsConfig()
                {
                    FileSystemLevel = WebAppLogLevel.Verbose
                },
                isDetailedErrorMessagesEnabled: true,
                isFailedRequestsTracingEnabled: true,
                httpLogs: new AppServiceHttpLogsConfig()
                {
                    FileSystem = new FileSystemHttpLogsConfig()
                    {
                        IsEnabled = true,
                        RetentionInDays = 1,
                        RetentionInMb = 35
                    }
                }))
        {
        }
    }
}
