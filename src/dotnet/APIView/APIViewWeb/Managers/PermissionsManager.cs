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
    private const string ApproversForLanguageCacheKeyPrefix = "ApproversForLanguage_";

    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(15);
    private readonly IMemoryCache _cache;
    private readonly ILogger<PermissionsManager> _logger;
    private readonly ICosmosPermissionsRepository _permissionsRepository;
    private readonly ICosmosUserProfileRepository _userProfileRepository;
    private readonly HashSet<string> _cachedLanguageApproverKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _languageCacheLock = new();

    public PermissionsManager(
        ICosmosPermissionsRepository permissionsRepository,
        ICosmosUserProfileRepository userProfileRepository,
        IMemoryCache cache,
        ILogger<PermissionsManager> logger)
    {
        _permissionsRepository = permissionsRepository;
        _userProfileRepository = userProfileRepository;
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
        InvalidateAllLanguageApproverCaches();

        _logger.LogInformation("Permission group '{GroupId}' updated by {User}", groupId, updatedBy);

        return existingGroup;
    }

    public async Task DeleteGroupAsync(string groupId)
    {
        var group = await _permissionsRepository.GetGroupAsync(groupId);
        if (group != null)
        {
            InvalidateMembersCaches(group.Members);
            InvalidateAllLanguageApproverCaches();
        }

        await _permissionsRepository.DeleteGroupAsync(groupId);
        _logger.LogInformation("Permission group '{GroupId}' deleted", groupId);
    }

    public async Task<AddMembersResult> AddMembersToGroupAsync(string groupId, IEnumerable<string> userNames, string addedBy)
    {
        List<string> userNamesList = userNames.ToList();
        List<string> existingUsers = (await _userProfileRepository.GetExistingUsersAsync(userNamesList)).ToList();
        var existingUsersSet = new HashSet<string>(existingUsers, StringComparer.OrdinalIgnoreCase);

        var result = new AddMembersResult();
        foreach (string userName in userNamesList)
        {
            if (existingUsersSet.Contains(userName))
            {
                result.AddedUsers.Add(userName);
            }
            else
            {
                result.InvalidUsers.Add(userName);
            }
        }

        if (result.AddedUsers.Count > 0)
        {
            await _permissionsRepository.AddMembersToGroupAsync(groupId, result.AddedUsers);
            InvalidateMembersCaches(result.AddedUsers);
            InvalidateAllLanguageApproverCaches();
        }

        return result;
    }

    public async Task RemoveMemberFromGroupAsync(string groupId, string userId, string removedBy)
    {
        await _permissionsRepository.RemoveMemberFromGroupAsync(groupId, userId);
        InvalidateMembersCaches(new[] { userId });
        InvalidateAllLanguageApproverCaches();

        _logger.LogInformation("Member '{UserId}' removed from group '{GroupId}' by {User}",
            userId, groupId, removedBy);
    }

    public async Task<bool> CanApproveAsync(string userId, string language)
    {
        EffectivePermissions permissions = await GetEffectivePermissionsAsync(userId);
        return permissions.IsApproverFor(language);
    }

    public async Task<bool> IsAdminAsync(string userId)
    {
        EffectivePermissions permissions = await GetEffectivePermissionsAsync(userId);
        return permissions.IsAdmin;
    }


    public async Task<IEnumerable<string>> GetAllUsernamesAsync()
    {
        return await _userProfileRepository.GetAllUsernamesAsync();
    }

    public async Task<HashSet<string>> GetApproversForLanguageAsync(string language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var cacheKey = $"{ApproversForLanguageCacheKeyPrefix}{language.ToLowerInvariant()}";
        if (_cache.TryGetValue(cacheKey, out HashSet<string> cachedApprovers))
        {
            return new HashSet<string>(cachedApprovers, StringComparer.OrdinalIgnoreCase);
        }

        IEnumerable<GroupPermissionsModel> groups = await _permissionsRepository.GetAllGroupsAsync();
        HashSet<string> approvers = groups
            .Where(g => g.Roles.GrantsApprovalFor(language))
            .SelectMany(g => g.Members)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        _cache.Set(cacheKey, approvers, CacheExpiration);

        lock (_languageCacheLock)
        {
            _cachedLanguageApproverKeys.Add(cacheKey);
        }

        return new HashSet<string>(approvers, StringComparer.OrdinalIgnoreCase);
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

        if (mergedRoles.Count == 0)
        {
            mergedRoles.Add(new GlobalRoleAssignment { Role = GlobalRole.SdkTeam });
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

    private void InvalidateAllLanguageApproverCaches()
    {
        lock (_languageCacheLock)
        {
            foreach (var cacheKey in _cachedLanguageApproverKeys)
            {
                _cache.Remove(cacheKey);
            }
            _cachedLanguageApproverKeys.Clear();
        }
    }

    public async Task<IEnumerable<GroupPermissionsModel>> GetGroupsForUserAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Enumerable.Empty<GroupPermissionsModel>();
        }

        return await _permissionsRepository.GetGroupsForUserAsync(userId);
    }

    public async Task<IEnumerable<string>> GetAdminUsernamesAsync()
    {
        IEnumerable<GroupPermissionsModel> groups = await _permissionsRepository.GetAllGroupsAsync();

        return groups
            .Where(g => g.Roles.HasGlobalRole(GlobalRole.Admin))
            .SelectMany(g => g.Members)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(u => u, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
