using Azure.Sdk.Tools.Cli.Models.Responses;
using Azure.Sdk.Tools.Cli.Services.APIView;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.APIView;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.APIView;

[TestFixture]
public class ApiViewReviewToolTests
{
    [SetUp]
    public void Setup()
    {
        _logger = new TestLogger<APIViewReviewTool>();
        _mockApiViewService = new Mock<IAPIViewService>();
        apiViewReviewTool = new APIViewReviewTool(_logger, _mockApiViewService.Object);
    }

    private APIViewReviewTool apiViewReviewTool;
    private Mock<IAPIViewService> _mockApiViewService;
    private TestLogger<APIViewReviewTool> _logger;

    [Test]
    public async Task GetRevisionComments_WithValidRevisionIdAndEnvironment_ReturnsSuccess()
    {
        string revisionId = "test-revision-123";
        string apiViewUrl = $"https://apiview.dev/review/123?activeApiRevisionId={revisionId}";
        string expectedComments = "[{\"lineNo\":10,\"commentText\":\"Staging comment\",\"isResolved\":true}]";
        _mockApiViewService
            .Setup(x => x.GetCommentsByRevisionAsync(revisionId))
            .ReturnsAsync(expectedComments);

        APIViewResponse response = await apiViewReviewTool.GetComments(apiViewUrl);

        Assert.That(response.Result, Is.EqualTo(expectedComments));
        Assert.That(response.ResponseError, Is.Null);
    }

    [Test]
    public async Task GetRevisionComments_WithValidUrl_ExtractsRevisionIdAndReturnsSuccess()
    {
        string apiViewUrl = "https://apiview.dev/review/123?activeApiRevisionId=test-revision-456";
        string expectedComments = "[{\"lineNo\":5,\"commentText\":\"URL comment\",\"isResolved\":false}]";
        _mockApiViewService
            .Setup(x => x.GetCommentsByRevisionAsync("test-revision-456"))
            .ReturnsAsync(expectedComments);

        APIViewResponse response = await apiViewReviewTool.GetComments(apiViewUrl);

        Assert.That(response.Result, Is.EqualTo(expectedComments));
        Assert.That(response.ResponseError, Is.Null);
    }

    [Test]
    public async Task GetRevisionComments_WithInvalidUrl_ReturnsError()
    {
        string invalidUrl = "https://apiview.dev/review/123"; // Missing activeApiRevisionId

        APIViewResponse result = await apiViewReviewTool.GetComments(invalidUrl);

        Assert.That(result.ResponseError, Does.Contain("activeApiRevisionId"));
    }

    [Test]
    public async Task GetRevisionComments_WithMalformedUrl_ReturnsError()
    {
        string malformedUrl = "not-a-valid-url";

        APIViewResponse result = await apiViewReviewTool.GetComments(malformedUrl);

        _mockApiViewService
            .Setup(x => x.GetCommentsByRevisionAsync(malformedUrl))
            .ReturnsAsync((string?)null);

        result = await apiViewReviewTool.GetComments(malformedUrl);
        Assert.That(result.ResponseError, Does.Contain("Failed to get comments: Input needs to be a valid APIView URL"));
    }

    [Test]
    public async Task GetRevisionComments_WhenServiceReturnsNull_ReturnsError()
    {
        string revisionId = "test-revision-123";
        string apiViewUrl = $"https://apiview.dev/review/123?activeApiRevisionId={revisionId}";
        _mockApiViewService
            .Setup(x => x.GetCommentsByRevisionAsync(revisionId))
            .ReturnsAsync((string?)null);

        APIViewResponse result = await apiViewReviewTool.GetComments(apiViewUrl);

        Assert.That(result.ResponseError, Does.Contain("Failed to retrieve comments"));
    }

    [Test]
    public async Task GetRevisionComments_WhenServiceThrowsException_ReturnsError()
    {
        string revisionId = "test-revision-123";
        string apiViewUrl = $"https://apiview.dev/review/123?activeApiRevisionId={revisionId}";
        _mockApiViewService
            .Setup(x => x.GetCommentsByRevisionAsync(revisionId))
            .ThrowsAsync(new Exception("Service error"));

        APIViewResponse result = await apiViewReviewTool.GetComments(apiViewUrl);

        Assert.That(result.ResponseError, Does.Contain("Failed to get comments: Service error"));
    }

    [Test]
    public async Task GetRevisionComments_WithEmptyRevisionId_ReturnsError()
    {
        string emptyRevisionId = "";
        APIViewResponse result = await apiViewReviewTool.GetComments(emptyRevisionId);

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
            .Setup(x => x.GetCommentsByRevisionAsync(expectedRevisionId))
            .ReturnsAsync(expectedComments);

        await apiViewReviewTool.GetComments(apiViewUrl);
        _mockApiViewService.Verify(x => x.GetCommentsByRevisionAsync(expectedRevisionId), Times.Once);
    }
}
