// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using APIViewWeb.Helpers;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace APIViewWeb.LeanControllers;

public class PermissionsController : BaseApiController
{
    private readonly IPermissionsManager _permissionsManager;

    public PermissionsController(IPermissionsManager permissionsManager)
    {
        _permissionsManager = permissionsManager;
    }

    /// <summary>
    ///     Get current user's effective permissions
    /// </summary>
    [HttpGet("me")]
    public async Task<ActionResult<EffectivePermissions>> GetMyPermissions()
    {
        var userName = User.GetGitHubLogin();
        var permissions = await _permissionsManager.GetEffectivePermissionsAsync(userName);
        return new LeanJsonResult(permissions, StatusCodes.Status200OK);
    }

    /// <summary>
    ///     Get all usernames for autocomplete (Admin only)
    /// </summary>
    [HttpGet("users")]
    public async Task<ActionResult<IEnumerable<string>>> GetAllUsernames()
    {
        var userName = User.GetGitHubLogin();
        if (!await _permissionsManager.IsAdminAsync(userName))
        {
            return Forbid();
        }

        IEnumerable<string> users = await _permissionsManager.GetAllUsernamesAsync();
        return new LeanJsonResult(users, StatusCodes.Status200OK);
    }

    /// <summary>
    ///     Get all permission groups (Admin only)
    /// </summary>
    [HttpGet("groups")]
    public async Task<ActionResult> GetAllGroups()
    {
        var userName = User.GetGitHubLogin();
        var isAdmin = await _permissionsManager.IsAdminAsync(userName);
        if (!isAdmin)
        {
            return Forbid();
        }

        var groups = await _permissionsManager.GetAllGroupsAsync();
        return new LeanJsonResult(groups, StatusCodes.Status200OK);
    }

    /// <summary>
    ///     Get a specific group by ID (Admin only)
    /// </summary>
    [HttpGet("groups/{groupId}")]
    public async Task<ActionResult<GroupPermissionsModel>> GetGroup(string groupId)
    {
        var userName = User.GetGitHubLogin();
        if (!await _permissionsManager.IsAdminAsync(userName))
        {
            return Forbid();
        }

        GroupPermissionsModel group = await _permissionsManager.GetGroupAsync(groupId);
        if (group == null)
        {
            return NotFound($"Group with ID '{groupId}' not found.");
        }

        return new LeanJsonResult(group, StatusCodes.Status200OK);
    }

