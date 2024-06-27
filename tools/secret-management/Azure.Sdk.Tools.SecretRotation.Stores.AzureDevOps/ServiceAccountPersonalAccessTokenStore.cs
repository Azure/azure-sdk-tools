using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Core;
using Azure.Sdk.Tools.SecretRotation.Azure;
using Azure.Sdk.Tools.SecretRotation.Configuration;
using Azure.Sdk.Tools.SecretRotation.Core;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Services.DelegatedAuthorization;
using Microsoft.VisualStudio.Services.DelegatedAuthorization.Client;
using Microsoft.VisualStudio.Services.WebApi;

namespace Azure.Sdk.Tools.SecretRotation.Stores.AzureDevOps;

public class ServiceAccountPersonalAccessTokenStore : AzureDevOpsStore
{
    public const string MappingKey = "Service Account ADO PAT";

    private readonly string patDisplayName;
    private readonly string scopes;
    private readonly RevocationAction revocationAction;

    private ServiceAccountPersonalAccessTokenStore(
        ILogger logger,
        string organization,
        string patDisplayName,
        string scopes,
        string serviceAccountTenantId,
        string serviceAccountName,
        Uri serviceAccountPasswordSecret,
        RevocationAction revocationAction,
        TokenCredential tokenCredential,
        ISecretProvider secretProvider)
        : base(
            logger,
            organization,
            serviceAccountTenantId,
            serviceAccountName,
            serviceAccountPasswordSecret,
            tokenCredential,
            secretProvider)
    {
        this.patDisplayName = patDisplayName;
        this.scopes = scopes;
        this.revocationAction = revocationAction;
    }

    public override bool CanOriginate => true;

    public override bool CanRevoke => true;

    public static Func<StoreConfiguration, SecretStore> GetSecretStoreFactory(TokenCredential credential, ISecretProvider secretProvider, ILogger logger)
    {
        return configuration =>
        {
            var parameters = configuration.Parameters?.Deserialize<Parameters>();

            if (string.IsNullOrEmpty(parameters?.Organization))
            {
                throw new Exception("Missing required parameter 'organization'");
            }

            if (string.IsNullOrEmpty(parameters.PatDisplayName))
            {
                throw new Exception("Missing required parameter 'patDisplayName'");
            }

            if (string.IsNullOrEmpty(parameters.Scopes))
            {
                throw new Exception("Missing required parameter 'scopes'");
            }

            if (string.IsNullOrEmpty(parameters.ServiceAccountTenantId))
            {
                throw new Exception("Missing required parameter 'serviceAccountTenantId'");
            }

            if (string.IsNullOrEmpty(parameters.ServiceAccountName))
            {
                throw new Exception("Missing required parameter 'serviceAccountName'");
            }

            if (string.IsNullOrEmpty(parameters.ServiceAccountPasswordSecret))
            {
                throw new Exception("Missing required parameter 'serviceAccountPasswordSecret'");
            }

            if (!Uri.TryCreate(parameters.ServiceAccountPasswordSecret, UriKind.Absolute, out Uri? serviceAccountPasswordSecret))
            {
                throw new Exception("Unable to parse Uri from parameter 'serviceAccountPasswordSecret'");
            }

            return new ServiceAccountPersonalAccessTokenStore(
                logger,
                parameters.Organization,
                parameters.PatDisplayName,
                parameters.Scopes,
                parameters.ServiceAccountTenantId,
                parameters.ServiceAccountName,
                serviceAccountPasswordSecret!,
                parameters.RevocationAction,
                credential,
                secretProvider);
        };
    }

    public override async Task<SecretValue> OriginateValueAsync(SecretState currentState, DateTimeOffset expirationDate, bool whatIf)
    {
        VssConnection connection = await GetConnectionAsync();
        TokensHttpClient client = connection.GetClient<TokensHttpClient>();

        if (whatIf)
        {
            this.logger.LogInformation("WHAT IF: Post tokens/pat request to Azure DevOps");

            return new SecretValue { Value = string.Empty, ExpirationDate = expirationDate };
        }

        this.logger.LogInformation("Posting pat creation request to '{OrganizationUrl}'",
        $"https://dev.azure.com/{this.organization}");

        PatTokenResult result = await client.CreatePatAsync(new PatTokenCreateRequest { AllOrgs = false, DisplayName = this.patDisplayName, Scope = this.scopes, ValidTo = expirationDate.UtcDateTime });

        if (result.PatTokenError != SessionTokenError.None)
        {
            throw new RotationException($"Unable to create PAT: {result.PatTokenError}");
        }

        string authorizationId = result.PatToken.AuthorizationId.ToString();

        this.logger.LogInformation("Azure DevOps responded with authorization id '{AuthorizationId}'", authorizationId);

        return new SecretValue
        {
            Value = result.PatToken.Token,
            ExpirationDate = result.PatToken.ValidTo,
            Tags = { ["AdoPatAuthorizationId"] = authorizationId }
        };
    }

    public override Func<Task>? GetRevocationActionAsync(SecretState secretState, bool whatIf)
    {
        if (!secretState.Tags.TryGetValue("AdoPatAuthorizationId", out string? authorizationIdString) ||
            this.revocationAction != RevocationAction.Revoke)
        {
            return null;
        }

        if (!Guid.TryParse(authorizationIdString, out Guid authorizationId))
        {
            this.logger.LogWarning("Unable to parse Authorization Id as a Guid: '{AuthorizationId}'", authorizationIdString);
            return null;
        }

        return async () =>
        {
            VssConnection connection = await GetConnectionAsync();
            TokensHttpClient client = connection.GetClient<TokensHttpClient>();

            // use the application's object id and the keyId to revoke the old password
            if (whatIf)
            {
                this.logger.LogInformation(
                    "WHAT IF: Remove PAT with authorization id '{AuthorizationId}' from '{OrganizationUrl}'",
                    authorizationId,
                    $"https://dev.azure.com/{this.organization}");

                return;
            }

            try
            {
                this.logger.LogInformation(
                    "Removing PAT with authorization id '{AuthorizationId}' from '{OrganizationUrl}'",
                    authorizationId,
                    $"https://dev.azure.com/{this.organization}");

                await client.RevokeAsync(authorizationId);
            }
            catch (SessionTokenNotFoundException)
            {
                // ignore "not found" exception on delete
            }
        };
    }

    private enum RevocationAction
    {
        [JsonPropertyName("none")]
        None = 0,

        [JsonPropertyName("revoke")]
        Revoke = 1
    }

    private class Parameters
    {
        [JsonPropertyName("organization")]
        public string? Organization { get; set; }

        [JsonPropertyName("patDisplayName")]
        public string? PatDisplayName { get; set; }

        [JsonPropertyName("scopes")]
        public string? Scopes { get; set; }

        [JsonPropertyName("serviceAccountTenantId")]
        public string? ServiceAccountTenantId { get; set; }

        [JsonPropertyName("serviceAccountName")]
        public string? ServiceAccountName { get; set; }

        [JsonPropertyName("serviceAccountPasswordSecret")]
        public string? ServiceAccountPasswordSecret { get; set; }

        [JsonPropertyName("revocationAction")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public RevocationAction RevocationAction { get; set; }
    }
}
