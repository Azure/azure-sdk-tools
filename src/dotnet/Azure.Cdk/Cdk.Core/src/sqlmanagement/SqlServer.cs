using Cdk.Core;
using Azure.Core;
using Azure.ResourceManager.Sql;
using Azure.ResourceManager.Sql.Models;
using Cdk.ResourceManager;

namespace Cdk.Sql
{
    public class SqlServer : Resource<SqlServerData>
    {
        private const string ResourceTypeName = "Microsoft.Sql/servers";

        public SqlServer(ResourceGroup scope, string name, string? version = default, AzureLocation? location = default)
            : base(scope, GetName(name), ResourceTypeName, version ?? "2022-08-01-preview", ArmSqlModelFactory.SqlServerData(
                name: GetName(name),
                location: GetLocation(location),
                resourceType: ResourceTypeName,
                version: "12.0",
                minimalTlsVersion: "1.2",
                publicNetworkAccess: ServerNetworkAccessFlag.Enabled,
                administratorLogin: "sqladmin",
                administratorLoginPassword: Guid.Empty.ToString()))
        {
        }

        private static string GetName(string? name) => name is null ? $"sql-{Infrastructure.Seed}" : $"{name}-{Infrastructure.Seed}";
    }
}
