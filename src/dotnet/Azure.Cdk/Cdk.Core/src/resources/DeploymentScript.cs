using Azure.Core;
using Azure.ResourceManager.Resources.Models;
using Cdk.Core;
using Cdk.ResourceManager;

namespace Cdk.Resources
{
    public class DeploymentScript : Resource<AzureCliScript>
    {
        private const string ResourceTypeName = "Microsoft.Resources/deploymentScripts";
        private const string _defaultVersion = "2020-10-01";

        protected override bool IsChildResource => true;

        public DeploymentScript(ResourceGroup scope, string resourceName, IEnumerable<ScriptEnvironmentVariable> scriptEnvironmentVariables, string scriptContent, string version = _defaultVersion, AzureLocation? location = default)
            : base(scope, GetName(resourceName), ResourceTypeName, version, ArmResourcesModelFactory.AzureCliScript(
                name: GetName(resourceName),
                resourceType: ResourceTypeName,
                location: location ?? Environment.GetEnvironmentVariable("AZURE_LOCATION") ?? AzureLocation.WestUS,
                azCliVersion: "2.37.0",
                retentionInterval: TimeSpan.FromHours(1),
                timeout: TimeSpan.FromMinutes(5),
                cleanupPreference: ScriptCleanupOptions.OnSuccess,
                environmentVariables: scriptEnvironmentVariables,
                scriptContent: scriptContent))
        {
        }

        public DeploymentScript(ResourceGroup scope, string resourceName, Resource database, Parameter appUserPasswordSecret, Parameter sqlAdminPasswordSecret, string version = _defaultVersion, AzureLocation? location = default)
            : base(scope, GetName(resourceName), ResourceTypeName, version, ArmResourcesModelFactory.AzureCliScript(
                name: GetName(resourceName),
                resourceType: ResourceTypeName,
                location: GetLocation(location),
                azCliVersion: "2.37.0",
                retentionInterval: TimeSpan.FromHours(1),
                timeout: TimeSpan.FromMinutes(5),
                cleanupPreference: ScriptCleanupOptions.OnSuccess,
                environmentVariables: new List<ScriptEnvironmentVariable>
                {
                    new ScriptEnvironmentVariable("APPUSERNAME") { Value = "appUser" },
                    new ScriptEnvironmentVariable("APPUSERPASSWORD") { SecureValue = $"_p_.{appUserPasswordSecret.Name}" },
                    new ScriptEnvironmentVariable("DBNAME") { Value = $"_p_.{database.Name}.name" },
                    new ScriptEnvironmentVariable("DBSERVER") { Value = $"_p_.{database.Scope!.Name}.properties.fullyQualifiedDomainName" },
                    new ScriptEnvironmentVariable("SQLCMDPASSWORD") { SecureValue = $"_p_.{sqlAdminPasswordSecret.Name}" },
                    new ScriptEnvironmentVariable("SQLADMIN") { Value = "sqlAdmin" },
                },
                scriptContent: """
                        wget https://github.com/microsoft/go-sqlcmd/releases/download/v0.8.1/sqlcmd-v0.8.1-linux-x64.tar.bz2
                        tar x -f sqlcmd-v0.8.1-linux-x64.tar.bz2 -C .

                        cat <<SCRIPT_END > ./initDb.sql
                        drop user ${APPUSERNAME}
                        go
                        create user ${APPUSERNAME} with password = '${APPUSERPASSWORD}'
                        go
                        alter role db_owner add member ${APPUSERNAME}
                        go
                        SCRIPT_END

                        ./sqlcmd -S ${DBSERVER} -d ${DBNAME} -U ${SQLADMIN} -i ./initDb.sql
                        """))
        {
            ModuleDependencies.Add(database.Scope!);
            Parameters.Add(appUserPasswordSecret);
            Parameters.Add(sqlAdminPasswordSecret);
            scope.Resources.Remove(this);
            database.Scope!.Resources.Add(this);
        }

        private static string GetName(string? name) => name is null ? $"deploymentScript-{Infrastructure.Seed}" : $"{name}-{Infrastructure.Seed}";
    }
}
