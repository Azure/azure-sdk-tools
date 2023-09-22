using Azure.Core;
using Azure.ResourceManager.Sql;
using Azure.ResourceManager.Sql.Models;
using Cdk.Core;

namespace Cdk.Sql
{
    public class SqlDatabase : Resource<SqlDatabaseData>
    {
        private const string ResourceTypeName = "Microsoft.Sql/servers/databases";

        public SqlDatabase(SqlServer scope, string? name = default, string version = "2022-08-01-preview", AzureLocation? location = default)
            : base(scope, GetName(name), ResourceTypeName, version, ArmSqlModelFactory.SqlDatabaseData(
                name: GetName(name),
                resourceType: ResourceTypeName,
                location: GetLocation(location)))
        {
        }

        public ConnectionString GetConnectionString(Resource passwordSecret, string userName = "appUser")
            => new ConnectionString(this, passwordSecret, userName);

        public ConnectionString GetConnectionString(Parameter passwordSecret, string userName = "appUser")
            => new ConnectionString(this, passwordSecret, userName);            

        private static string GetName(string? name) => name is null ? $"db-{Infrastructure.Seed}" : name;
    }
}
