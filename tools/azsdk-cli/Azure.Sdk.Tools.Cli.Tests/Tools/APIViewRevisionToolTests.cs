using Azure.Sdk.Tools.Cli.Models.Responses;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.APIView;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Tools;

[TestFixture]
public class APIViewRevisionToolTests
{
    [SetUp]
    public void Setup()
    {
        _logger = new TestLogger<APIViewRevisionTool>();
        _mockApiViewService = new Mock<IAPIViewService>();
        _apiViewRevisionTool = new APIViewRevisionTool(_logger, _mockApiViewService.Object);
    }

    private APIViewRevisionTool _apiViewRevisionTool;
    private Mock<IAPIViewService> _mockApiViewService;
    private TestLogger<APIViewRevisionTool> _logger;

    [Test]
    public async Task GetRevisionComments_WithValidRevisionId_ReturnsSuccess()
    {
        string revisionId = "test-revision-123";
        string expectedComments = "[{\"lineNo\":14,\"commentText\":\"Test comment\",\"isResolved\":false}]";
        _mockApiViewService
            .Setup(x => x.GetCommentsByRevisionAsync(revisionId, "production"))
            .ReturnsAsync(expectedComments);

        APIViewResponse result = await _apiViewRevisionTool.GetRevisionComments(revisionId, "production");

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data, Is.EqualTo(expectedComments));
        Assert.That(result.ResponseError, Is.Null);
    }

    [Test]
    public async Task GetRevisionComments_WithValidRevisionIdAndEnvironment_ReturnsSuccess()
    {
        string revisionId = "test-revision-123";
        string environment = "staging";
        string expectedComments = "[{\"lineNo\":10,\"commentText\":\"Staging comment\",\"isResolved\":true}]";
        _mockApiViewService
            .Setup(x => x.GetCommentsByRevisionAsync(revisionId, environment))
            .ReturnsAsync(expectedComments);

        APIViewResponse result = await _apiViewRevisionTool.GetRevisionComments(revisionId, environment);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data, Is.EqualTo(expectedComments));
        Assert.That(result.ResponseError, Is.Null);
    }

    [Test]
    public async Task GetRevisionComments_WithValidUrl_ExtractsRevisionIdAndReturnsSuccess()
    {
        string apiViewUrl = "https://apiview.dev/review/123?activeApiRevisionId=test-revision-456";
        string expectedComments = "[{\"lineNo\":5,\"commentText\":\"URL comment\",\"isResolved\":false}]";
        _mockApiViewService
            .Setup(x => x.GetCommentsByRevisionAsync("test-revision-456", "production"))
            .ReturnsAsync(expectedComments);

        APIViewResponse result = await _apiViewRevisionTool.GetRevisionComments(apiViewUrl, "production");

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data, Is.EqualTo(expectedComments));
        Assert.That(result.ResponseError, Is.Null);
    }

    [Test]
    public async Task GetRevisionComments_WithInvalidUrl_ReturnsError()
    {
        string invalidUrl = "https://apiview.dev/review/123"; // Missing activeApiRevisionId

        APIViewResponse result = await _apiViewRevisionTool.GetRevisionComments(invalidUrl);

        Assert.That(result.Success, Is.False);
        Assert.That(result.ResponseError, Does.Contain("activeApiRevisionId"));
    }

    [Test]
    public async Task GetRevisionComments_WithMalformedUrl_ReturnsError()
    {
        string malformedUrl = "not-a-valid-url";

        APIViewResponse result = await _apiViewRevisionTool.GetRevisionComments(malformedUrl);

        _mockApiViewService
            .Setup(x => x.GetCommentsByRevisionAsync(malformedUrl, "production"))
            .ReturnsAsync((string?)null);

        result = await _apiViewRevisionTool.GetRevisionComments(malformedUrl);
        Assert.That(result.Success, Is.False);
        Assert.That(result.ResponseError, Does.Contain("Failed to retrieve comments for revision not-a-valid-url"));
    }

    [Test]
    public async Task GetRevisionComments_WhenServiceReturnsNull_ReturnsError()
    {
        string revisionId = "test-revision-123";
        _mockApiViewService
            .Setup(x => x.GetCommentsByRevisionAsync(revisionId, "production"))
            .ReturnsAsync((string?)null);

        APIViewResponse result = await _apiViewRevisionTool.GetRevisionComments(revisionId);

        Assert.That(result.Success, Is.False);
        Assert.That(result.ResponseError, Does.Contain("Failed to retrieve comments"));
    }

    [Test]
    public async Task GetRevisionComments_WhenServiceThrowsException_ReturnsError()
    {
        string revisionId = "test-revision-123";
        _mockApiViewService
            .Setup(x => x.GetCommentsByRevisionAsync(revisionId, "production"))
            .ThrowsAsync(new Exception("Service error"));

        APIViewResponse result = await _apiViewRevisionTool.GetRevisionComments(revisionId);

        Assert.That(result.Success, Is.False);
        Assert.That(result.ResponseError, Does.Contain("Failed to retrieve comments for revision test-revision-123"));
    }

    [Test]
    public async Task GetRevisionComments_WithEmptyRevisionId_ReturnsError()
    {
        string emptyRevisionId = "";
        APIViewResponse result = await _apiViewRevisionTool.GetRevisionComments(emptyRevisionId);

        Assert.That(result.Success, Is.False);
        Assert.That(result.ResponseError, Does.Contain("cannot be null or empty"));
    }

    [Test]
    public async Task UrlExtraction_WithValidApiViewUrl_ShouldExtractCorrectRevisionId()
    {
        string apiViewUrl =
            "https://spa.apiviewstagingtest.com/review/96672963bb5747db9d65d32df5f82d5a?activeApiRevisionId=2668702652604dad92d75feb1321c7a4";
        string expectedRevisionId = "2668702652604dad92d75feb1321c7a4";
        string expectedComments = "Test comments";

        _mockApiViewService
            .Setup(x => x.GetCommentsByRevisionAsync(expectedRevisionId, "production"))
            .ReturnsAsync(expectedComments);

        await _apiViewRevisionTool.GetRevisionComments(apiViewUrl, "production");
        _mockApiViewService.Verify(x => x.GetCommentsByRevisionAsync(expectedRevisionId, "production"), Times.Once);
    }

    [Test]
    public async Task UrlExtraction_WithPlainRevisionId_ShouldPassThroughUnchanged()
    {
        string plainRevisionId = "simple-revision-id-123";
        string expectedComments = "Test comments";

        _mockApiViewService
            .Setup(x => x.GetCommentsByRevisionAsync(plainRevisionId, "production"))
            .ReturnsAsync(expectedComments);

        await _apiViewRevisionTool.GetRevisionComments(plainRevisionId, "production");

        _mockApiViewService.Verify(x => x.GetCommentsByRevisionAsync(plainRevisionId, "production"), Times.Once);
    }
}
