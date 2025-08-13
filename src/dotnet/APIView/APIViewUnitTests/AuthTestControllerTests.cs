using System.Collections.Generic;
using System.Security.Claims;
using APIViewWeb.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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

    [Fact]
    public void GetAuthStatus_WhenUserNotAuthenticated_ReturnsCorrectStatus()
    {
        SetupUnauthenticatedUser();
        OkObjectResult result = _controller.GetAuthStatus() as OkObjectResult;

        result.Should().NotBeNull();
        result.StatusCode.Should().Be(200);

        string json = JsonConvert.SerializeObject(result.Value);
        JObject response = JObject.Parse(json);

        Assert.False(response["IsAuthenticated"]?.Value<bool>() ?? true);
        Assert.Null(response["UserName"]?.Value<string>());
        Assert.False(response["IsManagedIdentity"]?.Value<bool>() ?? true);
        Assert.False(response["HasGitHubOrganization"]?.Value<bool>() ?? true);
    }

    [Fact]
    public void GetAuthStatus_WhenUserAuthenticated_ReturnsCorrectStatus()
    {
        SetupAuthenticatedUser("testuser", "Bearer");
        OkObjectResult result = _controller.GetAuthStatus() as OkObjectResult;

        result.Should().NotBeNull();
        result.StatusCode.Should().Be(200);

        string json = JsonConvert.SerializeObject(result.Value);
        JObject response = JObject.Parse(json);

        Assert.True(response["IsAuthenticated"]?.Value<bool>() ?? false);
        Assert.Equal("testuser", response["UserName"]?.Value<string>());
    }

    [Fact]
    public void TestAuth_WhenAuthenticated_ReturnsSuccessMessage()
    {
        SetupAuthenticatedUser("testuser", "Bearer");

        OkObjectResult result = _controller.TestAuth() as OkObjectResult;

        result.Should().NotBeNull();
        result.StatusCode.Should().Be(200);

        string json = JsonConvert.SerializeObject(result.Value);
        JObject response = JObject.Parse(json);

        Assert.Equal("Authentication successful!", response["Message"]?.Value<string>());
        Assert.Equal("testuser", response["UserName"]?.Value<string>());
        Assert.True(response["IsAuthenticated"]?.Value<bool>() ?? false);
        Assert.Equal("Bearer", response["AuthenticationType"]?.Value<string>());
    }

    [Fact]
    public void TestAuth_IncludesAllExpectedProperties()
    {
        SetupAuthenticatedUser("testuser", "Bearer",
            new[] { new Claim("test-claim", "test-value"), new Claim("role", "admin") });

        OkObjectResult result = _controller.TestAuth() as OkObjectResult;

        result.Should().NotBeNull();
        string json = JsonConvert.SerializeObject(result.Value);
        JObject response = JObject.Parse(json);

        Assert.NotNull(response["Message"]);
        Assert.NotNull(response["AuthenticationMethod"]);
        Assert.NotNull(response["UserName"]);
        Assert.NotNull(response["IsAuthenticated"]);
        Assert.NotNull(response["AuthenticationType"]);
        Assert.NotNull(response["IsManagedIdentity"]);
        Assert.NotNull(response["HasGitHubOrganization"]);
        Assert.NotNull(response["Claims"]);
    }

    [Fact]
    public void TestGitHubOnly_ReturnsExpectedStructure()
    {
        SetupGitHubAuthenticatedUser("github-user");

        OkObjectResult result = _controller.TestGitHubOnly() as OkObjectResult;

        result.Should().NotBeNull();
        result.StatusCode.Should().Be(200);

        string json = JsonConvert.SerializeObject(result.Value);
        JObject response = JObject.Parse(json);

        Assert.Equal("GitHub organization authentication successful!", response["Message"]?.Value<string>());
        Assert.NotNull(response["UserName"]);
        Assert.NotNull(response["Organizations"]);
        Assert.NotNull(response["IsGitHubAuthenticated"]);
    }

    [Fact]
    public void DevTest_InProduction_ReturnsNotFound()
    {
        _mockEnvironment.SetupGet(e => e.EnvironmentName).Returns("Production");

        NotFoundResult result = _controller.DevTest() as NotFoundResult;

        result.Should().NotBeNull();
        result?.StatusCode.Should().Be(404);
    }

    [Fact]
    public void DevTest_InDevelopment_WithoutBearerToken_ReturnsUnauthorized()
    {
        _mockEnvironment.SetupGet(e => e.EnvironmentName).Returns("Development");
        SetupHttpContext();

        UnauthorizedObjectResult result = _controller.DevTest() as UnauthorizedObjectResult;

        result.Should().NotBeNull();
        result.StatusCode.Should().Be(401);

        string json = JsonConvert.SerializeObject(result.Value);
        JObject response = JObject.Parse(json);

        Assert.Equal("Bearer token required for development testing", response["Message"]?.Value<string>());
        Assert.Equal("Add 'Authorization: Bearer <any-token>' header", response["Hint"]?.Value<string>());
    }

    [Fact]
    public void DevTest_InDevelopment_WithBearerToken_ReturnsSuccess()
    {
        _mockEnvironment.SetupGet(e => e.EnvironmentName).Returns("Development");
        SetupHttpContextWithBearerToken("Bearer test-token-12345");

        OkObjectResult result = _controller.DevTest() as OkObjectResult;

        result.Should().NotBeNull();
        result?.StatusCode.Should().Be(200);

        string json = JsonConvert.SerializeObject(result?.Value);
        JObject response = JObject.Parse(json);

        Assert.Contains("🎉 Development managed identity authentication successful!",
            response["Message"]?.Value<string>());
        Assert.Equal("Managed Identity (Development Simulation)", response["AuthenticationMethod"]?.Value<string>());
        Assert.Equal("managed-identity-dev-user", response["UserName"]?.Value<string>());
        Assert.True(response["IsAuthenticated"]?.Value<bool>() ?? false);
        Assert.Equal("Bearer", response["AuthenticationType"]?.Value<string>());
        Assert.Equal("Development", response["Environment"]?.Value<string>());
        Assert.Contains("Bearer test-token-12345", response["TokenPreview"]?.Value<string>());
    }

    [Fact]
    public void DevTest_WithLongBearerToken_TruncatesTokenPreview()
    {
        _mockEnvironment.SetupGet(e => e.EnvironmentName).Returns("Development");
        string longToken = "Bearer " + new string('x', 100);
        SetupHttpContextWithBearerToken(longToken);

        OkObjectResult result = _controller.DevTest() as OkObjectResult;

        result.Should().NotBeNull();
        string json = JsonConvert.SerializeObject(result?.Value);
        JObject response = JObject.Parse(json);

        string tokenPreview = response["TokenPreview"]?.Value<string>();
        tokenPreview.Should().EndWith("...");
        tokenPreview.Should().HaveLength(53); // 50 chars + "..."
    }

    [Fact]
    public void JwtDebug_InProduction_ReturnsNotFound()
    {
        _mockEnvironment.SetupGet(e => e.EnvironmentName).Returns("Production");

        NotFoundResult result = _controller.JwtDebug() as NotFoundResult;

        result.Should().NotBeNull();
        result.StatusCode.Should().Be(404);
    }

    [Fact]
    public void JwtDebug_InDevelopment_WithoutAuth_ReturnsDebugInfo()
    {
        _mockEnvironment.SetupGet(e => e.EnvironmentName).Returns("Development");
        SetupUnauthenticatedUser();

        OkObjectResult result = _controller.JwtDebug() as OkObjectResult;

        result.Should().NotBeNull();
        result?.StatusCode.Should().Be(200);

        string json = JsonConvert.SerializeObject(result.Value);
        JObject response = JObject.Parse(json);

        Assert.Equal("JWT Debug Information", response["Message"]?.Value<string>());
        Assert.False(response["HasAuthorizationHeader"]?.Value<bool>() ?? true);
        Assert.False(response["UserAuthenticated"]?.Value<bool>() ?? true);
        Assert.Equal("None", response["AuthenticationType"]?.Value<string>());
        Assert.Equal("Anonymous", response["UserName"]?.Value<string>());
        Assert.Equal(0, response["ClaimsCount"]?.Value<int>() ?? -1);
        Assert.False(response["IsManagedIdentity"]?.Value<bool>() ?? true);
        Assert.Equal("Development", response["Environment"]?.Value<string>());
    }

    [Fact]
    public void JwtDebug_InDevelopment_WithAuth_ReturnsDetailedInfo()
    {
        _mockEnvironment.SetupGet(e => e.EnvironmentName).Returns("Development");
        SetupAuthenticatedUser("testuser", "Bearer", new[] { new Claim("sub", "user123"), new Claim("role", "admin") });
        SetupHttpContextWithBearerToken("Bearer jwt-token-here");

        OkObjectResult result = _controller.JwtDebug() as OkObjectResult;

        result.Should().NotBeNull();
        string json = JsonConvert.SerializeObject(result.Value);
        JObject response = JObject.Parse(json);

        Assert.True(response["HasAuthorizationHeader"]?.Value<bool>() ?? false);
        Assert.True(response["UserAuthenticated"]?.Value<bool>() ?? false);
        Assert.Equal("Bearer", response["AuthenticationType"]?.Value<string>());
        Assert.Equal("testuser", response["UserName"]?.Value<string>());
        // Note: ClaimsIdentity automatically adds authentication type claim, so expect 3 instead of 2
        Assert.True((response["ClaimsCount"]?.Value<int>() ?? 0) == 3);
        Assert.NotNull(response["Claims"]);
    }

    [Fact]
    public void JwtDebug_WithManyClaimsOnly_ReturnsFirst20Claims()
    {
        _mockEnvironment.SetupGet(e => e.EnvironmentName).Returns("Development");
        List<Claim> claims = new();
        for (int i = 0; i < 30; i++)
        {
            claims.Add(new Claim($"claim{i}", $"value{i}"));
        }

        SetupAuthenticatedUser("testuser", "Bearer", claims.ToArray());

        OkObjectResult result = _controller.JwtDebug() as OkObjectResult;

        result.Should().NotBeNull();
        string json = JsonConvert.SerializeObject(result.Value);
        JObject response = JObject.Parse(json);

        // ClaimsIdentity automatically adds authentication type claim, so expect 31 total claims
        Assert.True((response["ClaimsCount"]?.Value<int>() ?? 0) >= 30);
        JArray responseClaims = response["Claims"] as JArray;
        Assert.Equal(20, responseClaims?.Count ?? -1); // Should be limited to 20
    }

    private void SetupUnauthenticatedUser()
    {
        ClaimsIdentity identity = new();
        ClaimsPrincipal principal = new(identity);

        DefaultHttpContext httpContext = new() { User = principal };

        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    private void SetupAuthenticatedUser(string name, string authenticationType, Claim[] additionalClaims = null)
    {
        List<Claim> claims = new() { new Claim(ClaimTypes.Name, name) };

        if (additionalClaims != null)
        {
            claims.AddRange(additionalClaims);
        }

        ClaimsIdentity identity = new(claims, authenticationType);
        ClaimsPrincipal principal = new(identity);

        DefaultHttpContext httpContext = new() { User = principal };

        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    private void SetupGitHubAuthenticatedUser(string gitHubLogin)
    {
        List<Claim> claims = [new("urn:github:login", gitHubLogin), new("urn:github:name", gitHubLogin)];

        ClaimsIdentity identity = new(claims, "GitHub");
        ClaimsPrincipal principal = new(identity);

        DefaultHttpContext httpContext = new() { User = principal };

        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    private void SetupHttpContext()
    {
        DefaultHttpContext httpContext = new();
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    private void SetupHttpContextWithBearerToken(string authorizationHeader)
    {
        DefaultHttpContext httpContext = new();
        httpContext.Request.Headers["Authorization"] = authorizationHeader;

        if (_controller.ControllerContext?.HttpContext?.User != null)
        {
            httpContext.User = _controller.ControllerContext.HttpContext.User;
        }

        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }
}
