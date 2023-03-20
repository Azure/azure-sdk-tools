using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Core;
using Azure.Identity;
using Azure.Sdk.Tools.SecretRotation.Azure;
using Azure.Sdk.Tools.SecretRotation.Configuration;
using Azure.Sdk.Tools.SecretRotation.Core;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Services.Client;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.DelegatedAuthorization;
using Microsoft.VisualStudio.Services.DelegatedAuthorization.Client;
using Microsoft.VisualStudio.Services.WebApi;
using AccessToken = Azure.Core.AccessToken;

namespace Azure.Sdk.Tools.SecretRotation.Stores.AzureDevOps;

public class ServiceAccountPersonalAccessTokenStore : SecretStore
{
    public const string MappingKey = "Service Account ADO PAT";
    private const string AzureDevOpsApplicationId = "499b84ac-1321-427f-aa17-267ca6975798";
    private const string AzureCliApplicationId = "04b07795-8ddb-461a-bbee-02f9e1bf7b46";

    private readonly ILogger logger;
    private readonly string organization;
    private readonly string patDisplayName;
    private readonly string scopes;
    private readonly string serviceAccountTenantId;
    private readonly string serviceAccountName;
    private readonly Uri serviceAccountPasswordSecret;
    private readonly RevocationAction revocationAction;
    private readonly TokenCredential tokenCredential;
    private readonly ISecretProvider secretProvider;

    private ServiceAccountPersonalAccessTokenStore(ILogger logger,
        string organization,
        string patDisplayName,
        string scopes,
        string serviceAccountTenantId,
        string serviceAccountName,
        Uri serviceAccountPasswordSecret,
        RevocationAction revocationAction,
        TokenCredential tokenCredential,
        ISecretProvider secretProvider)
    {
        this.logger = logger;
        this.organization = organization;
        this.patDisplayName = patDisplayName;
        this.scopes = scopes;
        this.serviceAccountTenantId = serviceAccountTenantId;
        this.serviceAccountName = serviceAccountName;
        this.serviceAccountPasswordSecret = serviceAccountPasswordSecret;
        this.revocationAction = revocationAction;
        this.tokenCredential = tokenCredential;
        this.secretProvider = secretProvider;
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

        string authorizationId = result.PatToken.AuthorizationId.ToString();

        this.logger.LogInformation("Azure DevOps responded with authorization id '{AuthorizationId}'", authorizationId);

        return new SecretValue
        {
            Value = result.PatToken.Token,
            ExpirationDate = result.PatToken.ValidTo,
            Tags = { ["AdoPatAuthorizationId"] = authorizationId }
        };
    }

    private static async Task<AccessToken> GetDevopsBearerTokenAsync(TokenCredential credential)
    {
        string[] scopes = { $"{AzureDevOpsApplicationId}/.default" };

        var tokenRequestContext = new TokenRequestContext(scopes, parentRequestId: null);

        AccessToken authenticationResult = await credential.GetTokenAsync(tokenRequestContext, CancellationToken.None);

        return authenticationResult;
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
    
    private static async Task<VssCredentials> GetVssCredentials(TokenCredential credential)
    {
        AccessToken token = await GetDevopsBearerTokenAsync(credential);

        return new VssAadCredential(new VssAadToken("Bearer", token.Token));
    }

    private async Task<VssConnection> GetConnectionAsync()
    {
        this.logger.LogDebug("Getting service account password from secret '{SecretName}'", this.serviceAccountPasswordSecret);

        string serviceAccountPassword = await this.secretProvider.GetSecretValueAsync(this.tokenCredential, this.serviceAccountPasswordSecret);

        this.logger.LogDebug("Getting token and devops client for 'dev.azure.com/{Organization}'", this.organization);

        var serviceAccountCredential = new UsernamePasswordCredential(this.serviceAccountName, serviceAccountPassword, this.serviceAccountTenantId, AzureCliApplicationId);

        VssCredentials vssCredentials = await GetVssCredentials(serviceAccountCredential);

        var connection = new VssConnection(new Uri($"https://vssps.dev.azure.com/{this.organization}"), vssCredentials);

        await connection.ConnectAsync();

        return connection;
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
