using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Azure.Core;
using Azure.Sdk.Tools.SecretRotation.Configuration;
using Azure.Sdk.Tools.SecretRotation.Core;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.SecretRotation.Stores.KeyVault;

public class KeyVaultSecretStore : SecretStore
{
    public enum RevocationAction
    {
        [JsonPropertyName("none")]
        None = 0,
        [JsonPropertyName("disableVersion")]
        DisableVersion = 1
    }

    public const string MappingKey = "Key Vault Secret";
    private const string RevokeAfterTag = "RevokeAfter";
    private const string OperationIdTag = "OperationId";
    private const string RevokedTag = "Revoked";

    private static readonly Regex uriRegex = new(
        @"^(?<VaultUri>https://.+?)/secrets/(?<SecretName>[^/]+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromSeconds(5));

    private readonly ILogger logger;
    private readonly RevocationAction revocationAction;

    private readonly SecretClient secretClient;
    private readonly string secretName;
    private readonly Uri vaultUri;

    public KeyVaultSecretStore(ILogger logger,
        TokenCredential credential,
        Uri vaultUri,
        string secretName,
        RevocationAction revocationAction)
    {
        this.vaultUri = vaultUri;
        this.secretName = secretName;
        this.revocationAction = revocationAction;
        this.logger = logger;
        this.secretClient = new SecretClient(vaultUri, credential);
    }

    public override bool CanRead => true;
    public override bool CanWrite => true;
    public override bool CanRevoke => true;
    public override bool CanAnnotate => true;

    public static Func<StoreConfiguration, SecretStore> GetSecretStoreFactory(TokenCredential credential,
        ILogger logger)
    {
        return storeConfiguration =>
        {
            var parameters = storeConfiguration.Parameters?.Deserialize<Parameters>();

            if (string.IsNullOrEmpty(parameters?.SecretUri))
            {
                throw new Exception("Missing required parameter 'secretUri'");
            }

            Match match = uriRegex.Match(parameters.SecretUri);
            string vaultUrl = match.Groups["VaultUri"].Value;
            string secretName = match.Groups["SecretName"].Value;

            if (!match.Success || !Uri.TryCreate(vaultUrl, UriKind.Absolute, out Uri? vaultUri))
            {
                throw new Exception("Unable to parse parameter 'secretUri'");
            }

            RevocationAction revocationAction = parameters.RevocationAction;

            return new KeyVaultSecretStore(logger,
                credential,
                vaultUri,
                secretName,
                revocationAction);
        };
    }

    public override async Task<SecretState> GetCurrentStateAsync()
    {
        try
        {
            this.logger.LogDebug("Getting secret '{SecretName}' from vault '{VaultUri}'", this.secretName,
                this.vaultUri);

            KeyVaultSecret secret = await this.secretClient.GetSecretAsync(this.secretName);

            secret.Properties.Tags.TryGetValue(OperationIdTag, out string? originId);

            var result = new SecretState
            {
                Id = secret.Properties.Version,
                OperationId = originId,
                ExpirationDate = secret.Properties.ExpiresOn,
                Tags = secret.Properties.Tags,
                Value = secret.Value,
            };

            return result;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return new SecretState { ErrorMessage = GetExceptionMessage(ex) };
        }
        catch (RequestFailedException ex)
        {
            throw new RotationException(GetExceptionMessage(ex), ex);
        }
    }

    public override async Task<IEnumerable<SecretState>> GetRotationArtifactsAsync()
    {
        var results = new List<SecretState>();

        try
        {

            this.logger.LogDebug("Getting all version metadata for secret '{SecretName}' from vault '{VaultUri}'", this.secretName,
                this.vaultUri);

            await foreach (SecretProperties? versionProperties in this.secretClient.GetPropertiesOfSecretVersionsAsync(
                               this.secretName))
            {
                if (versionProperties.Enabled == false
                    || versionProperties.Tags.ContainsKey(RevokedTag)
                    || versionProperties.Tags.TryGetValue(RevokeAfterTag, out string? revokeAfterString) == false)
                {
                    continue;
                }

                if (!DateTimeOffset.TryParse(revokeAfterString, out DateTimeOffset revokeAfterDate))
                {
                    // TODO: Warn about improperly formatted revokeAfter value
                    continue;
                }

                versionProperties.Tags.TryGetValue(OperationIdTag, out string? operationId);

                results.Add(new SecretState
                {
                    Id = versionProperties.Id.ToString(),
                    OperationId = operationId,
                    Tags = versionProperties.Tags,
                    RevokeAfterDate = revokeAfterDate
                });
            }

            return results;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return results;
        }
        catch (RequestFailedException ex)
        {
            throw new RotationException(GetExceptionMessage(ex), ex);
        }
    }

    public override async Task WriteSecretAsync(SecretValue secretValue,
        SecretState? currentState,
        DateTimeOffset? revokeAfterDate,
        bool whatIf)
    {
        var secret = new KeyVaultSecret(this.secretName, secretValue.Value)
        {
            Properties = { ExpiresOn = secretValue.ExpirationDate }
        };

        foreach ((string key, string value) in secretValue.Tags)
        {
            secret.Properties.Tags[key] = value;
        }

        if (!string.IsNullOrEmpty(OperationIdTag))
        {
            // TODO: Warn if Tags already contains this key?
            secret.Properties.Tags[OperationIdTag] = secretValue.OperationId;
        }

        if (whatIf)
        {
            this.logger.LogInformation(
                "WHAT IF: Set value for secret '{SecretName}' in vault '{VaultUri}'", this.secretName, this.vaultUri);

            return;
        }

        this.logger.LogInformation(
            "Setting value for secret '{SecretName}' in vault '{VaultUri}'", this.secretName, this.vaultUri);

        secret = await this.secretClient.SetSecretAsync(secret);

        if (this.IsPrimary)
        {
            secretValue.PrimaryState = secret;

            if (revokeAfterDate.HasValue)
            {
                await SetRevokeAfterForOldVersionsAsync(secret.Properties.Version, revokeAfterDate.Value);
            }
        }
    }

    public override Func<Task>? GetRevocationActionAsync(SecretState secretState, bool whatIf)
    {
        if (this.revocationAction == RevocationAction.None || string.IsNullOrEmpty(secretState.Id))
        {
            return null;
        }

        return async () =>
        {
            await foreach (SecretProperties? version in this.secretClient.GetPropertiesOfSecretVersionsAsync(
                               this.secretName))
            {
                if (version.Id.ToString() != secretState.Id)
                {
                    continue;
                }

                if (this.revocationAction == RevocationAction.DisableVersion)
                {
                    if (whatIf)
                    {
                        this.logger.LogInformation(
                            "WHAT IF: Disable verion '{VersionId}' of '{SecretName}' in vault '{VaultUri}'",
                            version.Version, this.secretName, this.vaultUri);
                    }
                    else
                    {
                        version.Enabled = false;
                    }
                }

                version.Tags.Remove(RevokeAfterTag);
                version.Tags[RevokedTag] = "true";

                if (whatIf)
                {
                    this.logger.LogInformation(
                        $"WHAT IF: Remove tag '{RevokeAfterTag}' and set tag '{RevokedTag}' to true on secret '{{SecretName}}' in vault '{{VaultUri}}'",
                        this.secretName, this.vaultUri);

                    return;
                }

                await this.secretClient.UpdateSecretPropertiesAsync(version);
            }
        };
    }

    public override async Task MarkRotationCompleteAsync(SecretValue secretValue, DateTimeOffset? revokeAfterDate, bool whatIf)
    {
        if (whatIf)
        {
            this.logger.LogInformation(
                "WHAT IF: Add tag 'rotation-complete' to secret '{SecretName}' in vault '{Vault}'",
                this.secretName, this.vaultUri);
            return;
        }

        if (secretValue.PrimaryState is not KeyVaultSecret secret)
        {
            throw new RotationException(
                "The PrimaryState value passed to KeyVaultSecretStore was not of type KeyVaultSecret");
        }

        this.logger.LogInformation("Adding tag 'rotation-complete' to secret '{SecretName}' in vault '{Vault}'",
            this.secretName, this.vaultUri);

        secret.Properties.Tags.Add("rotation-complete", "true");

        await this.secretClient.UpdateSecretPropertiesAsync(secret.Properties);
    }

    private static bool TryGetRevokeAfterDate(IDictionary<string, string> tags, out DateTimeOffset value)
    {
        if (tags.TryGetValue(RevokeAfterTag, out string? tagValue))
        {
            if (DateTimeOffset.TryParse(tagValue, out value))
            {
                return true;
            }
        }

        value = default;
        return false;
    }

    private async Task SetRevokeAfterForOldVersionsAsync(string currentVersionId, DateTimeOffset revokeAfterDate)
    {
        AsyncPageable<SecretProperties>? allSecretVersions =
            this.secretClient.GetPropertiesOfSecretVersionsAsync(this.secretName);

        await foreach (SecretProperties version in allSecretVersions)
        {
            if (version.Version == currentVersionId)
            {
                // Skip the current version
                continue;
            }

            if (TryGetRevokeAfterDate(version.Tags, out DateTimeOffset existingRevokeAfterDate) &&
                existingRevokeAfterDate < revokeAfterDate)
            {
                // Skip if already revoking before revokeAfterDate
                continue;
            }

            version.Tags[RevokeAfterTag] = revokeAfterDate.ToString("O");
            await this.secretClient.UpdateSecretPropertiesAsync(version);
        }
    }

    private string GetExceptionMessage(RequestFailedException exception)
    {
        if (exception.Status == 403)
        {
            return $"Key vault request not authorized for vault '{this.vaultUri}'";
        }

        if (exception.Status == 404)
        {
            if (exception.Message.StartsWith($"A secret with (name/id) "))
            {
                return $"Secret '{this.secretName}' not found in vault '{this.vaultUri}'";
            }

            return $"Vault {this.vaultUri} not found.";
        }

        return $"Key vault request failed with code {exception.Status}: {exception.Message}";
    }

    private class Parameters
    {
        [JsonPropertyName("secretUri")]
        public string? SecretUri { get; set; }

        [JsonPropertyName("revocationAction")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public RevocationAction RevocationAction { get; set; }
    }
}
