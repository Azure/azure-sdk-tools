// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers;
using APIViewWeb.Repositories;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.WorkItemTracking.Process.WebApi.Models;
using Moq;
using Xunit;

namespace APIViewUnitTests;

public class PermissionsManagerTests
{
    private readonly Mock<ICosmosPermissionsRepository> _mockRepository;
    private readonly Mock<IMemoryCache> _mockCache;
    private readonly Mock<ILogger<PermissionsManager>> _mockLogger;
    private readonly PermissionsManager _manager;

    public PermissionsManagerTests()
    {
        _mockRepository = new Mock<ICosmosPermissionsRepository>();
        _mockCache = new Mock<IMemoryCache>();
        _mockLogger = new Mock<ILogger<PermissionsManager>>();

        // Setup cache to return false for TryGetValue (cache miss by default)
        object outValue;
        _mockCache.Setup(c => c.TryGetValue(It.IsAny<object>(), out outValue)).Returns(false);
        _mockCache.Setup(c => c.CreateEntry(It.IsAny<object>())).Returns(Mock.Of<ICacheEntry>());

        _manager = new PermissionsManager(
            _mockRepository.Object,
            _mockCache.Object,
            _mockLogger.Object
        );
    }

    #region GetEffectivePermissionsAsync Tests

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetEffectivePermissionsAsync_WithNullOrEmptyUserId_ReturnsEmptyPermissions(string userId)
    {
        EffectivePermissions result = await _manager.GetEffectivePermissionsAsync(userId);
        result.Should().NotBeNull();
        result.UserId.Should().Be(userId);
        result.Roles.Should().BeEmpty();
    }

    [Fact]
    public async Task GetEffectivePermissionsAsync_WithUserInNoGroups_ReturnsEmptyRoles()
    {
        _mockRepository.Setup(r => r.GetGroupsForUserAsync("testuser"))
            .ReturnsAsync(new List<GroupPermissionsModel>());

        EffectivePermissions result = await _manager.GetEffectivePermissionsAsync("testuser");
        result.Should().NotBeNull();
        result.UserId.Should().Be("testuser");
        result.Roles.Should().BeEmpty();
    }

    [Fact]
    public async Task GetEffectivePermissionsAsync_WithUserInGroupWithGlobalRole_ReturnsMergedRoles()
    {
        var groups = new List<GroupPermissionsModel>
        {
            new()
            {
                GroupId = "admin-group",
                GroupName = "Admin Group",
                Members = new List<string> { "testuser" },
                Roles = new List<RoleAssignment>
                {
                    new GlobalRoleAssignment { Role = GlobalRole.Admin }
                }
            }
        };
        _mockRepository.Setup(r => r.GetGroupsForUserAsync("testuser"))
            .ReturnsAsync(groups);


        EffectivePermissions result = await _manager.GetEffectivePermissionsAsync("testuser");
        result.Should().NotBeNull();
        result.Roles.Should().HaveCount(1);
        result.Roles.First().Should().BeOfType<GlobalRoleAssignment>();
        ((GlobalRoleAssignment)result.Roles.First()).Role.Should().Be(GlobalRole.Admin);
    }

    [Fact]
    public async Task GetEffectivePermissionsAsync_WithUserInGroupWithScopedRole_ReturnsScopedRoles()
    {
        var groups = new List<GroupPermissionsModel>
        {
            new()
            {
                GroupId = "csharp-architects",
                GroupName = "C# Architects",
                Members = new List<string> { "testuser" },
                Roles = new List<RoleAssignment>
                {
                    new LanguageScopedRoleAssignment
                    {
                        Role = LanguageScopedRole.Architect,
                        Language = "CSharp"
                    }
                }
            }
        };
        _mockRepository.Setup(r => r.GetGroupsForUserAsync("testuser"))
            .ReturnsAsync(groups);

        
        EffectivePermissions result = await _manager.GetEffectivePermissionsAsync("testuser");
        result.Should().NotBeNull();
        result.Roles.Should().HaveCount(1);
        var scopedRole = result.Roles.First().Should().BeOfType<LanguageScopedRoleAssignment>().Subject;
        scopedRole.Role.Should().Be(LanguageScopedRole.Architect);
        scopedRole.Language.Should().Be("CSharp");
    }

    [Fact]
    public async Task GetEffectivePermissionsAsync_WithUserInMultipleGroups_MergesAllRoles()
    {
        var groups = new List<GroupPermissionsModel>
        {
            new()
            {
                GroupId = "sdk-team",
                GroupName = "SDK Team",
                Members = new List<string> { "testuser" },
                Roles = new List<RoleAssignment>
                {
                    new GlobalRoleAssignment { Role = GlobalRole.SdkTeam }
                }
            },
            new()
            {
                GroupId = "csharp-architects",
                GroupName = "C# Architects",
                Members = new List<string> { "testuser" },
                Roles = new List<RoleAssignment>
                {
                    new LanguageScopedRoleAssignment
                    {
                        Role = LanguageScopedRole.Architect,
                        Language = "CSharp"
                    }
                }
            }
        };
        _mockRepository.Setup(r => r.GetGroupsForUserAsync("testuser"))
            .ReturnsAsync(groups);


        EffectivePermissions result = await _manager.GetEffectivePermissionsAsync("testuser");
        result.Should().NotBeNull();
        result.Roles.Should().HaveCount(2);
        result.Roles.Should().ContainSingle(r => r is GlobalRoleAssignment);
        result.Roles.Should().ContainSingle(r => r is LanguageScopedRoleAssignment);
    }

