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

        try
        {
            return new ChainedTokenCredential(
                new AzureCliCredential(new AzureCliCredentialOptions { TenantId = tenantId }),
                new AzurePowerShellCredential(new AzurePowerShellCredentialOptions { TenantId = tenantId }),
                new AzureDeveloperCliCredential(new AzureDeveloperCliCredentialOptions { TenantId = tenantId }),
                new VisualStudioCredential(new VisualStudioCredentialOptions { TenantId = tenantId }),
                new ManagedIdentityCredential(GetManagedIdentityClientId())
            );
        }
        catch (CredentialUnavailableException)
        {
            return new InteractiveBrowserCredential(new InteractiveBrowserCredentialOptions { TenantId = tenantId });
        }
    }

    private static bool IsRunningInPipeline()
    {
        return IsGitHubAction() ||
               Environment.GetEnvironmentVariable("SYSTEM_TEAMPROJECTID") != null;
    }

    /// <summary>
    /// Gets the client ID for a user-assigned managed identity from the AZURE_CLIENT_ID environment variable.
    /// Returns null if not set, allowing the credential to use system-assigned managed identity.
    /// </summary>
    /// <returns>The client ID for user-assigned managed identity, or null for system-assigned.</returns>
    private static string? GetManagedIdentityClientId()
    {
        return Environment.GetEnvironmentVariable("AZURE_CLIENT_ID");
    }

    private static bool IsGitHubAction()
    {
        return Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true";
    }
}
