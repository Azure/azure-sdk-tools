using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using APIViewWeb;
using APIViewWeb.Controllers;
using APIViewWeb.Exceptions;
using APIViewWeb.Managers;
using APIViewWeb.Managers.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace APIViewUnitTests;

public class AutoReviewControllerTests
{
    private readonly AutoReviewController _controller;
    private readonly Mock<IAPIRevisionsManager> _mockApiRevisionsManager;
    private readonly Mock<IAuthorizationService> _mockAuthorizationService;
    private readonly Mock<ICodeFileManager> _mockCodeFileManager;
    private readonly Mock<ICommentsManager> _mockCommentsManager;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<IReviewManager> _mockReviewManager;

    public AutoReviewControllerTests()
    {
        _mockCodeFileManager = new Mock<ICodeFileManager>();
        _mockReviewManager = new Mock<IReviewManager>();
        _mockApiRevisionsManager = new Mock<IAPIRevisionsManager>();
        _mockCommentsManager = new Mock<ICommentsManager>();
        _mockAuthorizationService = new Mock<IAuthorizationService>();
        _mockConfiguration = new Mock<IConfiguration>();

        List<LanguageService> languageServices = new();

        _controller = new AutoReviewController(
            _mockAuthorizationService.Object,
            _mockCodeFileManager.Object,
            _mockReviewManager.Object,
            _mockApiRevisionsManager.Object,
            _mockCommentsManager.Object,
            _mockConfiguration.Object,
            languageServices);

        Claim[] claims = new[] { new Claim("login", "test-user"), new Claim("name", "Test User") };
        ClaimsIdentity identity = new(claims, "test");
        ClaimsPrincipal principal = new(identity);

        DefaultHttpContext httpContext = new() { User = principal };

        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    [Fact]
    public async Task UploadAutoReview_WithNullFile_ReturnsInternalServerError()
    {
        ActionResult result = await _controller.UploadAutoReview(null, "test-label");
        StatusCodeResult statusCodeResult = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(500, statusCodeResult.StatusCode);
    }

    [Fact]
    public async Task UploadAutoReview_WithDuplicateLineIdException_ReturnsBadRequest()
    {
        string fileName = "test-file.json";
        string fileContent = "{ \"test\": \"content\" }";
        string language = "C#";
        List<string> duplicateLineIds = new() { "duplicate-123", "duplicate-456" };

        Mock<IFormFile> mockFile = CreateMockFormFile(fileName, fileContent);
        DuplicateLineIdException exception = new(language, duplicateLineIds);

        _mockCodeFileManager
            .Setup(x => x.CreateCodeFileAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<MemoryStream>(),
                It.IsAny<Stream>(), It.IsAny<string>()))
            .ThrowsAsync(exception);

        ActionResult result = await _controller.UploadAutoReview(mockFile.Object, "test-label");
        ObjectResult badRequestResult = Assert.IsType<ObjectResult>(result);
        string errorMessage = badRequestResult.Value.ToString();

        Assert.Contains("APIView unexpectedly received", errorMessage);
        Assert.Contains("duplicate line identifiers", errorMessage);
        Assert.Contains("duplicate-123", errorMessage);
        Assert.Contains("duplicate-456", errorMessage);
        Assert.Contains(language, errorMessage);
    }

    private Mock<IFormFile> CreateMockFormFile(string fileName, string content)
    {
        Mock<IFormFile> mockFile = new();
        byte[] contentBytes = Encoding.UTF8.GetBytes(content);
        MemoryStream stream = new(contentBytes);

        mockFile.Setup(f => f.FileName).Returns(fileName);
        mockFile.Setup(f => f.Length).Returns(contentBytes.Length);
        mockFile.Setup(f => f.OpenReadStream()).Returns(stream);
        mockFile.Setup(f => f.ContentType).Returns("application/json");

        return mockFile;
    }
}
