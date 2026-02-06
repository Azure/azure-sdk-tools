// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.APIView;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Moq;
using OpenAI;
using OpenAI.Chat;

namespace Azure.Sdk.Tools.Cli.Tests.Services;

[TestFixture]
public class APIViewFeedbackServiceTests
{
    private Mock<IAPIViewService> _mockApiViewService = null!;
    private Mock<OpenAIClient> _mockOpenAIClient = null!;
    private Mock<IGitHubService> _mockGitHubService = null!;
    private TestLogger<APIViewFeedbackService> _logger = null!;
    private APIViewFeedbackService _service = null!;

    [SetUp]
    public void Setup()
    {
        _mockApiViewService = new Mock<IAPIViewService>();
        _mockOpenAIClient = new Mock<OpenAIClient>();
        _mockGitHubService = new Mock<IGitHubService>();
        _logger = new TestLogger<APIViewFeedbackService>();
        _service = new APIViewFeedbackService(
            _mockApiViewService.Object,
            _mockOpenAIClient.Object,
            _mockGitHubService.Object,
            _logger);
    }

    #region GetConsolidatedComments Tests

    [Test]
    public async Task GetConsolidatedComments_WithNoComments_ReturnsEmptyList()
    {
        // Arrange
        var revisionId = "test-revision";
        var emptyCommentsJson = "[]";
        _mockApiViewService.Setup(x => x.GetCommentsByRevisionAsync(revisionId))
            .ReturnsAsync(emptyCommentsJson);

        // Act
        var result = await _service.GetConsolidatedComments(revisionId);

        // Assert
        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task GetConsolidatedComments_WithResolvedAndQuestionComments_FiltersThemOut()
    {
        // Arrange
        var revisionId = "test-revision";
        var comments = new[]
        {
            CreateAPIViewComment("thread1", 1, "Resolved comment", isResolved: true),
            CreateAPIViewComment("thread2", 2, "Question comment", severity: "Question"),
            CreateAPIViewComment("thread3", 3, "Valid comment", isResolved: false)
        };
        var commentsJson = JsonSerializer.Serialize(comments);
        _mockApiViewService.Setup(x => x.GetCommentsByRevisionAsync(revisionId))
            .ReturnsAsync(commentsJson);

        // Act
        var result = await _service.GetConsolidatedComments(revisionId);

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].ThreadId, Is.EqualTo("thread3"));
        Assert.That(result[0].Comment, Is.EqualTo("Valid comment"));
    }

    [Test]
    public async Task GetConsolidatedComments_WithSingleCommentThread_ReturnsWithoutConsolidation()
    {
        // Arrange
        var revisionId = "test-revision";
        var comments = new[]
        {
            CreateAPIViewComment("thread1", 1, "Single comment", lineId: "id1", lineText: "line text")
        };
        var commentsJson = JsonSerializer.Serialize(comments);
        _mockApiViewService.Setup(x => x.GetCommentsByRevisionAsync(revisionId))
            .ReturnsAsync(commentsJson);

        // Act
        var result = await _service.GetConsolidatedComments(revisionId);

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].ThreadId, Is.EqualTo("thread1"));
        Assert.That(result[0].LineNo, Is.EqualTo(1));
        Assert.That(result[0].LineId, Is.EqualTo("id1"));
        Assert.That(result[0].LineText, Is.EqualTo("line text"));
        Assert.That(result[0].Comment, Is.EqualTo("Single comment"));
        
        // Verify OpenAI was NOT called for single comment
        _mockOpenAIClient.Verify(x => x.GetChatClient(It.IsAny<string>()), Times.Never);
    }

    #endregion

    #region ParseReviewMetadata Tests

    [Test]
    public async Task ParseReviewMetadata_WithValidResponse_ReturnsDeserializedMetadata()
    {
        // Arrange
        var revisionId = "test-revision";
        var metadata = new ReviewMetadata
        {
            ReviewId = "review123",
            PackageName = "TestPackage",
            Language = "Python",
            Revision = new RevisionMetadata { RevisionId = revisionId }
        };
        var metadataJson = JsonSerializer.Serialize(metadata);
        _mockApiViewService.Setup(x => x.GetMetadata(revisionId))
            .ReturnsAsync(metadataJson);

        // Act
        var result = await _service.ParseReviewMetadata(revisionId);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.ReviewId, Is.EqualTo("review123"));
        Assert.That(result.PackageName, Is.EqualTo("TestPackage"));
        Assert.That(result.Language, Is.EqualTo("Python"));
    }

    [Test]
    public void ParseReviewMetadata_WithEmptyResponse_ThrowsInvalidOperationException()
    {
        // Arrange
        var revisionId = "test-revision";
        _mockApiViewService.Setup(x => x.GetMetadata(revisionId))
            .ReturnsAsync(string.Empty);

        // Act & Assert
        var ex = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _service.ParseReviewMetadata(revisionId));
        Assert.That(ex!.Message, Does.Contain("Failed to get metadata for revision"));
    }

    [Test]
    public void ParseReviewMetadata_WithInvalidJson_ThrowsInvalidOperationException()
    {
        // Arrange
        var revisionId = "test-revision";
        _mockApiViewService.Setup(x => x.GetMetadata(revisionId))
            .ReturnsAsync("{invalid json");

        // Act & Assert
        var ex = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _service.ParseReviewMetadata(revisionId));
        Assert.That(ex!.Message, Does.Contain("Failed to parse metadata for revision"));
    }

    [Test]
    public void ParseReviewMetadata_WithNullAfterDeserialization_ThrowsInvalidOperationException()
    {
        // Arrange
        var revisionId = "test-revision";
        _mockApiViewService.Setup(x => x.GetMetadata(revisionId))
            .ReturnsAsync("null");

        // Act & Assert
        var ex = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _service.ParseReviewMetadata(revisionId));
        Assert.That(ex!.Message, Does.Contain("Failed to deserialize metadata for revision"));
    }

    #endregion

    #region DetectShaAndTspPath Tests

    [Test]
    public async Task DetectShaAndTspPath_WithPullRequestInSpecsRepo_ReturnsShaAndRepo()
    {
        // Arrange
        var metadata = new ReviewMetadata
        {
            PackageName = "TestPackage",
            Language = "Python",
            Revision = new RevisionMetadata
            {
                PullRequestNo = 123,
                PullRequestRepository = "Azure/azure-rest-api-specs"
            }
        };
        var expectedSha = "abc123def456";
        _mockGitHubService.Setup(x => x.GetPullRequestHeadSha("Azure", "azure-rest-api-specs", 123))
            .ReturnsAsync(expectedSha);

        // Act
        var (commitSha, tspProjectPath, targetRepo) = await _service.DetectShaAndTspPath(metadata);

        // Assert
        Assert.That(commitSha, Is.EqualTo(expectedSha));
        Assert.That(tspProjectPath, Is.Null);
        Assert.That(targetRepo, Is.EqualTo("Azure/azure-rest-api-specs"));
    }

    [Test]
    public async Task DetectShaAndTspPath_WithPullRequestInSdkRepo_ParsesTspLocation()
    {
        // Arrange
        var metadata = new ReviewMetadata
        {
            PackageName = "TestPackage",
            Language = "Python",
            Revision = new RevisionMetadata
            {
                PullRequestNo = 456,
                PullRequestRepository = "Azure/azure-sdk-for-python"
            }
        };
        var tspLocationYaml = @"
commit: 'sha789xyz'
directory: 'specification/foo/data-plane/Foo'
repo: 'Azure/azure-rest-api-specs'
";
        
        // Mock GitHub Code Search to find tsp-location.yaml
        // SearchCode constructor: (string name, string path, string sha, string url, string gitUrl, string htmlUrl, Repository repository)
        var searchCodeItem = new Octokit.SearchCode(
            "tsp-location.yaml",                        // name
            "sdk/search/TestPackage/tsp-location.yaml", // path
            null,                                        // sha
            null,                                        // url
            null,                                        // gitUrl
            null,                                        // htmlUrl
            null);                                       // repository
        var searchResult = new Octokit.SearchCodeResult(1, false, new[] { searchCodeItem });
        _mockGitHubService.Setup(x => x.SearchFilesAsync(It.Is<string>(q => q.Contains("azure-sdk-for-python"))))
            .ReturnsAsync(searchResult);
        
        _mockGitHubService.Setup(x => x.GetFileFromPullRequest("Azure", "azure-sdk-for-python", 456, "sdk/search/TestPackage/tsp-location.yaml"))
            .ReturnsAsync(tspLocationYaml);

        // Act
        var (commitSha, tspProjectPath, targetRepo) = await _service.DetectShaAndTspPath(metadata);

        // Assert - verify mocks were called
        _mockGitHubService.Verify(x => x.SearchFilesAsync(It.IsAny<string>()), Times.AtLeastOnce());
        _mockGitHubService.Verify(x => x.GetFileFromPullRequest("Azure", "azure-sdk-for-python", 456, "sdk/search/TestPackage/tsp-location.yaml"), Times.AtLeastOnce());
        
        Assert.That(commitSha, Is.EqualTo("sha789xyz"));
        Assert.That(tspProjectPath, Is.EqualTo("specification/foo/data-plane/Foo"));
        Assert.That(targetRepo, Is.EqualTo("Azure/azure-rest-api-specs"));
    }

    [Test]
    public async Task DetectShaAndTspPath_WithBranchInRevisionLabel_ParsesTspLocation()
    {
        // Arrange
        var metadata = new ReviewMetadata
        {
            PackageName = "TestPackage",
            Language = "Python",
            Revision = new RevisionMetadata
            {
                RevisionLabel = "feature/my-branch"
            }
        };
        var tspLocationYaml = @"
commit: 'branch-sha'
directory: 'specification/bar/resource-manager/Bar'
repo: 'Azure/azure-rest-api-specs'
";
        
        // Mock GitHub Code Search to find tsp-location.yaml
        // SearchCode constructor: (string name, string path, string sha, string url, string gitUrl, string htmlUrl, Repository repository)
        var searchCodeItem2 = new Octokit.SearchCode(
            "tsp-location.yaml",                      // name
            "sdk/bar/TestPackage/tsp-location.yaml",  // path
            null,                                      // sha
            null,                                      // url
            null,                                      // gitUrl
            null,                                      // htmlUrl
            null);                                     // repository
        var searchResult2 = new Octokit.SearchCodeResult(1, false, new[] { searchCodeItem2 });
        _mockGitHubService.Setup(x => x.SearchFilesAsync(It.Is<string>(q => q.Contains("azure-sdk-for-python"))))
            .ReturnsAsync(searchResult2);
        
        _mockGitHubService.Setup(x => x.GetFileFromBranch("Azure", "azure-sdk-for-python", "feature/my-branch", "sdk/bar/TestPackage/tsp-location.yaml"))
            .ReturnsAsync(tspLocationYaml);

        // Act
        var (commitSha, tspProjectPath, targetRepo) = await _service.DetectShaAndTspPath(metadata);

        // Assert
        Assert.That(commitSha, Is.EqualTo("branch-sha"));
        Assert.That(tspProjectPath, Is.EqualTo("specification/bar/resource-manager/Bar"));
        Assert.That(targetRepo, Is.EqualTo("Azure/azure-rest-api-specs"));
    }

    [Test]
    public async Task DetectShaAndTspPath_WithInvalidRepoFormat_ReturnsNulls()
    {
        // Arrange
        var metadata = new ReviewMetadata
        {
            PackageName = "TestPackage",
            Language = "Python",
            Revision = new RevisionMetadata
            {
                PullRequestNo = 789,
                PullRequestRepository = "InvalidRepoFormat"
            }
        };

        // Act
        var (commitSha, tspProjectPath, targetRepo) = await _service.DetectShaAndTspPath(metadata);

        // Assert
        Assert.That(commitSha, Is.Null);
        Assert.That(tspProjectPath, Is.Null);
        Assert.That(targetRepo, Is.Null);
        _mockGitHubService.Verify(x => x.GetPullRequestHeadSha(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Test]
    public async Task DetectShaAndTspPath_WithNoRevision_ReturnsNulls()
    {
        // Arrange
        var metadata = new ReviewMetadata
        {
            PackageName = "TestPackage",
            Language = "Python",
            Revision = null
        };

        // Act
        var (commitSha, tspProjectPath, targetRepo) = await _service.DetectShaAndTspPath(metadata);

        // Assert
        Assert.That(commitSha, Is.Null);
        Assert.That(tspProjectPath, Is.Null);
        Assert.That(targetRepo, Is.Null);
    }

    [Test]
    public async Task DetectShaAndTspPath_WithNoPrOrBranch_ReturnsNulls()
    {
        // Arrange
        var metadata = new ReviewMetadata
        {
            PackageName = "TestPackage",
            Language = "Python",
            Revision = new RevisionMetadata
            {
                PullRequestNo = null,
                RevisionLabel = null
            }
        };

        // Act
        var (commitSha, tspProjectPath, targetRepo) = await _service.DetectShaAndTspPath(metadata);

        // Assert
        Assert.That(commitSha, Is.Null);
        Assert.That(tspProjectPath, Is.Null);
        Assert.That(targetRepo, Is.Null);
    }

    #endregion

    #region ParseTspLocationYamlWithRepo Tests

    [Test]
    public void ParseTspLocationYaml_WithValidYaml_ReturnsCommitDirectoryAndRepo()
    {
        // Arrange
        var yamlContent = @"
commit: 'test-commit-sha'
directory: 'specification/test/data-plane/Test'
repo: 'Azure/azure-rest-api-specs'
";
        var service = new APIViewFeedbackService(
            _mockApiViewService.Object,
            _mockOpenAIClient.Object,
            _mockGitHubService.Object,
            _logger);

        // Use reflection to call private method
        var method = typeof(APIViewFeedbackService).GetMethod("ParseTspLocationYamlWithRepo", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        // Act
        var result = method!.Invoke(service, new object[] { yamlContent });
        var (commitSha, tspProjectPath, targetRepo) = ((string?, string?, string?))result!;

        // Assert
        Assert.That(commitSha, Is.EqualTo("test-commit-sha"));
        Assert.That(tspProjectPath, Is.EqualTo("specification/test/data-plane/Test"));
        Assert.That(targetRepo, Is.EqualTo("Azure/azure-rest-api-specs"));
    }

    [Test]
    public void ParseTspLocationYaml_WithMissingFields_ReturnsPartialData()
    {
        // Arrange
        var yamlContent = @"
commit: 'partial-sha'
repo: 'Azure/azure-rest-api-specs'
";
        var service = new APIViewFeedbackService(
            _mockApiViewService.Object,
            _mockOpenAIClient.Object,
            _mockGitHubService.Object,
            _logger);

        var method = typeof(APIViewFeedbackService).GetMethod("ParseTspLocationYamlWithRepo", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        // Act
        var result = method!.Invoke(service, new object[] { yamlContent });
        var (commitSha, tspProjectPath, targetRepo) = ((string?, string?, string?))result!;

        // Assert
        Assert.That(commitSha, Is.EqualTo("partial-sha"));
        Assert.That(tspProjectPath, Is.Null);
        Assert.That(targetRepo, Is.EqualTo("Azure/azure-rest-api-specs"));
    }

    [Test]
    public void ParseTspLocationYaml_WithMalformedYaml_ReturnsNulls()
    {
        // Arrange
        var yamlContent = "{ invalid yaml content @#$%";
        var service = new APIViewFeedbackService(
            _mockApiViewService.Object,
            _mockOpenAIClient.Object,
            _mockGitHubService.Object,
            _logger);

        var method = typeof(APIViewFeedbackService).GetMethod("ParseTspLocationYamlWithRepo", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        // Act
        var result = method!.Invoke(service, new object[] { yamlContent });
        var (commitSha, tspProjectPath, targetRepo) = ((string?, string?, string?))result!;

        // Assert
        Assert.That(commitSha, Is.Null);
        Assert.That(tspProjectPath, Is.Null);
        Assert.That(targetRepo, Is.Null);
    }

    #endregion

    #region ConsolidateComments Tests

    [Test]
    public async Task ConsolidateComments_WithOpenAIException_UsesFallback()
    {
        // Arrange
        var comments = new[]
        {
            CreateAPIViewComment("thread1", 1, "Comment 1", createdBy: "user1", createdOn: "2024-01-01"),
            CreateAPIViewComment("thread1", 1, "Comment 2", createdBy: "user2", createdOn: "2024-01-02")
        };
        var commentsJson = JsonSerializer.Serialize(comments);
        _mockApiViewService.Setup(x => x.GetCommentsByRevisionAsync(It.IsAny<string>()))
            .ReturnsAsync(commentsJson);

        var mockChatClient = new Mock<ChatClient>();
        mockChatClient.Setup(x => x.CompleteChatAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatCompletionOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("OpenAI API error"));
        _mockOpenAIClient.Setup(x => x.GetChatClient(It.IsAny<string>()))
            .Returns(mockChatClient.Object);

        // Act
        var result = await _service.GetConsolidatedComments("test-revision");

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Comment, Does.StartWith("["));
        Assert.That(result[0].Comment, Does.Contain("user1: Comment 1, user2: Comment 2"));
    }

    #endregion

    #region Helper Methods

    private static object CreateAPIViewComment(
        string threadId,
        int lineNo,
        string commentText,
        string? lineId = null,
        string? lineText = null,
        bool isResolved = false,
        string? severity = null,
        string? createdBy = null,
        string? createdOn = null)
    {
        return new
        {
            threadId,
            lineNo,
            _lineId = lineId,
            _lineText = lineText,
            commentText,
            isResolved,
            severity,
            createdBy,
            createdOn,
            upvotes = 0,
            downvotes = 0
        };
    }

    #endregion
}
