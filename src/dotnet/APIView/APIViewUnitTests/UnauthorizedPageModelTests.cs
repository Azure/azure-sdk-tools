using System.Security.Claims;
using System.Threading.Tasks;
using APIViewWeb;
using APIViewWeb.Pages;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace APIViewUnitTests;

public class UnauthorizedPageModelTests
{
    private readonly Mock<IAuthorizationService> _mockAuthorizationService;
    private readonly Mock<IWebHostEnvironment> _mockEnvironment;
    private readonly Mock<IOptions<OrganizationOptions>> _mockOptions;
    private readonly Mock<IUrlHelper> _mockUrlHelper;
    private readonly UnauthorizedModel _pageModel;
    private readonly ClaimsPrincipal _testUser;

    public UnauthorizedPageModelTests()
    {
        _mockAuthorizationService = new Mock<IAuthorizationService>();
        _mockOptions = new Mock<IOptions<OrganizationOptions>>();
        _mockEnvironment = new Mock<IWebHostEnvironment>();
        _mockUrlHelper = new Mock<IUrlHelper>();

        _mockOptions.Setup(o => o.Value).Returns(new OrganizationOptions
        {
            RequiredOrganization =
            [
                "TestOrganization"
            ]
        });

        _pageModel = new UnauthorizedModel(
            _mockOptions.Object,
            _mockAuthorizationService.Object,
            _mockEnvironment.Object);

        Claim[] claims = new[] { new Claim(ClaimTypes.NameIdentifier, "testuser") };
        ClaimsIdentity identity = new(claims, "Test");
        _testUser = new ClaimsPrincipal(identity);

        DefaultHttpContext httpContext = new() { User = _testUser };
        _pageModel.PageContext = new PageContext { HttpContext = httpContext };
        _pageModel.Url = _mockUrlHelper.Object;
    }

    [Fact]
    public async Task OnGetAsync_WhenAuthorized_WithLocalUrl_RedirectsToReturnUrl()
    {
        _pageModel.ReturnUrl = "/Reviews";
        _mockUrlHelper.Setup(u => u.IsLocalUrl("/Reviews")).Returns(true);
        _mockAuthorizationService
            .Setup(a => a.AuthorizeAsync(_testUser, null, Startup.RequireOrganizationPolicy))
            .ReturnsAsync(AuthorizationResult.Success());

        IActionResult result = await _pageModel.OnGetAsync();
        result.Should().BeOfType<RedirectResult>();
        RedirectResult redirectResult = result as RedirectResult;
        redirectResult!.Url.Should().Be("/Reviews");
    }

    [Fact]
    public async Task OnGetAsync_WhenAuthorized_WithExternalUrl_RedirectsToRoot()
    {
        _pageModel.ReturnUrl = "https://evil.com/redirect";
        _mockUrlHelper.Setup(u => u.IsLocalUrl("https://evil.com/redirect")).Returns(false);
        _mockEnvironment.SetupGet(e => e.EnvironmentName).Returns(Environments.Development);
        _mockAuthorizationService
            .Setup(a => a.AuthorizeAsync(_testUser, null, Startup.RequireOrganizationPolicy))
            .ReturnsAsync(AuthorizationResult.Success());

        IActionResult result = await _pageModel.OnGetAsync();
        result.Should().BeOfType<RedirectResult>();
        RedirectResult redirectResult = result as RedirectResult;
        redirectResult!.Url.Should().Be("/");
    }

    [Fact]
    public async Task OnGetAsync_WhenAuthorized_WithAllowedOrigin_RedirectsToReturnUrl()
    {
        _pageModel.ReturnUrl = "https://spa.apiview.org/Reviews";
        _mockUrlHelper.Setup(u => u.IsLocalUrl("https://spa.apiview.org/Reviews")).Returns(false);
        _mockEnvironment.SetupGet(e => e.EnvironmentName).Returns(Environments.Development);
        _mockAuthorizationService
            .Setup(a => a.AuthorizeAsync(_testUser, null, Startup.RequireOrganizationPolicy))
            .ReturnsAsync(AuthorizationResult.Success());

        IActionResult result = await _pageModel.OnGetAsync();
        result.Should().BeOfType<RedirectResult>();
        RedirectResult redirectResult = result as RedirectResult;
        redirectResult!.Url.Should().Be("https://spa.apiview.org/Reviews");
    }

