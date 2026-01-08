// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
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
    public async Task<ActionResult> AddMembers(string groupId, [FromBody] AddMembersRequest request)
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
            await _permissionsManager.AddMembersToGroupAsync(groupId, request.UserIds, userName);
            return Ok();
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
}
