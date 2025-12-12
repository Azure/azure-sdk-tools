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
            return new AzureCliCredential(new AzureCliCredentialOptions { TenantId = tenantId });
        }

        try
        {
            return new ChainedTokenCredential(
                new AzureCliCredential(new AzureCliCredentialOptions { TenantId = tenantId }),
                new AzurePowerShellCredential(new AzurePowerShellCredentialOptions { TenantId = tenantId }),
                new AzureDeveloperCliCredential(new AzureDeveloperCliCredentialOptions { TenantId = tenantId }),
                new VisualStudioCredential(new VisualStudioCredentialOptions { TenantId = tenantId }),
                new ManagedIdentityCredential(GetUserAssignedManagedIdentityClientId())
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

    private static string GetUserAssignedManagedIdentityClientId()
    {
        return Environment.GetEnvironmentVariable("AZURE_CLIENT_ID") ?? string.Empty;
    }
}
