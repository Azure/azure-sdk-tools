using Azure.ResourceManager.AppService.Models;
using Cdk.Core;

namespace Cdk.AppService
{
    internal class ApplicationSettingsResource : Resource<AppServiceConfigurationDictionary>
    {
        private const string ResourceTypeName = "Microsoft.Web/sites/config";

        public ApplicationSettingsResource(WebSite scope, IDictionary<string, string> appSettings, string version = "2021-02-01")
            : base(scope, "appsettings", ResourceTypeName, version, ArmAppServiceModelFactory.AppServiceConfigurationDictionary(
                name: "appsettings",
                properties: appSettings))
        {
        }

        public void AddApplicationSetting(string key, string value)
        {
            Properties.Properties.Add(key, value);
        }

        public void AddApplicationSetting(string key, Parameter value)
        {
            Properties.Properties.Add(key, $"_p_.{value.Name}");
        }
    }
}
