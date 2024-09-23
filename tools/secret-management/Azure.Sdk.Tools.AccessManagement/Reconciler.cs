using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;

namespace Azure.Sdk.Tools.AccessManagement;

public class Reconciler
{
    public IGraphClient GraphClient { get; set; }
    public IRbacClient RbacClient { get; set; }
    public IGitHubClient GitHubClient { get; set; }

    private ILogger Log { get; }

    public Reconciler(ILogger logger, IGraphClient graphClient, IRbacClient rbacClient, IGitHubClient gitHubClient)
    {
        GraphClient = graphClient;
        RbacClient = rbacClient;
        GitHubClient = gitHubClient;
        Log = logger;
    }

    public async Task Reconcile(AccessConfig accessConfig)
    {
        try
        {
            var exceptions = new List<Exception>();
            var failedConfigApps = new List<string>();
            var succeededConfigApps = new List<string>();

            foreach (var cfg in accessConfig.Configs.Select(c => c.ApplicationAccessConfig))
            {
                try
                {
                    var (app, servicePrincipal) = await ReconcileApplication(cfg);

                    // Inject application ID if we found or created a new app so
                    // downstream configs can reference it (e.g. GithubRepositorySecrets)
                    cfg.Properties["applicationId"] = app.AppId ?? string.Empty;
                    cfg.Render(failWhenMissingProperties: true);

                    await ReconcileRoleBasedAccessControls(servicePrincipal, cfg);
                    await ReconcileFederatedIdentityCredentials(app, cfg);
                    await ReconcileGithubRepositorySecrets(app, cfg);

                    succeededConfigApps.Add(cfg.AppDisplayName);
                }
                catch (ODataError ex)
                {
                    Log.LogInformation("Received error from Graph API:");
                    Log.LogInformation("    Code:" + ex.Error?.Code);
                    Log.LogInformation("    Message:" + ex.Error?.Message);
                    failedConfigApps.Add(cfg.AppDisplayName);
                    exceptions.Add(new Exception($"Received error from Graph API: {ex.Error?.Code} - {ex.Error?.Message}"));
                }
                catch (Exception ex)
                {
                    Log.LogInformation($"ERROR: failed to reconcile application access config for '{cfg.AppDisplayName}'");
                    Log.LogInformation(ex.Message);
                    failedConfigApps.Add(cfg.AppDisplayName);
                    exceptions.Add(ex);
                }
            }

            Log.LogInformation($"Updating config with new properties...");
            accessConfig.SyncProperties();
            await accessConfig.Save();

            Log.LogInformation("---");
            if (succeededConfigApps.Any())
            {
                Log.LogInformation($"Successfully reconciled {succeededConfigApps.Count} " +
                                  $"application access configs for apps: {string.Join(", ", succeededConfigApps)}");
            }

            if (exceptions.Any())
            {
                Log.LogError($"ERROR: failed to reconcile {exceptions.Count} " +
                                  $"application access configs for apps: {string.Join(", ", failedConfigApps)}");
                throw new AggregateException(exceptions);
            }
        }
        catch (AggregateException ex)
        {
            foreach (var inner in ex.InnerExceptions)
            {
                Log.LogInformation("---");
                Log.LogError(inner, inner.Message);
            }

            throw;
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
                    Log.LogInformation($"Setting GitHub repository secret '{secret.Key}:{secret.Value}' for repository '{repository}'...");
                    var split = repository.Split('/');
                    if (split.Length != 2)
                    {
                        throw new Exception($"Expected repository entry '{repository}' to match format '<owner>/<repository name>'");
                    }
                    var (owner, repoName) = (split[0], split[1]);
                    await GitHubClient.SetRepositorySecret(owner, repoName, secret.Key, secret.Value);
                    Log.LogInformation($"GitHub repository secret '{secret.Key}:{secret.Value}' for repository '{repository}' created");
                }
                Log.LogInformation($"Updated secrets for repository {repository} - {config.Secrets?.Count() ?? 0} created or updated");
            }
        }
    }

    public async Task<(Application, ServicePrincipal)> ReconcileApplication(ApplicationAccessConfig appAccessConfig)
    {
        Log.LogInformation($"Looking for app with display name {appAccessConfig.AppDisplayName}...");

        var app = await GraphClient.GetApplicationByDisplayName(appAccessConfig.AppDisplayName);

        if (app is not null)
        {
            Log.LogInformation($"Found {app.DisplayName} with AppId {app.AppId} and ObjectId {app.Id}");
        }
        else
        {
            Log.LogInformation($"App with display name {appAccessConfig.AppDisplayName} not found. Creating new app...");
            var requestBody = new Application
            {
                DisplayName = appAccessConfig.AppDisplayName
            };

            app = await GraphClient.CreateApplication(requestBody);
            Log.LogInformation($"Created app {appAccessConfig.AppDisplayName} with AppId {app.AppId} and ObjectId {app.Id}");
        }

        var servicePrincipal = await GraphClient.GetApplicationServicePrincipal(app);
        if (servicePrincipal is not null)
        {
            Log.LogInformation($"Found existing service principal with object id '{servicePrincipal.Id}' for app '{app.AppId}'");
        }
        else
        {
            Log.LogInformation($"No service principal found for app '{app.AppId}'. Creating new service principal...");
            servicePrincipal = await GraphClient.CreateApplicationServicePrincipal(app);
            Log.LogInformation($"Created service principal with object id '{servicePrincipal.Id}' for app '{app.AppId}'");
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

        Log.LogInformation($"Updated role assignments for service principal {servicePrincipal.DisplayName} " +
                          $"- {appAccessConfig.RoleBasedAccessControls?.Count() ?? 0} created or unchanged");
    }

    public async Task ReconcileFederatedIdentityCredentials(
        Application app,
        ApplicationAccessConfig appAccessConfig
    ) {
        Log.LogInformation("Syncing federated identity credentials for " + app.DisplayName);

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
        foreach (var config in appAccessConfig.FederatedIdentityCredentials ?? Enumerable.Empty<FederatedIdentityCredentialsConfig>())
        {
            var match = credentials?.FirstOrDefault(cred => config == cred);
            if (match is null)
            {
                await GraphClient.CreateFederatedIdentityCredential(app, config);
                created++;
            }
        }

        Log.LogInformation($"Updated federated identity credentials for app {app.DisplayName} " +
                          $"- {unchanged} unchanged, {removed} removed, {created} created");
    }
}