    /// <summary>
    ///     Create a new permission group (Admin only)
    /// </summary>
    [HttpPost("groups")]
    public async Task<ActionResult<GroupPermissionsModel>> CreateGroup([FromBody] GroupPermissionsRequest request)
    {
        var userName = User.GetGitHubLogin();
        if (!await _permissionsManager.IsAdminAsync(userName))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.GroupId))
        {
            return BadRequest("GroupId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.GroupName))
        {
            return BadRequest("GroupName is required.");
        }

        if (request.Roles != null)
        {
            foreach (var role in request.Roles)
            {
                if (role is LanguageScopedRoleAssignment scopedRole &&
                    string.IsNullOrWhiteSpace(scopedRole.Language))
                {
                    return BadRequest("Language is required for language-scoped role assignments.");
                }
            }
        }

        GroupPermissionsModel existingGroup = await _permissionsManager.GetGroupAsync(request.GroupId);
        if (existingGroup != null)
        {
            return Conflict($"Group with ID '{request.GroupId}' already exists.");
        }

        GroupPermissionsModel group = await _permissionsManager.CreateGroupAsync(request, userName);
        return new LeanJsonResult(group, StatusCodes.Status201Created);
    }

    /// <summary>
    ///     Update an existing permission group (Admin only)
    /// </summary>
    [HttpPut("groups/{groupId}")]
    public async Task<ActionResult<GroupPermissionsModel>> UpdateGroup(string groupId,
        [FromBody] GroupPermissionsRequest request)
    {
        var userName = User.GetGitHubLogin();
        if (!await _permissionsManager.IsAdminAsync(userName))
        {
            return Forbid();
        }

        if (request.Roles != null)
        {
            foreach (var role in request.Roles)
            {
                if (role is LanguageScopedRoleAssignment scopedRole &&
                    string.IsNullOrWhiteSpace(scopedRole.Language))
                {
                    return BadRequest("Language is required for language-scoped role assignments.");
                }
            }
        }

        try
        {
            GroupPermissionsModel group = await _permissionsManager.UpdateGroupAsync(groupId, request, userName);
            return new LeanJsonResult(group, StatusCodes.Status200OK);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>
    ///     Delete a permission group (Admin only)
    /// </summary>
    [HttpDelete("groups/{groupId}")]
    public async Task<ActionResult> DeleteGroup(string groupId)
    {
        var userName = User.GetGitHubLogin();
        if (!await _permissionsManager.IsAdminAsync(userName))
        {
            return Forbid();
        }

        await _permissionsManager.DeleteGroupAsync(groupId);
        return NoContent();
    }

    /// <summary>
    ///     Add members to a group (Admin only)
    /// </summary>
    [HttpPost("groups/{groupId}/members")]
    public async Task<ActionResult<AddMembersResult>> AddMembers(string groupId, [FromBody] AddMembersRequest request)
    {
        var userName = User.GetGitHubLogin();
        if (!await _permissionsManager.IsAdminAsync(userName))
        {
            return Forbid();
        }

        if (request.UserIds == null || request.UserIds.Count == 0)
        {
            return BadRequest("At least one userId is required.");
        }

        try
        {
            AddMembersResult result = await _permissionsManager.AddMembersToGroupAsync(groupId, request.UserIds, userName);
            
            if (result.InvalidUsers.Count > 0 && result.AddedUsers.Count == 0)
            {
                // All users were invalid
                return BadRequest(new { 
                    message = "None of the specified users exist in our database.", 
                    invalidUsers = result.InvalidUsers 
                });
            }

            return new LeanJsonResult(result, StatusCodes.Status200OK);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>
    ///     Remove a member from a group (Admin only)
    /// </summary>
    [HttpDelete("groups/{groupId}/members/{userId}")]
    public async Task<ActionResult> RemoveMember(string groupId, string userId)
    {
        var userName = User.GetGitHubLogin();
        if (!await _permissionsManager.IsAdminAsync(userName))
        {
            return Forbid();
        }

        try
        {
            await _permissionsManager.RemoveMemberFromGroupAsync(groupId, userId, userName);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>
    ///     Get the list of approvers for a specific language
    /// </summary>
    /// <param name="language">The programming language</param>
    /// <returns>List of usernames who can approve reviews for the specified language, sorted alphabetically</returns>
    [HttpGet("approvers/{language}")]
    public async Task<ActionResult<IEnumerable<string>>> GetApproversForLanguage(string language)
    {
        HashSet<string> approvers = await _permissionsManager.GetApproversForLanguageAsync(language);
        List<string> sortedApprovers = approvers.Where(a => !string.IsNullOrWhiteSpace(a))
            .OrderBy(a => a, StringComparer.OrdinalIgnoreCase).ToList();
        return new LeanJsonResult(sortedApprovers, StatusCodes.Status200OK);
    }

    /// <summary>
    ///     Get the groups that the current user belongs to
    /// </summary>
    /// <returns>List of groups the user is a member of</returns>
    [HttpGet("me/groups")]
    public async Task<ActionResult<IEnumerable<GroupPermissionsModel>>> GetMyGroups()
    {
        var userName = User.GetGitHubLogin();
        var groups = await _permissionsManager.GetGroupsForUserAsync(userName);
        return new LeanJsonResult(groups, StatusCodes.Status200OK);
    }

    /// <summary>
    ///     Get the list of admin usernames for contact information
    /// </summary>
    /// <returns>List of usernames who have admin permissions, sorted alphabetically</returns>
    [HttpGet("admins")]
    public async Task<ActionResult<IEnumerable<string>>> GetAdminUsernames()
    {
        var admins = await _permissionsManager.GetAdminUsernamesAsync();
        return new LeanJsonResult(admins, StatusCodes.Status200OK);
    }
}
