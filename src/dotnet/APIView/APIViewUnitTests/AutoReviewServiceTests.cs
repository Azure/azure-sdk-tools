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

        #region Version Comparison and Revision Reuse Tests

        [Fact]
        public async Task CreateAutomaticRevisionAsync_WithSameVersionAndContent_ReusesExistingRevision()
        {
            var codeFile = CreateCodeFile("TestPackage", "1.0.0", "C#");
            using var memoryStream = new MemoryStream();

            var existingReview = new ReviewListItemModel
            {
                Id = "review-id",
                PackageName = "TestPackage",
                Language = "C#"
            };

            var existingRevision = new APIRevisionListItemModel
            {
                Id = "existing-revision-id",
                ReviewId = "review-id",
                APIRevisionType = APIRevisionType.Automatic,
                IsApproved = false,
                IsReleased = false,
                CreatedOn = DateTime.UtcNow.AddDays(-1),
                Files = new List<APICodeFileModel> { new APICodeFileModel { PackageVersion = "1.0.0" } }
            };

            _mockReviewManager.Setup(m => m.GetReviewAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool?>()))
                .ReturnsAsync(existingReview);

            _mockApiRevisionsManager.Setup(m => m.GetAPIRevisionsAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<APIRevisionType>()))
                .ReturnsAsync(new List<APIRevisionListItemModel> { existingRevision });

            _mockApiRevisionsManager.Setup(m => m.AreAPIRevisionsTheSame(
                    It.IsAny<APIRevisionListItemModel>(), It.IsAny<RenderedCodeFile>(), It.IsAny<bool>(), It.IsAny<string>()))
                .ReturnsAsync(true); // Same content

            _mockCommentsManager.Setup(m => m.GetCommentsAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CommentType?>(), It.IsAny<bool>()))
                .ReturnsAsync(new List<CommentItemModel>());

            var (_, apiRevision) = await _service.CreateAutomaticRevisionAsync(
                _testUser, codeFile, "test-label", "test.json", memoryStream, null);

            _mockApiRevisionsManager.Verify(m => m.CreateAPIRevisionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<APIRevisionType>(),
                It.IsAny<string>(), It.IsAny<MemoryStream>(), It.IsAny<CodeFile>(),
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>()), Times.Never);

            apiRevision.Should().NotBeNull();
            apiRevision.Id.Should().Be("existing-revision-id");
        }

        [Fact]
        public async Task CreateAutomaticRevisionAsync_WithDifferentContent_CreatesNewRevision()
        {
            var codeFile = CreateCodeFile("TestPackage", "1.1.0", "C#");
            using var memoryStream = new MemoryStream();

            var existingReview = new ReviewListItemModel
            {
                Id = "review-id",
                PackageName = "TestPackage",
                Language = "C#"
            };

            var existingRevision = new APIRevisionListItemModel
            {
                Id = "existing-revision-id",
                ReviewId = "review-id",
                APIRevisionType = APIRevisionType.Automatic,
                IsApproved = false,
                IsReleased = false,
                CreatedOn = DateTime.UtcNow.AddDays(-1),
                Files = new List<APICodeFileModel> { new APICodeFileModel { PackageVersion = "1.0.0" } }
            };

            var newRevision = new APIRevisionListItemModel
            {
                Id = "new-revision-id",
                ReviewId = "review-id"
            };

            _mockReviewManager.Setup(m => m.GetReviewAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool?>()))
                .ReturnsAsync(existingReview);

            _mockApiRevisionsManager.Setup(m => m.GetAPIRevisionsAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<APIRevisionType>()))
                .ReturnsAsync(new List<APIRevisionListItemModel> { existingRevision });

            _mockApiRevisionsManager.Setup(m => m.AreAPIRevisionsTheSame(
                    It.IsAny<APIRevisionListItemModel>(), It.IsAny<RenderedCodeFile>(), It.IsAny<bool>(), It.IsAny<string>()))
                .ReturnsAsync(false); // Different content

            _mockCommentsManager.Setup(m => m.GetCommentsAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CommentType?>(), It.IsAny<bool>()))
                .ReturnsAsync(new List<CommentItemModel>());

            _mockApiRevisionsManager.Setup(m => m.CreateAPIRevisionAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<APIRevisionType>(),
                    It.IsAny<string>(), It.IsAny<MemoryStream>(), It.IsAny<CodeFile>(),
                    It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>()))
                .ReturnsAsync(newRevision);

            var (_, apiRevision) = await _service.CreateAutomaticRevisionAsync(
                _testUser, codeFile, "test-label", "test.json", memoryStream, null);

            _mockApiRevisionsManager.Verify(m => m.CreateAPIRevisionAsync(
                "testuser", "review-id", APIRevisionType.Automatic,
                "test-label", memoryStream, codeFile, "test.json", null, null), Times.Once);

            apiRevision.Should().NotBeNull();
            apiRevision.Id.Should().Be("new-revision-id");
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

            // New content doesn't match existing revision for version comparison (so we create a new one)
            _mockApiRevisionsManager.Setup(m => m.AreAPIRevisionsTheSame(
                    It.Is<APIRevisionListItemModel>(r => r.Id == "approved-revision-id"),
                    It.IsAny<RenderedCodeFile>(), true, It.IsAny<string>()))
                .ReturnsAsync(false);

            // But for approval copying (no version consideration), the content DOES match
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
        public async Task CreateAutomaticRevisionAsync_WhenAllPendingRevisionsDeleted_CreatesNewRevision()
        {
            // This test covers Edge Case 1: Multiple pending revisions that don't match get deleted
            var codeFile = CreateCodeFile("TestPackage", "1.0.0", "C#");
            using var memoryStream = new MemoryStream();

            var existingReview = new ReviewListItemModel
            {
                Id = "review-id",
                PackageName = "TestPackage",
                Language = "C#"
            };

            var pendingRevision1 = new APIRevisionListItemModel
            {
                Id = "pending-revision-1",
                ReviewId = "review-id",
                APIRevisionType = APIRevisionType.Automatic,
                IsApproved = false,
                IsReleased = false,
                CreatedOn = DateTime.UtcNow.AddDays(-3),
                Files = new List<APICodeFileModel> { new APICodeFileModel { PackageVersion = "0.9.0" } }
            };

            var pendingRevision2 = new APIRevisionListItemModel
            {
                Id = "pending-revision-2",
                ReviewId = "review-id",
                APIRevisionType = APIRevisionType.Automatic,
                IsApproved = false,
                IsReleased = false,
                CreatedOn = DateTime.UtcNow.AddDays(-2),
                Files = new List<APICodeFileModel> { new APICodeFileModel { PackageVersion = "0.9.1" } }
            };

            var pendingRevision3 = new APIRevisionListItemModel
            {
                Id = "pending-revision-3",
                ReviewId = "review-id",
                APIRevisionType = APIRevisionType.Automatic,
                IsApproved = false,
                IsReleased = false,
                CreatedOn = DateTime.UtcNow.AddDays(-1),
                Files = new List<APICodeFileModel> { new APICodeFileModel { PackageVersion = "0.9.2" } }
            };

            var newRevision = new APIRevisionListItemModel
            {
                Id = "new-revision-id",
                ReviewId = "review-id"
            };

            _mockReviewManager.Setup(m => m.GetReviewAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool?>()))
                .ReturnsAsync(existingReview);

            _mockApiRevisionsManager.Setup(m => m.GetAPIRevisionsAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<APIRevisionType>()))
                .ReturnsAsync(new List<APIRevisionListItemModel> { pendingRevision3, pendingRevision2, pendingRevision1 });

            // All revisions don't match the new content (so they get deleted)
            _mockApiRevisionsManager.Setup(m => m.AreAPIRevisionsTheSame(
                    It.IsAny<APIRevisionListItemModel>(), It.IsAny<RenderedCodeFile>(), It.IsAny<bool>(), It.IsAny<string>()))
                .ReturnsAsync(false);

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

            // Verify all three revisions were deleted since none match the new content
            _mockApiRevisionsManager.Verify(m => m.SoftDeleteAPIRevisionAsync(
                It.Is<APIRevisionListItemModel>(r => r.Id == "pending-revision-3"),
                It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            _mockApiRevisionsManager.Verify(m => m.SoftDeleteAPIRevisionAsync(
                It.Is<APIRevisionListItemModel>(r => r.Id == "pending-revision-2"),
                It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            _mockApiRevisionsManager.Verify(m => m.SoftDeleteAPIRevisionAsync(
                It.Is<APIRevisionListItemModel>(r => r.Id == "pending-revision-1"),
                It.IsAny<string>(), It.IsAny<string>()), Times.Once);

            // Should create new revision because pending-revision-1 doesn't match
            _mockApiRevisionsManager.Verify(m => m.CreateAPIRevisionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<APIRevisionType>(),
                It.IsAny<string>(), It.IsAny<MemoryStream>(), It.IsAny<CodeFile>(),
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>()), Times.Once);

            apiRevision.Should().NotBeNull();
            apiRevision.Id.Should().Be("new-revision-id");
        }

        [Fact]
        public async Task CreateAutomaticRevisionAsync_WhenRevisionMatchesWithVersionConsideration_ReusesIt()
        {
            // The reuse check uses considerPackageVersion=true when the incoming code file has a version.
            // A revision that matches with version should be reused, not deleted and recreated.
            var codeFile = CreateCodeFile("TestPackage", "1.0.0", "C#");
            using var memoryStream = new MemoryStream();

            var existingReview = new ReviewListItemModel
            {
                Id = "review-id",
                PackageName = "TestPackage",
                Language = "C#"
            };

            var pendingRevision = new APIRevisionListItemModel
            {
                Id = "pending-revision",
                ReviewId = "review-id",
                APIRevisionType = APIRevisionType.Automatic,
                IsApproved = false,
                IsReleased = false,
                CreatedOn = DateTime.UtcNow.AddDays(-1),
                Files = new List<APICodeFileModel> { new APICodeFileModel { PackageVersion = "1.0.0" } }
            };

            _mockReviewManager.Setup(m => m.GetReviewAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool?>()))
                .ReturnsAsync(existingReview);

            _mockApiRevisionsManager.Setup(m => m.GetAPIRevisionsAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<APIRevisionType>()))
                .ReturnsAsync(new List<APIRevisionListItemModel> { pendingRevision });

            // Matches when version is considered (considerPackageVersion=true)
            _mockApiRevisionsManager.Setup(m => m.AreAPIRevisionsTheSame(
                    It.IsAny<APIRevisionListItemModel>(), It.IsAny<RenderedCodeFile>(), true, It.IsAny<string>()))
                .ReturnsAsync(true);

            _mockCommentsManager.Setup(m => m.GetCommentsAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CommentType?>(), It.IsAny<bool>()))
                .ReturnsAsync(new List<CommentItemModel>());

            var (_, apiRevision) = await _service.CreateAutomaticRevisionAsync(
                _testUser, codeFile, "test-label", "test.json", memoryStream, null);

            // Revision matches → reused, not deleted
            _mockApiRevisionsManager.Verify(m => m.SoftDeleteAPIRevisionAsync(
                It.IsAny<APIRevisionListItemModel>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);

            _mockApiRevisionsManager.Verify(m => m.CreateAPIRevisionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<APIRevisionType>(),
                It.IsAny<string>(), It.IsAny<MemoryStream>(), It.IsAny<CodeFile>(),
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>()), Times.Never);

            apiRevision.Should().NotBeNull();
            apiRevision.Id.Should().Be("pending-revision");
        }

        [Fact]
        public async Task CreateAutomaticRevisionAsync_WhenRevisionWithCommentsNotDeleted_CanReuseIt()
        {
            // Verify that revisions with comments are not deleted and can be reused
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

            _mockApiRevisionsManager.Setup(m => m.AreAPIRevisionsTheSame(
                    It.IsAny<APIRevisionListItemModel>(), It.IsAny<RenderedCodeFile>(), It.IsAny<bool>(), It.IsAny<string>()))
                .ReturnsAsync(true); // Same content

            _mockCommentsManager.Setup(m => m.GetCommentsAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CommentType?>(), It.IsAny<bool>()))
                .ReturnsAsync(new List<CommentItemModel> { comment });

            var (_, apiRevision) = await _service.CreateAutomaticRevisionAsync(
                _testUser, codeFile, "test-label", "test.json", memoryStream, null);

            // Should NOT delete revision with comments
            _mockApiRevisionsManager.Verify(m => m.SoftDeleteAPIRevisionAsync(
                It.IsAny<APIRevisionListItemModel>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);

            // Should reuse existing revision
            _mockApiRevisionsManager.Verify(m => m.CreateAPIRevisionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<APIRevisionType>(),
                It.IsAny<string>(), It.IsAny<MemoryStream>(), It.IsAny<CodeFile>(),
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>()), Times.Never);

            apiRevision.Should().NotBeNull();
            apiRevision.Id.Should().Be("revision-with-comments");
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
                CreatedOn = DateTime.UtcNow.AddDays(-1), Files = new List<APICodeFileModel>()
            };
            var revisionV2 = new APIRevisionListItemModel
            {
                Id = "rev-v2", ReviewId = "review-id",
                APIRevisionType = APIRevisionType.Automatic, APIVersionId = "ver-v2",
                IsApproved = false, IsReleased = false,
                CreatedOn = DateTime.UtcNow.AddDays(-2), Files = new List<APICodeFileModel>()
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
        ///   B3 — pending, v2-beta  (same version as incoming, matches content → reused)
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

            // B3 matches incoming → reused, no new revision created
            _mockApiRevisionsManager.Setup(m => m.AreAPIRevisionsTheSame(
                    It.IsAny<APIRevisionListItemModel>(), It.IsAny<RenderedCodeFile>(), It.IsAny<bool>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            (_, APIRevisionListItemModel apiRevision) = await _service.CreateAutomaticRevisionAsync(
                _testUser, codeFile, "build-label", "test.json", memoryStream, null);

            apiRevision.Should().NotBeNull();
            apiRevision.Id.Should().Be("b3-v2beta");

            // v1-stable revisions are never even visited — must never be touched
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
        /// Simulates a realistic sequence of CI builds arriving for the same package:
        ///
        /// History (newest first, all same version / version filter disabled):
        ///   B4 — pending, different content  (most recent build, e.g. new API change)
        ///   B3 — pending, same content as B4 (duplicate CI run)
        ///   B2 — pending, same content as B4 (another duplicate)
        ///   B1 — approved                    (anchor — must never be touched)
        ///
        /// Expected outcome when B5 (also same content as B4) arrives:
        ///   - B4 is kept as the candidate (newest unprotected)
        ///   - B3 is deleted  (older unprotected)
        ///   - B2 is deleted  (older unprotected)
        ///   - B1 is NOT deleted (approved anchor — hard stop)
        ///   - B5 is NOT created (B4 content matches incoming)
        ///   - Returned revision is B4
        /// </summary>
        [Fact]
        public async Task CreateAutomaticRevisionAsync_MultiBuildProgression_OnlyNewestPendingKept()
        {
            var codeFile = CreateCodeFile("TestPackage", "2.0.0", "C#");
            using var memoryStream = new MemoryStream();

            var review = new ReviewListItemModel { Id = "review-id", PackageName = "TestPackage", Language = "C#" };

            // B1 - approved anchor (oldest)
            var b1Approved = new APIRevisionListItemModel
            {
                Id = "b1-approved",
                ReviewId = "review-id",
                APIRevisionType = APIRevisionType.Automatic,
                IsApproved = true,
                IsReleased = false,
                CreatedOn = DateTime.UtcNow.AddDays(-10),
                Files = [new APICodeFileModel { PackageVersion = "1.0.0" }]
            };

            // B2 - pending, same content as incoming (2nd oldest)
            var b2Pending = new APIRevisionListItemModel
            {
                Id = "b2-pending",
                ReviewId = "review-id",
                APIRevisionType = APIRevisionType.Automatic,
                IsApproved = false,
                IsReleased = false,
                CreatedOn = DateTime.UtcNow.AddDays(-3),
                Files = [new APICodeFileModel { PackageVersion = "2.0.0" }]
            };

            // B3 - pending, same content as incoming (2nd newest)
            var b3Pending = new APIRevisionListItemModel
            {
                Id = "b3-pending",
                ReviewId = "review-id",
                APIRevisionType = APIRevisionType.Automatic,
                IsApproved = false,
                IsReleased = false,
                CreatedOn = DateTime.UtcNow.AddDays(-2),
                Files = [new APICodeFileModel { PackageVersion = "2.0.0" }]
            };

            // B4 - pending, same content as incoming (newest)
            var b4Pending = new APIRevisionListItemModel
            {
                Id = "b4-pending",
                ReviewId = "review-id",
                APIRevisionType = APIRevisionType.Automatic,
                IsApproved = false,
                IsReleased = false,
                CreatedOn = DateTime.UtcNow.AddDays(-1),
                Files = [new APICodeFileModel { PackageVersion = "2.0.0" }]
            };

            _mockReviewManager.Setup(m => m.GetReviewAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool?>()))
                .ReturnsAsync(review);

            // Ordered newest first (as OrderByDescending produces)
            _mockApiRevisionsManager.Setup(m => m.GetAPIRevisionsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<APIRevisionType>()))
                .ReturnsAsync(new List<APIRevisionListItemModel> { b4Pending, b3Pending, b2Pending, b1Approved });

            _mockCommentsManager.Setup(m => m.GetCommentsAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CommentType?>(), It.IsAny<bool>()))
                .ReturnsAsync(new List<CommentItemModel>());

            _mockApiRevisionsManager.Setup(m => m.SoftDeleteAPIRevisionAsync(
                    It.IsAny<APIRevisionListItemModel>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // B4 matches incoming content (reuse check after cleanup)
            _mockApiRevisionsManager.Setup(m => m.AreAPIRevisionsTheSame(
                    It.Is<APIRevisionListItemModel>(r => r.Id == "b4-pending"),
                    It.IsAny<RenderedCodeFile>(), It.IsAny<bool>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            (_, APIRevisionListItemModel apiRevision) = await _service.CreateAutomaticRevisionAsync(
                _testUser, codeFile, "build-5-label", "test.json", memoryStream, null);

            // B4 is reused — no new revision created
            _mockApiRevisionsManager.Verify(m => m.CreateAPIRevisionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<APIRevisionType>(),
                It.IsAny<string>(), It.IsAny<MemoryStream>(), It.IsAny<CodeFile>(),
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>()), Times.Never);

            apiRevision.Should().NotBeNull();
            apiRevision.Id.Should().Be("b4-pending");

            // B3 and B2 (older unprotected) are deleted
            _mockApiRevisionsManager.Verify(m => m.SoftDeleteAPIRevisionAsync(
                It.Is<APIRevisionListItemModel>(r => r.Id == "b3-pending"),
                It.IsAny<string>(), It.IsAny<string>()), Times.Once, "B3 should be deleted as an older unprotected revision");

            _mockApiRevisionsManager.Verify(m => m.SoftDeleteAPIRevisionAsync(
                It.Is<APIRevisionListItemModel>(r => r.Id == "b2-pending"),
                It.IsAny<string>(), It.IsAny<string>()), Times.Once, "B2 should be deleted as an older unprotected revision");

            // B4 (kept as candidate) must NOT be deleted
            _mockApiRevisionsManager.Verify(m => m.SoftDeleteAPIRevisionAsync(
                It.Is<APIRevisionListItemModel>(r => r.Id == "b4-pending"),
                It.IsAny<string>(), It.IsAny<string>()), Times.Never, "B4 is the candidate and must be kept");

            // B1 (approved anchor) must NOT be deleted
            _mockApiRevisionsManager.Verify(m => m.SoftDeleteAPIRevisionAsync(
                It.Is<APIRevisionListItemModel>(r => r.Id == "b1-approved"),
                It.IsAny<string>(), It.IsAny<string>()), Times.Never, "B1 is approved and must never be touched");
        }

        /// <summary>
        /// Same multi-build history but the incoming build (B5) has a NEW API change (different content).
        ///
        /// History (newest first):
        ///   B4 — pending, old content
        ///   B3 — pending, old content
        ///   B2 — pending, old content
        ///   B1 — approved anchor
        ///
        /// Expected outcome when B5 (new content) arrives:
        ///   - B4 is kept as candidate (newest unprotected — never deleted by cleanup)
        ///   - B3 and B2 are deleted (older unprotected)
        ///   - B1 is NOT deleted (approved anchor)
        ///   - B5 is created as a new revision 
        /// </summary>
        [Fact]
        public async Task CreateAutomaticRevisionAsync_MultiBuildProgression_NewAPIChange_CreatesNewRevision()
        {
            var codeFile = CreateCodeFile("TestPackage", "2.0.0", "C#");
            using var memoryStream = new MemoryStream();

            var review = new ReviewListItemModel { Id = "review-id", PackageName = "TestPackage", Language = "C#" };

            var b1Approved = new APIRevisionListItemModel
            {
                Id = "b1-approved",
                ReviewId = "review-id",
                APIRevisionType = APIRevisionType.Automatic,
                IsApproved = true,
                CreatedOn = DateTime.UtcNow.AddDays(-10),
                Files = [new APICodeFileModel { PackageVersion = "1.0.0" }]
            };

            var b2Pending = new APIRevisionListItemModel
            {
                Id = "b2-pending",
                ReviewId = "review-id",
                APIRevisionType = APIRevisionType.Automatic,
                IsApproved = false,
                CreatedOn = DateTime.UtcNow.AddDays(-3),
                Files = [new APICodeFileModel { PackageVersion = "2.0.0" }]
            };

            var b3Pending = new APIRevisionListItemModel
            {
                Id = "b3-pending",
                ReviewId = "review-id",
                APIRevisionType = APIRevisionType.Automatic,
                IsApproved = false,
                CreatedOn = DateTime.UtcNow.AddDays(-2),
                Files = [new APICodeFileModel { PackageVersion = "2.0.0" }]
            };

            var b4Pending = new APIRevisionListItemModel
            {
                Id = "b4-pending",
                ReviewId = "review-id",
                APIRevisionType = APIRevisionType.Automatic,
                IsApproved = false,
                CreatedOn = DateTime.UtcNow.AddDays(-1),
                Files = [new APICodeFileModel { PackageVersion = "2.0.0" }]
            };

            var b5New = new APIRevisionListItemModel { Id = "b5-new", ReviewId = "review-id" };

            _mockReviewManager.Setup(m => m.GetReviewAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool?>()))
                .ReturnsAsync(review);

            _mockApiRevisionsManager.Setup(m => m.GetAPIRevisionsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<APIRevisionType>()))
                .ReturnsAsync(new List<APIRevisionListItemModel> { b4Pending, b3Pending, b2Pending, b1Approved });

            _mockCommentsManager.Setup(m => m.GetCommentsAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CommentType?>(), It.IsAny<bool>()))
                .ReturnsAsync(new List<CommentItemModel>());

            _mockApiRevisionsManager.Setup(m => m.SoftDeleteAPIRevisionAsync(
                    It.IsAny<APIRevisionListItemModel>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // None of the existing revisions match incoming content (new API change)
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

            // B4 is the candidate but doesn't match — deleted before new revision is created
            _mockApiRevisionsManager.Verify(m => m.SoftDeleteAPIRevisionAsync(
                It.Is<APIRevisionListItemModel>(r => r.Id == "b4-pending"),
                It.IsAny<string>(), It.IsAny<string>()), Times.Once, "B4 is stale and must be deleted before B5 is created");

            _mockApiRevisionsManager.Verify(m => m.SoftDeleteAPIRevisionAsync(
                It.Is<APIRevisionListItemModel>(r => r.Id == "b3-pending"),
                It.IsAny<string>(), It.IsAny<string>()), Times.Once, "B3 should be deleted");

            _mockApiRevisionsManager.Verify(m => m.SoftDeleteAPIRevisionAsync(
                It.Is<APIRevisionListItemModel>(r => r.Id == "b2-pending"),
                It.IsAny<string>(), It.IsAny<string>()), Times.Once, "B2 should be deleted");

            _mockApiRevisionsManager.Verify(m => m.SoftDeleteAPIRevisionAsync(
                It.Is<APIRevisionListItemModel>(r => r.Id == "b1-approved"),
                It.IsAny<string>(), It.IsAny<string>()), Times.Never, "B1 is approved and must never be touched");

            _mockApiRevisionsManager.Verify(m => m.CreateAPIRevisionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<APIRevisionType>(),
                It.IsAny<string>(), It.IsAny<MemoryStream>(), It.IsAny<CodeFile>(),
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>()), Times.Once);
        }

        /// <summary>
        /// Pending revisions exist both above and below an approval anchor, and the
        /// second-newest pending revision has a comment attached (protected).
        ///
        /// History (newest first):
        ///   B4 — pending, no comments
        ///   B3 — pending, HAS a comment  ← protected
        ///   B2 — pending, no comments
        ///   B1 — approved anchor
        ///
        /// Expected outcome when B5 (any content) arrives:
        ///   - B4 is kept as candidate
        ///   - B3 is hit: protected by comment → hard stop, B2 is never visited
        ///   - B2 and B1 are NOT deleted
        /// </summary>
        [Fact]
        public async Task CreateAutomaticRevisionAsync_MultiBuildProgression_StopsAtCommentedRevision()
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

            var b3WithComment = new APIRevisionListItemModel
            {
                Id = "b3-with-comment", ReviewId = "review-id",
                APIRevisionType = APIRevisionType.Automatic,
                IsApproved = false, CreatedOn = DateTime.UtcNow.AddDays(-2),
                Files = new List<APICodeFileModel> { new APICodeFileModel { PackageVersion = "2.0.0" } }
            };

            var b4Pending = new APIRevisionListItemModel
            {
                Id = "b4-pending", ReviewId = "review-id",
                APIRevisionType = APIRevisionType.Automatic,
                IsApproved = false, CreatedOn = DateTime.UtcNow.AddDays(-1),
                Files = new List<APICodeFileModel> { new APICodeFileModel { PackageVersion = "2.0.0" } }
            };

            var b5New = new APIRevisionListItemModel { Id = "b5-new", ReviewId = "review-id" };
            var comment = new CommentItemModel { APIRevisionId = "b3-with-comment", ReviewId = "review-id" };

            _mockReviewManager.Setup(m => m.GetReviewAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool?>()))
                .ReturnsAsync(review);

            _mockApiRevisionsManager.Setup(m => m.GetAPIRevisionsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<APIRevisionType>()))
                .ReturnsAsync(new List<APIRevisionListItemModel> { b4Pending, b3WithComment, b2Pending, b1Approved });

            _mockCommentsManager.Setup(m => m.GetCommentsAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CommentType?>(), It.IsAny<bool>()))
                .ReturnsAsync(new List<CommentItemModel> { comment });

            _mockApiRevisionsManager.Setup(m => m.SoftDeleteAPIRevisionAsync(
                    It.IsAny<APIRevisionListItemModel>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // B4 doesn't match; B5 is new content
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

            // B4 is the candidate but doesn't match — deleted before new revision is created
            _mockApiRevisionsManager.Verify(m => m.SoftDeleteAPIRevisionAsync(
                It.Is<APIRevisionListItemModel>(r => r.Id == "b4-pending"),
                It.IsAny<string>(), It.IsAny<string>()), Times.Once, "B4 is stale and must be deleted before B5 is created");

            // B3 has a comment → hard stop, must NOT be deleted
            _mockApiRevisionsManager.Verify(m => m.SoftDeleteAPIRevisionAsync(
                It.Is<APIRevisionListItemModel>(r => r.Id == "b3-with-comment"),
                It.IsAny<string>(), It.IsAny<string>()), Times.Never, "B3 has a comment and must be kept");

            // B2 is never reached because B3 caused a hard stop
            _mockApiRevisionsManager.Verify(m => m.SoftDeleteAPIRevisionAsync(
                It.Is<APIRevisionListItemModel>(r => r.Id == "b2-pending"),
                It.IsAny<string>(), It.IsAny<string>()), Times.Never, "B2 is behind the comment anchor and must not be touched");

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
