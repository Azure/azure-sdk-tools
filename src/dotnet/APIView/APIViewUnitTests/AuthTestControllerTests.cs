using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using APIViewWeb.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace APIViewUnitTests;

public class AuthTestControllerTests
{
    private readonly AuthTestController _controller;
    private readonly Mock<IWebHostEnvironment> _mockEnvironment;

    public AuthTestControllerTests()
    {
        _mockEnvironment = new Mock<IWebHostEnvironment>();
        _controller = new AuthTestController(_mockEnvironment.Object);
    }

    // Helper methods to reduce duplication
    private void SetUser(ClaimsPrincipal principal)
    {
        _controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = principal } };
    }

    private ClaimsPrincipal CreatePrincipal(string authType, params Claim[] claims)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(new List<Claim>(claims), authType));
    }

    private JsonObject InvokeJson(Func<IActionResult> action, out IActionResult raw)
    {
        raw = action();
        raw.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)raw;
        string json = JsonSerializer.Serialize(ok.Value);
        return JsonNode.Parse(json).AsObject();
    }

    private JsonObject InvokeJson(Func<IActionResult> action) => InvokeJson(action, out _);

    private void SetEnv(string name) => _mockEnvironment.SetupGet(e => e.EnvironmentName).Returns(name);

    [Fact]
    public void GetAuthStatus_WhenUserNotAuthenticated_ReturnsCorrectStatus()
    {
        SetUser(CreatePrincipal(null));
        var json = InvokeJson(() => _controller.GetAuthStatus());
        Assert.False(json["IsAuthenticated"]?.GetValue<bool>() ?? true);
        Assert.Null(json["UserName"]?.GetValue<string>());
        Assert.False(json["IsManagedIdentity"]?.GetValue<bool>() ?? true);
        Assert.False(json["HasGitHubOrganization"]?.GetValue<bool>() ?? true);
    }

    [Fact]
    public void GetAuthStatus_WhenUserAuthenticated_ReturnsCorrectStatus()
    {
        SetUser(CreatePrincipal("Bearer", new Claim(ClaimTypes.Name, "testuser")));
        var json = InvokeJson(() => _controller.GetAuthStatus());
        Assert.True(json["IsAuthenticated"]?.GetValue<bool>() ?? false);
        Assert.Equal("testuser", json["UserName"]?.GetValue<string>());
    }

    [Fact]
    public void TestAuth_WhenAuthenticated_ReturnsSuccessMessage()
    {
        SetUser(CreatePrincipal("Bearer", new Claim(ClaimTypes.Name, "testuser")));
        var json = InvokeJson(() => _controller.TestAuth());
        Assert.Equal("Combined authentication successful!", json["Message"]?.GetValue<string>());
        Assert.Equal("testuser", json["UserName"]?.GetValue<string>());
        Assert.True(json["IsAuthenticated"]?.GetValue<bool>() ?? false);
        Assert.Equal("Bearer", json["AuthenticationType"]?.GetValue<string>());
    }

    [Fact]
    public void TestAuth_IncludesAllExpectedProperties()
    {
        SetUser(CreatePrincipal("Bearer",
            new Claim(ClaimTypes.Name, "testuser"),
            new Claim("test-claim", "test-value"), new Claim("role", "admin")));
        var json = InvokeJson(() => _controller.TestAuth());
        Assert.NotNull(json["Message"]);
        Assert.NotNull(json["AuthenticationMethod"]);
        Assert.NotNull(json["UserName"]);
        Assert.NotNull(json["IsAuthenticated"]);
        Assert.NotNull(json["AuthenticationType"]);
        Assert.NotNull(json["IsManagedIdentity"]);
        Assert.NotNull(json["HasGitHubOrganization"]);
        Assert.NotNull(json["Claims"]);
    }

    [Fact]
    public void TestGitHubOnly_ReturnsExpectedStructure()
    {
        SetUser(CreatePrincipal("GitHub",
            new Claim("urn:github:login", "github-user"),
            new Claim("urn:github:name", "github-user"),
            new Claim("urn:github:orgs", "org1")));
        var json = InvokeJson(() => _controller.TestGitHubOnly());
        Assert.Equal("GitHub organization authentication successful!", json["Message"]?.GetValue<string>());
        Assert.NotNull(json["UserName"]);
        Assert.NotNull(json["Organizations"]);
        Assert.NotNull(json["IsGitHubAuthenticated"]);
    }

    [Fact]
    public void DevTest_InProduction_ReturnsNotFound()
    {
        SetEnv("Production");
        var result = _controller.DevTest();
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public void DevTest_InDevelopment_WithoutBearerToken_ReturnsUnauthorized()
    {
        SetEnv("Development");
        _controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        var result = _controller.DevTest();
        result.Should().BeOfType<UnauthorizedObjectResult>();
        var json = JsonNode.Parse(JsonSerializer.Serialize(((UnauthorizedObjectResult)result).Value));
        Assert.Equal("Bearer token required for development testing", json["Message"]?.GetValue<string>());
    }

    [Fact]
    public void DevTest_InDevelopment_WithBearerToken_ReturnsSuccess()
    {
        SetEnv("Development");
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["Authorization"] = "Bearer test-token-12345";
        _controller.ControllerContext = new ControllerContext { HttpContext = ctx };
        var json = InvokeJson(() => _controller.DevTest());
        Assert.Contains("Development managed identity authentication successful!", json["Message"]?.GetValue<string>());
        Assert.Equal("Bearer", json["AuthenticationType"]?.GetValue<string>());
    }

    [Fact]
    public void DevTest_WithLongBearerToken_TruncatesTokenPreview()
    {
        SetEnv("Development");
        string longToken = "Bearer " + new string('x', 100);
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["Authorization"] = longToken;
        _controller.ControllerContext = new ControllerContext { HttpContext = ctx };
        var json = InvokeJson(() => _controller.DevTest());
        string tokenPreview = json["TokenPreview"]?.GetValue<string>();
        tokenPreview.Should().EndWith("...");
    }

#if DEBUG
    [Fact]
    public void JwtDebug_InProduction_ReturnsNotFound()
    {
        SetEnv("Production");
        _controller.JwtDebug().Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public void JwtDebug_InDevelopment_WithoutAuth_ReturnsDebugInfo()
    {
        SetEnv("Development");
        SetUser(CreatePrincipal(null));
        var json = InvokeJson(() => _controller.JwtDebug());
        Assert.Equal("JWT Debug Information", json["Message"]?.GetValue<string>());
        Assert.False(json["UserAuthenticated"]?.GetValue<bool>() ?? true);
    }

    [Fact]
    public void JwtDebug_InDevelopment_WithAuth_ReturnsDetailedInfo()
    {
        SetEnv("Development");
        SetUser(CreatePrincipal("Bearer", new Claim(ClaimTypes.Name, "testuser"), new Claim("sub", "user123")));
        var ctx = _controller.ControllerContext.HttpContext;
        ctx.Request.Headers["Authorization"] = "Bearer jwt-token-here";
        var json = InvokeJson(() => _controller.JwtDebug());
        Assert.True(json["HasAuthorizationHeader"]?.GetValue<bool>() ?? false);
        Assert.True(json["UserAuthenticated"]?.GetValue<bool>() ?? false);
    }

    [Fact]
    public void JwtDebug_WithManyClaimsOnly_ReturnsFirst20Claims()
    {
        SetEnv("Development");
        List<Claim> claims = new();
        for (int i = 0; i < 30; i++) claims.Add(new Claim($"claim{i}", $"value{i}"));
        SetUser(CreatePrincipal("Bearer", claims.ToArray()));
        var json = InvokeJson(() => _controller.JwtDebug());
        Assert.True((json["ClaimsCount"]?.GetValue<int>() ?? 0) >= 30);
        Assert.Equal(20, json["Claims"]?.AsArray()?.Count ?? -1);
    }
#endif

    [Theory]
    [InlineData("Bearer", true, "Managed Identity", "oid", "12345")]
    [InlineData("GitHubToken", false, "GitHub Token", "urn:github:orgs", "org1")]
    public void TestTokenOnly_VariousPrincipals(string authType, bool expectMi, string tokenType, string extraClaimType, string extraClaimValue)
    {
        SetEnv("Production");
        var claims = new List<Claim> { new Claim(ClaimTypes.Name, authType + "-user"), new Claim(extraClaimType, extraClaimValue) };
        if (authType == "GitHubToken") claims.Add(new Claim("urn:github:login", "gh-user"));
        SetUser(CreatePrincipal(authType, claims.ToArray()));
        var json = InvokeJson(() => _controller.TestTokenOnly());
        Assert.Equal(expectMi, json["IsManagedIdentity"]?.GetValue<bool>() ?? !expectMi);
        Assert.Equal(tokenType, json["TokenType"]?.GetValue<string>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GetAuthStatus_VariousAuthSources(bool managedIdentity)
    {
        ClaimsPrincipal principal = managedIdentity
            ? CreatePrincipal("Bearer", new Claim(ClaimTypes.Name, "mi-user"), new Claim("oid", Guid.NewGuid().ToString()))
            : CreatePrincipal("GitHubToken", new Claim(ClaimTypes.Name, "gh-user"), new Claim("urn:github:login", "gh-user"), new Claim("urn:github:orgs", "orgX"));
        SetUser(principal);
        var json = InvokeJson(() => _controller.GetAuthStatus());
        Assert.Equal(managedIdentity, json["IsManagedIdentity"]?.GetValue<bool>() ?? !managedIdentity);
        Assert.Equal(!managedIdentity, json["HasGitHubOrganization"]?.GetValue<bool>() ?? managedIdentity);
    }

    [Fact]
    public void TestCookieOnly_WithGitHubOrgClaims_ReturnsSuccess()
    {
        SetUser(CreatePrincipal("Cookies", new Claim("urn:github:login", "cookie-user"), new Claim("urn:github:orgs", "orgA")));
        var json = InvokeJson(() => _controller.TestCookieOnly());
        Assert.Equal("Cookie-based authentication successful!", json["Message"]?.GetValue<string>());
        Assert.True(json["IsGitHubAuthenticated"]?.GetValue<bool>() ?? false);
    }
}
