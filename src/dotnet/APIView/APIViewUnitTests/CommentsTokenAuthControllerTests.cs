using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ApiView;
using APIView.Model.V2;
using APIViewWeb;
using APIViewWeb.Helpers;
using APIViewWeb.LeanControllers;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace APIViewUnitTests;

public class CommentsTokenAuthControllerTests
{
    private readonly CommentsTokenAuthController _controller;
    private readonly Mock<ICommentsManager> _mockCommentsManager;
    private readonly Mock<IAPIRevisionsManager> _mockApiRevisionsManager;
    private readonly Mock<IBlobCodeFileRepository> _mockCodeFileRepository;

    public CommentsTokenAuthControllerTests()
    {
        _mockCommentsManager = new Mock<ICommentsManager>();
        _mockApiRevisionsManager = new Mock<IAPIRevisionsManager>();
        _mockCodeFileRepository = new Mock<IBlobCodeFileRepository>();

        _controller = new CommentsTokenAuthController(
            _mockCommentsManager.Object,
            _mockApiRevisionsManager.Object,
            _mockCodeFileRepository.Object
        );
    }

    [Fact]
    public async Task GetRevisionComments_WithValidRevisionId_ReturnsComments()
    {
        // Arrange
        string apiRevisionId = "revision123";
        APIRevisionListItemModel mockRevision = CreateMockAPIRevision(apiRevisionId);
        RenderedCodeFile mockCodeFile = new(new ApiView.CodeFile());
        List<CommentItemModel> mockComments = new();
        List<ApiViewAgentComment> expectedComments = new();

        _mockApiRevisionsManager
            .Setup(x => x.GetAPIRevisionAsync(apiRevisionId))
            .ReturnsAsync(mockRevision);

        _mockCodeFileRepository
            .Setup(x => x.GetCodeFileAsync(mockRevision, false))
            .ReturnsAsync(mockCodeFile);

        _mockCommentsManager
            .Setup(x => x.GetCommentsAsync(mockRevision.ReviewId, false, CommentType.APIRevision))
            .ReturnsAsync(mockComments);

        // Act
        ActionResult<List<ApiViewAgentComment>> result = await _controller.GetRevisionComments(apiRevisionId);

        // Assert
        LeanJsonResult jsonResult = Assert.IsType<LeanJsonResult>(result.Result);
        Assert.IsType<List<ApiViewAgentComment>>(jsonResult.Value);
        _mockApiRevisionsManager.Verify(x => x.GetAPIRevisionAsync(apiRevisionId), Times.Once);
        _mockCodeFileRepository.Verify(x => x.GetCodeFileAsync(mockRevision, false), Times.Once);
        _mockCommentsManager.Verify(x => x.GetCommentsAsync(mockRevision.ReviewId, false, CommentType.APIRevision), Times.Once);
    }

    [Fact]
    public async Task GetRevisionComments_WithInvalidRevisionId_Returns404()
    {
        // Arrange
        string invalidRevisionId = "invalid123";

        _mockApiRevisionsManager
            .Setup(x => x.GetAPIRevisionAsync(invalidRevisionId))
            .ReturnsAsync((APIRevisionListItemModel)null);

        // Act
        ActionResult<List<ApiViewAgentComment>> result = await _controller.GetRevisionComments(invalidRevisionId);

        // Assert
        LeanJsonResult jsonResult = Assert.IsType<LeanJsonResult>(result.Result);
        Assert.Equal("API revision not found", jsonResult.Value);
        _mockApiRevisionsManager.Verify(x => x.GetAPIRevisionAsync(invalidRevisionId), Times.Once);
        _mockCodeFileRepository.Verify(x => x.GetCodeFileAsync(It.IsAny<APIRevisionListItemModel>(), It.IsAny<bool>()), Times.Never);
        _mockCommentsManager.Verify(x => x.GetCommentsAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CommentType>()), Times.Never);
    }

    [Fact]
    public async Task GetRevisionComments_ReturnsCommentsWithLineIdAndLineText()
    {
        string apiRevisionId = "revision456";
        string expectedLineId = "Azure.Core.HttpHeader.Common";
        string expectedLineText = "public class HttpHeader ";

        APIRevisionListItemModel mockRevision = CreateMockAPIRevision(apiRevisionId);
        CodeFile codeFile = new()
        {
            Language = "CSharp",
            ReviewLines =
            [
                new ReviewLine
                {
                    LineId = expectedLineId,
                    Tokens =
                    [
                        new ReviewToken { Value = "public" }, new ReviewToken { Value = "class" },
                        new ReviewToken { Value = "HttpHeader" }
                    ]
                }
            ]
        };

        RenderedCodeFile mockCodeFile = new(codeFile);
        List<CommentItemModel> mockComments =
        [
            new()
            {
                ElementId = expectedLineId,
                CommentText = "This needs review",
                CreatedBy = "testuser@example.com",
                CreatedOn = DateTime.UtcNow,
                Upvotes = [],
                Downvotes = [],
                IsResolved = false,
                ThreadId = "thread1"
            }
        ];

        _mockApiRevisionsManager
            .Setup(x => x.GetAPIRevisionAsync(apiRevisionId))
            .ReturnsAsync(mockRevision);

        _mockCodeFileRepository
            .Setup(x => x.GetCodeFileAsync(mockRevision, false))
            .ReturnsAsync(mockCodeFile);

        _mockCommentsManager
            .Setup(x => x.GetCommentsAsync(mockRevision.ReviewId, false, CommentType.APIRevision))
            .ReturnsAsync(mockComments);

        ActionResult<List<ApiViewAgentComment>> result = await _controller.GetRevisionComments(apiRevisionId);

        LeanJsonResult jsonResult = Assert.IsType<LeanJsonResult>(result.Result);
        List<ApiViewAgentComment> returnedComments = Assert.IsType<List<ApiViewAgentComment>>(jsonResult.Value);

        Assert.Single(returnedComments);
        ApiViewAgentComment comment = returnedComments[0];
        Assert.Equal(expectedLineId, comment.LineId);
        Assert.Equal(expectedLineText, comment.LineText);
        Assert.Equal(1, comment.LineNumber);
        Assert.Equal("This needs review", comment.CommentText);
    }

    private static APIRevisionListItemModel CreateMockAPIRevision(string id)
    {
        return new APIRevisionListItemModel
        {
            Id = id,
            ReviewId = "review123",
            Language = "CSharp",
            Files = [new APICodeFileModel { FileId = "file1" }],
            ChangeHistory = [],
            Approvers = [],
            AssignedReviewers = [],
            HeadingsOfSectionsWithDiff = new Dictionary<string, HashSet<int>>()
        };
    }
}
