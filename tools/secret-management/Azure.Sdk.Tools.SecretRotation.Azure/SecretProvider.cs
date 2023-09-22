using System.Text.RegularExpressions;
using Azure.Core;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.SecretRotation.Azure;

public class SecretProvider : ISecretProvider
{
    private static readonly Regex secretRegex = new (@"(?<vault>https://.*?\.vault\.azure\.net)/secrets/(?<secret>.*)", RegexOptions.Compiled, TimeSpan.FromSeconds(5));
    private readonly ILogger logger;

    public SecretProvider(ILogger logger)
    {
        this.logger = logger;
    }

    public async Task<string> GetSecretValueAsync(TokenCredential credential, Uri secretUri)
    {
        Match match = secretRegex.Match(secretUri.AbsoluteUri);

        if (!match.Success)
        {
            throw new ArgumentException("Unable to parse uri as a Key Vault secret uri");
        }

        string vaultUrl = match.Groups["vault"].Value;
        string secretName = match.Groups["secret"].Value;

        try
        {
            var secretClient = new SecretClient(new Uri(vaultUrl), credential);

            this.logger.LogDebug("Retrieving value for secret '{SecretUrl}'", secretUri);

            KeyVaultSecret secret = await secretClient.GetSecretAsync(secretName);

            return secret.Value;
        }
        catch (Exception exception)
        {
            this.logger.LogError(exception, "Unable to read secret {SecretName} from vault {VaultUrl}", secretName, vaultUrl);
            throw;
        }
    }
}
