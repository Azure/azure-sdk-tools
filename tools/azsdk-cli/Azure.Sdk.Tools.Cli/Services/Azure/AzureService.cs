using Azure.Core;
using Azure.Identity;

namespace Azure.Sdk.Tools.Cli.Services;

public interface IAzureService
{
    TokenCredential GetCredential(string? tenantId = null);
}

public class AzureService : IAzureService
{
    public TokenCredential GetCredential(string? tenantId = null)
    {
        // We don't bother checking for a cached credential because this may be
        // called as part of a token refresh flow.
        // Currently this isn't used enough across one instance of the app
        // that we need to optimize for a cached credential.
        if (IsRunningInPipeline())
        {
            string? azureSubscriptionTenant = Environment.GetEnvironmentVariable("AZURESUBSCRIPTION_TENANT_ID");
            string? azureSubscriptionClient = Environment.GetEnvironmentVariable("AZURESUBSCRIPTION_CLIENT_ID");
            string? azureServiceConnection = Environment.GetEnvironmentVariable("AZURESUBSCRIPTION_SERVICE_CONNECTION_ID");
            string? accessToken = Environment.GetEnvironmentVariable("SYSTEM_ACCESSTOKEN");

            if (IsGitHubAction() || string.IsNullOrEmpty(azureSubscriptionTenant) || string.IsNullOrEmpty(azureSubscriptionClient) || string.IsNullOrEmpty(azureServiceConnection) || string.IsNullOrEmpty(accessToken))
            {
                return new ChainedTokenCredential(
                    new WorkloadIdentityCredential(new WorkloadIdentityCredentialOptions { TenantId = tenantId }),
                    new AzureCliCredential(new AzureCliCredentialOptions { TenantId = tenantId })
                );
            }

            // Use AzurePipelineCredential in chain only when env values are present
            return new ChainedTokenCredential(                        
                // Environment variables for Azure pipeline credentials are created by Azure pipeline tasks AzureCLI@2 and AzurePowerShell@5
                new AzurePipelinesCredential(azureSubscriptionClient, azureSubscriptionTenant, azureServiceConnection, accessToken),
                new WorkloadIdentityCredential(new WorkloadIdentityCredentialOptions { TenantId = tenantId }),
                new AzureCliCredential(new AzureCliCredentialOptions { TenantId = tenantId })
            );
        }

        // Local dev: prefer developer credentials (Azure CLI, then PowerShell, Developer CLI, Visual Studio) and
        // fall back to managed identity last, so a signed-in developer's `az login` always wins.
        return new ChainedTokenCredential(
            new AzureCliCredential(new AzureCliCredentialOptions { TenantId = tenantId }),
            new AzurePowerShellCredential(new AzurePowerShellCredentialOptions { TenantId = tenantId }),
            new AzureDeveloperCliCredential(new AzureDeveloperCliCredentialOptions { TenantId = tenantId }),
            new VisualStudioCredential(new VisualStudioCredentialOptions { TenantId = tenantId }),
            CreateManagedIdentityCredential()
        );
    }

    private static bool IsRunningInPipeline()
    {
        return IsGitHubAction() ||
               Environment.GetEnvironmentVariable("SYSTEM_TEAMPROJECTID") != null;
    }

    /// <summary>
    /// Builds the managed-identity link for the local-dev credential chain. A bare
    /// ManagedIdentityCredential placed in a ChainedTokenCredential blocks on the IMDS
    /// endpoint when running off-Azure, because a hand-built chain does not enable the fast-fail
    /// IMDS probe. Wrapping managed identity in a managed-identity-only DefaultAzureCredential restores
    /// that probe, so the credential fails fast with CredentialUnavailableException (about a second)
    /// when no IMDS endpoint is reachable instead of hanging. ManagedIdentityClientId defaults to the AZURE_CLIENT_ID
    /// environment variable, so a user-assigned identity is honored when one is configured.
    /// </summary>
    private static DefaultAzureCredential CreateManagedIdentityCredential()
    {
        return new DefaultAzureCredential(ManagedIdentityCredentialOptions);
    }

    /// <summary>
    /// The options used to build the managed-identity-only <see cref="DefaultAzureCredential"/>. Every credential
    /// type that DefaultAzureCredential can probe must be excluded except ManagedIdentity, otherwise the wrapper
    /// could silently pick up an unwanted identity from the environment.
    /// </summary>
    public static DefaultAzureCredentialOptions ManagedIdentityCredentialOptions => new()
    {
        ExcludeEnvironmentCredential = true,
        ExcludeWorkloadIdentityCredential = true,
        ExcludeManagedIdentityCredential = false,
        ExcludeVisualStudioCredential = true,
        ExcludeVisualStudioCodeCredential = true,
        ExcludeAzureCliCredential = true,
        ExcludeAzurePowerShellCredential = true,
        ExcludeAzureDeveloperCliCredential = true,
        ExcludeInteractiveBrowserCredential = true,
        ExcludeBrokerCredential = true,
    };

    private static bool IsGitHubAction()
    {
        return Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true";
    }
}
