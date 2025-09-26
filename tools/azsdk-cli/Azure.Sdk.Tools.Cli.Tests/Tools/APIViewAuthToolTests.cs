using Azure.Sdk.Tools.Cli.Models.APIView;
using Azure.Sdk.Tools.Cli.Models.Responses;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.APIView;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Tools;

[TestFixture]
public class ApiViewAuthToolTests
{
    [SetUp]
    public void Setup()
    {
        _logger = new TestLogger<APIViewAuthTool>();
        _mockApiViewService = new Mock<IAPIViewService>();
        _apiViewAuthTool = new APIViewAuthTool(_logger, _mockApiViewService.Object);
    }

    private APIViewAuthTool _apiViewAuthTool;
    private Mock<IAPIViewService> _mockApiViewService;
    private TestLogger<APIViewAuthTool> _logger;

    [Test]
    public async Task CheckAuthentication_WhenServiceThrowsException_ReturnsError()
    {
        _mockApiViewService
            .Setup(x => x.CheckAuthenticationStatusAsync(It.IsAny<string>()))
            .ThrowsAsync(new Exception("Auth service error"));

        APIViewResponse result = await _apiViewAuthTool.CheckAuthentication();

        Assert.That(result.Success, Is.False);
        Assert.That(result.ResponseError, Does.Contain("Auth service error"));
    }

    [Test]
    public async Task GetAuthenticationGuidance_ReturnsSuccess()
    {
        AuthenticationGuidance expectedGuidance = new()
        {
            IsAuthenticated = false,
            CurrentTokenSource = "azure-credentials",
            Instructions = "Authentication guidance content",
            QuickSetup = "Run: az login"
        };
        _mockApiViewService
            .Setup(x => x.GetAuthenticationGuidanceAsync())
            .ReturnsAsync(expectedGuidance);

        APIViewResponse result = await _apiViewAuthTool.GetAuthenticationGuidance();

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data!.ToString(), Does.Contain("\"IsAuthenticated\": false"));
        Assert.That(result.Data!.ToString(), Does.Contain("\"CurrentTokenSource\": \"azure-credentials\""));
        Assert.That(result.Data!.ToString(), Does.Contain("Authentication guidance content"));
        Assert.That(result.ResponseError, Is.Null);
    }

    [Test]
    public async Task GetAuthenticationGuidance_WhenServiceThrowsException_ReturnsError()
    {
        _mockApiViewService
            .Setup(x => x.GetAuthenticationGuidanceAsync())
            .ThrowsAsync(new Exception("Guidance service error"));

        APIViewResponse result = await _apiViewAuthTool.GetAuthenticationGuidance();

        Assert.That(result.Success, Is.False);
        Assert.That(result.ResponseError, Does.Contain("Guidance service error"));
    }

    [Test]
    public async Task CheckAuthentication_WithAuthenticationFailure_ReturnsFailureStatus()
    {
        string environment = "staging";
        AuthenticationStatus expectedStatus = new()
        {
            HasToken = false,
            IsAuthenticationWorking = false,
            TokenSource = null!,
            Endpoint = "https://example-for-this-test.com",
            AuthenticationError = "No valid credentials found",
            Guidance = "Please authenticate using Azure CLI"
        };
        _mockApiViewService
            .Setup(x => x.CheckAuthenticationStatusAsync(environment))
            .ReturnsAsync(expectedStatus);

        APIViewResponse result = await _apiViewAuthTool.CheckAuthentication(environment);

        Assert.That(result.Success, Is.True); // The call succeeds even if auth failed
        Assert.That(result.Data!.ToString(), Does.Contain("\"HasToken\": false"));
        Assert.That(result.Data!.ToString(), Does.Contain("\"IsAuthenticationWorking\": false"));
        Assert.That(result.Data!.ToString(), Does.Contain("No valid credentials found"));
        Assert.That(result.ResponseError, Is.Null);
    }
}
