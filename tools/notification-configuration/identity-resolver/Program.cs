using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NotificationConfiguration.Helpers;
using System;
using System.Threading.Tasks;

namespace identity_resolver
{
    class Program
    {

        /// <summary>
        /// Retrieves github<->ms mapping information given the full name of an employee.
        /// </summary>
        /// <param name="aadAppIdVar">AAD App ID environment variable name (Kusto access)</param>
        /// <param name="aadAppSecretVar">AAD App Secret environment variable name (Kusto access)</param>
        /// <param name="aadTenantVar">AAD Tenant environment variable name (Kusto access)</param>
        /// <param name="kustoUrlVar">Kusto URL environment variable name</param>
        /// <param name="kustoDatabaseVar">Kusto DB environment variable name</param>
        /// <param name="kustoTableVar">Kusto Table environment variable name</param>
        /// <param name="identity">The full name of the employee</param>
        /// <returns></returns>
        public static async Task Main(
            string aadAppIdVar,
            string aadAppSecretVar,
            string aadTenantVar,
            string kustoUrlVar,
            string kustoDatabaseVar,
            string kustoTableVar,
            string identity
            )
        {

#pragma warning disable CS0618 // Type or member is obsolete
            using (var loggerFactory = new LoggerFactory().AddConsole(includeScopes: true))
#pragma warning restore CS0618 // Type or member is obsolete
            {
                var githubNameResolver = new GitHubNameResolver(
                    Environment.GetEnvironmentVariable(aadAppIdVar),
                    Environment.GetEnvironmentVariable(aadAppSecretVar),
                    Environment.GetEnvironmentVariable(aadTenantVar),
                    Environment.GetEnvironmentVariable(kustoUrlVar),
                    Environment.GetEnvironmentVariable(kustoDatabaseVar),
                    Environment.GetEnvironmentVariable(kustoTableVar),
                    loggerFactory.CreateLogger<GitHubNameResolver>()
                );

                var result = await githubNameResolver.GetMappingInformationFromAADName(identity);

                Console.Write(JsonConvert.SerializeObject(result));
            }
        }
    }
}
