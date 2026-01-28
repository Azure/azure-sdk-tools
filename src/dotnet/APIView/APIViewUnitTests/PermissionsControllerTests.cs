using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using APIView.Identity;
using APIViewWeb.Helpers;
using APIViewWeb.LeanControllers;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace APIViewUnitTests;

public class PermissionsControllerTests
{
    private readonly Mock<IPermissionsManager> _mockPermissionsManager;
    private readonly PermissionsController _controller;

    public PermissionsControllerTests()
    {
        _mockPermissionsManager = new Mock<IPermissionsManager>();

        _controller = new PermissionsController(_mockPermissionsManager.Object);

        var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
        {
            new(ClaimConstants.Login, "testuser"),
            new(ClaimTypes.Name, "Test User")
        }, "mock"));

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
    }

    private void SetupAdminUser()
    {
        _mockPermissionsManager.Setup(m => m.IsAdminAsync("testuser")).ReturnsAsync(true);
    }

    private void SetupNonAdminUser()
    {
        _mockPermissionsManager.Setup(m => m.IsAdminAsync("testuser")).ReturnsAsync(false);
    }

    #region GetMyPermissions Tests

    [Fact]
    public async Task GetMyPermissions_ReturnsUserPermissions()
    {
        var expectedPermissions = new EffectivePermissions
        {
            UserId = "testuser",
            Roles = new List<RoleAssignment>
            {
                new GlobalRoleAssignment { Role = GlobalRole.SdkTeam }
            }
        };

        _mockPermissionsManager.Setup(m => m.GetEffectivePermissionsAsync("testuser"))
            .ReturnsAsync(expectedPermissions);

        var result = await _controller.GetMyPermissions();
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<LeanJsonResult>();
    }

    #endregion

    #region GetAllGroups Tests

    [Fact]
    public async Task GetAllGroups_WhenAdmin_ReturnsGroups()
    {
        SetupAdminUser();

        var groups = new List<GroupPermissionsModel>
        {
            new() { GroupId = "group1", GroupName = "Group 1" },
            new() { GroupId = "group2", GroupName = "Group 2" }
        };
        _mockPermissionsManager.Setup(m => m.GetAllGroupsAsync()).ReturnsAsync(groups);

        var result = await _controller.GetAllGroups();
        result.Should().BeOfType<LeanJsonResult>();
    }

    [Fact]
    public async Task GetAllGroups_WhenNotAdmin_ReturnsForbid()
    {
        SetupNonAdminUser();

        var result = await _controller.GetAllGroups();
        result.Should().BeOfType<ForbidResult>();
    }

    #endregion

    #region GetGroup Tests

    [Fact]
    public async Task GetGroup_WhenAdminAndGroupExists_ReturnsGroup()
    {
        SetupAdminUser();

        var group = new GroupPermissionsModel { GroupId = "test-group", GroupName = "Test Group" };
        _mockPermissionsManager.Setup(m => m.GetGroupAsync("test-group")).ReturnsAsync(group);

        var result = await _controller.GetGroup("test-group");
        result.Result.Should().BeOfType<LeanJsonResult>();
    }

    [Fact]
    public async Task GetGroup_WhenAdminAndGroupNotFound_ReturnsNotFound()
    {
        SetupAdminUser();
        _mockPermissionsManager.Setup(m => m.GetGroupAsync("invalid")).ReturnsAsync((GroupPermissionsModel)null);

        var result = await _controller.GetGroup("invalid");
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetGroup_WhenNotAdmin_ReturnsForbid()
    {
        SetupNonAdminUser();

        var result = await _controller.GetGroup("test-group");
        result.Result.Should().BeOfType<ForbidResult>();
    }

    #endregion

    #region CreateGroup Tests

    [Fact]
    public async Task CreateGroup_WhenAdminWithValidRequest_ReturnsCreatedGroup()
    {
        SetupAdminUser();

        var request = new GroupPermissionsRequest
        {
            GroupId = "new-group",
            GroupName = "New Group",
            Roles = new List<RoleAssignment>()
        };

        var createdGroup = new GroupPermissionsModel
        {
            GroupId = "new-group",
            GroupName = "New Group"
        };

        _mockPermissionsManager.Setup(m => m.GetGroupAsync("new-group")).ReturnsAsync((GroupPermissionsModel)null);
        _mockPermissionsManager.Setup(m => m.CreateGroupAsync(request, "testuser")).ReturnsAsync(createdGroup);

        var result = await _controller.CreateGroup(request);
        result.Result.Should().BeOfType<LeanJsonResult>();
        _mockPermissionsManager.Verify(m => m.CreateGroupAsync(request, "testuser"), Times.Once);
    }

    [Fact]
    public async Task CreateGroup_WhenNotAdmin_ReturnsForbid()
    {

        SetupNonAdminUser();

        var request = new GroupPermissionsRequest
        {
            GroupId = "new-group",
            GroupName = "New Group"
        };

        var result = await _controller.CreateGroup(request);
        result.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task CreateGroup_WithMissingGroupId_ReturnsBadRequest()
    {

        SetupAdminUser();

        var request = new GroupPermissionsRequest
        {
            GroupId = "",
            GroupName = "New Group"
        };

        var result = await _controller.CreateGroup(request);
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateGroup_WithMissingGroupName_ReturnsBadRequest()
    {

        SetupAdminUser();

        var request = new GroupPermissionsRequest
        {
            GroupId = "new-group",
            GroupName = ""
        };

        var result = await _controller.CreateGroup(request);
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateGroup_WithExistingGroupId_ReturnsConflict()
    {
        SetupAdminUser();

        var existingGroup = new GroupPermissionsModel { GroupId = "existing-group" };
        _mockPermissionsManager.Setup(m => m.GetGroupAsync("existing-group")).ReturnsAsync(existingGroup);

        var request = new GroupPermissionsRequest
        {
            GroupId = "existing-group",
            GroupName = "Existing Group"
        };

        var result = await _controller.CreateGroup(request);
        result.Result.Should().BeOfType<ConflictObjectResult>();
    }

    #endregion

    #region UpdateGroup Tests

    [Fact]
    public async Task UpdateGroup_WhenAdminWithValidRequest_ReturnsUpdatedGroup()
    {
        SetupAdminUser();

        var request = new GroupPermissionsRequest
        {
            GroupId = "test-group",
            GroupName = "Updated Group"
        };

        var updatedGroup = new GroupPermissionsModel
        {
            GroupId = "test-group",
            GroupName = "Updated Group"
        };

        _mockPermissionsManager.Setup(m => m.UpdateGroupAsync("test-group", request, "testuser"))
            .ReturnsAsync(updatedGroup);

        var result = await _controller.UpdateGroup("test-group", request);
        result.Result.Should().BeOfType<LeanJsonResult>();
    }

    [Fact]
    public async Task UpdateGroup_WhenNotAdmin_ReturnsForbid()
    {
        SetupNonAdminUser();

        var request = new GroupPermissionsRequest
        {
            GroupId = "test-group",
            GroupName = "Updated Group"
        };

        var result = await _controller.UpdateGroup("test-group", request);
        result.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task UpdateGroup_WhenGroupNotFound_ReturnsNotFound()
    {
        SetupAdminUser();

        var request = new GroupPermissionsRequest
        {
            GroupId = "invalid",
            GroupName = "Updated Group"
        };

        _mockPermissionsManager.Setup(m => m.UpdateGroupAsync("invalid", request, "testuser"))
            .ThrowsAsync(new ArgumentException("Group not found"));

        var result = await _controller.UpdateGroup("invalid", request);
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region DeleteGroup Tests

    [Fact]
    public async Task DeleteGroup_WhenAdmin_ReturnsNoContent()
    {
        SetupAdminUser();
        _mockPermissionsManager.Setup(m => m.DeleteGroupAsync("test-group")).Returns(Task.CompletedTask);

        var result = await _controller.DeleteGroup("test-group");
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeleteGroup_WhenNotAdmin_ReturnsForbid()
    {
        SetupNonAdminUser();

        var result = await _controller.DeleteGroup("test-group");
        result.Should().BeOfType<ForbidResult>();
    }

    #endregion

    #region AddMembers Tests

    [Fact]
    public async Task AddMembers_WhenAdminWithValidRequest_ReturnsOk()
    {
        SetupAdminUser();

        var request = new AddMembersRequest { UserIds = new List<string> { "user1", "user2" } };

        var addMembersResult = new AddMembersResult
        {
            AddedUsers = new List<string> { "user1", "user2" },
            InvalidUsers = new List<string>()
        };

        _mockPermissionsManager.Setup(m => m.AddMembersToGroupAsync("test-group", It.IsAny<IEnumerable<string>>(), "testuser"))
            .ReturnsAsync(addMembersResult);

        var result = await _controller.AddMembers("test-group", request);
        result.Result.Should().BeOfType<LeanJsonResult>();
    }

    [Fact]
    public async Task AddMembers_WhenNotAdmin_ReturnsForbid()
    {
        SetupNonAdminUser();

        var request = new AddMembersRequest { UserIds = new List<string> { "user1" } };

        var result = await _controller.AddMembers("test-group", request);
        result.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task AddMembers_WithEmptyUserIds_ReturnsBadRequest()
    {
        SetupAdminUser();

        var request = new AddMembersRequest { UserIds = new List<string>() };

        var result = await _controller.AddMembers("test-group", request);
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task AddMembers_WithNullUserIds_ReturnsBadRequest()
    {
        SetupAdminUser();

        var request = new AddMembersRequest { UserIds = null };

        var result = await _controller.AddMembers("test-group", request);
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task AddMembers_WhenGroupNotFound_ReturnsNotFound()
    {
        SetupAdminUser();

        var request = new AddMembersRequest { UserIds = new List<string> { "user1" } };

        _mockPermissionsManager.Setup(m => m.AddMembersToGroupAsync("invalid", request.UserIds, "testuser"))
            .ThrowsAsync(new ArgumentException("Group not found"));

        var result = await _controller.AddMembers("invalid", request);
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region RemoveMember Tests

    [Fact]
    public async Task RemoveMember_WhenAdmin_ReturnsNoContent()
    {
        SetupAdminUser();
        _mockPermissionsManager.Setup(m => m.RemoveMemberFromGroupAsync("test-group", "user1", "testuser"))
            .Returns(Task.CompletedTask);

        var result = await _controller.RemoveMember("test-group", "user1");
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task RemoveMember_WhenNotAdmin_ReturnsForbid()
    {
        SetupNonAdminUser();

        var result = await _controller.RemoveMember("test-group", "user1");
        result.Should().BeOfType<ForbidResult>();
    }

    #endregion

    #region GetAllUsernames Tests

    [Fact]
    public async Task GetAllUsernames_WhenAdmin_ReturnsUsers()
    {
        SetupAdminUser();
        var expectedUsers = new List<string> { "user1", "user2" };
        _mockPermissionsManager.Setup(m => m.GetAllUsernamesAsync())
            .ReturnsAsync(expectedUsers);

        var result = await _controller.GetAllUsernames();
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<LeanJsonResult>();
    }

    [Fact]
    public async Task GetAllUsernames_WhenNotAdmin_ReturnsForbid()
    {
        SetupNonAdminUser();

        var result = await _controller.GetAllUsernames();
        result.Result.Should().BeOfType<ForbidResult>();
    }

    #endregion
}
