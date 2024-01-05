using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Core;
using Azure.Sdk.Tools.SecretRotation.Configuration;
using Azure.Sdk.Tools.SecretRotation.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Applications.Item.AddPassword;
using Microsoft.Graph.Applications.Item.RemovePassword;
using Microsoft.Graph.Models;

namespace Azure.Sdk.Tools.SecretRotation.Stores.AzureActiveDirectory;

public class AadApplicationSecretStore : SecretStore
{
    public enum RevocationAction
    {
        None = 0,
        Delete = 1
    }

    public const string MappingKey = "AAD Application Secret";

    private readonly string applicationId;
    private readonly TokenCredential credential;
    private readonly string displayName;
    private readonly ILogger logger;
    private readonly RevocationAction revocationAction;

    public AadApplicationSecretStore(string applicationId, string displayName, RevocationAction revocationAction,
        TokenCredential credential, ILogger logger)
    {
        this.applicationId = applicationId;
        this.displayName = displayName;
        this.revocationAction = revocationAction;
        this.credential = credential;
        this.logger = logger;
    }

    public override bool CanOriginate => true;

    public override bool CanRevoke => true;

    public static Func<StoreConfiguration, SecretStore> GetSecretStoreFactory(TokenCredential credential,
        ILogger logger)
    {
        return configuration =>
        {
            var parameters = configuration.Parameters?.Deserialize<Parameters>();

            if (string.IsNullOrEmpty(parameters?.ApplicationId))
            {
                throw new Exception("Missing required parameter 'applicationId'");
            }

            if (string.IsNullOrEmpty(parameters?.DisplayName))
            {
                throw new Exception("Missing required parameter 'displayName'");
            }

            return new AadApplicationSecretStore(parameters.ApplicationId, parameters.DisplayName,
                parameters.RevocationAction, credential, logger);
        };
    }

    public override async Task<SecretValue> OriginateValueAsync(SecretState currentState, DateTimeOffset expirationDate,
        bool whatIf)
    {
        GraphServiceClient graphClient = GetGraphServiceClient();

        Application? application = await GetApplicationAsync(graphClient) ??
            throw new RotationException($"Unable to locate AAD application with id '{this.applicationId}'");
        
        this.logger.LogInformation("Found AAD application with id '{ApplicationId}', object id '{ObjectId}'",
            this.applicationId,
            application.Id);

        var requestBody = new AddPasswordPostRequestBody
        {
            PasswordCredential = new PasswordCredential
            {
                DisplayName = this.displayName, 
                StartDateTime = DateTimeOffset.UtcNow, 
                EndDateTime = expirationDate
            }
        };

        if (whatIf)
        {
            this.logger.LogInformation(
                "WHAT IF: Post 'add password' request to graph api for application object id '{ObjectId}'",
                application.Id);

            return new SecretValue { Value = string.Empty, ExpirationDate = expirationDate };
        }

        this.logger.LogInformation("Posting new password request to graph api for application object id '{ObjectId}'",
            application.Id);

        PasswordCredential newSecret =
            await graphClient.Applications[application.Id].AddPassword.PostAsync(requestBody) ??
            throw new RotationException($"Unable to create new password credential for AAD application with id '{this.applicationId}' and password name of '{this.displayName}'");

        this.logger.LogInformation("Graph api responded with key id '{KeyId}'", newSecret.KeyId);

        string keyId = newSecret.KeyId.ToString()!;

        return new SecretValue
        {
            Value = newSecret.SecretText ?? string.Empty,
            ExpirationDate = newSecret.EndDateTime,
            Tags = { ["AadApplicationId"] = this.applicationId, ["AadSecretId"] = keyId }
        };
    }

    public override Func<Task>? GetRevocationActionAsync(SecretState secretState, bool whatIf)
    {
        if (!secretState.Tags.TryGetValue("AadSecretId", out string? keyIdString) ||
            this.revocationAction != RevocationAction.Delete)
        {
            return null;
        }

        if (!Guid.TryParse(keyIdString, out Guid aadKeyId))
        {
            this.logger.LogWarning("Unable to parse OperationId as a Guid: '{OperationId}'", secretState.OperationId);
            return null;
        }

        return async () =>
        {
            GraphServiceClient graphClient = GetGraphServiceClient();

            Application application = await GetApplicationAsync(graphClient)
                ?? throw new RotationException($"Unable to locate AAD application with id '{this.applicationId}'");

            // use the application's object id and the keyId to revoke the old password
            if (whatIf)
            {
                this.logger.LogInformation(
                    "WHAT IF: Post 'remove password' request to graph api for application object id '{ObjectId}' and password id '{PasswordId}'",
                    application.Id,
                    aadKeyId);
            }
            else
            {
                try
                {
                    var requestBody = new RemovePasswordPostRequestBody
                    {
                        KeyId = aadKeyId,
                    };

                    await graphClient.Applications[application.Id].RemovePassword.PostAsync(requestBody);
                }
                catch (ServiceException ex) when
                    (ex.ToString().Contains("No password credential found with keyId"))
                {
                    // ignore "not found" exception on delete
                }
            }
        };
    }

    private async Task<Application?> GetApplicationAsync(GraphServiceClient graphClient)
    {
        this.logger.LogInformation("Getting details for application '{ApplicationId}' from graph api.",
            this.applicationId);
        return await graphClient.ApplicationsWithAppId(this.applicationId).GetAsync();
    }

    private GraphServiceClient GetGraphServiceClient()
    {
        return new GraphServiceClient(this.credential);
    }

    private class Parameters
    {
        [JsonPropertyName("applicationId")]
        public string? ApplicationId { get; set; }

        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("revocationAction")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public RevocationAction RevocationAction { get; set; }
    }
}
