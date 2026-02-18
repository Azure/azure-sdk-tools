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
            return new ChainedTokenCredential(                
                new WorkloadIdentityCredential(new WorkloadIdentityCredentialOptions { TenantId = tenantId }),
                // Environment variables for Azure pipeline credentials are created by Azure pipeline tasks AzureCLI@2 and AzurePowerShell@5
                new AzurePipelinesCredential(Environment.GetEnvironmentVariable("AZURESUBSCRIPTION_TENANT_ID"), Environment.GetEnvironmentVariable("AZURESUBSCRIPTION_TENANT_ID"), Environment.GetEnvironmentVariable("AZURESUBSCRIPTION_SERVICE_CONNECTION_ID"), Environment.GetEnvironmentVariable("SYSTEM_ACCESSTOKEN")),
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
        return Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true" ||
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
}
