using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Azure.Sdk.Tools.SecretRotation.Configuration;
using Azure.Sdk.Tools.SecretRotation.Core;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.SecretRotation.Stores.AzureAppService;

public class AzureWebsiteStore : SecretStore
{
    public const string MappingKey = "Azure Website";

    private readonly TokenCredential credential;
    private readonly ILogger logger;
    private readonly string subscriptionId;
    private readonly string resourceGroupName;
    private readonly string websiteName;
    private readonly RotationAction rotationAction;

    public AzureWebsiteStore(string subscriptionId, string resourceGroupName, string websiteName, RotationAction rotationAction,
        TokenCredential credential, ILogger logger)
    {
        this.subscriptionId = subscriptionId;
        this.resourceGroupName = resourceGroupName;
        this.websiteName = websiteName;
        this.rotationAction = rotationAction;
        this.credential = credential;
        this.logger = logger;
    }

    public override bool CanWrite => true;

    public static Func<StoreConfiguration, SecretStore> GetSecretStoreFactory(TokenCredential credential, ILogger logger)
    {
        return configuration =>
        {
            var parameters = configuration.Parameters?.Deserialize<Parameters>();

            if (string.IsNullOrEmpty(parameters?.SubscriptionId))
            {
                throw new Exception("Missing required parameter 'subscriptionId'");
            }

            if (string.IsNullOrEmpty(parameters?.ResourceGroup))
            {
                throw new Exception("Missing required parameter 'resourceGroup'");
            }

            if (string.IsNullOrEmpty(parameters?.Website))
            {
                throw new Exception("Missing required parameter 'website'");
            }

            return new AzureWebsiteStore(
                parameters.SubscriptionId,
                parameters.ResourceGroup,
                parameters.Website,
                parameters.RotationAction,
                credential,
                logger);
        };
    }

    public override async Task WriteSecretAsync(SecretValue secretValue, SecretState currentState, DateTimeOffset? revokeAfterDate, bool whatIf)
    {
        ResourceIdentifier resourceId = WebSiteResource.CreateResourceIdentifier(this.subscriptionId, this.resourceGroupName, this.websiteName);
        ArmClient client = new ArmClient(this.credential);
        WebSiteResource website = client.GetWebSiteResource(resourceId);

        if (this.rotationAction == RotationAction.None)
        {
            return;
        }

        if (!whatIf)
        {
            this.logger.LogInformation("Restarting Azure Website '{SubscriptionId}/{ResourceGroup}/{Website}'", this.subscriptionId, this.resourceGroupName, this.websiteName);

            await website.RestartAsync(softRestart: false, synchronous: true);
        }
        else
        {
            this.logger.LogInformation("WHAT IF: Restart Azure Website '{SubscriptionId}/{ResourceGroup}/{Website}'", this.subscriptionId, this.resourceGroupName, this.websiteName);
        }
    }

    public enum RotationAction
    {
        [JsonPropertyName("none")]
        None,

        [JsonPropertyName("restartWebsite")]
        RestartWebsite,
    }

    private class Parameters
    {
        [JsonPropertyName("subscriptionId")]
        public string? SubscriptionId { get; set; }

        [JsonPropertyName("resourceGroup")]
        public string? ResourceGroup { get; set; }

        [JsonPropertyName("website")]
        public string? Website { get; set; }

        [JsonPropertyName("rotationAction")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public RotationAction RotationAction { get; set; }
    }
}
