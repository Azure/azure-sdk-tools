using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Authorization;
using Azure.ResourceManager.Authorization.Models;
using Microsoft.Graph.Models;

/*
 * Wrapper for Azure.ResourceManager ArmClient
 */
public class RbacClient : IRbacClient
{
    public ArmClient ArmClient { get; set; }

    public RbacClient(DefaultAzureCredential credential)
    {
        ArmClient = new ArmClient(credential);
    }

    public async Task CreateRoleAssignment(ServicePrincipal servicePrincipal, RoleBasedAccessControl rbac)
    {
        var resource = ArmClient.GetGenericResource(new ResourceIdentifier(rbac.Scope!));
        var role = await resource.GetAuthorizationRoleDefinitions().GetAllAsync($"roleName eq '{rbac.Role}'").FirstAsync();
        Console.WriteLine($"Found role '{role.Data.RoleName}' with id '{role.Data.Name}'");

        var principalId = Guid.Parse(servicePrincipal?.Id ?? string.Empty);
        var content = new RoleAssignmentCreateOrUpdateContent(role.Data.Id, principalId);
        content.PrincipalType = RoleManagementPrincipalType.ServicePrincipal;

        try
        {
            Console.WriteLine($"Creating role assignment for principal '{principalId}' with role '{role.Data.RoleName}' in scope '{rbac.Scope}'...");
            await resource.GetRoleAssignments().CreateOrUpdateAsync(WaitUntil.Completed, role.Data.Name, content);
        }
        catch (RequestFailedException ex)
        {
            if (ex.Status == 409)
            {
                Console.WriteLine($"The role assignment was already created by a different source. Skipping.");
                return;
            }
            throw ex;
        }
        Console.WriteLine($"Created role assignment for principal '{principalId}' with role '{role.Data.RoleName}' in scope '{rbac.Scope}'");
    }
}

public interface IRbacClient
{
    public Task CreateRoleAssignment(ServicePrincipal app, RoleBasedAccessControl rbac);
}