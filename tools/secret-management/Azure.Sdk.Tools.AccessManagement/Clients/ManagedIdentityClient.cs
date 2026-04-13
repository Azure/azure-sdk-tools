using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.ManagedServiceIdentities;
using Azure.ResourceManager.Resources;

namespace Azure.Sdk.Tools.AccessManagement;

public record ManagedIdentityInfo(Guid ClientId, Guid PrincipalId, Guid TenantId);

public record FederatedCredentialInfo(string Name, string Issuer, string Subject, IReadOnlyList<string> Audiences);

public class ManagedIdentityClient : IManagedIdentityClient
{
    private ArmClient ArmClient { get; }
    private ILogger Log { get; }

    public ManagedIdentityClient(ILogger logger, TokenCredential credential)
    {
        Log = logger;
        ArmClient = new ArmClient(credential);
    }

    public async Task<ManagedIdentityInfo?> GetManagedIdentity(string subscriptionId, string resourceGroup, string identityName)
    {
        try
        {
            var resourceId = UserAssignedIdentityResource.CreateResourceIdentifier(subscriptionId, resourceGroup, identityName);
            var resource = ArmClient.GetUserAssignedIdentityResource(resourceId);
            var response = await resource.GetAsync();
            var data = response.Value.Data;
            Log.LogInformation($"Found managed identity '{identityName}' with clientId '{data.ClientId}' and principalId '{data.PrincipalId}'");
            return new ManagedIdentityInfo(data.ClientId ?? Guid.Empty, data.PrincipalId ?? Guid.Empty, data.TenantId ?? Guid.Empty);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<ManagedIdentityInfo> CreateManagedIdentity(string subscriptionId, string resourceGroup, string identityName, string location)
    {
        var subscription = ArmClient.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(subscriptionId));
        var rg = await subscription.GetResourceGroupAsync(resourceGroup);
        var collection = rg.Value.GetUserAssignedIdentities();
        var identityData = new UserAssignedIdentityData(new AzureLocation(location));

        Log.LogInformation($"Creating managed identity '{identityName}' in resource group '{resourceGroup}'...");
        var result = await collection.CreateOrUpdateAsync(WaitUntil.Completed, identityName, identityData);
        var data = result.Value.Data;
        Log.LogInformation($"Created managed identity '{identityName}' with clientId '{data.ClientId}' and principalId '{data.PrincipalId}'");

        return new ManagedIdentityInfo(data.ClientId ?? Guid.Empty, data.PrincipalId ?? Guid.Empty, data.TenantId ?? Guid.Empty);
    }

    public async Task<List<FederatedCredentialInfo>> ListFederatedIdentityCredentials(string subscriptionId, string resourceGroup, string identityName)
    {
        var resourceId = UserAssignedIdentityResource.CreateResourceIdentifier(subscriptionId, resourceGroup, identityName);
        var resource = ArmClient.GetUserAssignedIdentityResource(resourceId);
        var collection = resource.GetFederatedIdentityCredentials();
        var results = new List<FederatedCredentialInfo>();

        Log.LogInformation($"Listing federated identity credentials for identity '{identityName}'...");
        await foreach (var credential in collection.GetAllAsync())
        {
            var data = credential.Data;
            var info = new FederatedCredentialInfo(
                data.Name,
                data.IssuerUri?.ToString() ?? string.Empty,
                data.Subject ?? string.Empty,
                data.Audiences?.ToList() ?? new List<string>());
            results.Add(info);
        }

        Log.LogInformation($"Found {results.Count} federated identity credentials");
        return results;
    }

    public async Task CreateFederatedIdentityCredential(
        string subscriptionId, string resourceGroup, string identityName,
        FederatedIdentityCredentialsConfig config)
    {
        var resourceId = UserAssignedIdentityResource.CreateResourceIdentifier(subscriptionId, resourceGroup, identityName);
        var resource = ArmClient.GetUserAssignedIdentityResource(resourceId);
        var collection = resource.GetFederatedIdentityCredentials();

        var credentialData = new FederatedIdentityCredentialData
        {
            IssuerUri = new Uri(config.Issuer!),
            Subject = config.Subject!,
        };
        foreach (var audience in config.Audiences ?? Enumerable.Empty<string>())
        {
            credentialData.Audiences.Add(audience);
        }

        Log.LogInformation($"Creating federated identity credential '{config.Name}' for identity '{identityName}'...");
        await collection.CreateOrUpdateAsync(WaitUntil.Completed, config.Name!, credentialData);
        Log.LogInformation($"Created federated identity credential '{config.Name}' for identity '{identityName}'");
    }

    public async Task DeleteFederatedIdentityCredential(
        string subscriptionId, string resourceGroup, string identityName,
        string credentialName)
    {
        var resourceId = UserAssignedIdentityResource.CreateResourceIdentifier(subscriptionId, resourceGroup, identityName);
        var resource = ArmClient.GetUserAssignedIdentityResource(resourceId);

        Log.LogInformation($"Deleting federated identity credential '{credentialName}' for identity '{identityName}'...");
        var credential = await resource.GetFederatedIdentityCredentialAsync(credentialName);
        await credential.Value.DeleteAsync(WaitUntil.Completed);
        Log.LogInformation($"Deleted federated identity credential '{credentialName}' for identity '{identityName}'");
    }
}

public interface IManagedIdentityClient
{
    Task<ManagedIdentityInfo?> GetManagedIdentity(string subscriptionId, string resourceGroup, string identityName);
    Task<ManagedIdentityInfo> CreateManagedIdentity(string subscriptionId, string resourceGroup, string identityName, string location);
    Task<List<FederatedCredentialInfo>> ListFederatedIdentityCredentials(string subscriptionId, string resourceGroup, string identityName);
    Task CreateFederatedIdentityCredential(string subscriptionId, string resourceGroup, string identityName, FederatedIdentityCredentialsConfig config);
    Task DeleteFederatedIdentityCredential(string subscriptionId, string resourceGroup, string identityName, string credentialName);
}
