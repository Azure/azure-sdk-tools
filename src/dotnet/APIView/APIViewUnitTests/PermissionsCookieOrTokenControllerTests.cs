using System;
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
    public async Task GetApproversForLanguage_ReturnsSortedApprovers()
    {
        var approvers = new System.Collections.Generic.HashSet<string> { "zebra", "Alice", "bob" };
        _mockPermissionsManager.Setup(m => m.GetApproversForLanguageAsync("Python")).ReturnsAsync(approvers);

        var result = await _controller.GetApproversForLanguage("Python");

        var leanResult = Assert.IsType<LeanJsonResult>(result.Result);
        var usernames = leanResult.Value.Should().BeAssignableTo<IEnumerable<string>>().Subject;
        usernames.Should().BeInAscendingOrder(StringComparer.OrdinalIgnoreCase);
        usernames.Should().BeEquivalentTo(new[] { "Alice", "bob", "zebra" }, o => o.WithStrictOrdering());
    }

    [Fact]
    public async Task GetApproversForLanguage_FiltersBlankUsernames()
    {
        var approvers = new System.Collections.Generic.HashSet<string> { "user1", "", "  ", "user2" };
        _mockPermissionsManager.Setup(m => m.GetApproversForLanguageAsync("Java")).ReturnsAsync(approvers);

        var result = await _controller.GetApproversForLanguage("Java");

        var leanResult = Assert.IsType<LeanJsonResult>(result.Result);
        var usernames = leanResult.Value.Should().BeAssignableTo<IEnumerable<string>>().Subject;
        usernames.Should().BeEquivalentTo(new[] { "user1", "user2" });
        usernames.Should().NotContain(s => string.IsNullOrWhiteSpace(s));
    }

    #endregion

    #region GetAdminUsernames Tests

    [Fact]
    public async Task GetAdminUsernames_ReturnsAdmins()
    {
        var admins = new List<string> { "admin1", "admin2" };
        _mockPermissionsManager.Setup(m => m.GetAdminUsernamesAsync()).ReturnsAsync(admins);

        var result = await _controller.GetAdminUsernames();

        var leanResult = Assert.IsType<LeanJsonResult>(result.Result);
        var usernames = leanResult.Value.Should().BeAssignableTo<IEnumerable<string>>().Subject;
        usernames.Should().BeEquivalentTo(new[] { "admin1", "admin2" });
    }

    #endregion
}
