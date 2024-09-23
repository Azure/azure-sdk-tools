using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace Azure.Sdk.Tools.AccessManagement;

/*
 * Wrapper for Microsoft.Graph GraphServiceClient
 */
public class GraphClient : IGraphClient
{
    public GraphServiceClient GraphServiceClient { get; }
    private ILogger Log { get; }

    public GraphClient(ILogger logger, DefaultAzureCredential credential)
    {
        Log = logger;
        GraphServiceClient = new GraphServiceClient(credential);
    }

    public async Task<Application?> GetApplicationByDisplayName(string displayName)
    {
        var result = await GraphServiceClient.Applications.GetAsync((requestConfiguration) =>
        {
            requestConfiguration.QueryParameters.Search = $"\"displayName:{displayName}\"";
            requestConfiguration.QueryParameters.Count = true;
            requestConfiguration.QueryParameters.Top = 1;
            requestConfiguration.QueryParameters.Orderby = new string []{ "displayName" };
            requestConfiguration.Headers.Add("ConsistencyLevel", "eventual");
        });

        return result?.Value?.FirstOrDefault();
    }

    public async Task<Application> CreateApplication(Application application)
    {
        var app = await GraphServiceClient.Applications.PostAsync(application);
        if (app is null)
        {
            throw new Exception($"Failed to create app with display name {application.DisplayName}, Graph API returned empty response.");
        }
        return app;
    }

    public async Task<ServicePrincipal?> GetApplicationServicePrincipal(Application app)
    {
        var sp = await GraphServiceClient.ServicePrincipals.GetAsync((requestConfiguration) =>
        {
            requestConfiguration.QueryParameters.Filter = $"appId eq '{app.AppId}' and displayName eq '{app.DisplayName}'";
            requestConfiguration.QueryParameters.Top = 1;
        });

        return sp?.Value?.FirstOrDefault();
    }

    public async Task<ServicePrincipal> CreateApplicationServicePrincipal(Application app)
    {
        var servicePrincipalRequest = new ServicePrincipal
        {
            AppId = app.AppId,
            DisplayName = app.DisplayName,
        };

        var servicePrincipal = await GraphServiceClient.ServicePrincipals.PostAsync(servicePrincipalRequest);
        if (servicePrincipal is null)
        {
            throw new Exception($"Failed to create service principal for app {app.AppId}, Graph API returned empty response.");
        }

        return servicePrincipal;
    }

    public async Task<List<FederatedIdentityCredential>> ListFederatedIdentityCredentials(Application app)
    {
        Log.LogInformation($"Listing federated identity credentials for app {app.AppId}...");
        var result = await GraphServiceClient.Applications[app.Id].FederatedIdentityCredentials.GetAsync();

        var credentials = result?.Value;

        Log.LogInformation($"Found {credentials?.Count() ?? 0} federated identity credentials ->");
        credentials?.ForEach(c => Log.LogInformation(((FederatedIdentityCredentialsConfig)c).ToIndentedString(1)));

        return credentials ?? new List<FederatedIdentityCredential>();
    }

    public async Task<FederatedIdentityCredential> CreateFederatedIdentityCredential(Application app, FederatedIdentityCredential credential)
    {
        Log.LogInformation($"Creating federated identity credential {credential.Name} for app {app.AppId}...");
        var created = await GraphServiceClient.Applications[app.Id].FederatedIdentityCredentials.PostAsync(credential);
        if (created is null)
        {
            throw new Exception($"Failed to create federated identity credential {credential.Name} for app {app.AppId}, Graph API returned empty response.");
        }
        Log.LogInformation($"Created federated identity credential {created.Name} for app {app.AppId}");
        return created;
    }

    public async Task DeleteFederatedIdentityCredential(Application app, FederatedIdentityCredential credential)
    {
        Log.LogInformation($"Deleting federated identity credential {credential.Name} for app {app.AppId}...");
        await GraphServiceClient.Applications[app.Id].FederatedIdentityCredentials[credential.Id].DeleteAsync();
        Log.LogInformation($"Deleted federated identity credential {credential.Name} for app {app.AppId}...");
    }
}

public interface IGraphClient
{
    public Task<Application?> GetApplicationByDisplayName(string displayName);
    public Task<Application> CreateApplication(Application application);
    public Task<ServicePrincipal?> GetApplicationServicePrincipal(Application app);
    public Task<ServicePrincipal> CreateApplicationServicePrincipal(Application app);
    public Task<List<FederatedIdentityCredential>> ListFederatedIdentityCredentials(Application app);
    public Task<FederatedIdentityCredential> CreateFederatedIdentityCredential(Application app, FederatedIdentityCredential credential);
    public Task DeleteFederatedIdentityCredential(Application app, FederatedIdentityCredential credential);
}
