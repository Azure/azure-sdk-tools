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
        private readonly AutoReviewService _service;
        private readonly ClaimsPrincipal _testUser;

        public AutoReviewServiceTests()
        {
            _mockReviewManager = new Mock<IReviewManager>();
            _mockApiRevisionsManager = new Mock<IAPIRevisionsManager>();
            _mockCommentsManager = new Mock<ICommentsManager>();

            _service = new AutoReviewService(
                _mockReviewManager.Object,
                _mockApiRevisionsManager.Object,
                _mockCommentsManager.Object);

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
                    It.IsAny<APIRevisionListItemModel>(), It.IsAny<RenderedCodeFile>(), It.IsAny<bool>()))
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
                    It.IsAny<APIRevisionListItemModel>(), It.IsAny<RenderedCodeFile>(), It.IsAny<bool>()))
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
                    It.IsAny<RenderedCodeFile>(), It.IsAny<bool>()))
                .ReturnsAsync(true);

            _mockApiRevisionsManager.Setup(m => m.AreAPIRevisionsTheSame(
                    It.Is<APIRevisionListItemModel>(r => r.Id == "latest-revision-id"),
                    It.IsAny<RenderedCodeFile>(), It.IsAny<bool>()))
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
                    It.IsAny<RenderedCodeFile>(), true))
                .ReturnsAsync(false);

            // But for approval copying (no version consideration), the content DOES match
            _mockApiRevisionsManager.Setup(m => m.AreAPIRevisionsTheSame(
                    It.Is<APIRevisionListItemModel>(r => r.Id == "approved-revision-id"),
                    It.IsAny<RenderedCodeFile>(), false))
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
                    It.IsAny<APIRevisionListItemModel>(), It.IsAny<RenderedCodeFile>(), It.IsAny<bool>()))
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
                    It.IsAny<APIRevisionListItemModel>(), It.IsAny<RenderedCodeFile>(), It.IsAny<bool>()))
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
        public async Task CreateAutomaticRevisionAsync_WhenLastRevisionDeletedButMatchesWithVersion_CreatesNewRevision()
        {
            // This test covers Edge Case 2: Comparison parameter inconsistency
            // Without version: different (deleted), with version: same (would match after deletion)
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
                Id = "pending-revision-to-delete",
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
                .ReturnsAsync(new List<APIRevisionListItemModel> { pendingRevision });

            // Setup comparison: without version = false (different), with version = true (same)
            _mockApiRevisionsManager.Setup(m => m.AreAPIRevisionsTheSame(
                    It.IsAny<APIRevisionListItemModel>(), It.IsAny<RenderedCodeFile>(), false))
                .ReturnsAsync(false); // Different without version, so it gets deleted

            _mockApiRevisionsManager.Setup(m => m.AreAPIRevisionsTheSame(
                    It.IsAny<APIRevisionListItemModel>(), It.IsAny<RenderedCodeFile>(), true))
                .ReturnsAsync(true); // Same with version, would match if not deleted

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

            // Verify revision was deleted
            _mockApiRevisionsManager.Verify(m => m.SoftDeleteAPIRevisionAsync(
                It.Is<APIRevisionListItemModel>(r => r.Id == "pending-revision-to-delete"),
                It.IsAny<string>(), It.IsAny<string>()), Times.Once);

            // Should create new revision instead of returning deleted one (even though it would match with version)
            _mockApiRevisionsManager.Verify(m => m.CreateAPIRevisionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<APIRevisionType>(),
                It.IsAny<string>(), It.IsAny<MemoryStream>(), It.IsAny<CodeFile>(),
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>()), Times.Once);

            apiRevision.Should().NotBeNull();
            apiRevision.Id.Should().Be("new-revision-id");
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
                    It.IsAny<APIRevisionListItemModel>(), It.IsAny<RenderedCodeFile>(), It.IsAny<bool>()))
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
