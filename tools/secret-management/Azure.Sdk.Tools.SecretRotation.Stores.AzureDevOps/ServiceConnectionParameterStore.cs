using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Core;
using Azure.Sdk.Tools.SecretRotation.Configuration;
using Azure.Sdk.Tools.SecretRotation.Core;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Services.Client;
using Microsoft.VisualStudio.Services.ServiceEndpoints.WebApi;
using Microsoft.VisualStudio.Services.WebApi;

namespace Azure.Sdk.Tools.SecretRotation.Stores.AzureDevOps;

public class ServiceConnectionParameterStore : SecretStore
{
    public const string MappingKey = "ADO Service Connection Parameter";

    private readonly string accountName;
    private readonly string connectionId;
    private readonly TokenCredential credential;
    private readonly ILogger logger;
    private readonly string parameterName;
    private readonly string projectName;

    public ServiceConnectionParameterStore(string accountName, string projectName, string connectionId,
        string parameterName, TokenCredential credential, ILogger logger)
    {
        this.accountName = accountName;
        this.projectName = projectName;
        this.connectionId = connectionId;
        this.parameterName = parameterName;
        this.credential = credential;
        this.logger = logger;
    }

    public override bool CanWrite => true;

    public static Func<StoreConfiguration, SecretStore> GetSecretStoreFactory(TokenCredential credential,
        ILogger logger)
    {
        return configuration =>
        {
            var parameters = configuration.Parameters?.Deserialize<Parameters>();

            if (string.IsNullOrEmpty(parameters?.AccountName))
            {
                throw new Exception("Missing required parameter 'accountName'");
            }

            if (string.IsNullOrEmpty(parameters?.ProjectName))
            {
                throw new Exception("Missing required parameter 'projectName'");
            }

            if (string.IsNullOrEmpty(parameters?.ConnectionId))
            {
                throw new Exception("Missing required parameter 'connectionId'");
            }

            if (string.IsNullOrEmpty(parameters?.ParameterName))
            {
                throw new Exception("Missing required parameter 'parameterName'");
            }

            return new ServiceConnectionParameterStore(
                parameters.AccountName,
                parameters.ProjectName,
                parameters.ConnectionId,
                parameters.ParameterName,
                credential,
                logger);
        };
    }

    public override async Task WriteSecretAsync(SecretValue secretValue, SecretState currentState,
        DateTimeOffset? revokeAfterDate, bool whatIf)
    {
        this.logger.LogDebug("Getting token and devops client for dev.azure.com/{AccountName}", this.accountName);
        VssConnection connection = await GetConnectionAsync();
        var client = await connection.GetClientAsync<ServiceEndpointHttpClient>();

        Guid endpointId = Guid.Parse(this.connectionId);

        this.logger.LogDebug("Getting service endpoint details for endpoint '{EndpointId}'", endpointId);
        ServiceEndpoint? endpointDetails = await client.GetServiceEndpointDetailsAsync(this.projectName, endpointId);

        if (endpointDetails == null || endpointDetails.Authorization == null)
        {
            throw new RotationException($"Unable to access endpoint '{endpointId}' so updating it in DevOps failed");
        }

        endpointDetails.Authorization.Parameters[this.parameterName] = secretValue.Value;

        if (!whatIf)
        {
            this.logger.LogInformation(
                "Updating parameter '{ParameterName}' on service connection '{ConnectionId}'",
                this.parameterName,
                 this.connectionId);
            await client.UpdateServiceEndpointAsync(endpointId, endpointDetails);
        }
        else
        {
            this.logger.LogInformation(
                "WHAT IF: Update parameter '{ParameterName}' on service connection '{ConnectionId}'", this.parameterName,
                this.connectionId);
        }
    }

    private async Task<VssConnection> GetConnectionAsync()
    {
        string[] scopes = { "499b84ac-1321-427f-aa17-267ca6975798/.default" };
        string? parentRequestId = null;

        var tokenRequestContext = new TokenRequestContext(scopes, parentRequestId);

        AccessToken authenticationResult = await this.credential.GetTokenAsync(
            tokenRequestContext,
            CancellationToken.None);

        var connection = new VssConnection(
            new Uri($"https://dev.azure.com/{this.accountName}"),
            new VssAadCredential(new VssAadToken("Bearer", authenticationResult.Token)));

        await connection.ConnectAsync();

        return connection;
    }

    private class Parameters
    {
        [JsonPropertyName("accountName")]
        public string? AccountName { get; set; }

        [JsonPropertyName("projectName")]
        public string? ProjectName { get; set; }

        [JsonPropertyName("connectionId")]
        public string? ConnectionId { get; set; }

        [JsonPropertyName("parameterName")]
        public string? ParameterName { get; set; }
    }
}
