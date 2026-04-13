namespace Azure.Sdk.Tools.AccessManagement;

public class Reconciler
{
    public IManagedIdentityClient ManagedIdentityClient { get; set; }
    public IRbacClient RbacClient { get; set; }
    public IGitHubClient GitHubClient { get; set; }

    private ILogger Log { get; }

    public Reconciler(ILogger logger, IManagedIdentityClient managedIdentityClient, IRbacClient rbacClient, IGitHubClient gitHubClient)
    {
        ManagedIdentityClient = managedIdentityClient;
        RbacClient = rbacClient;
        GitHubClient = gitHubClient;
        Log = logger;
    }

    public async Task Reconcile(AccessConfig accessConfig, ReconcileOptions options)
    {
        try
        {
            var exceptions = new List<Exception>();
            var failedConfigs = new List<string>();
            var succeededConfigs = new List<string>();

            foreach (var cfg in accessConfig.Configs.Select(c => c.ApplicationAccessConfig))
            {
                try
                {
                    var identityInfo = await ReconcileManagedIdentity(cfg, options);
                    if (identityInfo is null)
                    {
                        Log.LogInformation($"[DRY RUN] Managed identity '{cfg.IdentityName}' does not exist, skipping remaining reconciliation");
                        succeededConfigs.Add(cfg.IdentityName!);
                        continue;
                    }

                    // Inject identity IDs so downstream configs can reference them
                    cfg.Properties["clientId"] = identityInfo.ClientId.ToString();
                    cfg.Properties["principalId"] = identityInfo.PrincipalId.ToString();
                    cfg.Render(failWhenMissingProperties: true);

                    await ReconcileRoleBasedAccessControls(identityInfo.PrincipalId, cfg, options);
                    await ReconcileFederatedIdentityCredentials(cfg, options);
                    await ReconcileGithubRepositorySecrets(cfg, options);

                    succeededConfigs.Add(cfg.IdentityName!);
                }
                catch (Exception ex)
                {
                    Log.LogInformation($"ERROR: failed to reconcile access config for '{cfg.IdentityName}'");
                    Log.LogInformation(ex.Message);
                    failedConfigs.Add(cfg.IdentityName!);
                    exceptions.Add(ex);
                }
            }

            if (!options.DryRun)
            {
                Log.LogInformation($"Updating config with new properties...");
                accessConfig.SyncProperties();
                await accessConfig.Save();
            }

            Log.LogInformation("---");
            if (succeededConfigs.Any())
            {
                Log.LogInformation($"Successfully reconciled {succeededConfigs.Count} " +
                                  $"access configs for identities: {string.Join(", ", succeededConfigs)}");
            }

            if (exceptions.Any())
            {
                Log.LogError($"ERROR: failed to reconcile {exceptions.Count} " +
                                  $"access configs for identities: {string.Join(", ", failedConfigs)}");
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

    public async Task<ManagedIdentityInfo?> ReconcileManagedIdentity(ApplicationAccessConfig cfg, ReconcileOptions options)
    {
        Log.LogInformation($"Looking for managed identity '{cfg.IdentityName}' in subscription '{cfg.SubscriptionId}', resource group '{cfg.ResourceGroup}'...");

        var identityInfo = await ManagedIdentityClient.GetManagedIdentity(cfg.SubscriptionId!, cfg.ResourceGroup!, cfg.IdentityName!);

        if (identityInfo is not null)
        {
            Log.LogInformation($"Found managed identity '{cfg.IdentityName}' with clientId '{identityInfo.ClientId}' and principalId '{identityInfo.PrincipalId}'");
            return identityInfo;
        }

        if (options.DryRun)
        {
            Log.LogInformation($"[DRY RUN] Would create managed identity '{cfg.IdentityName}'");
            return null;
        }

        if (string.IsNullOrEmpty(cfg.Location))
        {
            throw new Exception($"Managed identity '{cfg.IdentityName}' not found and no location specified for creation.");
        }

        Log.LogInformation($"Managed identity '{cfg.IdentityName}' not found. Creating...");
        identityInfo = await ManagedIdentityClient.CreateManagedIdentity(cfg.SubscriptionId!, cfg.ResourceGroup!, cfg.IdentityName!, cfg.Location!);
        Log.LogInformation($"Created managed identity '{cfg.IdentityName}' with clientId '{identityInfo.ClientId}' and principalId '{identityInfo.PrincipalId}'");

        return identityInfo;
    }

    public async Task ReconcileGithubRepositorySecrets(ApplicationAccessConfig appAccessConfig, ReconcileOptions options)
    {
        if (options.NoGitHubSecrets)
        {
            Log.LogInformation("Skipping GitHub repository secrets (--no-github-secrets)");
            return;
        }

        foreach (var config in appAccessConfig.GithubRepositorySecrets)
        {
            foreach (var repository in config.Repositories)
            {
                foreach (var secret in config.Secrets!)
                {
                    if (options.DryRun)
                    {
                        Log.LogInformation($"[DRY RUN] Would set GitHub repository secret '{secret.Key}' for repository '{repository}'");
                        continue;
                    }

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

    public async Task ReconcileRoleBasedAccessControls(
        Guid principalId,
        ApplicationAccessConfig appAccessConfig,
        ReconcileOptions options
    ) {
        foreach (var rbac in appAccessConfig.RoleBasedAccessControls ?? Enumerable.Empty<RoleBasedAccessControlsConfig>())
        {
            if (options.DryRun)
            {
                Log.LogInformation($"[DRY RUN] Would create role assignment for principal '{principalId}' with role '{rbac.Role}' in scope '{rbac.Scope}'");
                continue;
            }

            // This is idempotent, so don't bother checking if one already exists
            await RbacClient.CreateRoleAssignment(principalId, rbac);
        }

        Log.LogInformation($"Updated role assignments for principal {principalId} " +
                          $"- {appAccessConfig.RoleBasedAccessControls?.Count() ?? 0} created or unchanged");
    }

    public async Task ReconcileFederatedIdentityCredentials(
        ApplicationAccessConfig appAccessConfig,
        ReconcileOptions options
    ) {
        Log.LogInformation($"Syncing federated identity credentials for identity '{appAccessConfig.IdentityName}'...");

        var credentials = await ManagedIdentityClient.ListFederatedIdentityCredentials(
            appAccessConfig.SubscriptionId!, appAccessConfig.ResourceGroup!, appAccessConfig.IdentityName!);

        int unchanged = 0, removed = 0, created = 0, skipped = 0;

        // Remove any federated identity credentials that do not match the config
        foreach (var cred in credentials)
        {
            var match = appAccessConfig.FederatedIdentityCredentials?.FirstOrDefault(config => config.Matches(cred));
            if (match is null)
            {
                if (options.NoDelete)
                {
                    Log.LogInformation($"Skipping deletion of federated identity credential '{cred.Name}' (--no-delete)");
                    skipped++;
                }
                else if (options.DryRun)
                {
                    Log.LogInformation($"[DRY RUN] Would delete federated identity credential '{cred.Name}'");
                    removed++;
                }
                else
                {
                    await ManagedIdentityClient.DeleteFederatedIdentityCredential(
                        appAccessConfig.SubscriptionId!, appAccessConfig.ResourceGroup!, appAccessConfig.IdentityName!, cred.Name);
                    removed++;
                }
            }
            else
            {
                unchanged++;
            }
        }

        // Create any federated identity credentials that are in the config without a match
        foreach (var config in appAccessConfig.FederatedIdentityCredentials ?? Enumerable.Empty<FederatedIdentityCredentialsConfig>())
        {
            var match = credentials.FirstOrDefault(cred => config.Matches(cred));
            if (match is null)
            {
                if (options.DryRun)
                {
                    Log.LogInformation($"[DRY RUN] Would create federated identity credential '{config.Name}'");
                }
                else
                {
                    await ManagedIdentityClient.CreateFederatedIdentityCredential(
                        appAccessConfig.SubscriptionId!, appAccessConfig.ResourceGroup!, appAccessConfig.IdentityName!, config);
                }
                created++;
            }
        }

        Log.LogInformation($"Updated federated identity credentials for identity '{appAccessConfig.IdentityName}' " +
                          $"- {unchanged} unchanged, {removed} removed, {created} created" +
                          (skipped > 0 ? $", {skipped} skipped (--no-delete)" : ""));
    }
}
