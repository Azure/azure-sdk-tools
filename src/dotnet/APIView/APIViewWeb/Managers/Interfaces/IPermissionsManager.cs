using System.Collections.Generic;
using System.Threading.Tasks;
using APIViewWeb.LeanModels;

namespace APIViewWeb.Managers.Interfaces;

public interface IPermissionsManager
{
    Task<EffectivePermissions> GetEffectivePermissionsAsync(string userId);
    Task<IEnumerable<GroupPermissionsModel>> GetAllGroupsAsync();
    Task<GroupPermissionsModel> GetGroupAsync(string groupId);
    Task<GroupPermissionsModel> CreateGroupAsync(GroupPermissionsRequest request, string createdBy);
    Task<GroupPermissionsModel> UpdateGroupAsync(string groupId, GroupPermissionsRequest request, string updatedBy);
    Task DeleteGroupAsync(string groupId);
    Task<AddMembersResult> AddMembersToGroupAsync(string groupId, IEnumerable<string> userIds, string addedBy);
    Task RemoveMemberFromGroupAsync(string groupId, string userId, string removedBy);
    Task<bool> CanApproveAsync(string userId, string language);
    Task<bool> IsAdminAsync(string userId);
    Task<IEnumerable<string>> GetAllUsernamesAsync();
    Task<HashSet<string>> GetApproversForLanguageAsync(string language);
    Task<IEnumerable<GroupPermissionsModel>> GetGroupsForUserAsync(string userId);
    Task<IEnumerable<string>> GetAdminUsernamesAsync();
}
