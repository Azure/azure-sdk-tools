using Azure.Core;
using Azure.Identity;
using Azure.Sdk.Tools.SecretRotation.Azure;
using Azure.Sdk.Tools.SecretRotation.Core;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Services.Client;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using AccessToken = Azure.Core.AccessToken;

namespace Azure.Sdk.Tools.SecretRotation.Stores.AzureDevOps;

public abstract class AzureDevOpsStore : SecretStore
{
    private const string AzureDevOpsApplicationId = "499b84ac-1321-427f-aa17-267ca6975798";
    private const string AzureCliApplicationId = "04b07795-8ddb-461a-bbee-02f9e1bf7b46";

    protected readonly ILogger logger;
    protected readonly string organization;
    private readonly string? serviceAccountTenantId;
    private readonly string? serviceAccountName;
    private readonly Uri? serviceAccountPasswordSecret;
    private readonly TokenCredential tokenCredential;
    private readonly ISecretProvider secretProvider;

    protected AzureDevOpsStore(ILogger logger,
        string organization,
        string? serviceAccountTenantId,
        string? serviceAccountName,
        Uri? serviceAccountPasswordSecret,
        TokenCredential tokenCredential,
        ISecretProvider secretProvider)
    {
        this.logger = logger;
        this.organization = organization;
        this.serviceAccountTenantId = serviceAccountTenantId;
        this.serviceAccountName = serviceAccountName;
        this.serviceAccountPasswordSecret = serviceAccountPasswordSecret;
        this.tokenCredential = tokenCredential;
        this.secretProvider = secretProvider;
    }

    protected async Task<VssConnection> GetConnectionAsync()
    {
        string[] scopes = { "499b84ac-1321-427f-aa17-267ca6975798/.default" };
        string? parentRequestId = null;

        VssCredentials vssCredentials = this.serviceAccountPasswordSecret != null
            ? await GetServiceAccountCredentialsAsync()
            : await GetTokenPrincipleCredentialsAsync(scopes, parentRequestId);

        VssConnection connection = new(new Uri($"https://vssps.dev.azure.com/{this.organization}"), vssCredentials);

        await connection.ConnectAsync();

        return connection;
    }

    private static async Task<AccessToken> GetDevopsAadBearerTokenAsync(TokenCredential credential)
    {
        string[] scopes = { $"{AzureDevOpsApplicationId}/.default" };

        var tokenRequestContext = new TokenRequestContext(scopes, parentRequestId: null);

        AccessToken authenticationResult = await credential.GetTokenAsync(tokenRequestContext, CancellationToken.None);

        return authenticationResult;
    }

    private async Task<VssCredentials> GetTokenPrincipleCredentialsAsync(string[] scopes, string? parentRequestId)
    {
        TokenRequestContext tokenRequestContext = new (scopes, parentRequestId);

        AccessToken authenticationResult = await this.tokenCredential.GetTokenAsync(
            tokenRequestContext,
            CancellationToken.None);

        VssAadCredential credentials = new (new VssAadToken("Bearer", authenticationResult.Token));

        return credentials;
    }

    private async Task<VssCredentials> GetServiceAccountCredentialsAsync()
    {
        if (this.serviceAccountPasswordSecret == null)
        {
            throw new ArgumentNullException(nameof(serviceAccountPasswordSecret));
        }

        this.logger.LogDebug("Getting service account password from secret '{SecretName}'", this.serviceAccountPasswordSecret);

        string serviceAccountPassword = await this.secretProvider.GetSecretValueAsync(this.tokenCredential, this.serviceAccountPasswordSecret);

        this.logger.LogDebug("Getting token and devops client for 'dev.azure.com/{Organization}'", this.organization);

        UsernamePasswordCredential serviceAccountCredential = new (this.serviceAccountName, serviceAccountPassword, this.serviceAccountTenantId, AzureCliApplicationId);

        AccessToken token = await GetDevopsAadBearerTokenAsync(serviceAccountCredential);

        VssAadCredential credentials = new (new VssAadToken("Bearer", token.Token));

        return credentials;
    }
}
