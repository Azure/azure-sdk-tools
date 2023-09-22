using Cdk.Core;
using Cdk.AppService;
using Cdk.KeyVault;
using Cdk.ResourceManager;
using Cdk.Resources;
using Cdk.Sql;

namespace Hello.Cdk
{
    public class HelloCdkInfrastructure : Infrastructure
    {
        public HelloCdkInfrastructure()
        {
            Parameter sqlAdminPasswordParam = new Parameter("sqlAdminPassword", "SQL Server administrator password", isSecure: true);
            Parameter appUserPasswordParam = new Parameter("appUserPassword", "Application user password", isSecure: true);

            ResourceGroup resourceGroup = new ResourceGroup();
            resourceGroup.Properties.Tags.Add("key", "value");

            AppServicePlan appServicePlan = new AppServicePlan(resourceGroup, "appServicePlan");

            WebSite frontEnd = new WebSite(resourceGroup, "frontEnd", appServicePlan, Runtime.Node, "18-lts");
            var frontEndPrincipalId = frontEnd.AddOutput("SERVICE_API_IDENTITY_PRINCIPAL_ID", nameof(frontEnd.Properties.Identity.PrincipalId), isSecure: true);

            KeyVault keyVault = new KeyVault(resourceGroup, "kevault");
            keyVault.AddOutput("endpoint", nameof(keyVault.Properties.Properties.VaultUri));
            keyVault.AddAccessPolicy(frontEndPrincipalId);

            KeyVaultSecret sqlAdminSecret = new KeyVaultSecret(keyVault, "sqlAdminPassword");
            sqlAdminSecret.AssignParameter(nameof(sqlAdminSecret.Properties.Properties.Value), sqlAdminPasswordParam);

            KeyVaultSecret appUserSecret = new KeyVaultSecret(keyVault, "appUserPassword");
            appUserSecret.AssignParameter(nameof(appUserSecret.Properties.Properties.Value), appUserPasswordParam);

            SqlServer sqlServer = new SqlServer(resourceGroup, "sqlserver");
            sqlServer.AssignParameter(nameof(sqlServer.Properties.AdministratorLoginPassword), sqlAdminPasswordParam);

            SqlDatabase sqlDatabase = new SqlDatabase(sqlServer);

            KeyVaultSecret sqlAzureConnectionStringSecret = new KeyVaultSecret(keyVault, "connectionString", sqlDatabase.GetConnectionString(appUserPasswordParam));

            SqlFirewallRule sqlFirewallRule = new SqlFirewallRule(sqlServer, "firewallRule");

            DeploymentScript deploymentScript = new DeploymentScript(resourceGroup, "cliScript", sqlDatabase, appUserPasswordParam, sqlAdminPasswordParam);

            WebSite backEnd = new WebSite(resourceGroup, "backEnd", appServicePlan, Runtime.Dotnetcore, "6.0");

            WebSiteConfigLogs logs = new WebSiteConfigLogs(frontEnd, "logs");
        }
    }
}
