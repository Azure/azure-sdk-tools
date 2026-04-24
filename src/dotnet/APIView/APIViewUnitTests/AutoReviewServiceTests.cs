using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using ApiView;
using APIViewWeb;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Models;
using FluentAssertions;
using Moq;
using Xunit;

namespace APIViewUnitTests
{
    public class AutoReviewServiceTests
    {
        private readonly Mock<IReviewManager> _mockReviewManager;
        private readonly Mock<IAPIRevisionsManager> _mockApiRevisionsManager;
        private readonly Mock<ICommentsManager> _mockCommentsManager;
        private readonly Mock<IProjectsManager> _mockProjectsManager;
        private readonly Mock<ICodeFileManager> _mockCodeFileManager;
        private readonly Mock<IAPIVersionsManager> _mockApiVersionsManager;
        private readonly AutoReviewService _service;
        private readonly ClaimsPrincipal _testUser;

        public AutoReviewServiceTests()
        {
            _mockReviewManager = new Mock<IReviewManager>();
            _mockApiRevisionsManager = new Mock<IAPIRevisionsManager>();
            _mockCommentsManager = new Mock<ICommentsManager>();
            _mockProjectsManager = new Mock<IProjectsManager>();
            _mockCodeFileManager = new Mock<ICodeFileManager>();
            _mockApiVersionsManager = new Mock<IAPIVersionsManager>();
            _mockCodeFileManager
                .Setup(x => x.ComputeAPIContentHashAsync(It.IsAny<CodeFile>()))
                .ReturnsAsync("test-hash");

            _service = new AutoReviewService(
                _mockReviewManager.Object,
                _mockApiRevisionsManager.Object,
                _mockCommentsManager.Object,
                _mockProjectsManager.Object,
                _mockCodeFileManager.Object,
                _mockApiVersionsManager.Object);

            var claims = new List<Claim>
            {
                new Claim("urn:github:login", "testuser")
            };
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            _testUser = new ClaimsPrincipal(identity);
        }

        #region Package Type Parsing Tests

        [Theory]
        [InlineData("client", PackageType.client)]
        [InlineData("mgmt", PackageType.mgmt)]
        [InlineData("CLIENT", PackageType.client)]
        [InlineData("MGMT", PackageType.mgmt)]
        [InlineData("Client", PackageType.client)]
        [InlineData("Mgmt", PackageType.mgmt)]
        public async Task CreateAutomaticRevisionAsync_WithValidPackageType_CreatesReviewWithCorrectPackageType(string packageTypeValue, PackageType expectedPackageType)
        {
            var codeFile = CreateCodeFile("TestPackage", "1.0.0", "C#");
            using var memoryStream = new MemoryStream();

            _mockReviewManager.Setup(m => m.GetReviewAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool?>()))
                .ReturnsAsync((ReviewListItemModel)null); // No existing review

            _mockReviewManager.Setup(m => m.CreateReviewAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<PackageType?>(), It.IsAny<string>()))
                .ReturnsAsync(new ReviewListItemModel
                {
                    Id = "new-review-id",
                    PackageName = "TestPackage",
                    Language = "C#",
                    PackageType = expectedPackageType
                });

