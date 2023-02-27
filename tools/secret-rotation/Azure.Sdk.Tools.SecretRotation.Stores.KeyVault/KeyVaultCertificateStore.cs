using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Azure.Core;
using Azure.Sdk.Tools.SecretRotation.Configuration;
using Azure.Sdk.Tools.SecretRotation.Core;
using Azure.Security.KeyVault.Certificates;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.SecretRotation.Stores.KeyVault;

public class KeyVaultCertificateStore : SecretStore
{
    public const string MappingKey = "Key Vault Certificate";

    private static readonly Regex uriRegex = new(
        @"^(?<VaultUri>https://.+?)/certificates/(?<CertificateName>[^/]+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromSeconds(5));

    private readonly CertificateClient certificateClient;
    private readonly string certificateName;
    private readonly ILogger logger;
    private readonly Uri vaultUri;

    public KeyVaultCertificateStore(
        Uri vaultUri,
        string certificateName,
        TokenCredential credential,
        ILogger logger)
    {
        this.vaultUri = vaultUri;
        this.certificateName = certificateName;
        this.logger = logger;
        this.certificateClient = new CertificateClient(vaultUri, credential);
    }

    public override bool CanRead => true;

    public override bool CanOriginate => true;

    public override bool CanAnnotate => true;

    public override bool CanRevoke => true;

    public static Func<StoreConfiguration, SecretStore> GetSecretStoreFactory(TokenCredential credential,
        ILogger logger)
    {
        return configuration =>
        {
            var parameters = configuration.Parameters?.Deserialize<Parameters>();

            if (string.IsNullOrEmpty(parameters?.CertificateUri))
            {
                throw new Exception("Missing required parameter 'certificateUri'");
            }

            Match match = uriRegex.Match(parameters.CertificateUri);
            string vaultUrl = match.Groups["VaultUri"].Value;
            string certificateName = match.Groups["CertificateName"].Value;

            if (!match.Success || !Uri.TryCreate(vaultUrl, UriKind.Absolute, out Uri? vaultUri))
            {
                throw new Exception("Unable to parse parameter 'certificateUri'");
            }

            return new KeyVaultCertificateStore(vaultUri, certificateName, credential, logger);
        };
    }

    public override async Task<SecretState> GetCurrentStateAsync()
    {
        try
        {
            this.logger.LogDebug("Getting certificate '{SecretName}' from vault '{VaultUri}'", this.certificateName,
                this.vaultUri);
            Response<KeyVaultCertificateWithPolicy>? response =
                await this.certificateClient.GetCertificateAsync(this.certificateName);

            return new SecretState
            {
                ExpirationDate = response.Value.Properties.ExpiresOn, StatusCode = response.GetRawResponse().Status
            };
        }
        catch (RequestFailedException ex)
        {
            return new SecretState { ExpirationDate = null, StatusCode = ex.Status, ErrorMessage = ex.Message };
        }
    }

    public override async Task<SecretValue> OriginateValueAsync(SecretState currentState, DateTimeOffset expirationDate,
        bool whatIf)
    {
        this.logger.LogInformation("Getting certificate policy for certificate '{CertificateName}' in vault '{Vault}'",
            this.certificateName, this.vaultUri);

        Response<CertificatePolicy>? policy =
            await this.certificateClient.GetCertificatePolicyAsync(this.certificateName);

        if (whatIf)
        {
            this.logger.LogInformation(
                "WHAT IF: Create new version for certificate '{CertificateName}' in vault '{Vault}'", this.certificateName,
                this.vaultUri);
            return new SecretValue { ExpirationDate = expirationDate };
        }

        this.logger.LogInformation(
            "Starting new certificate operation for certificate '{CertificateName}' in vault '{Vault}'",
            this.certificateName, this.vaultUri);
        CertificateOperation? operation =
            await this.certificateClient.StartCreateCertificateAsync(this.certificateName, policy);

        this.logger.LogInformation("Waiting for certificate operation '{OperationId}' to complete", operation.Id);
        Response<KeyVaultCertificateWithPolicy>? response = await operation.WaitForCompletionAsync();

        string base64 = Convert.ToBase64String(response.Value.Cer);

        return new SecretValue
        {
            ExpirationDate = response.Value.Properties.ExpiresOn, OriginState = response.Value, Value = base64
        };
    }

    public override async Task MarkRotationCompleteAsync(SecretValue secretValue, DateTimeOffset? revokeAfterDate,
        bool whatIf)
    {
        if (whatIf)
        {
            this.logger.LogInformation(
                "WHAT IF: Add tag 'rotation-complete' to certificate '{CertificateName}' in vault '{Vault}'",
                this.certificateName, this.vaultUri);
            return;
        }

        if (secretValue.OriginState is not KeyVaultCertificateWithPolicy certificate)
        {
            throw new RotationException(
                "The OriginState value passed to KeyVaultCertificateStore was not of type KeyVaultCertificateWithPolicy");
        }

        this.logger.LogInformation("Adding tag 'rotation-complete' to certificate '{CertificateName}' in vault '{Vault}'",
            this.certificateName, this.vaultUri);

        certificate.Properties.Tags.Add("rotation-complete", "true");

        await this.certificateClient.UpdateCertificatePropertiesAsync(certificate.Properties);
    }

    public override async Task<IEnumerable<SecretState>> GetRotationArtifactsAsync()
    {
        var results = new List<SecretState>();

        await foreach (CertificateProperties? version in
                       this.certificateClient.GetPropertiesOfCertificateVersionsAsync(this.certificateName))
        {
            if (!version.Tags.TryGetValue("revokeAfter", out string? revokeAfterString))
            {
                continue;
            }

            if (!DateTimeOffset.TryParse(revokeAfterString, out DateTimeOffset revokeAfterDate))
            {
                // TODO: Warning
                continue;
            }

            results.Add(new SecretState { Tags = version.Tags, RevokeAfterDate = revokeAfterDate });
        }

        return results;
    }

    public override Func<Task>? GetRevocationActionAsync(SecretState secretState, bool whatIf)
    {
        throw new NotImplementedException();
    }

    private class Parameters
    {
        [JsonPropertyName("certificateUri")]
        public string? CertificateUri { get; set; }
    }
}
