using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Microsoft.Graph;
using Microsoft.IdentityModel.JsonWebTokens;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Azure.Sdk.Tools.PipelineOwnersExtractor
{
    public class Program
    {
        private static readonly JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented, Converters = { new StringEnumConverter() } };

        public static async Task Main(string[] args)
        {
            Console.WriteLine("Initializing PipelineOwnersExtractor");

            await DumpMeInfoAsync(new ManagedIdentityCredential(null, new TokenCredentialOptions{ Retry = { MaxRetries = 2, Delay = TimeSpan.FromSeconds(1), NetworkTimeout = TimeSpan.FromSeconds(3)} }));
            await DumpMeInfoAsync(new AzureCliCredential());
            await DumpMeInfoAsync(new AzurePowerShellCredential());
            await DumpMeInfoAsync(new DefaultAzureCredential());
        }

        public static async Task DumpMeInfoAsync(TokenCredential credential)
        {
            Console.WriteLine();
            Console.WriteLine(credential.GetType().Name);

            try
            {
                var claimIds = new[] {"aud", "iss", "app_displayname", "appid", "name", "idtype", "scp", "upn", "unique_name" };
                string[] scopes = { "https://graph.microsoft.com/.default" };

                var token = await credential.GetTokenAsync(new TokenRequestContext(scopes, null), CancellationToken.None);
                var parsed = new JwtSecurityToken(token.Token);

                Console.WriteLine(JsonConvert.SerializeObject(new { parsed.Audiences, parsed.Issuer, Claims = parsed.Claims.Where(x => claimIds.Contains(x.Type)).Select(x => $"{x.Type}: {x.Value}") }, jsonSerializerSettings));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

            Console.WriteLine();
        }
    }
}
