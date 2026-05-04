using System.Collections.Generic;
using System.Threading.Tasks;
using APIViewWeb.Helpers;
using APIViewWeb.LeanControllers;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace APIViewUnitTests;

public class PermissionsCookieOrTokenControllerTests
{
    private readonly Mock<IPermissionsManager> _mockPermissionsManager;
    private readonly PermissionsCookieOrTokenController _controller;

    public PermissionsCookieOrTokenControllerTests()
    {
        _mockPermissionsManager = new Mock<IPermissionsManager>();
        _controller = new PermissionsCookieOrTokenController(_mockPermissionsManager.Object);
    }

    #region GetAllGroups Tests

    [Fact]
    public async Task GetAllGroups_ReturnsGroups()
    {
        var groups = new List<GroupPermissionsModel>
        {
            new() { GroupId = "group1", GroupName = "Group 1" },
            new() { GroupId = "group2", GroupName = "Group 2" }
        };
        _mockPermissionsManager.Setup(m => m.GetAllGroupsAsync()).ReturnsAsync(groups);

        var result = await _controller.GetAllGroups();
        result.Should().BeOfType<LeanJsonResult>();
    }

    #endregion
}
