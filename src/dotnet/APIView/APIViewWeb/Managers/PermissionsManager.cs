using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Repositories;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace APIViewWeb.Managers;

public class PermissionsManager : IPermissionsManager
{
    private const string EffectivePermissionsCacheKeyPrefix = "EffectivePermissions_";

    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(15);
    private readonly IMemoryCache _cache;
    private readonly ILogger<PermissionsManager> _logger;
    private readonly ICosmosPermissionsRepository _permissionsRepository;

    public PermissionsManager(
        ICosmosPermissionsRepository permissionsRepository,
        IMemoryCache cache,
        ILogger<PermissionsManager> logger)
    {
        _permissionsRepository = permissionsRepository;
        _cache = cache;
        _logger = logger;
    }

    public async Task<EffectivePermissions> GetEffectivePermissionsAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return new EffectivePermissions { UserId = userId, Roles = new List<RoleAssignment>() };
        }

        var cacheKey = $"{EffectivePermissionsCacheKeyPrefix}{userId.ToLowerInvariant()}";
        if (_cache.TryGetValue(cacheKey, out EffectivePermissions cachedPermissions))
        {
            return cachedPermissions;
        }

        IEnumerable<GroupPermissionsModel> groups = await _permissionsRepository.GetGroupsForUserAsync(userId);
        EffectivePermissions effectivePermissions = MergePermissions(userId, groups);

        _cache.Set(cacheKey, effectivePermissions, CacheExpiration);
        return effectivePermissions;
    }

    public async Task<IEnumerable<GroupPermissionsModel>> GetAllGroupsAsync()
    {
        return await _permissionsRepository.GetAllGroupsAsync();
    }

    public async Task<GroupPermissionsModel> GetGroupAsync(string groupId)
    {
        return await _permissionsRepository.GetGroupAsync(groupId);
    }

    public async Task<GroupPermissionsModel> CreateGroupAsync(GroupPermissionsRequest request, string createdBy)
    {
        var group = new GroupPermissionsModel
        {
            GroupId = request.GroupId,
            GroupName = request.GroupName,
            Roles = request.Roles ?? new List<RoleAssignment>(),
            Members = new List<string>(),
            ServiceNames = request.ServiceNames ?? new List<string>(),
            LastUpdatedBy = createdBy,
            LastUpdatedOn = DateTime.UtcNow
        };

        await _permissionsRepository.UpsertGroupAsync(group);
        _logger.LogInformation("Permission group '{GroupId}' created by {User}", group.GroupId, createdBy);

        return group;
    }

    public async Task<GroupPermissionsModel> UpdateGroupAsync(string groupId, GroupPermissionsRequest request, string updatedBy)
    {
        var existingGroup = await _permissionsRepository.GetGroupAsync(groupId);
        if (existingGroup == null)
        {
            throw new ArgumentException($"Group with ID '{groupId}' not found.");
        }

        existingGroup.GroupName = request.GroupName;
        existingGroup.Roles = request.Roles ?? new List<RoleAssignment>();
        existingGroup.ServiceNames = request.ServiceNames ?? new List<string>();
        existingGroup.LastUpdatedBy = updatedBy;
        existingGroup.LastUpdatedOn = DateTime.UtcNow;

        await _permissionsRepository.UpsertGroupAsync(existingGroup);

        InvalidateMembersCaches(existingGroup.Members);

        _logger.LogInformation("Permission group '{GroupId}' updated by {User}", groupId, updatedBy);

        return existingGroup;
    }

    public async Task DeleteGroupAsync(string groupId)
    {
        var group = await _permissionsRepository.GetGroupAsync(groupId);
        if (group != null)
        {
            InvalidateMembersCaches(group.Members);
        }

        await _permissionsRepository.DeleteGroupAsync(groupId);
        _logger.LogInformation("Permission group '{GroupId}' deleted", groupId);
    }

    public async Task AddMembersToGroupAsync(string groupId, IEnumerable<string> userIds, string addedBy)
    {
        await _permissionsRepository.AddMembersToGroupAsync(groupId, userIds);
        InvalidateMembersCaches(userIds);

        _logger.LogInformation("Members added to group '{GroupId}' by {User}: {Members}",
            groupId, addedBy, string.Join(", ", userIds));
    }

    public async Task RemoveMemberFromGroupAsync(string groupId, string userId, string removedBy)
    {
        await _permissionsRepository.RemoveMemberFromGroupAsync(groupId, userId);
        InvalidateMembersCaches(new[] { userId });

        _logger.LogInformation("Member '{UserId}' removed from group '{GroupId}' by {User}",
            userId, groupId, removedBy);
    }

    public async Task<bool> CanApproveAsync(string userId, string language)
    {
        EffectivePermissions permissions = await GetEffectivePermissionsAsync(userId);
        return permissions.CanApprove(language);
    }

    public async Task<bool> IsAdminAsync(string userId)
    {
        EffectivePermissions permissions = await GetEffectivePermissionsAsync(userId);
        return permissions.IsAdmin;
    }

    public async Task<bool> HasElevatedAccessAsync(string userId)
    {
        EffectivePermissions permissions = await GetEffectivePermissionsAsync(userId);
        return permissions.HasElevatedAccess;
    }

    private EffectivePermissions MergePermissions(string userId, IEnumerable<GroupPermissionsModel> groups)
    {
        var mergedRoles = new List<RoleAssignment>();
        var globalRolesAdded = new HashSet<GlobalRole>();
        var languageScopedRolesAdded = new HashSet<(LanguageScopedRole role, string language)>();

        foreach (var group in groups)
        {
            foreach (var role in group.Roles)
            {
                switch (role)
                {
                    case GlobalRoleAssignment globalRole:
                    {
                        if (globalRolesAdded.Add(globalRole.Role))
                        {
                            mergedRoles.Add(role);
                        }

                        break;
                    }
                    case LanguageScopedRoleAssignment scopedRole:
                    {
                        var key = (scopedRole.Role, scopedRole.Language?.ToLowerInvariant());
                        if (languageScopedRolesAdded.Add(key))
                        {
                            mergedRoles.Add(role);
                        }

                        break;
                    }
                }
            }
        }

        return new EffectivePermissions { UserId = userId, Roles = mergedRoles };
    }

    private void InvalidateMembersCaches(IEnumerable<string> userIds)
    {
        foreach (var userId in userIds)
        {
            var cacheKey = $"{EffectivePermissionsCacheKeyPrefix}{userId.ToLowerInvariant()}";
            _cache.Remove(cacheKey);
        }
    }
}