    [Fact]
    public async Task GetEffectivePermissionsAsync_CachesResult()
    {
        var groups = new List<GroupPermissionsModel>
        {
            new()
            {
                GroupId = "admin-group",
                Roles = new List<RoleAssignment>
                {
                    new GlobalRoleAssignment { Role = GlobalRole.Admin }
                }
            }
        };

        _mockRepository.Setup(r => r.GetGroupsForUserAsync("testuser"))
            .ReturnsAsync(groups);
        
        await _manager.GetEffectivePermissionsAsync("testuser");
        _mockCache.Verify(c => c.CreateEntry(It.Is<object>(k => k.ToString().Contains("testuser"))), Times.Once);
    }

    #endregion

    #region Group CRUD Tests

    [Fact]
    public async Task GetAllGroupsAsync_ReturnsAllGroups()
    {
        var groups = new List<GroupPermissionsModel>
        {
            new() { GroupId = "group1", GroupName = "Group 1" },
            new() { GroupId = "group2", GroupName = "Group 2" }
        };

        _mockRepository.Setup(r => r.GetAllGroupsAsync()).ReturnsAsync(groups);
        
        IEnumerable<GroupPermissionsModel> result = await _manager.GetAllGroupsAsync();
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetGroupAsync_WithValidId_ReturnsGroup()
    {
        var group = new GroupPermissionsModel { GroupId = "test-group", GroupName = "Test Group" };
        _mockRepository.Setup(r => r.GetGroupAsync("test-group")).ReturnsAsync(group);

        GroupPermissionsModel result = await _manager.GetGroupAsync("test-group");
        result.Should().NotBeNull();
        result.GroupId.Should().Be("test-group");
    }

    [Fact]
    public async Task GetGroupAsync_WithInvalidId_ReturnsNull()
    {
        _mockRepository.Setup(r => r.GetGroupAsync("invalid")).ReturnsAsync((GroupPermissionsModel)null);
        GroupPermissionsModel result = await _manager.GetGroupAsync("invalid");
        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateGroupAsync_CreatesNewGroup()
    {
        var request = new GroupPermissionsRequest
        {
            GroupId = "new-group",
            GroupName = "New Group",
            Roles = new List<RoleAssignment>
            {
                new GlobalRoleAssignment { Role = GlobalRole.SdkTeam }
            }
        };
        _mockRepository.Setup(r => r.UpsertGroupAsync(It.IsAny<GroupPermissionsModel>()))
            .Returns(Task.CompletedTask);

        GroupPermissionsModel result = await _manager.CreateGroupAsync(request, "admin");
        result.Should().NotBeNull();
        result.GroupId.Should().Be("new-group");
        result.GroupName.Should().Be("New Group");
        result.LastUpdatedBy.Should().Be("admin");
        result.Members.Should().BeEmpty();
        result.Roles.Should().HaveCount(1);

        _mockRepository.Verify(r => r.UpsertGroupAsync(It.Is<GroupPermissionsModel>(
            g => g.GroupId == "new-group" && g.GroupName == "New Group"
        )), Times.Once);
    }

    [Fact]
    public async Task UpdateGroupAsync_WithValidId_UpdatesGroup()
    {
        var existingGroup = new GroupPermissionsModel
        {
            GroupId = "test-group",
            GroupName = "Old Name",
            Members = new List<string> { "user1" },
            Roles = new List<RoleAssignment>()
        };
        _mockRepository.Setup(r => r.GetGroupAsync("test-group")).ReturnsAsync(existingGroup);
        _mockRepository.Setup(r => r.UpsertGroupAsync(It.IsAny<GroupPermissionsModel>()))
            .Returns(Task.CompletedTask);

        var request = new GroupPermissionsRequest
        {
            GroupId = "test-group",
            GroupName = "New Name",
            Roles = new List<RoleAssignment>
            {
                new GlobalRoleAssignment { Role = GlobalRole.Admin }
            }
        };
        
        GroupPermissionsModel result = await _manager.UpdateGroupAsync("test-group", request, "admin");
        result.Should().NotBeNull();
        result.GroupName.Should().Be("New Name");
        result.LastUpdatedBy.Should().Be("admin");
    }

    [Fact]
    public async Task DeleteGroupAsync_CallsRepository()
    {
        var group = new GroupPermissionsModel
        {
            GroupId = "test-group",
            Members = ["user1"]
        };
        _mockRepository.Setup(r => r.GetGroupAsync("test-group")).ReturnsAsync(group);
        _mockRepository.Setup(r => r.DeleteGroupAsync("test-group")).Returns(Task.CompletedTask);

        
        await _manager.DeleteGroupAsync("test-group");
        _mockRepository.Verify(r => r.DeleteGroupAsync("test-group"), Times.Once);
    }

    #endregion

    #region Member Management Tests

    [Fact]
    public async Task AddMembersToGroupAsync_CallsRepository()
    {
        var userIds = new List<string> { "user1", "user2" };
        _mockRepository.Setup(r => r.AddMembersToGroupAsync("test-group", userIds))
            .Returns(Task.CompletedTask);

        await _manager.AddMembersToGroupAsync("test-group", userIds, "admin");
        _mockRepository.Verify(r => r.AddMembersToGroupAsync("test-group", userIds), Times.Once);
    }

    [Fact]
    public async Task RemoveMemberFromGroupAsync_CallsRepository()
    {
        _mockRepository.Setup(r => r.RemoveMemberFromGroupAsync("test-group", "user1"))
            .Returns(Task.CompletedTask);
        await _manager.RemoveMemberFromGroupAsync("test-group", "user1", "admin");

        _mockRepository.Verify(r => r.RemoveMemberFromGroupAsync("test-group", "user1"), Times.Once);
    }

    #endregion

    #region IsAdminAsync Tests

    [Fact]
    public async Task IsAdminAsync_WithAdminUser_ReturnsTrue()
    {
        var groups = new List<GroupPermissionsModel>
        {
            new()
            {
                GroupId = "admin-group",
                Roles = new List<RoleAssignment>
                {
                    new GlobalRoleAssignment { Role = GlobalRole.Admin }
                }
            }
        };
        _mockRepository.Setup(r => r.GetGroupsForUserAsync("admin")).ReturnsAsync(groups);
        
        bool result = await _manager.IsAdminAsync("admin");
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsAdminAsync_WithNonAdminUser_ReturnsFalse()
    {
        var groups = new List<GroupPermissionsModel>
        {
            new()
            {
                GroupId = "sdk-team",
                Roles = new List<RoleAssignment>
                {
                    new GlobalRoleAssignment { Role = GlobalRole.SdkTeam }
                }
            }
        };
        _mockRepository.Setup(r => r.GetGroupsForUserAsync("user")).ReturnsAsync(groups);

        bool result = await _manager.IsAdminAsync("user");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsAdminAsync_WithUserInNoGroups_ReturnsFalse()
    {
        _mockRepository.Setup(r => r.GetGroupsForUserAsync("user"))
            .ReturnsAsync(new List<GroupPermissionsModel>());
        
        bool result = await _manager.IsAdminAsync("user");
        result.Should().BeFalse();
    }

    #endregion

    #region CanApproveForLanguageAsync Tests

    [Fact]
    public async Task CanApproveForLanguageAsync_WithArchitectRole_ReturnsTrue()
    {
        var groups = new List<GroupPermissionsModel>
        {
            new()
            {
                GroupId = "csharp-architects",
                Roles = new List<RoleAssignment>
                {
                    new LanguageScopedRoleAssignment
                    {
                        Role = LanguageScopedRole.Architect,
                        Language = "CSharp"
                    }
                }
            }
        };

        _mockRepository.Setup(r => r.GetGroupsForUserAsync("user")).ReturnsAsync(groups);

        bool result = await _manager.CanApproveAsync("user", "CSharp");
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanApproveForLanguageAsync_WithDeputyArchitectRole_ReturnsTrue()
    {
        var groups = new List<GroupPermissionsModel>
        {
            new()
            {
                GroupId = "python-deputies",
                Roles =
                [
                    new LanguageScopedRoleAssignment { Role = LanguageScopedRole.DeputyArchitect, Language = "Python" }
                ]
            }
        };
        _mockRepository.Setup(r => r.GetGroupsForUserAsync("user")).ReturnsAsync(groups);
        
        var result = await _manager.CanApproveAsync("user", "Python");
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanApproveForLanguageAsync_WithDifferentLanguage_ReturnsFalse()
    {
        var groups = new List<GroupPermissionsModel>
        {
            new()
            {
                GroupId = "csharp-architects",
                Roles = new List<RoleAssignment>
                {
                    new LanguageScopedRoleAssignment
                    {
                        Role = LanguageScopedRole.Architect,
                        Language = "CSharp"
                    }
                }
            }
        };
        _mockRepository.Setup(r => r.GetGroupsForUserAsync("user")).ReturnsAsync(groups);

        bool result = await _manager.CanApproveAsync("user", "Python");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanApproveForLanguageAsync_ForAdmin_ReturnsTrue()
    {
        var groups = new List<GroupPermissionsModel>
        {
            new()
            {
                GroupId = "admin-group",
                Roles = new List<RoleAssignment>
                {
                    new GlobalRoleAssignment { Role = GlobalRole.Admin }
                }
            }
        };
        _mockRepository.Setup(r => r.GetGroupsForUserAsync("user")).ReturnsAsync(groups);

        bool result = await _manager.CanApproveAsync("user", "CSharp");
        result.Should().BeTrue();
    }

    #endregion
}