            _mockApiRevisionsManager.Setup(m => m.CreateAPIRevisionAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<APIRevisionType>(),
                    It.IsAny<string>(), It.IsAny<MemoryStream>(), It.IsAny<CodeFile>(),
                    It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>()))
                .ReturnsAsync(new APIRevisionListItemModel { Id = "revision-id", ReviewId = "new-review-id" });

             await _service.CreateAutomaticRevisionAsync(
                _testUser, codeFile, "test-label", "test.json", memoryStream, packageTypeValue);

            _mockReviewManager.Verify(m => m.CreateReviewAsync(
                "TestPackage",
                "C#",
                false,
                It.Is<PackageType?>(pt => pt.HasValue && pt.Value == expectedPackageType),
                null),
                Times.Once);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("invalid")]
        [InlineData("unknown")]
        public async Task CreateAutomaticRevisionAsync_WithInvalidPackageType_CreatesReviewWithNullPackageType(string packageTypeValue)
        {
            var codeFile = CreateCodeFile("TestPackage", "1.0.0", "C#");
            using var memoryStream = new MemoryStream();

            _mockReviewManager.Setup(m => m.GetReviewAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool?>()))
                .ReturnsAsync((ReviewListItemModel)null);

            _mockReviewManager.Setup(m => m.CreateReviewAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<PackageType?>(), It.IsAny<string>()))
                .ReturnsAsync(new ReviewListItemModel
                {
                    Id = "new-review-id",
                    PackageName = "TestPackage",
                    Language = "C#",
                    PackageType = null
                });

            _mockApiRevisionsManager.Setup(m => m.CreateAPIRevisionAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<APIRevisionType>(),
                    It.IsAny<string>(), It.IsAny<MemoryStream>(), It.IsAny<CodeFile>(),
                    It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>()))
                .ReturnsAsync(new APIRevisionListItemModel { Id = "revision-id", ReviewId = "new-review-id" });

            await _service.CreateAutomaticRevisionAsync(
                _testUser, codeFile, "test-label", "test.json", memoryStream, packageTypeValue);

            _mockReviewManager.Verify(m => m.CreateReviewAsync(
                "TestPackage",
                "C#",
                false,
                It.Is<PackageType?>(pt => !pt.HasValue),
                null),
                Times.Once);
        }

        #endregion

        #region Existing Review Package Type Update Tests

        [Fact]
        public async Task CreateAutomaticRevisionAsync_WithExistingReviewWithoutPackageType_UpdatesPackageType()
        {
            var codeFile = CreateCodeFile("TestPackage", "1.0.0", "C#");
            using var memoryStream = new MemoryStream();

            var existingReview = new ReviewListItemModel
            {
                Id = "existing-review-id",
                PackageName = "TestPackage",
                Language = "C#",
                PackageType = null // No package type initially
            };

            _mockReviewManager.Setup(m => m.GetReviewAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool?>()))
                .ReturnsAsync(existingReview);

            _mockReviewManager.Setup(m => m.UpdateReviewAsync(It.IsAny<ReviewListItemModel>()))
                .ReturnsAsync((ReviewListItemModel r) => r);

            _mockApiRevisionsManager.Setup(m => m.GetAPIRevisionsAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<APIRevisionType>()))
                .ReturnsAsync(new List<APIRevisionListItemModel>());

            _mockApiRevisionsManager.Setup(m => m.CreateAPIRevisionAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<APIRevisionType>(),
                    It.IsAny<string>(), It.IsAny<MemoryStream>(), It.IsAny<CodeFile>(),
                    It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>()))
                .ReturnsAsync(new APIRevisionListItemModel { Id = "revision-id", ReviewId = "existing-review-id" });

            await _service.CreateAutomaticRevisionAsync(
                _testUser, codeFile, "test-label", "test.json", memoryStream, "mgmt");

            _mockReviewManager.Verify(m => m.UpdateReviewAsync(
                It.Is<ReviewListItemModel>(r => r.PackageType == PackageType.mgmt)),
                Times.Once);
        }

        [Fact]
        public async Task CreateAutomaticRevisionAsync_WithExistingReviewWithPackageType_DoesNotOverridePackageType()
        {
            var codeFile = CreateCodeFile("TestPackage", "1.0.0", "C#");
            using var memoryStream = new MemoryStream();

            var existingReview = new ReviewListItemModel
            {
                Id = "existing-review-id",
                PackageName = "TestPackage",
                Language = "C#",
                PackageType = PackageType.client
            };

            _mockReviewManager.Setup(m => m.GetReviewAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool?>()))
                .ReturnsAsync(existingReview);

            _mockApiRevisionsManager.Setup(m => m.GetAPIRevisionsAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<APIRevisionType>()))
                .ReturnsAsync(new List<APIRevisionListItemModel>());

            _mockApiRevisionsManager.Setup(m => m.CreateAPIRevisionAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<APIRevisionType>(),
                    It.IsAny<string>(), It.IsAny<MemoryStream>(), It.IsAny<CodeFile>(),
                    It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>()))
                .ReturnsAsync(new APIRevisionListItemModel { Id = "revision-id", ReviewId = "existing-review-id" });

            await _service.CreateAutomaticRevisionAsync(
                _testUser, codeFile, "test-label", "test.json", memoryStream, "mgmt");

            _mockReviewManager.Verify(m => m.UpdateReviewAsync(It.IsAny<ReviewListItemModel>()), Times.Never);
        }

        #endregion

        #region Compare All Revisions Tests

        [Fact]
        public async Task CreateAutomaticRevisionAsync_WithCompareAllRevisionsTrue_ReturnsMatchingApprovedRevision()
        {
            var codeFile = CreateCodeFile("TestPackage", "1.0.0", "C#");
            using var memoryStream = new MemoryStream();

            var existingReview = new ReviewListItemModel
            {
                Id = "review-id",
                PackageName = "TestPackage",
                Language = "C#"
            };

            var approvedRevision = new APIRevisionListItemModel
            {
                Id = "approved-revision-id",
                ReviewId = "review-id",
                APIRevisionType = APIRevisionType.Automatic,
                IsApproved = true,
                IsReleased = false,
                CreatedOn = DateTime.UtcNow.AddDays(-5),
                Files = new List<APICodeFileModel> { new APICodeFileModel { PackageVersion = "1.0.0" } }
            };

            var latestRevision = new APIRevisionListItemModel
            {
                Id = "latest-revision-id",
                ReviewId = "review-id",
                APIRevisionType = APIRevisionType.Automatic,
                IsApproved = false,
                IsReleased = false,
                CreatedOn = DateTime.UtcNow.AddDays(-1),
                Files = new List<APICodeFileModel> { new APICodeFileModel { PackageVersion = "1.1.0" } }
            };

            _mockReviewManager.Setup(m => m.GetReviewAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool?>()))
                .ReturnsAsync(existingReview);

            _mockApiRevisionsManager.Setup(m => m.GetAPIRevisionsAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<APIRevisionType>()))
                .ReturnsAsync(new List<APIRevisionListItemModel> { latestRevision, approvedRevision });

            _mockApiRevisionsManager.Setup(m => m.AreAPIRevisionsTheSame(
                    It.Is<APIRevisionListItemModel>(r => r.Id == "approved-revision-id"),
                    It.IsAny<RenderedCodeFile>(), It.IsAny<bool>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            _mockApiRevisionsManager.Setup(m => m.AreAPIRevisionsTheSame(
                    It.Is<APIRevisionListItemModel>(r => r.Id == "latest-revision-id"),
                    It.IsAny<RenderedCodeFile>(), It.IsAny<bool>(), It.IsAny<string>()))
                .ReturnsAsync(false);

            _mockCommentsManager.Setup(m => m.GetCommentsAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CommentType?>(), It.IsAny<bool>()))
                .ReturnsAsync(new List<CommentItemModel>());

            var (_, apiRevision) = await _service.CreateAutomaticRevisionAsync(
                _testUser, codeFile, "test-label", "test.json", memoryStream, null, compareAllRevisions: true);

            // Assert - should return the approved revision without creating new one
            _mockApiRevisionsManager.Verify(m => m.CreateAPIRevisionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<APIRevisionType>(),
                It.IsAny<string>(), It.IsAny<MemoryStream>(), It.IsAny<CodeFile>(),
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>()), Times.Never);

            apiRevision.Should().NotBeNull();
            apiRevision.Id.Should().Be("approved-revision-id");
        }

        #endregion

        #region Approval Copying Tests

        [Fact]
        public async Task CreateAutomaticRevisionAsync_CopiesApprovalFromMatchingApprovedRevision()
        {
            var codeFile = CreateCodeFile("TestPackage", "1.0.1", "C#");
            using var memoryStream = new MemoryStream();

            var existingReview = new ReviewListItemModel
            {
                Id = "review-id",
                PackageName = "TestPackage",
                Language = "C#"
            };

            var approvedRevision = new APIRevisionListItemModel
            {
                Id = "approved-revision-id",
                ReviewId = "review-id",
                APIRevisionType = APIRevisionType.Automatic,
                IsApproved = true,
                IsReleased = false,
                CreatedOn = DateTime.UtcNow.AddDays(-5),
                Approvers = new HashSet<string> { "approver1" },
                Files = new List<APICodeFileModel> { new APICodeFileModel { PackageVersion = "1.0.0" } }
            };

            var newRevision = new APIRevisionListItemModel
            {
                Id = "new-revision-id",
                ReviewId = "review-id",
                IsApproved = false
            };

            _mockReviewManager.Setup(m => m.GetReviewAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool?>()))
                .ReturnsAsync(existingReview);

            _mockApiRevisionsManager.Setup(m => m.GetAPIRevisionsAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<APIRevisionType>()))
                .ReturnsAsync(new List<APIRevisionListItemModel> { approvedRevision });

            // The approved revision is excluded as a candidate (!r.IsApproved filter), so a new revision is created.
            // In the post-creation approval-copying loop, the API surface matches → approval is carried forward.
            _mockApiRevisionsManager.Setup(m => m.AreAPIRevisionsTheSame(
                    It.Is<APIRevisionListItemModel>(r => r.Id == "approved-revision-id"),
                    It.IsAny<RenderedCodeFile>(), false, It.IsAny<string>()))
                .ReturnsAsync(true);

            _mockCommentsManager.Setup(m => m.GetCommentsAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CommentType?>(), It.IsAny<bool>()))
                .ReturnsAsync(new List<CommentItemModel>());

            _mockApiRevisionsManager.Setup(m => m.CreateAPIRevisionAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<APIRevisionType>(),
                    It.IsAny<string>(), It.IsAny<MemoryStream>(), It.IsAny<CodeFile>(),
                    It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>()))
                .ReturnsAsync(newRevision);

            _mockApiRevisionsManager.Setup(m => m.CarryForwardRevisionDataAsync(It.IsAny<APIRevisionListItemModel>(), It.IsAny<APIRevisionListItemModel>()))
                .Returns(Task.CompletedTask);

            await _service.CreateAutomaticRevisionAsync(
                _testUser, codeFile, "test-label", "test.json", memoryStream, null);

            // Approval should be /copied
            _mockApiRevisionsManager.Verify(m => m.CarryForwardRevisionDataAsync(It.IsAny<APIRevisionListItemModel>(), It.IsAny<APIRevisionListItemModel>()),
                Times.Once);
        }

        [Fact]
        public async Task CreateAutomaticRevisionAsync_DoesNotCopyApprovalWhenNoMatchingApprovedRevision()
        {
            var codeFile = CreateCodeFile("TestPackage", "2.0.0", "C#");
            using var memoryStream = new MemoryStream();

            var existingReview = new ReviewListItemModel
            {
                Id = "review-id",
                PackageName = "TestPackage",
                Language = "C#"
            };

            var approvedRevision = new APIRevisionListItemModel
            {
                Id = "approved-revision-id",
                ReviewId = "review-id",
                APIRevisionType = APIRevisionType.Automatic,
                IsApproved = true,
                IsReleased = false,
                CreatedOn = DateTime.UtcNow.AddDays(-5),
                Approvers = new HashSet<string> { "approver1" },
                Files = new List<APICodeFileModel> { new APICodeFileModel { PackageVersion = "1.0.0" } }
            };

            var newRevision = new APIRevisionListItemModel
            {
                Id = "new-revision-id",
                ReviewId = "review-id",
                IsApproved = false
            };

            _mockReviewManager.Setup(m => m.GetReviewAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool?>()))
                .ReturnsAsync(existingReview);

            _mockApiRevisionsManager.Setup(m => m.GetAPIRevisionsAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<APIRevisionType>()))
                .ReturnsAsync(new List<APIRevisionListItemModel> { approvedRevision });

            // Content doesn't match at all
            _mockApiRevisionsManager.Setup(m => m.AreAPIRevisionsTheSame(
                    It.IsAny<APIRevisionListItemModel>(), It.IsAny<RenderedCodeFile>(), It.IsAny<bool>(), It.IsAny<string>()))
                .ReturnsAsync(false);

            _mockCommentsManager.Setup(m => m.GetCommentsAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CommentType?>(), It.IsAny<bool>()))
                .ReturnsAsync(new List<CommentItemModel>());

            _mockApiRevisionsManager.Setup(m => m.CreateAPIRevisionAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<APIRevisionType>(),
                    It.IsAny<string>(), It.IsAny<MemoryStream>(), It.IsAny<CodeFile>(),
                    It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>()))
                .ReturnsAsync(newRevision);

            await _service.CreateAutomaticRevisionAsync(
                _testUser, codeFile, "test-label", "test.json", memoryStream, null);

            // Assert - approval should NOT be toggled/copied
            _mockApiRevisionsManager.Verify(m => m.ToggleAPIRevisionApprovalAsync(
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<APIRevisionListItemModel>(),
                It.IsAny<string>(),
                It.IsAny<string>()),
                Times.Never);
        }

        #endregion

        #region New Review Creation Tests

        [Fact]
        public async Task CreateAutomaticRevisionAsync_WithNoExistingReview_CreatesNewReview()
        {
            var codeFile = CreateCodeFile("NewPackage", "1.0.0", "Python");
            using var memoryStream = new MemoryStream();

            _mockReviewManager.Setup(m => m.GetReviewAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool?>()))
                .ReturnsAsync((ReviewListItemModel)null);

            var newReview = new ReviewListItemModel
            {
                Id = "new-review-id",
                PackageName = "NewPackage",
                Language = "Python"
            };

            _mockReviewManager.Setup(m => m.CreateReviewAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<PackageType?>(), It.IsAny<string>()))
                .ReturnsAsync(newReview);

            _mockApiRevisionsManager.Setup(m => m.CreateAPIRevisionAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<APIRevisionType>(),
                    It.IsAny<string>(), It.IsAny<MemoryStream>(), It.IsAny<CodeFile>(),
                    It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>()))
                .ReturnsAsync(new APIRevisionListItemModel { Id = "revision-id", ReviewId = "new-review-id" });

            var (review, _) = await _service.CreateAutomaticRevisionAsync(
                _testUser, codeFile, "test-label", "test.json", memoryStream, null);

            _mockReviewManager.Verify(m => m.CreateReviewAsync("NewPackage", "Python", false, null, null), Times.Once);
            review.Should().NotBeNull();
            review.Id.Should().Be("new-review-id");
        }

        [Fact]
        public async Task CreateAutomaticRevisionAsync_WithExistingReviewAndNoRevisions_CreatesNewRevision()
        {
            var codeFile = CreateCodeFile("TestPackage", "1.0.0", "C#");
            using var memoryStream = new MemoryStream();

            var existingReview = new ReviewListItemModel
            {
                Id = "existing-review-id",
                PackageName = "TestPackage",
                Language = "C#"
            };

            _mockReviewManager.Setup(m => m.GetReviewAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool?>()))
                .ReturnsAsync(existingReview);

            _mockApiRevisionsManager.Setup(m => m.GetAPIRevisionsAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<APIRevisionType>()))
                .ReturnsAsync(new List<APIRevisionListItemModel>()); // No existing revisions

            var newRevision = new APIRevisionListItemModel
            {
                Id = "new-revision-id",
                ReviewId = "existing-review-id"
            };

            _mockApiRevisionsManager.Setup(m => m.CreateAPIRevisionAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<APIRevisionType>(),
                    It.IsAny<string>(), It.IsAny<MemoryStream>(), It.IsAny<CodeFile>(),
                    It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>()))
                .ReturnsAsync(newRevision);

            var (_, apiRevision) = await _service.CreateAutomaticRevisionAsync(
                _testUser, codeFile, "test-label", "test.json", memoryStream, null);

            _mockApiRevisionsManager.Verify(m => m.CreateAPIRevisionAsync(
                "testuser", "existing-review-id", APIRevisionType.Automatic,
                "test-label", memoryStream, codeFile, "test.json", null, null), Times.Once);

            apiRevision.Should().NotBeNull();
            apiRevision.Id.Should().Be("new-revision-id");
        }

        #endregion

        #region Deleted Revision Bug Tests

        [Fact]
        public async Task CreateAutomaticRevisionAsync_WhenOnlyRevisionHasComment_CreatesNewRevision()
        {
            // A revision with comments is protected and skipped when selecting the candidate.
            var codeFile = CreateCodeFile("TestPackage", "1.0.0", "C#");
            using var memoryStream = new MemoryStream();

            var existingReview = new ReviewListItemModel
            {
                Id = "review-id",
                PackageName = "TestPackage",
                Language = "C#"
            };

            var revisionWithComments = new APIRevisionListItemModel
            {
                Id = "revision-with-comments",
                ReviewId = "review-id",
                APIRevisionType = APIRevisionType.Automatic,
                IsApproved = false,
                IsReleased = false,
                CreatedOn = DateTime.UtcNow.AddDays(-1),
                Files = new List<APICodeFileModel> { new APICodeFileModel { PackageVersion = "1.0.0" } }
            };

            var newRevision = new APIRevisionListItemModel { Id = "new-revision-id", ReviewId = "review-id" };

            var comment = new CommentItemModel
            {
                Id = "comment-1",
                APIRevisionId = "revision-with-comments",
                ReviewId = "review-id"
            };

            _mockReviewManager.Setup(m => m.GetReviewAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool?>()))
                .ReturnsAsync(existingReview);

            _mockApiRevisionsManager.Setup(m => m.GetAPIRevisionsAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<APIRevisionType>()))
                .ReturnsAsync(new List<APIRevisionListItemModel> { revisionWithComments });

            _mockCommentsManager.Setup(m => m.GetCommentsAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CommentType?>(), It.IsAny<bool>()))
                .ReturnsAsync(new List<CommentItemModel> { comment });

            _mockApiRevisionsManager.Setup(m => m.CreateAPIRevisionAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<APIRevisionType>(),
                    It.IsAny<string>(), It.IsAny<MemoryStream>(), It.IsAny<CodeFile>(),
                    It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>()))
                .ReturnsAsync(newRevision);

            var (_, apiRevision) = await _service.CreateAutomaticRevisionAsync(
                _testUser, codeFile, "test-label", "test.json", memoryStream, null);

            // Commented revision is never touched
            _mockApiRevisionsManager.Verify(m => m.SoftDeleteAPIRevisionAsync(
                It.IsAny<APIRevisionListItemModel>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);

            // New revision created since there was no usable candidate
            _mockApiRevisionsManager.Verify(m => m.CreateAPIRevisionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<APIRevisionType>(),
                It.IsAny<string>(), It.IsAny<MemoryStream>(), It.IsAny<CodeFile>(),
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>()), Times.Once);

            apiRevision.Should().NotBeNull();
            apiRevision.Id.Should().Be("new-revision-id");
        }

        [Fact]
        public async Task CreateAutomaticRevisionAsync_WhenCandidateHasSameContent_StillDeletesAndCreatesNew()
        {
            // Even when the incoming upload is byte-for-byte identical to the existing revision,
            // we always delete the candidate and create a fresh revision. No reuse.
            var codeFile = CreateCodeFile("TestPackage", "1.0.0", "C#");
            using var memoryStream = new MemoryStream();

            var review = new ReviewListItemModel { Id = "review-id", PackageName = "TestPackage", Language = "C#" };

            var existingRevision = new APIRevisionListItemModel
            {
                Id = "existing-revision", ReviewId = "review-id",
                APIRevisionType = APIRevisionType.Automatic,
                IsApproved = false, IsReleased = false,
                CreatedOn = DateTime.UtcNow.AddDays(-1),
                Files = [new APICodeFileModel { PackageVersion = "1.0.0" }]
            };

            var newRevision = new APIRevisionListItemModel { Id = "new-revision", ReviewId = "review-id" };

            _mockReviewManager.Setup(m => m.GetReviewAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool?>()))
                .ReturnsAsync(review);
            _mockApiRevisionsManager.Setup(m => m.GetAPIRevisionsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<APIRevisionType>()))
                .ReturnsAsync(new List<APIRevisionListItemModel> { existingRevision });
            _mockCommentsManager.Setup(m => m.GetCommentsAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CommentType?>(), It.IsAny<bool>()))
                .ReturnsAsync(new List<CommentItemModel>());
            _mockApiRevisionsManager.Setup(m => m.SoftDeleteAPIRevisionAsync(
                    It.IsAny<APIRevisionListItemModel>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            _mockApiRevisionsManager.Setup(m => m.CreateAPIRevisionAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<APIRevisionType>(),
                    It.IsAny<string>(), It.IsAny<MemoryStream>(), It.IsAny<CodeFile>(),
                    It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>()))
                .ReturnsAsync(newRevision);

            var (_, apiRevision) = await _service.CreateAutomaticRevisionAsync(
                _testUser, codeFile, "test-label", "test.json", memoryStream, null);

            // Candidate must be deleted even though content is identical
            _mockApiRevisionsManager.Verify(m => m.SoftDeleteAPIRevisionAsync(
                It.Is<APIRevisionListItemModel>(r => r.Id == "existing-revision"),
                It.IsAny<string>(), It.IsAny<string>()), Times.Once);

            // A new revision must always be created
            _mockApiRevisionsManager.Verify(m => m.CreateAPIRevisionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<APIRevisionType>(),
                It.IsAny<string>(), It.IsAny<MemoryStream>(), It.IsAny<CodeFile>(),
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>()), Times.Once);

            apiRevision.Should().NotBeNull();
            apiRevision.Id.Should().Be("new-revision");
        }

        #endregion

        #region Rolling Build / Package Version Tests

        /// <summary>
        /// Rollin builds that re-run with the same version string
        /// (e.g. a CI retry) delete the previous pending revision and create a fresh one.
        /// Both belong to the same <c>APIVersionId</c> so the v1-stable bucket is never touched.
        /// </summary>
        [Fact]
        public async Task CreateAutomaticRevisionAsync_RollingAlphaBuild_DeletesPreviousAndCreatesNew()
        {
            var codeFile = CreateCodeFile("TestPackage", "1.4.0-alpha.20201212.0", "C#");
            using var memoryStream = new MemoryStream();

            var review = new ReviewListItemModel { Id = "review-id", PackageName = "TestPackage", Language = "C#" };
            var versionModel = new APIVersionModel { Id = "ver-alpha" };

            // Same version as the incoming build — represents a CI retry or resubmit
            var previousBuild = new APIRevisionListItemModel
            {
                Id = "alpha-20201212-prev",
                ReviewId = "review-id",
                APIRevisionType = APIRevisionType.Automatic,
                IsApproved = false,
                APIVersionId = "ver-alpha",
                CreatedOn = DateTime.UtcNow.AddHours(-1),
                Files = [new APICodeFileModel { PackageVersion = "1.4.0-alpha.20201212.0" }]
            };

            var newBuild = new APIRevisionListItemModel { Id = "alpha-20201212-new", ReviewId = "review-id" };

            _mockReviewManager.Setup(m => m.GetReviewAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool?>()))
                .ReturnsAsync(review);
            _mockApiRevisionsManager.Setup(m => m.GetAPIRevisionsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<APIRevisionType>()))
                .ReturnsAsync(new List<APIRevisionListItemModel> { previousBuild });
            _mockApiVersionsManager.Setup(m => m.GetOrCreateVersionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>()))
                .ReturnsAsync(versionModel);
            _mockCommentsManager.Setup(m => m.GetCommentsAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CommentType?>(), It.IsAny<bool>()))
                .ReturnsAsync(new List<CommentItemModel>());
            _mockApiRevisionsManager.Setup(m => m.SoftDeleteAPIRevisionAsync(
                    It.IsAny<APIRevisionListItemModel>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            _mockApiRevisionsManager.Setup(m => m.CreateAPIRevisionAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<APIRevisionType>(),
                    It.IsAny<string>(), It.IsAny<MemoryStream>(), It.IsAny<CodeFile>(),
                    It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>()))
                .ReturnsAsync(newBuild);

            var (_, apiRevision) = await _service.CreateAutomaticRevisionAsync(
                _testUser, codeFile, "build-label", "test.json", memoryStream, null);

            // Previous build (same version) is deleted; fresh revision created
            _mockApiRevisionsManager.Verify(m => m.SoftDeleteAPIRevisionAsync(
                It.Is<APIRevisionListItemModel>(r => r.Id == "alpha-20201212-prev"),
                It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            _mockApiRevisionsManager.Verify(m => m.CreateAPIRevisionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<APIRevisionType>(),
                It.IsAny<string>(), It.IsAny<MemoryStream>(), It.IsAny<CodeFile>(),
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>()), Times.Once);

            apiRevision.Should().NotBeNull();
            apiRevision.Id.Should().Be("alpha-20201212-new");
        }

        [Fact]
        public async Task CreateAutomaticRevisionAsync_RollingAlphaBuild_DifferentDailyVersion_OnlyCreatesRevision()
        {
            var codeFile = CreateCodeFile("TestPackage", "1.4.0", "C#");
            using var memoryStream = new MemoryStream();

            var review = new ReviewListItemModel { Id = "review-id", PackageName = "TestPackage", Language = "C#" };
            var versionModel = new APIVersionModel { Id = "ver-alpha" };
            var previousBuild = new APIRevisionListItemModel
            {
                Id = "beta-20201211", ReviewId = "review-id",
                APIRevisionType = APIRevisionType.Automatic,
                IsApproved = false, APIVersionId = "ver-alpha",
                CreatedOn = DateTime.UtcNow.AddDays(-1),
                Files = [new APICodeFileModel { PackageVersion = "1.5.0" }]
            };

            var newBuild = new APIRevisionListItemModel { Id = "beta-20201212", ReviewId = "review-id" };

            _mockReviewManager.Setup(m => m.GetReviewAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool?>()))
                .ReturnsAsync(review);
            _mockApiRevisionsManager.Setup(m => m.GetAPIRevisionsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<APIRevisionType>()))
                .ReturnsAsync(new List<APIRevisionListItemModel> { previousBuild });
            _mockApiVersionsManager.Setup(m => m.GetOrCreateVersionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>()))
                .ReturnsAsync(versionModel);
            _mockCommentsManager.Setup(m => m.GetCommentsAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CommentType?>(), It.IsAny<bool>()))
                .ReturnsAsync(new List<CommentItemModel>());
            _mockApiRevisionsManager.Setup(m => m.SoftDeleteAPIRevisionAsync(
                    It.IsAny<APIRevisionListItemModel>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            _mockApiRevisionsManager.Setup(m => m.CreateAPIRevisionAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<APIRevisionType>(),
                    It.IsAny<string>(), It.IsAny<MemoryStream>(), It.IsAny<CodeFile>(),
                    It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>()))
                .ReturnsAsync(newBuild);

            var (_, apiRevision) = await _service.CreateAutomaticRevisionAsync(
                _testUser, codeFile, "build-label", "test.json", memoryStream, null);

            // Different versions — no candidate match, nothing is deleted
            _mockApiRevisionsManager.Verify(m => m.SoftDeleteAPIRevisionAsync(
                It.IsAny<APIRevisionListItemModel>(),
                It.IsAny<string>(), It.IsAny<string>()), Times.Never);

            // A new revision is still always created
            _mockApiRevisionsManager.Verify(m => m.CreateAPIRevisionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<APIRevisionType>(),
                It.IsAny<string>(), It.IsAny<MemoryStream>(), It.IsAny<CodeFile>(),
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>()), Times.Once);

            apiRevision.Should().NotBeNull();
            apiRevision.Id.Should().Be("beta-20201212");
        }

        /// <summary>
        /// When <c>codeFile.PackageVersion</c> is null, empty, or whitespace, no existing revision
        /// can ever match the candidate filter (<c>r.PackageVersion == codeFile.PackageVersion</c>
        /// will be false for any revision that carries a real version string), so nothing is deleted
        /// and a new revision is always created — and nothing throws.
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task CreateAutomaticRevisionAsync_NullOrEmptyPackageVersion_NothingDeletedNewRevisionCreated(string packageVersion)
        {
            var codeFile = new CodeFile
            {
                Name = "test", Language = "C#",
                PackageName = "TestPackage",
                PackageVersion = packageVersion
            };
            using var memoryStream = new MemoryStream();

            var review = new ReviewListItemModel { Id = "review-id", PackageName = "TestPackage", Language = "C#" };
            var versionModel = new APIVersionModel { Id = "ver-1" };

            var existingRevision = new APIRevisionListItemModel
            {
                Id = "existing", ReviewId = "review-id",
                APIRevisionType = APIRevisionType.Automatic,
                IsApproved = false, IsReleased = false,
                CreatedOn = DateTime.UtcNow.AddDays(-1),
                Files = [new APICodeFileModel { PackageVersion = "1.0.0" }]
            };

            var newRevision = new APIRevisionListItemModel { Id = "new-rev", ReviewId = "review-id" };

            _mockReviewManager.Setup(m => m.GetReviewAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool?>()))
                .ReturnsAsync(review);
            _mockApiRevisionsManager.Setup(m => m.GetAPIRevisionsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<APIRevisionType>()))
                .ReturnsAsync(new List<APIRevisionListItemModel> { existingRevision });
            _mockApiVersionsManager.Setup(m => m.GetOrCreateVersionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>()))
                .ReturnsAsync(versionModel);
            _mockCommentsManager.Setup(m => m.GetCommentsAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CommentType?>(), It.IsAny<bool>()))
                .ReturnsAsync(new List<CommentItemModel>());
            _mockApiRevisionsManager.Setup(m => m.SoftDeleteAPIRevisionAsync(
                    It.IsAny<APIRevisionListItemModel>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            _mockApiRevisionsManager.Setup(m => m.CreateAPIRevisionAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<APIRevisionType>(),
                    It.IsAny<string>(), It.IsAny<MemoryStream>(), It.IsAny<CodeFile>(),
                    It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>()))
                .ReturnsAsync(newRevision);

            // Must not throw regardless of null/empty/whitespace PackageVersion
            var (_, apiRevision) = await _service.CreateAutomaticRevisionAsync(
                _testUser, codeFile, "test-label", "test.json", memoryStream, null);

            // Existing revision has a real version ("1.0.0") — it will never match null/empty/whitespace
            _mockApiRevisionsManager.Verify(m => m.SoftDeleteAPIRevisionAsync(
                It.IsAny<APIRevisionListItemModel>(),
                It.IsAny<string>(), It.IsAny<string>()), Times.Never);

            // A new revision is always created
            _mockApiRevisionsManager.Verify(m => m.CreateAPIRevisionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<APIRevisionType>(),
                It.IsAny<string>(), It.IsAny<MemoryStream>(), It.IsAny<CodeFile>(),
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>()), Times.Once);

            apiRevision.Should().NotBeNull();
            apiRevision.Id.Should().Be("new-rev");
        }

        #endregion

        #region APIVersionId Tests

        [Fact]
        public async Task CreateAutomaticRevisionAsync_SupersessionScopedToSameVersionId()
        {
            // Supersession (soft-delete of stale pending revisions) must only affect revisions
            // belonging to the same APIVersionId as the incoming upload
            var codeFile = CreateCodeFile("TestPackage", "1.0.0", "C#");
            using var memoryStream = new MemoryStream();

            var review = new ReviewListItemModel { Id = "review-id", PackageName = "TestPackage", Language = "C#" };
            var versionV1 = new APIVersionModel { Id = "ver-v1" };

            var revisionV1 = new APIRevisionListItemModel
            {
                Id = "rev-v1", ReviewId = "review-id",
                APIRevisionType = APIRevisionType.Automatic, APIVersionId = "ver-v1",
                IsApproved = false, IsReleased = false,
                CreatedOn = DateTime.UtcNow.AddDays(-1),
                Files = [new APICodeFileModel { PackageVersion = "1.0.0" }]
            };
            var revisionV2 = new APIRevisionListItemModel
            {
                Id = "rev-v2", ReviewId = "review-id",
                APIRevisionType = APIRevisionType.Automatic, APIVersionId = "ver-v2",
                IsApproved = false, IsReleased = false,
                CreatedOn = DateTime.UtcNow.AddDays(-2),
                Files = [new APICodeFileModel { PackageVersion = "2.0.0" }]
            };

            _mockReviewManager.Setup(m => m.GetReviewAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool?>()))
                .ReturnsAsync(review);
            _mockApiRevisionsManager.Setup(m => m.GetAPIRevisionsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<APIRevisionType>()))
                .ReturnsAsync(new List<APIRevisionListItemModel> { revisionV1, revisionV2 });
            _mockApiVersionsManager.Setup(m => m.GetOrCreateVersionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>()))
                .ReturnsAsync(versionV1);
            _mockCommentsManager.Setup(m => m.GetCommentsAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CommentType?>(), It.IsAny<bool>()))
                .ReturnsAsync(new List<CommentItemModel>());
            _mockApiRevisionsManager.Setup(m => m.AreAPIRevisionsTheSame(
                    It.IsAny<APIRevisionListItemModel>(), It.IsAny<RenderedCodeFile>(), It.IsAny<bool>(), It.IsAny<string>()))
                .ReturnsAsync(false);
            _mockApiRevisionsManager.Setup(m => m.SoftDeleteAPIRevisionAsync(
                    It.IsAny<APIRevisionListItemModel>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            _mockApiRevisionsManager.Setup(m => m.CreateAPIRevisionAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<APIRevisionType>(),
                    It.IsAny<string>(), It.IsAny<MemoryStream>(), It.IsAny<CodeFile>(),
                    It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>()))
                .ReturnsAsync(new APIRevisionListItemModel { Id = "new-rev", ReviewId = "review-id" });

            await _service.CreateAutomaticRevisionAsync(_testUser, codeFile, "test-label", "test.json", memoryStream, null);

            // Only the v1 revision (same version as incoming) should be considered for deletion.
            _mockApiRevisionsManager.Verify(
                m => m.SoftDeleteAPIRevisionAsync(
                    It.Is<APIRevisionListItemModel>(r => r.Id == "rev-v1"),
                    It.IsAny<string>(), It.IsAny<string>()),
                Times.Once);
            _mockApiRevisionsManager.Verify(
                m => m.SoftDeleteAPIRevisionAsync(
                    It.Is<APIRevisionListItemModel>(r => r.Id == "rev-v2"),
                    It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }

        /// <summary>
        /// Revisions for a different version are excluded from cleanup entirely.
        ///
        /// History (newest first):
        ///   B3 — pending, v2-beta  (same version as incoming — candidate, deleted)
        ///   B2 — pending, v1-stable  (different version — must never be touched)
        ///   B1 — pending, v1-stable  (different version — must never be touched)
        /// </summary>
        [Fact]
        public async Task CreateAutomaticRevisionAsync_DifferentVersionRevisions_NeverTouched()
        {
            var codeFile = CreateCodeFile("TestPackage", "2.0.0-beta", "C#");
            using var memoryStream = new MemoryStream();

            var review = new ReviewListItemModel { Id = "review-id", PackageName = "TestPackage", Language = "C#" };
            var v2BetaVersionModel = new APIVersionModel { Id = "ver-v2-beta" };

            var b1V1 = new APIRevisionListItemModel
            {
                Id = "b1-v1stable", ReviewId = "review-id",
                APIRevisionType = APIRevisionType.Automatic,
                APIVersionId = "ver-v1-stable",
                CreatedOn = DateTime.UtcNow.AddDays(-5),
                Files = [new APICodeFileModel { PackageVersion = "1.0.0" }]
            };

            var b2V1 = new APIRevisionListItemModel
            {
                Id = "b2-v1stable", ReviewId = "review-id",
                APIRevisionType = APIRevisionType.Automatic,
                APIVersionId = "ver-v1-stable",
                CreatedOn = DateTime.UtcNow.AddDays(-3),
                Files = [new APICodeFileModel { PackageVersion = "1.0.0" }]
            };

            var b3V2 = new APIRevisionListItemModel
            {
                Id = "b3-v2beta", ReviewId = "review-id",
                APIRevisionType = APIRevisionType.Automatic,
                APIVersionId = "ver-v2-beta",
                CreatedOn = DateTime.UtcNow.AddDays(-1),
                Files = [new APICodeFileModel { PackageVersion = "2.0.0-beta" }]
            };

            _mockReviewManager.Setup(m => m.GetReviewAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool?>()))
                .ReturnsAsync(review);
            _mockApiRevisionsManager.Setup(m => m.GetAPIRevisionsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<APIRevisionType>()))
                .ReturnsAsync(new List<APIRevisionListItemModel> { b3V2, b2V1, b1V1 });
            _mockApiVersionsManager.Setup(m => m.GetOrCreateVersionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>()))
                .ReturnsAsync(v2BetaVersionModel);
            _mockCommentsManager.Setup(m => m.GetCommentsAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CommentType?>(), It.IsAny<bool>()))
                .ReturnsAsync(new List<CommentItemModel>());

            _mockApiRevisionsManager.Setup(m => m.SoftDeleteAPIRevisionAsync(
                    It.IsAny<APIRevisionListItemModel>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            _mockApiRevisionsManager.Setup(m => m.CreateAPIRevisionAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<APIRevisionType>(),
                    It.IsAny<string>(), It.IsAny<MemoryStream>(), It.IsAny<CodeFile>(),
                    It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>()))
                .ReturnsAsync(new APIRevisionListItemModel { Id = "new-v2beta", ReviewId = "review-id" });

            (_, APIRevisionListItemModel apiRevision) = await _service.CreateAutomaticRevisionAsync(
                _testUser, codeFile, "build-label", "test.json", memoryStream, null);

            apiRevision.Should().NotBeNull();
            apiRevision.Id.Should().Be("new-v2beta");

            // B3 (v2-beta candidate) is deleted and replaced
            _mockApiRevisionsManager.Verify(m => m.SoftDeleteAPIRevisionAsync(
                It.Is<APIRevisionListItemModel>(r => r.Id == "b3-v2beta"),
                It.IsAny<string>(), It.IsAny<string>()), Times.Once);

            // v1-stable revisions are never touched
            _mockApiRevisionsManager.Verify(m => m.SoftDeleteAPIRevisionAsync(
                It.Is<APIRevisionListItemModel>(r => r.Id == "b1-v1stable"),
                It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _mockApiRevisionsManager.Verify(m => m.SoftDeleteAPIRevisionAsync(
                It.Is<APIRevisionListItemModel>(r => r.Id == "b2-v1stable"),
                It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        #endregion

        #region Multi-Build Progression Tests

        /// <summary>
        /// When incoming content does NOT match the candidate (newest unprotected),
        /// only the candidate is deleted and a new revision is created.
        /// Older pending revisions are not touched.
        ///
        /// History (newest first):
        ///   B2 — pending (candidate — doesn't match → deleted)
        ///   B1 — pending (older, not the candidate — not touched)
        ///
        /// Expected outcome: B2 deleted, B3 created, B1 untouched.
        /// </summary>
        [Fact]
        public async Task CreateAutomaticRevisionAsync_WhenCandidateDoesntMatch_OnlyCandidateIsDeleted()
        {
            var codeFile = CreateCodeFile("TestPackage", "2.0.0", "C#");
            using var memoryStream = new MemoryStream();

            var review = new ReviewListItemModel { Id = "review-id", PackageName = "TestPackage", Language = "C#" };

            var b1Pending = new APIRevisionListItemModel
            {
                Id = "b1-pending", ReviewId = "review-id",
                APIRevisionType = APIRevisionType.Automatic,
                IsApproved = false, CreatedOn = DateTime.UtcNow.AddDays(-2),
                Files = [new APICodeFileModel { PackageVersion = "2.0.0" }]
            };

            var b2Pending = new APIRevisionListItemModel
            {
                Id = "b2-pending", ReviewId = "review-id",
                APIRevisionType = APIRevisionType.Automatic,
                IsApproved = false, CreatedOn = DateTime.UtcNow.AddDays(-1),
                Files = [new APICodeFileModel { PackageVersion = "2.0.0" }]
            };

            var b3New = new APIRevisionListItemModel { Id = "b3-new", ReviewId = "review-id" };

            _mockReviewManager.Setup(m => m.GetReviewAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool?>()))
                .ReturnsAsync(review);
            _mockApiRevisionsManager.Setup(m => m.GetAPIRevisionsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<APIRevisionType>()))
                .ReturnsAsync(new List<APIRevisionListItemModel> { b2Pending, b1Pending });
            _mockCommentsManager.Setup(m => m.GetCommentsAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CommentType?>(), It.IsAny<bool>()))
                .ReturnsAsync(new List<CommentItemModel>());
            _mockApiRevisionsManager.Setup(m => m.SoftDeleteAPIRevisionAsync(
                    It.IsAny<APIRevisionListItemModel>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            _mockApiRevisionsManager.Setup(m => m.AreAPIRevisionsTheSame(
                    It.IsAny<APIRevisionListItemModel>(), It.IsAny<RenderedCodeFile>(), It.IsAny<bool>(), It.IsAny<string>()))
                .ReturnsAsync(false);
            _mockApiRevisionsManager.Setup(m => m.CreateAPIRevisionAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<APIRevisionType>(),
                    It.IsAny<string>(), It.IsAny<MemoryStream>(), It.IsAny<CodeFile>(),
                    It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>()))
                .ReturnsAsync(b3New);

            var (_, apiRevision) = await _service.CreateAutomaticRevisionAsync(
                _testUser, codeFile, "build-label", "test.json", memoryStream, null);

            apiRevision.Should().NotBeNull();
            apiRevision.Id.Should().Be("b3-new");

            _mockApiRevisionsManager.Verify(m => m.SoftDeleteAPIRevisionAsync(
                It.Is<APIRevisionListItemModel>(r => r.Id == "b2-pending"),
                It.IsAny<string>(), It.IsAny<string>()), Times.Once, "B2 is the candidate and must be deleted");

            _mockApiRevisionsManager.Verify(m => m.SoftDeleteAPIRevisionAsync(
                It.Is<APIRevisionListItemModel>(r => r.Id == "b1-pending"),
                It.IsAny<string>(), It.IsAny<string>()), Times.Never, "B1 is not the candidate and must not be touched");

            _mockApiRevisionsManager.Verify(m => m.CreateAPIRevisionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<APIRevisionType>(),
                It.IsAny<string>(), It.IsAny<MemoryStream>(), It.IsAny<CodeFile>(),
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>()), Times.Once);
        }

        /// <summary>
        /// When the newest revision has a comment it is protected and skipped by <c>FirstOrDefault</c>.
        /// The next newest unprotected revision becomes the candidate instead.
        ///
        /// History (newest first):
        ///   B4 — HAS a comment  (protected — skipped by FirstOrDefault)
        ///   B3 — pending, no comments  ← candidate
        ///   B2 — pending, no comments  (not the candidate)
        ///   B1 — approved anchor       (protected — skipped)
        ///
        /// Expected outcome: B3 deleted (candidate, doesn't match), B5 created, B4/B2/B1 untouched.
        /// </summary>
        [Fact]
        public async Task CreateAutomaticRevisionAsync_WhenNewestRevisionHasComment_SkipsItAndSelectsNextAsCandidate()
        {
            var codeFile = CreateCodeFile("TestPackage", "2.0.0", "C#");
            using var memoryStream = new MemoryStream();

            var review = new ReviewListItemModel { Id = "review-id", PackageName = "TestPackage", Language = "C#" };

            var b1Approved = new APIRevisionListItemModel
            {
                Id = "b1-approved", ReviewId = "review-id",
                APIRevisionType = APIRevisionType.Automatic,
                IsApproved = true, CreatedOn = DateTime.UtcNow.AddDays(-10),
                Files = new List<APICodeFileModel> { new APICodeFileModel { PackageVersion = "2.0.0" } }
            };

            var b2Pending = new APIRevisionListItemModel
            {
                Id = "b2-pending", ReviewId = "review-id",
                APIRevisionType = APIRevisionType.Automatic,
                IsApproved = false, CreatedOn = DateTime.UtcNow.AddDays(-3),
                Files = new List<APICodeFileModel> { new APICodeFileModel { PackageVersion = "2.0.0" } }
            };

            var b3Pending = new APIRevisionListItemModel
            {
                Id = "b3-pending", ReviewId = "review-id",
                APIRevisionType = APIRevisionType.Automatic,
                IsApproved = false, CreatedOn = DateTime.UtcNow.AddDays(-2),
                Files = new List<APICodeFileModel> { new APICodeFileModel { PackageVersion = "2.0.0" } }
            };

            // Newest revision — but protected by a comment, so it is skipped as candidate
            var b4WithComment = new APIRevisionListItemModel
            {
                Id = "b4-with-comment", ReviewId = "review-id",
                APIRevisionType = APIRevisionType.Automatic,
                IsApproved = false, CreatedOn = DateTime.UtcNow.AddDays(-1),
                Files = new List<APICodeFileModel> { new APICodeFileModel { PackageVersion = "2.0.0" } }
            };

            var b5New = new APIRevisionListItemModel { Id = "b5-new", ReviewId = "review-id" };
            var comment = new CommentItemModel { APIRevisionId = "b4-with-comment", ReviewId = "review-id" };

            _mockReviewManager.Setup(m => m.GetReviewAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool?>()))
                .ReturnsAsync(review);

            _mockApiRevisionsManager.Setup(m => m.GetAPIRevisionsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<APIRevisionType>()))
                .ReturnsAsync(new List<APIRevisionListItemModel> { b4WithComment, b3Pending, b2Pending, b1Approved });

            _mockCommentsManager.Setup(m => m.GetCommentsAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CommentType?>(), It.IsAny<bool>()))
                .ReturnsAsync(new List<CommentItemModel> { comment });

            _mockApiRevisionsManager.Setup(m => m.SoftDeleteAPIRevisionAsync(
                    It.IsAny<APIRevisionListItemModel>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // B3 (candidate) doesn't match incoming content
            _mockApiRevisionsManager.Setup(m => m.AreAPIRevisionsTheSame(
                    It.IsAny<APIRevisionListItemModel>(), It.IsAny<RenderedCodeFile>(), It.IsAny<bool>(), It.IsAny<string>()))
                .ReturnsAsync(false);

            _mockApiRevisionsManager.Setup(m => m.CreateAPIRevisionAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<APIRevisionType>(),
                    It.IsAny<string>(), It.IsAny<MemoryStream>(), It.IsAny<CodeFile>(),
                    It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>()))
                .ReturnsAsync(b5New);

            var (_, apiRevision) = await _service.CreateAutomaticRevisionAsync(
                _testUser, codeFile, "build-5-label", "test.json", memoryStream, null);

            apiRevision.Should().NotBeNull();
            apiRevision.Id.Should().Be("b5-new");

            // B3 is the candidate (B4 was skipped due to comment) — it must be deleted
            _mockApiRevisionsManager.Verify(m => m.SoftDeleteAPIRevisionAsync(
                It.Is<APIRevisionListItemModel>(r => r.Id == "b3-pending"),
                It.IsAny<string>(), It.IsAny<string>()), Times.Once, "B3 is the candidate and must be deleted");

            // B4 has a comment — it was skipped as candidate and must NOT be deleted
            _mockApiRevisionsManager.Verify(m => m.SoftDeleteAPIRevisionAsync(
                It.Is<APIRevisionListItemModel>(r => r.Id == "b4-with-comment"),
                It.IsAny<string>(), It.IsAny<string>()), Times.Never, "B4 has a comment and must not be touched");

            // B2 is not the candidate and must not be touched
            _mockApiRevisionsManager.Verify(m => m.SoftDeleteAPIRevisionAsync(
                It.Is<APIRevisionListItemModel>(r => r.Id == "b2-pending"),
                It.IsAny<string>(), It.IsAny<string>()), Times.Never, "B2 is not the candidate and must not be touched");

            _mockApiRevisionsManager.Verify(m => m.SoftDeleteAPIRevisionAsync(
                It.Is<APIRevisionListItemModel>(r => r.Id == "b1-approved"),
                It.IsAny<string>(), It.IsAny<string>()), Times.Never, "B1 is approved and must never be touched");
        }

        #endregion

        #region Helper Methods

        private static CodeFile CreateCodeFile(string packageName, string packageVersion, string language)
        {
            return new CodeFile
            {
                Name = "test",
                Language = language,
                PackageName = packageName,
                PackageVersion = packageVersion
            };
        }

        #endregion
    }
}
