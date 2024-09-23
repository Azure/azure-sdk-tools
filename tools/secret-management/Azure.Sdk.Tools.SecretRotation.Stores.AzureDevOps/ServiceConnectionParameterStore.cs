using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Core;
using Azure.Sdk.Tools.SecretRotation.Azure;
using Azure.Sdk.Tools.SecretRotation.Configuration;
using Azure.Sdk.Tools.SecretRotation.Core;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Services.ServiceEndpoints.WebApi;
using Microsoft.VisualStudio.Services.WebApi;

namespace Azure.Sdk.Tools.SecretRotation.Stores.AzureDevOps;

public class ServiceConnectionParameterStore : AzureDevOpsStore
{
    public const string MappingKey = "ADO Service Connection Parameter";

    private readonly string connectionId;
    private readonly string parameterName;
    private readonly string projectName;

    public ServiceConnectionParameterStore(
        string organization,
        string projectName,
        string connectionId,
        string parameterName,
        string? serviceAccountTenantId,
        string? serviceAccountName,
        Uri? serviceAccountPasswordSecret,
        TokenCredential tokenCredential,
        ISecretProvider secretProvider,
        ILogger logger)
        : base(
            logger,
            organization,
            serviceAccountTenantId,
            serviceAccountName,
            serviceAccountPasswordSecret,
            tokenCredential,
            secretProvider)
    {
        this.projectName = projectName;
        this.connectionId = connectionId;
        this.parameterName = parameterName;
    }

    public override bool CanWrite => true;

    public static Func<StoreConfiguration, SecretStore> GetSecretStoreFactory(TokenCredential credential,
        ISecretProvider secretProvider,
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

            var passwordSecretParsed = Uri.TryCreate(parameters.ServiceAccountPasswordSecret, UriKind.Absolute, out Uri? serviceAccountPasswordSecret);

            if (!string.IsNullOrEmpty(parameters.ServiceAccountName))
            {
                if (string.IsNullOrEmpty(parameters.ServiceAccountPasswordSecret))
                {
                    throw new Exception("Missing required parameter 'serviceAccountPasswordSecret'");
                }

                if (!passwordSecretParsed)
                {
                    throw new Exception("Unable to parse Uri from parameter 'serviceAccountPasswordSecret'");
                }
            }

            return new ServiceConnectionParameterStore(
                parameters.AccountName,
                parameters.ProjectName,
                parameters.ConnectionId,
                parameters.ParameterName,
                parameters.ServiceAccountTenantId,
                parameters.ServiceAccountName,
                serviceAccountPasswordSecret,
                credential,
                secretProvider,
                logger);
        };
    }

    public override async Task WriteSecretAsync(SecretValue secretValue, SecretState currentState,
        DateTimeOffset? revokeAfterDate, bool whatIf)
    {
        this.logger.LogDebug("Getting token and devops client for dev.azure.com/{Organization}", this.organization);
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

        [JsonPropertyName("serviceAccountTenantId")]
        public string? ServiceAccountTenantId { get; set; }

        [JsonPropertyName("serviceAccountName")]
        public string? ServiceAccountName { get; set; }

        [JsonPropertyName("serviceAccountPasswordSecret")]
        public string? ServiceAccountPasswordSecret { get; set; }

    }
}
