using Azure.Identity;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;

public class Reconciler
{
    public IGraphClient GraphClient { get; set; }
    public IRbacClient RbacClient { get; set; }

    public Reconciler(IGraphClient graphClient, IRbacClient rbacClient)
    {
        GraphClient = graphClient;
        RbacClient = rbacClient;
    }

    public async Task Reconcile(AccessConfig accessConfig)
    {
        try
        {
            foreach (var cfg in accessConfig.ApplicationAccessConfigs ?? Enumerable.Empty<ApplicationAccessConfig>())
            {
                var (app, servicePrincipal) = await ReconcileApplication(cfg);
                await ReconcileRoleBasedAccessControls(servicePrincipal, cfg);
                await ReconcileFederatedIdentityCredentials(app, cfg);
            }
        }
        catch (ODataError ex)
        {
            Console.WriteLine("Received error from Graph API:");
            Console.WriteLine("    Code:" + ex.Error?.Code);
            Console.WriteLine("    Message:" + ex.Error?.Message);
            Environment.Exit(2);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            Environment.Exit(1);
        }
    }

    public async Task<(Application, ServicePrincipal)> ReconcileApplication(ApplicationAccessConfig appAccessConfig)
    {
        Console.WriteLine($"Looking for app with display name {appAccessConfig.AppDisplayName}...");

        var app = await GraphClient.GetApplicationByDisplayName(appAccessConfig.AppDisplayName);

        if (app is not null)
        {
            Console.WriteLine($"Found {app.DisplayName} with AppId {app.AppId} and ObjectId {app.Id}");
        }
        else
        {
            Console.WriteLine($"App with display name {appAccessConfig.AppDisplayName} not found. Creating new app...");
            var requestBody = new Application
            {
                DisplayName = appAccessConfig.AppDisplayName
            };

            app = await GraphClient.CreateApplication(requestBody);
            Console.WriteLine($"Created app {appAccessConfig.AppDisplayName} with AppId {app.AppId} and ObjectId {app.Id}");
        }

        var servicePrincipal = await GraphClient.GetApplicationServicePrincipal(app);
        if (servicePrincipal is not null)
        {
            Console.WriteLine($"Found existing service principal with object id '{servicePrincipal.Id}' for app '{app.AppId}'");
        }
        else
        {
            Console.WriteLine($"No service principal found for app '{app.AppId}'. Creating new service principal...");
            servicePrincipal = await GraphClient.CreateApplicationServicePrincipal(app);
            Console.WriteLine($"Created service principal with object id '{servicePrincipal.Id}' for app '{app.AppId}'");
        }

        return (app, servicePrincipal);
    }

    public async Task ReconcileRoleBasedAccessControls(ServicePrincipal servicePrincipal, ApplicationAccessConfig appAccessConfig)
    {
        foreach (var rbac in appAccessConfig.RoleBasedAccessControls ?? Enumerable.Empty<RoleBasedAccessControl>())
        {
            // This is idempotent, so don't bother checking if one already exists
            await RbacClient.CreateRoleAssignment(servicePrincipal, rbac);
        }
    }

    public async Task ReconcileFederatedIdentityCredentials(Application app, ApplicationAccessConfig appAccessConfig)
    {
        Console.WriteLine("Syncing federated identity credentials for " + app.DisplayName);

        var credentials = await GraphClient.ListFederatedIdentityCredentials(app);

        int unchanged = 0, removed = 0, created = 0;

        // Remove any federated identity credentials that do not match the config
        foreach (var cred in credentials ?? Enumerable.Empty<FederatedIdentityCredential>())
        {
            var match = appAccessConfig.FederatedIdentityCredentials?.FirstOrDefault(config => config == cred);
            if (match is null)
            {
                await GraphClient.DeleteFederatedIdentityCredential(app, cred);
                removed++;
            }
            else
            {
                unchanged++;
            }
        }

        // Create any federated identity credentials that are in the config without a match in the registered application
        foreach (var config in appAccessConfig.FederatedIdentityCredentials)
        {
            var match = credentials?.FirstOrDefault(cred => config == cred);
            if (match is null)
            {
                await GraphClient.CreateFederatedIdentityCredential(app, config);
                created++;
            }
        }

        Console.WriteLine($"Updated federated identity credentials for app {app.DisplayName} - {unchanged} unchanged, {removed} removed, {created} created");
    }
}