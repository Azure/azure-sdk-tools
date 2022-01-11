using Microsoft.Extensions.Logging;
using Azure.Sdk.Tools.NotificationConfiguration.Helpers;
using Azure.Sdk.Tools.NotificationConfiguration.Models;
using System;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.IdentityConverter
{
    class Program
    {
        /// <summary>
        /// Github identity and ms alias conversion.
        /// </summary>
        /// <param name="aadAppIdVar">AAD App ID environment variable name (Kusto access)</param>
        /// <param name="aadAppSecretVar">AAD App Secret environment variable name (Kusto access)</param>
        /// <param name="aadTenantVar">AAD Tenant environment variable name (Kusto access)</param>
        /// <param name="kustoUrlVar">Kusto URL environment variable name</param>
        /// <param name="kustoDatabaseVar">Kusto DB environment variable name</param>
        /// <param name="kustoTableVar">Kusto Table environment variable name</param>
        /// <param name="inputFormat">The identity format to convert from. The value is case insensitive. Options: fullName, msAlias, github, addId, aadUpn</param>
        /// <param name="inputValue">The input value to convert from. </param>
        /// <returns>Exit code</returns>
        public static async Task<int> Main(
            string aadAppIdVar,
            string aadAppSecretVar,
            string aadTenantVar,
            string kustoUrlVar,
            string kustoDatabaseVar,
            string kustoTableVar,
            string inputFormat,
            string inputValue
            )
        {
            
            if (inputFormat == null || inputValue == null)
            {
                Console.Error.WriteLine("You must specify [Yellow!inputFormat] and [Yellow!inputValue].");
                return 1;
            }
            #pragma warning disable CS0618 // Type or member is obsolete
            using (var loggerFactory = LoggerFactory.Create(builder => {
                builder.AddConsole(config => { config.IncludeScopes = true; });
            }))
            try
            {
                
                #pragma warning restore CS0618 // Type or member is obsolete
                var githubNameResolver = new GitHubNameResolver(
                        aadAppIdVar,
                        aadAppSecretVar,
                        aadTenantVar,
                        kustoUrlVar,
                        kustoDatabaseVar,
                        kustoTableVar,
                        loggerFactory.CreateLogger<GitHubNameResolver>()
                    );
                var result = default(IdentityDetail);

                if (String.Equals(inputFormat, "github", StringComparison.OrdinalIgnoreCase))
                {
                    result = await githubNameResolver.GetMappingInformationFromGithubUserName(inputValue);
                    if (result != null && result != default(IdentityDetail))
                    {
                        Console.WriteLine(result.Alias);
                        return 0;
                    }
                }

                if (String.Equals(inputFormat, "msAlias", StringComparison.OrdinalIgnoreCase))
                {
                    result = await githubNameResolver.GetMappingInformationFromAlias(inputValue);
                    if (result != null && result != default(IdentityDetail))
                    {
                        Console.WriteLine(result.GithubUserName);
                        return 0;
                    }
                }

                Console.Error.WriteLine("Did not retrieve any infomation for input ${inputFormat} and ${inputValue}. Acceptable input format is {'github', 'msAlias'}. ");
                return 1;
            }
            catch (Exception)
            {
                Console.Error.WriteLine("Failed to connect with kusto table. Please check your inputs.");
                return 1;
            }
        }
    }
}
