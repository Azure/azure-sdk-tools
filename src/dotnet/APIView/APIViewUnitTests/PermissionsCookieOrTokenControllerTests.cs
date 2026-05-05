using System.Collections.Generic;
using System.Threading.Tasks;
using APIViewWeb.Helpers;
using APIViewWeb.LeanControllers;
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

    #region GetApproversForLanguage Tests

    [Fact]
    public async Task GetApproversForLanguage_ReturnsApprovers()
    {
        var approvers = new System.Collections.Generic.HashSet<string> { "user2", "user1" };
        _mockPermissionsManager.Setup(m => m.GetApproversForLanguageAsync("Python")).ReturnsAsync(approvers);

        var result = await _controller.GetApproversForLanguage("Python");
        result.Result.Should().BeOfType<LeanJsonResult>();
    }

    #endregion

    #region GetAdminUsernames Tests

    [Fact]
    public async Task GetAdminUsernames_ReturnsAdmins()
    {
        var admins = new List<string> { "admin1", "admin2" };
        _mockPermissionsManager.Setup(m => m.GetAdminUsernamesAsync()).ReturnsAsync(admins);

        var result = await _controller.GetAdminUsernames();
        result.Result.Should().BeOfType<LeanJsonResult>();
    }

    #endregion
}
