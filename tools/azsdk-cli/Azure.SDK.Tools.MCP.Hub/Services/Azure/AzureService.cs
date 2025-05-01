// Adapted from https://github.com/Azure/azure-mcp/blob/main/src/Services/Azure/BaseAzureService.cs
using Azure.Identity;

namespace Azure.SDK.Tools.MCP.Hub.Services.Azure;

public interface IAzureService
{
    DefaultAzureCredential GetCredential(string? tenantId = null);
}

public class AzureService : IAzureService
{
    private DefaultAzureCredential? credential;
    private string? lastTenantId;

    public DefaultAzureCredential GetCredential(string? tenantId = null)
    {
        // Return cached credential if it exists and tenant ID hasn't changed
        if (this.credential != null && this.lastTenantId == tenantId)
        {
            return this.credential;
        }

        try
        {
            // Create new credential and cache it
            this.credential = tenantId == null
                ? new DefaultAzureCredential()
                : new DefaultAzureCredential(new DefaultAzureCredentialOptions { TenantId = tenantId });
            this.lastTenantId = tenantId;

            return this.credential;
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to get credential: {ex.Message}", ex);
        }
    }
}