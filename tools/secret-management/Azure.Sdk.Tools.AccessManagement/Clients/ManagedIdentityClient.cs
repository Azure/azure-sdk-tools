using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.ManagedServiceIdentities;
using Azure.ResourceManager.Resources;
using System.Text.Json;

namespace Azure.Sdk.Tools.AccessManagement;

public record ManagedIdentityInfo(Guid ClientId, Guid PrincipalId, Guid TenantId);

public record FederatedCredentialInfo(string Name, string Issuer, string Subject, IReadOnlyList<string> Audiences);

public class ManagedIdentityClient : IManagedIdentityClient
{
    private ArmClient ArmClient { get; }
    private ILogger Log { get; }

    private TokenCredential Credential { get; }

    public ManagedIdentityClient(ILogger logger, TokenCredential credential)
    {
        Log = logger;
        Credential = credential;
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
        // Use a direct ARM REST call instead of the SDK's FederatedIdentityCredentialData
        // because its IssuerUri property is typed as System.Uri, which normalizes
        // "https://token.actions.githubusercontent.com" to include a trailing slash.
        // Azure Policy performs exact string matching on the issuer and rejects the
        // trailing slash variant with RequestDisallowedByPolicy.
        var url = $"https://management.azure.com/subscriptions/{subscriptionId}" +
                  $"/resourceGroups/{resourceGroup}" +
                  $"/providers/Microsoft.ManagedIdentity/userAssignedIdentities/{identityName}" +
                  $"/federatedIdentityCredentials/{config.Name}?api-version=2023-01-31";

        var body = new
        {
            properties = new
            {
                issuer = config.Issuer!,
                subject = config.Subject!,
                audiences = config.Audiences ?? new List<string>()
            }
        };

        var token = await Credential.GetTokenAsync(
            new TokenRequestContext(new[] { "https://management.azure.com/.default" }), default);

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);

        var json = JsonSerializer.Serialize(body);
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        Log.LogInformation($"Creating federated identity credential '{config.Name}' for identity '{identityName}'...");
        var response = await httpClient.PutAsync(url, content);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new RequestFailedException((int)response.StatusCode, errorBody);
        }

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
