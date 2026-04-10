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
            .Setup(x => x.GetCommentsByRevisionAsync(revisionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedComments);

        APIViewResponse response = await apiViewReviewTool.GetComments(apiViewUrl, CancellationToken.None);

        Assert.That(response.Result, Is.EqualTo(expectedComments));
        Assert.That(response.ResponseError, Is.Null);
    }

    [Test]
    public async Task GetRevisionComments_WithValidUrl_ExtractsRevisionIdAndReturnsSuccess()
    {
        string apiViewUrl = "https://apiview.dev/review/123?activeApiRevisionId=test-revision-456";
        string expectedComments = "[{\"lineNo\":5,\"commentText\":\"URL comment\",\"isResolved\":false}]";
        _mockApiViewService
            .Setup(x => x.GetCommentsByRevisionAsync("test-revision-456", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedComments);

        APIViewResponse response = await apiViewReviewTool.GetComments(apiViewUrl, CancellationToken.None);

        Assert.That(response.Result, Is.EqualTo(expectedComments));
        Assert.That(response.ResponseError, Is.Null);
    }

    [Test]
    public async Task GetRevisionComments_WithInvalidUrl_ReturnsError()
    {
        string invalidUrl = "https://apiview.dev/review/123"; // Missing activeApiRevisionId

        APIViewResponse result = await apiViewReviewTool.GetComments(invalidUrl, CancellationToken.None);

        Assert.That(result.ResponseError, Does.Contain("activeApiRevisionId"));
    }

    [Test]
    public async Task GetRevisionComments_WithMalformedUrl_ReturnsError()
    {
        string malformedUrl = "not-a-valid-url";

        APIViewResponse result = await apiViewReviewTool.GetComments(malformedUrl, CancellationToken.None);

        _mockApiViewService
            .Setup(x => x.GetCommentsByRevisionAsync(malformedUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        result = await apiViewReviewTool.GetComments(malformedUrl, CancellationToken.None);
        Assert.That(result.ResponseError, Does.Contain("Failed to get comments: Input needs to be a valid APIView URL"));
    }

    [Test]
    public async Task GetRevisionComments_WhenServiceReturnsNull_ReturnsError()
    {
        string revisionId = "test-revision-123";
        string apiViewUrl = $"https://apiview.dev/review/123?activeApiRevisionId={revisionId}";
        _mockApiViewService
            .Setup(x => x.GetCommentsByRevisionAsync(revisionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        APIViewResponse result = await apiViewReviewTool.GetComments(apiViewUrl, CancellationToken.None);

        Assert.That(result.ResponseError, Does.Contain("Failed to retrieve comments"));
    }

    [Test]
    public async Task GetRevisionComments_WhenServiceThrowsException_ReturnsError()
    {
        string revisionId = "test-revision-123";
        string apiViewUrl = $"https://apiview.dev/review/123?activeApiRevisionId={revisionId}";
        _mockApiViewService
            .Setup(x => x.GetCommentsByRevisionAsync(revisionId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Service error"));

        APIViewResponse result = await apiViewReviewTool.GetComments(apiViewUrl, CancellationToken.None);

        Assert.That(result.ResponseError, Does.Contain("Failed to get comments: Service error"));
    }

    [Test]
    public async Task GetRevisionComments_WithEmptyRevisionId_ReturnsError()
    {
        string emptyRevisionId = "";
        APIViewResponse result = await apiViewReviewTool.GetComments(emptyRevisionId, CancellationToken.None);

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
            .Setup(x => x.GetCommentsByRevisionAsync(expectedRevisionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedComments);

        await apiViewReviewTool.GetComments(apiViewUrl, CancellationToken.None);
        _mockApiViewService.Verify(x => x.GetCommentsByRevisionAsync(expectedRevisionId, It.IsAny<CancellationToken>()), Times.Once);
    }

    // create-ci-revision CLI command tests

    [Test]
    public async Task CreateCIRevision_WithRequiredPipelineParams_ReturnsSuccess()
    {
        string expectedContent = "review created";
        _mockApiViewService
            .Setup(x => x.CreateCIReviewAsync(
                "12345", "packages", "azure-core-1.0.0.whl", "azure-core_python.json",
                "Azure/azure-sdk-for-python", "azure-core", "internal",
                "CI Build", false, null, false, null, null, default))
            .ReturnsAsync((expectedContent, 200));

        var command = apiViewReviewTool.GetCommandInstances().First(c => c.Name == "create-ci-revision");
        var parseResult = command.Parse(
            "create-ci-revision --build-id 12345 --artifact-name packages " +
            "--source-file-path azure-core-1.0.0.whl --code-token-file-path azure-core_python.json " +
            "--label \"CI Build\" --repo-name Azure/azure-sdk-for-python --package-name azure-core --project internal");
        var response = (APIViewResponse)await apiViewReviewTool.HandleCommand(parseResult, CancellationToken.None);

        Assert.That(response.Result, Is.EqualTo(expectedContent));
        Assert.That(response.ResponseError, Is.Null);
        Assert.That(response.Message, Does.Contain("approved"));
    }

    [Test]
    public async Task CreateCIRevision_WithDefaultsAndAutoLabel_UsesSourceBranchForLabel()
    {
        string expectedContent = "review created";
        _mockApiViewService
            .Setup(x => x.CreateCIReviewAsync(
                "12345", "packages", "azure-core-1.0.0.whl", "azure-core_python.json",
                "Azure/azure-sdk-for-python", "azure-core", "internal",
                "Source Branch:main", false, null, false, null, "main", default))
            .ReturnsAsync((expectedContent, 200));

        var command = apiViewReviewTool.GetCommandInstances().First(c => c.Name == "create-ci-revision");
        var parseResult = command.Parse(
            "create-ci-revision --build-id 12345 --artifact-name packages --project internal " +
            "--source-file-path azure-core-1.0.0.whl --code-token-file-path azure-core_python.json " +
            "--repo-name Azure/azure-sdk-for-python --package-name azure-core --source-branch main");
        var response = (APIViewResponse)await apiViewReviewTool.HandleCommand(parseResult, CancellationToken.None);

        Assert.That(response.Result, Is.EqualTo(expectedContent));
        Assert.That(response.ResponseError, Is.Null);
    }

    [Test]
    public async Task CreateCIRevision_WithAllPipelineParams_ReturnsSuccess()
    {
        string expectedContent = "review created";
        _mockApiViewService
            .Setup(x => x.CreateCIReviewAsync(
                "12345", "packages", "azure-core-1.0.0.whl", "azure-core_python.json",
                "Azure/azure-sdk-for-python", "azure-core", "internal",
                "CI Build", true, "1.0.0", true, "client", "refs/heads/main", default))
            .ReturnsAsync((expectedContent, 202));

        var command = apiViewReviewTool.GetCommandInstances().First(c => c.Name == "create-ci-revision");
        var parseResult = command.Parse(
            "create-ci-revision --build-id 12345 --artifact-name packages " +
            "--source-file-path azure-core-1.0.0.whl --code-token-file-path azure-core_python.json " +
            "--label \"CI Build\" --repo-name Azure/azure-sdk-for-python --package-name azure-core --project internal " +
            "--compare-all-revisions --package-version 1.0.0 --set-release-tag --package-type client --source-branch refs/heads/main");
        var response = (APIViewResponse)await apiViewReviewTool.HandleCommand(parseResult, CancellationToken.None);

        Assert.That(response.Result, Is.EqualTo(expectedContent));
        Assert.That(response.ResponseError, Is.Null);
    }

    [Test]
    public async Task CreateCIRevision_WhenServiceReturnsError_ReturnsError()
    {
        _mockApiViewService
            .Setup(x => x.CreateCIReviewAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(((string?)null, 500));

        var command = apiViewReviewTool.GetCommandInstances().First(c => c.Name == "create-ci-revision");
        var parseResult = command.Parse(
            "create-ci-revision --build-id 12345 --artifact-name packages " +
            "--source-file-path file.whl --code-token-file-path file.json " +
            "--label test --repo-name Azure/repo --package-name pkg --project internal");
        var response = (APIViewResponse)await apiViewReviewTool.HandleCommand(parseResult, CancellationToken.None);

        Assert.That(response.ResponseError, Does.Contain("Invalid status code"));
        Assert.That(response.ResponseError, Does.Contain("engineering systems"));
    }

    [Test]
    public async Task CreateCIRevision_WhenServiceThrowsException_ReturnsError()
    {
        _mockApiViewService
            .Setup(x => x.CreateCIReviewAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var command = apiViewReviewTool.GetCommandInstances().First(c => c.Name == "create-ci-revision");
        var parseResult = command.Parse(
            "create-ci-revision --build-id 12345 --artifact-name packages " +
            "--source-file-path file.whl --code-token-file-path file.json " +
            "--label test --repo-name Azure/repo --package-name pkg --project internal");
        var response = (APIViewResponse)await apiViewReviewTool.HandleCommand(parseResult, CancellationToken.None);

        Assert.That(response.ResponseError, Does.Contain("Failed to create API revision"));
        Assert.That(response.ResponseError, Does.Contain("Connection refused"));
    }

    [Test]
    public async Task CreateCIRevision_WhenPackageNotFoundInArtifacts_ReturnsError()
    {
        _mockApiViewService
            .Setup(x => x.CreateCIReviewAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(((string?)null, 204));

        var command = apiViewReviewTool.GetCommandInstances().First(c => c.Name == "create-ci-revision");
        var parseResult = command.Parse(
            "create-ci-revision --build-id 12345 --artifact-name packages " +
            "--source-file-path file.whl --code-token-file-path file.json " +
            "--label test --repo-name Azure/repo --package-name pkg --project internal");
        var response = (APIViewResponse)await apiViewReviewTool.HandleCommand(parseResult, CancellationToken.None);

        Assert.That(response.ResponseError, Does.Contain("Invalid status code"));
    }

    [Test]
    public async Task CreateCIRevision_WithInvalidRepoNameFormat_ReturnsError()
    {
        var command = apiViewReviewTool.GetCommandInstances().First(c => c.Name == "create-ci-revision");
        var parseResult = command.Parse(
            "create-ci-revision --build-id 12345 --artifact-name packages " +
            "--source-file-path file.whl --code-token-file-path file.json " +
            "--label test --repo-name azure-sdk-for-python --package-name pkg --project internal");
        var response = (APIViewResponse)await apiViewReviewTool.HandleCommand(parseResult, CancellationToken.None);

        Assert.That(response.ResponseError, Does.Contain("owner/repo"));
    }

    // create-pull-request-revision tests

    [Test]
    public async Task CreatePullRequestRevision_WithApiChanges_Returns201Success()
    {
        string expectedContent = "{\"message\":\"revision created\"}";
        _mockApiViewService
            .Setup(x => x.CreatePullRequestRevisionAsync(
                "99999", "packages", "azure-core/azure-core-1.0.0.whl", "abc123def",
                "Azure/azure-sdk-for-python", "azure-core",
                42, "azure-core_python.json", null, "python", "internal", null, null, default))
            .ReturnsAsync((expectedContent, 201));

        var commands = apiViewReviewTool.GetCommandInstances();
        var command = commands.First(c => c.Name == "create-pull-request-revision");
        var parseResult = command.Parse(
            "create-pull-request-revision --build-id 99999 --artifact-name packages " +
            "--source-file-path azure-core/azure-core-1.0.0.whl --commit-sha abc123def " +
            "--repo-name Azure/azure-sdk-for-python --package-name azure-core " +
            "--pull-request-number 42 --code-token-file-path azure-core_python.json --language python --project internal");
        var response = (APIViewResponse)await apiViewReviewTool.HandleCommand(parseResult, CancellationToken.None);

        Assert.That(response.ResponseError, Is.Null);
        Assert.That(response.Result, Is.EqualTo(expectedContent));
        Assert.That(response.Message, Does.Contain("API changes detected"));
    }

    [Test]
    public async Task CreatePullRequestRevision_WithNoApiChanges_Returns208Success()
    {
        string expectedContent = "{\"message\":\"no changes\"}";
        _mockApiViewService
            .Setup(x => x.CreatePullRequestRevisionAsync(
                "99999", "packages", "azure-core/azure-core-1.0.0.whl", "abc123def",
                "Azure/azure-sdk-for-python", "azure-core",
                42, "azure-core_python.json", null, null, "internal", null, null, default))
            .ReturnsAsync((expectedContent, 208));

        var commands = apiViewReviewTool.GetCommandInstances();
        var command = commands.First(c => c.Name == "create-pull-request-revision");
        var parseResult = command.Parse(
            "create-pull-request-revision --build-id 99999 --artifact-name packages " +
            "--source-file-path azure-core/azure-core-1.0.0.whl --commit-sha abc123def " +
            "--repo-name Azure/azure-sdk-for-python --package-name azure-core " +
            "--pull-request-number 42 --code-token-file-path azure-core_python.json --project internal");
        var response = (APIViewResponse)await apiViewReviewTool.HandleCommand(parseResult, CancellationToken.None);

        Assert.That(response.ResponseError, Is.Null);
        Assert.That(response.Result, Is.EqualTo(expectedContent));
        Assert.That(response.Message, Does.Contain("No API changes detected"));
    }

    [Test]
    public async Task CreatePullRequestRevision_WithAllParams_ReturnsSuccess()
    {
        string expectedContent = "{\"message\":\"revision created\"}";
        _mockApiViewService
            .Setup(x => x.CreatePullRequestRevisionAsync(
                "99999", "packages", "azure-core-1.0.0.whl", "abc123def",
                "Azure/azure-sdk-for-python", "azure-core",
                42, "azure-core_python.json", "azure-core_python_baseline.json",
                "python", "internal", "client", "typespec-metadata.json", default))
            .ReturnsAsync((expectedContent, 201));

        var commands = apiViewReviewTool.GetCommandInstances();
        var command = commands.First(c => c.Name == "create-pull-request-revision");
        var parseResult = command.Parse(
            "create-pull-request-revision --build-id 99999 --artifact-name packages " +
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
    public async Task CreatePullRequestRevision_WhenServiceReturnsError_ReturnsError()
    {
        _mockApiViewService
            .Setup(x => x.CreatePullRequestRevisionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(((string?)null, 500));

        var commands = apiViewReviewTool.GetCommandInstances();
        var command = commands.First(c => c.Name == "create-pull-request-revision");
        var parseResult = command.Parse(
            "create-pull-request-revision --build-id 99999 --artifact-name packages " +
            "--source-file-path file.whl --code-token-file-path file.json --commit-sha abc123 " +
            "--repo-name Azure/repo --package-name pkg --pull-request-number 1 --project internal");
        var response = (APIViewResponse)await apiViewReviewTool.HandleCommand(parseResult, CancellationToken.None);

        Assert.That(response.ResponseError, Does.Contain("Invalid status code"));
        Assert.That(response.ResponseError, Does.Contain("engineering systems"));
    }

    [Test]
    public async Task CreatePullRequestRevision_WhenServiceThrowsException_ReturnsError()
    {
        _mockApiViewService
            .Setup(x => x.CreatePullRequestRevisionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var commands = apiViewReviewTool.GetCommandInstances();
        var command = commands.First(c => c.Name == "create-pull-request-revision");
        var parseResult = command.Parse(
            "create-pull-request-revision --build-id 99999 --artifact-name packages " +
            "--source-file-path file.whl --code-token-file-path file.json --commit-sha abc123 " +
            "--repo-name Azure/repo --package-name pkg --pull-request-number 1 --project internal");
        var response = (APIViewResponse)await apiViewReviewTool.HandleCommand(parseResult, CancellationToken.None);

        Assert.That(response.ResponseError, Does.Contain("Failed to create API revision"));
        Assert.That(response.ResponseError, Does.Contain("Connection refused"));
    }

    [Test]
    public async Task CreatePullRequestRevision_WithInvalidRepoNameFormat_ReturnsError()
    {
        var commands = apiViewReviewTool.GetCommandInstances();
        var command = commands.First(c => c.Name == "create-pull-request-revision");
        var parseResult = command.Parse(
            "create-pull-request-revision --build-id 99999 --artifact-name packages " +
            "--source-file-path file.whl --code-token-file-path file.json --commit-sha abc123 " +
            "--repo-name azure-sdk-for-python --package-name pkg --pull-request-number 1 --project internal");
        var response = (APIViewResponse)await apiViewReviewTool.HandleCommand(parseResult, CancellationToken.None);

        Assert.That(response.ResponseError, Does.Contain("owner/repo"));
    }

    // get-review-url tests

    [Test]
    public async Task GetReviewUrlByPackage_WithValidPackageAndLanguage_ReturnsUrl()
    {
        string expectedUrl = "https://apiview.dev/review/abc123?activeApiRevisionId=rev456";
        _mockApiViewService
            .Setup(x => x.GetReviewUrlByPackageAsync("Azure.Storage.Blobs", "C#", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedUrl);

        APIViewResponse response = await apiViewReviewTool.GetReviewUrlByPackage("Azure.Storage.Blobs", "C#", null, CancellationToken.None);

        Assert.That(response.Result, Is.EqualTo(expectedUrl));
        Assert.That(response.ResponseError, Is.Null);
    }

    [Test]
    public async Task GetReviewUrlByPackage_WithVersion_PassesVersionToService()
    {
        string expectedUrl = "https://apiview.dev/review/abc123?activeApiRevisionId=rev456";
        _mockApiViewService
            .Setup(x => x.GetReviewUrlByPackageAsync("azure-storage-blob", "Python", "12.0.0", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedUrl);

        APIViewResponse response = await apiViewReviewTool.GetReviewUrlByPackage("azure-storage-blob", "Python", "12.0.0", CancellationToken.None);

        Assert.That(response.Result, Is.EqualTo(expectedUrl));
        Assert.That(response.ResponseError, Is.Null);
        _mockApiViewService.Verify(x => x.GetReviewUrlByPackageAsync("azure-storage-blob", "Python", "12.0.0", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GetReviewUrlByPackage_WhenServiceReturnsNull_ReturnsError()
    {
        _mockApiViewService
            .Setup(x => x.GetReviewUrlByPackageAsync("Unknown.Package", "C#", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        APIViewResponse response = await apiViewReviewTool.GetReviewUrlByPackage("Unknown.Package", "C#", null, CancellationToken.None);

        Assert.That(response.ResponseError, Does.Contain("Could not find an APIView review"));
        Assert.That(response.ResponseError, Does.Contain("Unknown.Package"));
        Assert.That(response.ResponseError, Does.Contain("C#"));
    }

    [Test]
    public async Task GetReviewUrlByPackage_WhenServiceReturnsNullWithVersion_IncludesVersionInError()
    {
        _mockApiViewService
            .Setup(x => x.GetReviewUrlByPackageAsync("Unknown.Package", "C#", "1.0.0", It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        APIViewResponse response = await apiViewReviewTool.GetReviewUrlByPackage("Unknown.Package", "C#", "1.0.0", CancellationToken.None);

        Assert.That(response.ResponseError, Does.Contain("1.0.0"));
    }

    [TestCase(null, "C#")]
    [TestCase("", "C#")]
    [TestCase("Azure.Storage.Blobs", null)]
    [TestCase("Azure.Storage.Blobs", "")]
    public async Task GetReviewUrlByPackage_WithMissingRequiredParams_ReturnsValidationError(string? package, string? language)
    {
        APIViewResponse response = await apiViewReviewTool.GetReviewUrlByPackage(package!, language!, null, CancellationToken.None);

        Assert.That(response.ResponseError, Does.Contain("required"));
    }

    [Test]
    public async Task GetReviewUrlByPackage_WithUnsupportedLanguage_ReturnsValidationError()
    {
        APIViewResponse response = await apiViewReviewTool.GetReviewUrlByPackage("Azure.Storage.Blobs", "COBOL", null, CancellationToken.None);

        Assert.That(response.ResponseError, Does.Contain("Unsupported language 'COBOL'"));
        Assert.That(response.ResponseError, Does.Contain("Supported languages are:"));
        Assert.That(response.ResponseError, Does.Contain("C#"));
        Assert.That(response.ResponseError, Does.Contain("Python"));
        _mockApiViewService.Verify(x => x.GetReviewUrlByPackageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestCase("csharp", "C#")]
    [TestCase("dotnet", "C#")]
    [TestCase("net", "C#")]
    [TestCase(".net", "C#")]
    [TestCase("js", "JavaScript")]
    [TestCase("typescript", "JavaScript")]
    [TestCase("cpp", "C++")]
    [TestCase("golang", "Go")]
    [TestCase("py", "Python")]
    [TestCase("PYTHON", "Python")]
    [TestCase("java", "Java")]
    public async Task GetReviewUrlByPackage_WithLanguageAlias_ResolvesAndCallsService(string alias, string expectedCanonical)
    {
        string expectedUrl = "https://apiview.dev/review/abc123?activeApiRevisionId=rev456";
        _mockApiViewService
            .Setup(x => x.GetReviewUrlByPackageAsync("Azure.Core", expectedCanonical, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedUrl);

        APIViewResponse response = await apiViewReviewTool.GetReviewUrlByPackage("Azure.Core", alias, null, CancellationToken.None);

        Assert.That(response.Result, Is.EqualTo(expectedUrl));
        Assert.That(response.ResponseError, Is.Null);
        _mockApiViewService.Verify(x => x.GetReviewUrlByPackageAsync("Azure.Core", expectedCanonical, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestCase("csharp", "C#")]
    [TestCase("dotnet", "C#")]
    [TestCase("net", "C#")]
    [TestCase(".net", "C#")]
    [TestCase("js", "JavaScript")]
    [TestCase("typescript", "JavaScript")]
    [TestCase("cpp", "C++")]
    [TestCase("golang", "Go")]
    [TestCase("py", "Python")]
    public void ResolveLanguage_WithAlias_ReturnsCanonicalName(string alias, string expectedCanonical)
    {
        string? result = APIViewReviewTool.ResolveLanguage(alias);
        Assert.That(result, Is.EqualTo(expectedCanonical));
    }

    [TestCase("COBOL")]
    [TestCase("Ruby")]
    [TestCase("unknown")]
    public void ResolveLanguage_WithUnknownLanguage_ReturnsNull(string language)
    {
        string? result = APIViewReviewTool.ResolveLanguage(language);
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetReviewUrlByPackage_WhenServiceThrowsException_ReturnsError()
    {
        _mockApiViewService
            .Setup(x => x.GetReviewUrlByPackageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        APIViewResponse response = await apiViewReviewTool.GetReviewUrlByPackage("Azure.Storage.Blobs", "C#", null, CancellationToken.None);

        Assert.That(response.ResponseError, Does.Contain("Failed to get review URL"));
        Assert.That(response.ResponseError, Does.Contain("Azure.Storage.Blobs"));
        Assert.That(response.ResponseError, Does.Contain("Connection refused"));
    }

    [Test]
    public async Task GetReviewUrl_ViaCliCommand_CallsServiceWithCorrectParams()
    {
        string expectedUrl = "https://apiview.dev/review/abc?activeApiRevisionId=rev";
        _mockApiViewService
            .Setup(x => x.GetReviewUrlByPackageAsync("Azure.Core", "C#", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedUrl);

        var command = apiViewReviewTool.GetCommandInstances().First(c => c.Name == "get-review-url");
        var parseResult = command.Parse("get-review-url --package-name Azure.Core --language C#");
        var response = (APIViewResponse)await apiViewReviewTool.HandleCommand(parseResult, CancellationToken.None);

        Assert.That(response.Result, Is.EqualTo(expectedUrl));
        Assert.That(response.ResponseError, Is.Null);
    }

    // request-copilot-review tests

    [Test]
    public async Task RequestCopilotReview_WithApiText_StartsReviewAndReturnsContent()
    {
        string apiText = "public class Foo { }";
        string expectedContent = "{\"jobId\":\"job-123\"}";
        _mockApiViewService
            .Setup(x => x.StartCopilotReviewAsync(apiText, null, null, null, null, default))
            .ReturnsAsync((expectedContent, 200));

        APIViewResponse response = await apiViewReviewTool.RequestCopilotReview(apiText: apiText);

        Assert.That(response.ResponseError, Is.Null);
        Assert.That(response.Result, Is.EqualTo(expectedContent));
        Assert.That(response.Message, Does.Contain("get-copilot-review"));
    }

    [Test]
    public async Task RequestCopilotReview_WithApiViewUrl_FetchesContentAndStartsReview()
    {
        string revisionId = "rev-abc";
        string reviewId = "review-xyz";
        string apiViewUrl = $"https://apiview.dev/review/{reviewId}?activeApiRevisionId={revisionId}";
        string fetchedText = "public class Bar { }";
        string expectedContent = "{\"jobId\":\"job-456\"}";
        _mockApiViewService
            .Setup(x => x.GetRevisionContent(revisionId, reviewId, "text", It.IsAny<CancellationToken>()))
            .ReturnsAsync(fetchedText);
        _mockApiViewService
            .Setup(x => x.StartCopilotReviewAsync(fetchedText, null, null, null, null, default))
            .ReturnsAsync((expectedContent, 200));

        APIViewResponse response = await apiViewReviewTool.RequestCopilotReview(apiViewUrl: apiViewUrl);

        Assert.That(response.ResponseError, Is.Null);
        Assert.That(response.Result, Is.EqualTo(expectedContent));
        _mockApiViewService.Verify(x => x.GetRevisionContent(revisionId, reviewId, "text", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task RequestCopilotReview_WithNeitherUrlNorApiText_ReturnsError()
    {
        APIViewResponse response = await apiViewReviewTool.RequestCopilotReview();

        Assert.That(response.ResponseError, Does.Contain("--url or --api-text"));
    }

    [Test]
    public async Task RequestCopilotReview_WhenServiceReturnsEmpty_ReturnsError()
    {
        _mockApiViewService
            .Setup(x => x.StartCopilotReviewAsync(It.IsAny<string>(), null, null, null, null, default))
            .ReturnsAsync(((string?)null, 500));

        APIViewResponse response = await apiViewReviewTool.RequestCopilotReview(apiText: "some text");

        Assert.That(response.ResponseError, Does.Contain("Failed to start Copilot review job"));
        Assert.That(response.ResponseError, Does.Contain("500"));
    }

    [Test]
    public async Task RequestCopilotReview_WhenServiceThrowsException_ReturnsError()
    {
        _mockApiViewService
            .Setup(x => x.StartCopilotReviewAsync(It.IsAny<string>(), null, null, null, null, default))
            .ThrowsAsync(new Exception("Upstream error"));

        APIViewResponse response = await apiViewReviewTool.RequestCopilotReview(apiText: "some text");

        Assert.That(response.ResponseError, Does.Contain("Failed to submit Copilot review"));
        Assert.That(response.ResponseError, Does.Contain("Upstream error"));
    }

    // get-copilot-review tests

    [Test]
    public async Task GetCopilotReview_WithValidJobId_ReturnsContent()
    {
        string jobId = "job-789";
        string expectedContent = "{\"status\":\"completed\",\"comments\":[]}";
        _mockApiViewService
            .Setup(x => x.GetCopilotReviewAsync(jobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((expectedContent, 200));

        APIViewResponse response = await apiViewReviewTool.GetCopilotReview(jobId);

        Assert.That(response.ResponseError, Is.Null);
        Assert.That(response.Result, Is.EqualTo(expectedContent));
    }

    [Test]
    public async Task GetCopilotReview_WhenServiceReturnsNull_ReturnsError()
    {
        string jobId = "job-missing";
        _mockApiViewService
            .Setup(x => x.GetCopilotReviewAsync(jobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((string?)null, 404));

        APIViewResponse response = await apiViewReviewTool.GetCopilotReview(jobId);

        Assert.That(response.ResponseError, Does.Contain(jobId));
    }

    [Test]
    public async Task GetCopilotReview_WhenServiceThrowsException_ReturnsError()
    {
        string jobId = "job-error";
        _mockApiViewService
            .Setup(x => x.GetCopilotReviewAsync(jobId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Timeout"));

        APIViewResponse response = await apiViewReviewTool.GetCopilotReview(jobId);

        Assert.That(response.ResponseError, Does.Contain("Failed to get Copilot review results"));
        Assert.That(response.ResponseError, Does.Contain("Timeout"));
    }
}