    [Fact]
    public async Task OnGetAsync_WhenAuthorized_WithAllowedStagingOrigin_RedirectsToReturnUrl()
    {
        _pageModel.ReturnUrl = "https://localhost:4200/Reviews";
        _mockUrlHelper.Setup(u => u.IsLocalUrl("https://localhost:4200/Reviews")).Returns(false);
        _mockEnvironment.SetupGet(e => e.EnvironmentName).Returns(Environments.Development);
        _mockAuthorizationService
            .Setup(a => a.AuthorizeAsync(_testUser, null, Startup.RequireOrganizationPolicy))
            .ReturnsAsync(AuthorizationResult.Success());

        IActionResult result = await _pageModel.OnGetAsync();
        result.Should().BeOfType<RedirectResult>();
        RedirectResult redirectResult = result as RedirectResult;
        redirectResult!.Url.Should().Be("https://localhost:4200/Reviews");
    }

    [Fact]
    public async Task OnGetAsync_WhenAuthorized_WithUnallowedOrigin_RedirectsToRoot()
    {
        _pageModel.ReturnUrl = "https://malicious-site.com/steal-data";
        _mockUrlHelper.Setup(u => u.IsLocalUrl("https://malicious-site.com/steal-data")).Returns(false);
        _mockEnvironment.SetupGet(e => e.EnvironmentName).Returns(Environments.Development);
        _mockAuthorizationService
            .Setup(a => a.AuthorizeAsync(_testUser, null, Startup.RequireOrganizationPolicy))
            .ReturnsAsync(AuthorizationResult.Success());

        IActionResult result = await _pageModel.OnGetAsync();
        result.Should().BeOfType<RedirectResult>();
        RedirectResult redirectResult = result as RedirectResult;
        redirectResult!.Url.Should().Be("/");
    }

    [Fact]
    public async Task OnGetAsync_WhenNotAuthorized_ReturnsPage()
    {
        _pageModel.ReturnUrl = "/Reviews";
        _mockAuthorizationService
            .Setup(a => a.AuthorizeAsync(_testUser, null, Startup.RequireOrganizationPolicy))
            .ReturnsAsync(AuthorizationResult.Failed());

        IActionResult result = await _pageModel.OnGetAsync();
        result.Should().BeOfType<PageResult>();
    }

    [Fact]
    public async Task OnGetAsync_WhenAuthorized_WithDefaultReturnUrl_RedirectsToRoot()
    {
        _mockUrlHelper.Setup(u => u.IsLocalUrl("/")).Returns(true);
        _mockAuthorizationService
            .Setup(a => a.AuthorizeAsync(_testUser, null, Startup.RequireOrganizationPolicy))
            .ReturnsAsync(AuthorizationResult.Success());

        IActionResult result = await _pageModel.OnGetAsync();
        result.Should().BeOfType<RedirectResult>();
        RedirectResult redirectResult = result as RedirectResult;
        redirectResult!.Url.Should().Be("/");
    }

    [Fact]
    public async Task OnGetAsync_WhenAuthorized_WithInvalidUri_RedirectsToRoot()
    {
        _pageModel.ReturnUrl = "not a valid uri://malformed";
        _mockUrlHelper.Setup(u => u.IsLocalUrl("not a valid uri://malformed")).Returns(false);
        _mockEnvironment.SetupGet(e => e.EnvironmentName).Returns(Environments.Development);
        _mockAuthorizationService
            .Setup(a => a.AuthorizeAsync(_testUser, null, Startup.RequireOrganizationPolicy))
            .ReturnsAsync(AuthorizationResult.Success());

        IActionResult result = await _pageModel.OnGetAsync();
        result.Should().BeOfType<RedirectResult>();
        RedirectResult redirectResult = result as RedirectResult;
        redirectResult!.Url.Should().Be("/");
    }
}
