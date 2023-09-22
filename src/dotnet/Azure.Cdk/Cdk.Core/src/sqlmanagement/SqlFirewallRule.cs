using Azure.ResourceManager.Sql;
using Azure.ResourceManager.Sql.Models;
using Cdk.Core;

namespace Cdk.Sql
{
    public class SqlFirewallRule : Resource<SqlFirewallRuleData>
    {
        private const string ResourceTypeName = "Microsoft.Sql/servers/firewallRules";

        public SqlFirewallRule(SqlServer scope, string? name = default, string version = "2020-11-01-preview")
            : base(scope, GetName(name), ResourceTypeName, version, ArmSqlModelFactory.SqlFirewallRuleData(
                name: GetName(name),
                resourceType: ResourceTypeName,
                startIPAddress: "0.0.0.1",
                endIPAddress: "255.255.255.254"
                ))
        {
        }

        private static string GetName(string? name) => name is null ? $"fw-{Infrastructure.Seed}" : $"{name}-{Infrastructure.Seed}";
    }
}
