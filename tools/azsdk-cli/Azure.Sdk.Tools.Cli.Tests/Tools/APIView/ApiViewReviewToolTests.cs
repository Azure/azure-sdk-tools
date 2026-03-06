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

    // create-review CLI command tests

    [Test]
    public async Task CreateReview_WithRequiredPipelineParams_ReturnsSuccess()
    {
        string expectedContent = "review created";
        _mockApiViewService
            .Setup(x => x.CreateReviewFromPipelineAsync(
                "12345", "packages", "azure-core-1.0.0.whl", "azure-core_python.json",
                "azure-sdk-for-python", "azure-core", "internal",
                "CI Build", false, null, false, null, null))
            .ReturnsAsync((expectedContent, 200));

        var command = apiViewReviewTool.GetCommandInstances().First(c => c.Name == "create-review");
        var parseResult = command.Parse(
            "create-review --build-id 12345 --artifact-name packages " +
            "--source-file-path azure-core-1.0.0.whl --code-token-file-path azure-core_python.json " +
            "--label \"CI Build\" --repo-name azure-sdk-for-python --package-name azure-core --project internal");
        var response = (APIViewResponse)await apiViewReviewTool.HandleCommand(parseResult, CancellationToken.None);

        Assert.That(response.Result, Is.EqualTo(expectedContent));
        Assert.That(response.ResponseError, Is.Null);
        Assert.That(response.Message, Does.Contain("approved"));
    }

    [Test]
    public async Task CreateReview_WithDefaultsAndAutoLabel_UsesSourceBranchForLabel()
    {
        string expectedContent = "review created";
        _mockApiViewService
            .Setup(x => x.CreateReviewFromPipelineAsync(
                "12345", "packages", "azure-core-1.0.0.whl", "azure-core_python.json",
                "azure-sdk-for-python", "azure-core", "internal",
                "Source Branch:main", false, null, false, null, "main"))
            .ReturnsAsync((expectedContent, 200));

        var command = apiViewReviewTool.GetCommandInstances().First(c => c.Name == "create-review");
        var parseResult = command.Parse(
            "create-review --build-id 12345 --artifact-name packages --project internal " +
            "--source-file-path azure-core-1.0.0.whl --code-token-file-path azure-core_python.json " +
            "--repo-name azure-sdk-for-python --package-name azure-core --source-branch main");
        var response = (APIViewResponse)await apiViewReviewTool.HandleCommand(parseResult, CancellationToken.None);

        Assert.That(response.Result, Is.EqualTo(expectedContent));
        Assert.That(response.ResponseError, Is.Null);
    }

    [Test]
    public async Task CreateReview_WithAllPipelineParams_ReturnsSuccess()
    {
        string expectedContent = "review created";
        _mockApiViewService
            .Setup(x => x.CreateReviewFromPipelineAsync(
                "12345", "packages", "azure-core-1.0.0.whl", "azure-core_python.json",
                "azure-sdk-for-python", "azure-core", "internal",
                "CI Build", true, "1.0.0", true, "client", "refs/heads/main"))
            .ReturnsAsync((expectedContent, 202));

        var command = apiViewReviewTool.GetCommandInstances().First(c => c.Name == "create-review");
        var parseResult = command.Parse(
            "create-review --build-id 12345 --artifact-name packages " +
            "--source-file-path azure-core-1.0.0.whl --code-token-file-path azure-core_python.json " +
            "--label \"CI Build\" --repo-name azure-sdk-for-python --package-name azure-core --project internal " +
            "--compare-all-revisions --package-version 1.0.0 --set-release-tag --package-type client --source-branch refs/heads/main");
        var response = (APIViewResponse)await apiViewReviewTool.HandleCommand(parseResult, CancellationToken.None);

        Assert.That(response.Result, Is.EqualTo(expectedContent));
        Assert.That(response.ResponseError, Is.Null);
    }

    [Test]
    public async Task CreateReview_WhenServiceReturnsError_ReturnsError()
    {
        _mockApiViewService
            .Setup(x => x.CreateReviewFromPipelineAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(((string?)null, 500));

        var command = apiViewReviewTool.GetCommandInstances().First(c => c.Name == "create-review");
        var parseResult = command.Parse(
            "create-review --build-id 12345 --artifact-name packages " +
            "--source-file-path file.whl --code-token-file-path file.json " +
            "--label test --repo-name repo --package-name pkg --project internal");
        var response = (APIViewResponse)await apiViewReviewTool.HandleCommand(parseResult, CancellationToken.None);

        Assert.That(response.ResponseError, Does.Contain("Invalid status code"));
        Assert.That(response.ResponseError, Does.Contain("engineering systems"));
    }

    [Test]
    public async Task CreateReview_WhenServiceThrowsException_ReturnsError()
    {
        _mockApiViewService
            .Setup(x => x.CreateReviewFromPipelineAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var command = apiViewReviewTool.GetCommandInstances().First(c => c.Name == "create-review");
        var parseResult = command.Parse(
            "create-review --build-id 12345 --artifact-name packages " +
            "--source-file-path file.whl --code-token-file-path file.json " +
            "--label test --repo-name repo --package-name pkg --project internal");
        var response = (APIViewResponse)await apiViewReviewTool.HandleCommand(parseResult, CancellationToken.None);

        Assert.That(response.ResponseError, Does.Contain("Failed to create API review"));
        Assert.That(response.ResponseError, Does.Contain("Connection refused"));
    }

    [Test]
    public async Task CreateReview_WhenPackageNotFoundInArtifacts_ReturnsError()
    {
        _mockApiViewService
            .Setup(x => x.CreateReviewFromPipelineAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(((string?)null, 204));

        var command = apiViewReviewTool.GetCommandInstances().First(c => c.Name == "create-review");
        var parseResult = command.Parse(
            "create-review --build-id 12345 --artifact-name packages " +
            "--source-file-path file.whl --code-token-file-path file.json " +
            "--label test --repo-name repo --package-name pkg --project internal");
        var response = (APIViewResponse)await apiViewReviewTool.HandleCommand(parseResult, CancellationToken.None);

        Assert.That(response.ResponseError, Does.Contain("Invalid status code"));
    }

    // create-api-revision-if-changes tests

    [Test]
    public async Task CreateApiRevisionIfChanges_WithApiChanges_Returns201Success()
    {
        string expectedContent = "{\"message\":\"revision created\"}";
        _mockApiViewService
            .Setup(x => x.CreateApiRevisionIfChangesAsync(
                "99999", "packages", "azure-core/azure-core-1.0.0.whl", "abc123def",
                "Azure/azure-sdk-for-python", "azure-core",
                42, "azure-core_python.json", null, "python", "internal", null, null))
            .ReturnsAsync((expectedContent, 201));

        var commands = apiViewReviewTool.GetCommandInstances();
        var command = commands.First(c => c.Name == "create-api-revision-if-changes");
        var parseResult = command.Parse(
            "create-api-revision-if-changes --build-id 99999 --artifact-name packages " +
            "--source-file-path azure-core/azure-core-1.0.0.whl --commit-sha abc123def " +
            "--repo-name Azure/azure-sdk-for-python --package-name azure-core " +
            "--pull-request-number 42 --code-token-file-path azure-core_python.json --language python --project internal");
        var response = (APIViewResponse)await apiViewReviewTool.HandleCommand(parseResult, CancellationToken.None);

        Assert.That(response.ResponseError, Is.Null);
        Assert.That(response.Result, Is.EqualTo(expectedContent));
        Assert.That(response.Message, Does.Contain("API changes detected"));
    }

    [Test]
    public async Task CreateApiRevisionIfChanges_WithNoApiChanges_Returns208Success()
    {
        string expectedContent = "{\"message\":\"no changes\"}";
        _mockApiViewService
            .Setup(x => x.CreateApiRevisionIfChangesAsync(
                "99999", "packages", "azure-core/azure-core-1.0.0.whl", "abc123def",
                "Azure/azure-sdk-for-python", "azure-core",
                42, "azure-core_python.json", null, null, "internal", null, null))
            .ReturnsAsync((expectedContent, 208));

        var commands = apiViewReviewTool.GetCommandInstances();
        var command = commands.First(c => c.Name == "create-api-revision-if-changes");
        var parseResult = command.Parse(
            "create-api-revision-if-changes --build-id 99999 --artifact-name packages " +
            "--source-file-path azure-core/azure-core-1.0.0.whl --commit-sha abc123def " +
            "--repo-name Azure/azure-sdk-for-python --package-name azure-core " +
            "--pull-request-number 42 --code-token-file-path azure-core_python.json --project internal");
        var response = (APIViewResponse)await apiViewReviewTool.HandleCommand(parseResult, CancellationToken.None);

        Assert.That(response.ResponseError, Is.Null);
        Assert.That(response.Result, Is.EqualTo(expectedContent));
        Assert.That(response.Message, Does.Contain("No API changes detected"));
    }

    [Test]
    public async Task CreateApiRevisionIfChanges_WithAllParams_ReturnsSuccess()
    {
        string expectedContent = "{\"message\":\"revision created\"}";
        _mockApiViewService
            .Setup(x => x.CreateApiRevisionIfChangesAsync(
                "99999", "packages", "azure-core-1.0.0.whl", "abc123def",
                "Azure/azure-sdk-for-python", "azure-core",
                42, "azure-core_python.json", "azure-core_python_baseline.json",
                "python", "internal", "client", "typespec-metadata.json"))
            .ReturnsAsync((expectedContent, 201));

        var commands = apiViewReviewTool.GetCommandInstances();
        var command = commands.First(c => c.Name == "create-api-revision-if-changes");
        var parseResult = command.Parse(
            "create-api-revision-if-changes --build-id 99999 --artifact-name packages " +
            "--source-file-path azure-core-1.0.0.whl --commit-sha abc123def " +
            "--repo-name Azure/azure-sdk-for-python --package-name azure-core " +
            "--pull-request-number 42 --code-token-file-path azure-core_python.json " +
            "--baseline-code-file azure-core_python_baseline.json " +
            "--language python --project internal --package-type client --metadata-file typespec-metadata.json");
        var response = (APIViewResponse)await apiViewReviewTool.HandleCommand(parseResult, CancellationToken.None);

        Assert.That(response.ResponseError, Is.Null);
        Assert.That(response.Result, Is.EqualTo(expectedContent));
        Assert.That(response.Message, Does.Contain("API changes detected"));
    }

    [Test]
    public async Task CreateApiRevisionIfChanges_WhenServiceReturnsError_ReturnsError()
    {
        _mockApiViewService
            .Setup(x => x.CreateApiRevisionIfChangesAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(((string?)null, 500));

        var commands = apiViewReviewTool.GetCommandInstances();
        var command = commands.First(c => c.Name == "create-api-revision-if-changes");
        var parseResult = command.Parse(
            "create-api-revision-if-changes --build-id 99999 --artifact-name packages " +
            "--source-file-path file.whl --code-token-file-path file.json --commit-sha abc123 " +
            "--repo-name Azure/repo --package-name pkg --pull-request-number 1 --project internal");
        var response = (APIViewResponse)await apiViewReviewTool.HandleCommand(parseResult, CancellationToken.None);

        Assert.That(response.ResponseError, Does.Contain("Invalid status code"));
        Assert.That(response.ResponseError, Does.Contain("engineering systems"));
    }

    [Test]
    public async Task CreateApiRevisionIfChanges_WhenServiceThrowsException_ReturnsError()
    {
        _mockApiViewService
            .Setup(x => x.CreateApiRevisionIfChangesAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var commands = apiViewReviewTool.GetCommandInstances();
        var command = commands.First(c => c.Name == "create-api-revision-if-changes");
        var parseResult = command.Parse(
            "create-api-revision-if-changes --build-id 99999 --artifact-name packages " +
            "--source-file-path file.whl --code-token-file-path file.json --commit-sha abc123 " +
            "--repo-name Azure/repo --package-name pkg --pull-request-number 1 --project internal");
        var response = (APIViewResponse)await apiViewReviewTool.HandleCommand(parseResult, CancellationToken.None);

        Assert.That(response.ResponseError, Does.Contain("Failed to create API revision"));
        Assert.That(response.ResponseError, Does.Contain("Connection refused"));
    }
}
