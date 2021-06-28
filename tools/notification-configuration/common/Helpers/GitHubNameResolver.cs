using Kusto.Data;
using Kusto.Data.Common;
using Kusto.Data.Net.Client;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System;
using System.Threading.Tasks;
using NotificationConfiguration.Models;
using System.Collections.Concurrent;

namespace NotificationConfiguration.Helpers
{
    /// <summary>
    /// Class used to enable the retrieval of github<->aad mapping questions
    /// </summary>
    public class GitHubNameResolver : IDisposable
    {
        private readonly ICslQueryProvider client;
        private readonly string kustoTable;
        private readonly ILogger<GitHubNameResolver> logger;
        private static ConcurrentDictionary<string, string> githubUserNameCache = new ConcurrentDictionary<string, string>();

        private static ICslQueryProvider GetKustoClient(
            string aadAppId,
            string aadAppSecret, 
            string aadTenant, 
            string kustoUrl,
            string kustoDatabaseName)
        {
            var authContext = new AuthenticationContext($"https://login.windows.net/{aadTenant}");
            var clientCredential = new ClientCredential(aadAppId, aadAppSecret);
            var authResult = authContext.AcquireTokenAsync(kustoUrl, clientCredential)
                .GetAwaiter()
                .GetResult();

            var connectionStringBuilder = new KustoConnectionStringBuilder(
                $"{kustoUrl}/{kustoDatabaseName};fed=true;UserToken={authResult.AccessToken}"
            );

            return KustoClientFactory.CreateCslQueryProvider(connectionStringBuilder);
        }

        /// <summary>
        /// Create a new GitHubNameResolver
        /// </summary>
        /// <param name="aadAppId">AAD App Id</param>
        /// <param name="aadAppSecret">AAD App Secret</param>
        /// <param name="aadTenant">AAD Tenant</param>
        /// <param name="kustoUrl">Kusto URL</param>
        /// <param name="kustoDatabaseName">Kusto DB Name</param>
        /// <param name="kustoTable">Kusto Table Name</param>
        /// <param name="logger">Logger</param>
        public GitHubNameResolver(
            string aadAppId, 
            string aadAppSecret, 
            string aadTenant, 
            string kustoUrl, 
            string kustoDatabaseName, 
            string kustoTable,
            ILogger<GitHubNameResolver> logger)
        {
            this.client = GetKustoClient(aadAppId, aadAppSecret, aadTenant, kustoUrl, kustoDatabaseName);
            this.kustoTable = kustoTable;
            this.logger = logger;
        }

        /// <summary>
        /// Queries Kusto for an internal alias from a given GitHub alias
        /// </summary>
        /// <param name="githubUserName">GitHub alias</param>
        /// <returns>Internal alias or null if no internal user found</returns>
        public async Task<string> GetInternalUserPrincipal(string githubUserName)
        {
            string result;
            if (githubUserNameCache.TryGetValue(githubUserName, out result))
            {
                return result;
            }

            result = await GetInternalUserPrincipalImpl(githubUserName);
            githubUserNameCache.TryAdd(githubUserName, result);
            return result;
        }

#pragma warning disable 1998
        public async Task<string> GetInternalUserPrincipalImpl(string githubUserName)
        {
            var query = $"{kustoTable} | where githubUserName == '{githubUserName}' | project aadUpn | limit 1;";

            // TODO: Figure out how to make this async
            using (var reader = client.ExecuteQuery(query))
            {
                if (reader.Read())
                {
                    return reader.GetString(0);
                }

                logger.LogWarning("Could Not Resolve GitHub User Username = {0}", githubUserName);
                return default;
            }
        }

        /// <summary>
        /// Queries Kusto for mapping information present in a target table. Assumes githubemployeelink. 
        /// </summary>
        /// <param name="aadName">Full name in AAD. EG: "Scott Beddall". Can be sourced from devops variable $(Build.QueuedBy).</param>
        /// <returns>Internal alias or null if no internal user found</returns>
        public async Task<IdentityDetail> GetMappingInformationFromAADName(string aadName)
        {
            var query = $"{kustoTable} | where aadName startswith '{aadName}' | project githubUserName, aadId, aadName, aadAlias, aadUpn | limit 1;";

            // TODO: Figure out how to make this async
            using (var reader = client.ExecuteQuery(query))
            {
                if (reader.Read())
                {
                    return new IdentityDetail(){
                        GithubUserName = reader.GetString(0),
                        AadId = reader.GetString(1),
                        Name = reader.GetString(2),
                        Alias = reader.GetString(3),
                        AadUpn = reader.GetString(4)
                    };
                }

                logger.LogWarning("Could Not Resolve Identity of Name = {0}", aadName);
                return default;
            }
        }
#pragma warning restore 1998

        /// <summary>
        /// Disposes the GitHubNameResolver
        /// </summary>
        public void Dispose()
        {
            client.Dispose();
        }
    }
}
