using Cdk.Core;

namespace Cdk.Sql
{
    public class ConnectionString
    {
        public string Value { get; }

        internal SqlDatabase Database { get; }

        public ConnectionString(SqlDatabase database, Resource password, string userName)
        {
            Database = database;
            Value = $"Server=${{{database.Scope!.Name}.properties.fullyQualifiedDomainName}}; Database=${{{database.Name}.name}}; User={userName}; Password=${{{password.Name}}}";
        }

        public ConnectionString(SqlDatabase database, Parameter password, string userName)
        {
            Database = database;
            Value = $"Server=${{{database.Scope!.Name}.properties.fullyQualifiedDomainName}}; Database=${{{database.Name}.name}}; User={userName}; Password=${{{password.Name}}}";
        }
    }
}
