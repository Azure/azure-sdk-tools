using System.Collections.Generic;
using System.Threading.Tasks;
using APIViewWeb.LeanModels;

namespace APIViewWeb.Repositories;

public interface ICosmosPermissionsRepository
{
    Task<IEnumerable<GroupPermissionsModel>> GetGroupsForUserAsync(string userId);
    Task<IEnumerable<GroupPermissionsModel>> GetAllGroupsAsync();
    Task<GroupPermissionsModel> GetGroupAsync(string groupId);
    Task UpsertGroupAsync(GroupPermissionsModel group);
    Task DeleteGroupAsync(string groupId);
    Task AddMembersToGroupAsync(string groupId, IEnumerable<string> userIds);
    Task RemoveMemberFromGroupAsync(string groupId, string userId);
}
