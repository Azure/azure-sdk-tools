using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;

public class Reconciler
{
    public IGraphClient GraphClient { get; set; }
    public IRbacClient RbacClient { get; set; }
    public IGitHubClient GitHubClient { get; set; }

    public Reconciler(IGraphClient graphClient, IRbacClient rbacClient, IGitHubClient gitHubClient)
    {
        GraphClient = graphClient;
        RbacClient = rbacClient;
        GitHubClient = gitHubClient;
    }

    public async Task Reconcile(AccessConfig accessConfig)
    {
        try
        {
            foreach (var cfg in accessConfig.ApplicationAccessConfigs ?? Enumerable.Empty<ApplicationAccessConfig>())
            {
                var (app, servicePrincipal) = await ReconcileApplication(cfg);

                // Inject application ID if we found or created a new app so
                // downstream configs can reference it (e.g. GithubRepositorySecrets)
                cfg.Properties["applicationId"] = app.AppId ?? string.Empty;
                cfg.Render();

                await ReconcileRoleBasedAccessControls(servicePrincipal, cfg);
                await ReconcileFederatedIdentityCredentials(app, cfg);
                await ReconcileGithubRepositorySecrets(app, cfg);
            }

            Console.WriteLine($"Updating config with new properties...");
            accessConfig.SyncProperties();
            await accessConfig.Save();
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

    public async Task ReconcileGithubRepositorySecrets(Application app, ApplicationAccessConfig appAccessConfig)
    {
        foreach (var config in appAccessConfig.GithubRepositorySecrets)
        {
            foreach (var repository in config.Repositories)
            {
                foreach (var secret in config.Secrets!)
                {
                    Console.WriteLine($"Setting GitHub repository secret '{secret.Key}:{secret.Value}' for repository '{repository}'...");
                    var split = repository.Split('/');
                    if (split.Length != 2)
                    {
                        throw new Exception($"Expected repository entry '{repository}' to match format '<owner>/<repository name>'");
                    }
                    var (owner, repoName) = (split[0], split[1]);
                    await GitHubClient.SetRepositorySecret(owner, repoName, secret.Key, secret.Value);
                    Console.WriteLine($"GitHub repository secret '{secret.Key}:{secret.Value}' for repository '{repository}' created");
                }
                Console.WriteLine($"Updated secrets for repository {repository} - {config.Secrets?.Count() ?? 0} created or updated");
            }
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

    public async Task ReconcileRoleBasedAccessControls(
        ServicePrincipal servicePrincipal,
        ApplicationAccessConfig appAccessConfig
    ) {
        foreach (var rbac in appAccessConfig.RoleBasedAccessControls ?? Enumerable.Empty<RoleBasedAccessControlsConfig>())
        {
            // This is idempotent, so don't bother checking if one already exists
            await RbacClient.CreateRoleAssignment(servicePrincipal, rbac);
        }

        Console.WriteLine($"Updated role assignments for service principal {servicePrincipal.DisplayName} " +
                          $"- {appAccessConfig.RoleBasedAccessControls?.Count() ?? 0} created or unchanged");
    }

    public async Task ReconcileFederatedIdentityCredentials(
        Application app,
        ApplicationAccessConfig appAccessConfig
    ) {
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

        Console.WriteLine($"Updated federated identity credentials for app {app.DisplayName} " +
                          $"- {unchanged} unchanged, {removed} removed, {created} created");
    }
}
