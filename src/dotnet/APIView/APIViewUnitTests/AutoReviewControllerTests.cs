using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using ApiView;
using APIViewWeb;
using APIViewWeb.LeanControllers;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Models;
using FluentAssertions;
using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace APIViewUnitTests
{
    public class AutoReviewControllerTests
    {
        private readonly Mock<ICodeFileManager> _mockCodeFileManager;
        private readonly Mock<IReviewManager> _mockReviewManager;
        private readonly Mock<IAPIRevisionsManager> _mockApiRevisionsManager;
        private readonly Mock<IAutoReviewService> _mockAutoReviewService;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly List<LanguageService> _languageServices;
        private readonly TelemetryClient _telemetryClient;
        private readonly AutoReviewController _controller;

        public AutoReviewControllerTests()
        {
            _mockCodeFileManager = new Mock<ICodeFileManager>();
            _mockReviewManager = new Mock<IReviewManager>();
            _mockApiRevisionsManager = new Mock<IAPIRevisionsManager>();
            _mockAutoReviewService = new Mock<IAutoReviewService>();
            _mockConfiguration = new Mock<IConfiguration>();
            _languageServices = new List<LanguageService>
            {
                new MockLanguageService("C#", false)
            };
            _telemetryClient = new TelemetryClient();

            _controller = new AutoReviewController(
                _mockCodeFileManager.Object,
                _mockApiRevisionsManager.Object,
                _mockAutoReviewService.Object,
                _languageServices,
                _mockConfiguration.Object,
                _telemetryClient);

            // Set up the HTTP context with a mock user principal
            SetupControllerContext();
        }

        [Theory]
        [InlineData("client")]
        [InlineData("mgmt")]
        [InlineData("CLIENT")]
        [InlineData("MGMT")]
        public async Task UploadAutoReview_WithValidPackageType_PassesCorrectPackageTypeToService(string packageTypeValue)
        {
            var mockFile = CreateMockFormFile("test.json", "dummy content");
            var mockCodeFile = new CodeFile()
            {
                Name = "test",
                Language = "C#",
                PackageName = "TestPackage",
                PackageVersion = "1.0.0"
            };

            var mockReview = new ReviewListItemModel()
            {
                Id = "test-review-id",
                PackageName = "TestPackage",
                Language = "C#",
                IsApproved = false,
                PackageType = Enum.Parse<PackageType>(packageTypeValue, true)
            };

            var mockApiRevision = new APIRevisionListItemModel()
            {
                Id = "test-revision-id",
                ReviewId = "test-review-id",
                Language = "C#",
                IsApproved = false
            };

            _mockCodeFileManager.Setup(m => m.CreateCodeFileAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<MemoryStream>(), It.IsAny<Stream>(), It.IsAny<string>()))
                .ReturnsAsync(mockCodeFile);

            _mockAutoReviewService.Setup(m => m.CreateAutomaticRevisionAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<CodeFile>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<MemoryStream>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<string>()))
                .ReturnsAsync((mockReview, mockApiRevision));

            _mockApiRevisionsManager.Setup(m => m.UpdateRevisionMetadataAsync(It.IsAny<APIRevisionListItemModel>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(mockApiRevision);

            _mockConfiguration.Setup(c => c["ReviewUrl"]).Returns("https://test.com");

             
            var result = await _controller.UploadAutoReview(mockFile.Object, "test-label", packageType: packageTypeValue);

            result.Should().NotBeNull();
            var objectResult = result.Should().BeOfType<ObjectResult>().Which;
            objectResult.StatusCode.Should().Be(StatusCodes.Status202Accepted);
            objectResult.Value.Should().NotBeNull();
            objectResult.Value.Should().BeOfType<string>().Which.Should().Contain("/Assemblies/Review/");

            _mockAutoReviewService.Verify(m => m.CreateAutomaticRevisionAsync(
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<CodeFile>(),
                "test-label",
                "test.json",
                It.IsAny<MemoryStream>(),
                packageTypeValue,
                false,
                null),
                Times.Once);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("invalid")]
        public async Task UploadAutoReview_WithInvalidPackageType_PassesValueToServiceAsIs(string packageTypeValue)
        {
            var mockFile = CreateMockFormFile("test.json", "dummy content");
            var mockCodeFile = new CodeFile()
            {
                Name = "test",
                Language = "C#",
                PackageName = "TestPackage",
                PackageVersion = "1.0.0"
            };

            var mockReview = new ReviewListItemModel()
            {
                Id = "test-review-id",
                PackageName = "TestPackage",
                Language = "C#",
                IsApproved = false,
                PackageType = null
            };

            var mockApiRevision = new APIRevisionListItemModel()
            {
                Id = "test-revision-id",
                ReviewId = "test-review-id",
                Language = "C#",
                IsApproved = false
            };

            _mockCodeFileManager.Setup(m => m.CreateCodeFileAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<MemoryStream>(), It.IsAny<Stream>(), It.IsAny<string>()))
                .ReturnsAsync(mockCodeFile);

            _mockAutoReviewService.Setup(m => m.CreateAutomaticRevisionAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<CodeFile>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<MemoryStream>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<string>()))
                .ReturnsAsync((mockReview, mockApiRevision));

            _mockApiRevisionsManager.Setup(m => m.UpdateRevisionMetadataAsync(It.IsAny<APIRevisionListItemModel>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(mockApiRevision);

            _mockConfiguration.Setup(c => c["ReviewUrl"]).Returns("https://test.com");

            
            var result = await _controller.UploadAutoReview(mockFile.Object, "test-label", packageType: packageTypeValue);

            result.Should().NotBeNull();
            var objectResult = result.Should().BeOfType<ObjectResult>().Which;
            objectResult.StatusCode.Should().Be(StatusCodes.Status202Accepted);
            objectResult.Value.Should().NotBeNull();
            objectResult.Value.Should().BeOfType<string>();

            _mockAutoReviewService.Verify(m => m.CreateAutomaticRevisionAsync(
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<CodeFile>(),
                "test-label",
                "test.json",
                It.IsAny<MemoryStream>(),
                packageTypeValue,
                false,
                null),
                Times.Once);
        }

        [Fact]
        public async Task UploadAutoReview_WhenServiceReturnsApprovedRevision_ReturnsOk()
        {
            var mockFile = CreateMockFormFile("test.json", "dummy content");
            var mockCodeFile = new CodeFile()
            {
                Name = "test",
                Language = "C#",
                PackageName = "TestPackage",
                PackageVersion = "1.0.0"
            };

            var mockReview = new ReviewListItemModel()
            {
                Id = "test-review-id",
                PackageName = "TestPackage",
                Language = "C#",
                IsApproved = false
            };

            var mockApiRevision = new APIRevisionListItemModel()
            {
                Id = "test-revision-id",
                ReviewId = "test-review-id",
                Language = "C#",
                IsApproved = true // Approved revision
            };

            _mockCodeFileManager.Setup(m => m.CreateCodeFileAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<MemoryStream>(), It.IsAny<Stream>(), It.IsAny<string>()))
                .ReturnsAsync(mockCodeFile);

            _mockAutoReviewService.Setup(m => m.CreateAutomaticRevisionAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<CodeFile>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<MemoryStream>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<string>()))
                .ReturnsAsync((mockReview, mockApiRevision));

            _mockApiRevisionsManager.Setup(m => m.UpdateRevisionMetadataAsync(It.IsAny<APIRevisionListItemModel>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(mockApiRevision);

            _mockConfiguration.Setup(c => c["ReviewUrl"]).Returns("https://test.com");

            var result = await _controller.UploadAutoReview(mockFile.Object, "test-label");

            result.Should().NotBeNull();
            var okResult = result.Should().BeOfType<OkObjectResult>().Which;
            okResult.Value.Should().NotBeNull();
            okResult.Value.Should().BeOfType<string>().Which.Should().Contain("/Assemblies/Review/");
        }

        [Fact]
        public async Task UploadAutoReview_WhenReviewIsApprovedButRevisionIsNot_Returns201Created()
        {
            var mockFile = CreateMockFormFile("test.json", "dummy content");
            var mockCodeFile = new CodeFile()
            {
                Name = "test",
                Language = "C#",
                PackageName = "TestPackage",
                PackageVersion = "1.0.0"
            };

            var mockReview = new ReviewListItemModel()
            {
                Id = "test-review-id",
                PackageName = "TestPackage",
                Language = "C#",
                IsApproved = true 
            };

            var mockApiRevision = new APIRevisionListItemModel()
            {
                Id = "test-revision-id",
                ReviewId = "test-review-id",
                Language = "C#",
                IsApproved = false 
            };

            _mockCodeFileManager.Setup(m => m.CreateCodeFileAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<MemoryStream>(), It.IsAny<Stream>(), It.IsAny<string>()))
                .ReturnsAsync(mockCodeFile);

            _mockAutoReviewService.Setup(m => m.CreateAutomaticRevisionAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<CodeFile>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<MemoryStream>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<string>()))
                .ReturnsAsync((mockReview, mockApiRevision));

            _mockApiRevisionsManager.Setup(m => m.UpdateRevisionMetadataAsync(It.IsAny<APIRevisionListItemModel>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(mockApiRevision);

            _mockConfiguration.Setup(c => c["ReviewUrl"]).Returns("https://test.com");

            var result = await _controller.UploadAutoReview(mockFile.Object, "test-label");

            result.Should().NotBeNull();
            var objectResult = result.Should().BeOfType<ObjectResult>().Which;
            objectResult.StatusCode.Should().Be(StatusCodes.Status201Created);
        }

        [Fact]
        public async Task UploadAutoReview_WhenCodeFileCreationFails_Returns500WithDetails()
        {
            var mockFile = CreateMockFormFile("test.json", "dummy content");

            _mockCodeFileManager.Setup(m => m.CreateCodeFileAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<MemoryStream>(), It.IsAny<Stream>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("Failed to parse code file: Invalid format"));

            var result = await _controller.UploadAutoReview(mockFile.Object, "test-label");

            result.Should().NotBeNull();
            var objectResult = result.Should().BeOfType<ObjectResult>().Which;
            objectResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
            objectResult.Value.Should().NotBeNull();

            string errorDetails = objectResult.Value.ToString();
            errorDetails.Should().Contain("Failed to parse code file: Invalid format");
        }

        [Fact]
        public async Task UploadAutoReview_WhenServiceThrows_Returns500WithDetails()
        {
            var mockFile = CreateMockFormFile("test.json", "dummy content");
            var mockCodeFile = new CodeFile()
            {
                Name = "test",
                Language = "C#",
                PackageName = "TestPackage",
                PackageVersion = "1.0.0"
            };

            _mockCodeFileManager.Setup(m => m.CreateCodeFileAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<MemoryStream>(), It.IsAny<Stream>(), It.IsAny<string>()))
                .ReturnsAsync(mockCodeFile);

            _mockAutoReviewService.Setup(m => m.CreateAutomaticRevisionAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<CodeFile>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<MemoryStream>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("Database connection failed"));

            ActionResult result = await _controller.UploadAutoReview(mockFile.Object, "test-label");

            result.Should().NotBeNull();
            var objectResult = result.Should().BeOfType<ObjectResult>().Which;
            objectResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
            objectResult.Value.Should().NotBeNull();

            string errorDetails = objectResult.Value.ToString();
            errorDetails.Should().Contain("Database connection failed");
        }

        [Fact]
        public async Task UploadAutoReview_WhenServiceReturnsNullRevision_Returns500()
        {
            var mockFile = CreateMockFormFile("test.json", "dummy content");
            var mockCodeFile = new CodeFile()
            {
                Name = "test",
                Language = "C#",
                PackageName = "TestPackage",
                PackageVersion = "1.0.0"
            };

            _mockCodeFileManager.Setup(m => m.CreateCodeFileAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<MemoryStream>(), It.IsAny<Stream>(), It.IsAny<string>()))
                .ReturnsAsync(mockCodeFile);

            _mockAutoReviewService.Setup(m => m.CreateAutomaticRevisionAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<CodeFile>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<MemoryStream>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<string>()))
                .ReturnsAsync((new ReviewListItemModel(), (APIRevisionListItemModel)null));

            var result = await _controller.UploadAutoReview(mockFile.Object, "test-label");

            result.Should().NotBeNull();
            var objectResult = result.Should().BeOfType<ObjectResult>().Which;
            objectResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        }

        [Fact]
        public async Task UploadAutoReview_PassesCompareAllRevisionsParameterToService()
        {
            var mockFile = CreateMockFormFile("test.json", "dummy content");
            var mockCodeFile = new CodeFile()
            {
                Name = "test",
                Language = "C#",
                PackageName = "TestPackage",
                PackageVersion = "1.0.0"
            };

            var mockReview = new ReviewListItemModel()
            {
                Id = "test-review-id",
                PackageName = "TestPackage",
                Language = "C#",
                IsApproved = false
            };

            var mockApiRevision = new APIRevisionListItemModel()
            {
                Id = "test-revision-id",
                ReviewId = "test-review-id",
                Language = "C#",
                IsApproved = false
            };

            _mockCodeFileManager.Setup(m => m.CreateCodeFileAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<MemoryStream>(), It.IsAny<Stream>(), It.IsAny<string>()))
                .ReturnsAsync(mockCodeFile);

            _mockAutoReviewService.Setup(m => m.CreateAutomaticRevisionAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<CodeFile>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<MemoryStream>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<string>()))
                .ReturnsAsync((mockReview, mockApiRevision));

            _mockApiRevisionsManager.Setup(m => m.UpdateRevisionMetadataAsync(It.IsAny<APIRevisionListItemModel>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(mockApiRevision);

            _mockConfiguration.Setup(c => c["ReviewUrl"]).Returns("https://test.com");

            await _controller.UploadAutoReview(mockFile.Object, "test-label", compareAllRevisions: true);

            _mockAutoReviewService.Verify(m => m.CreateAutomaticRevisionAsync(
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<CodeFile>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<MemoryStream>(),
                It.IsAny<string>(),
                true, // compareAllRevisions = true
                null),
                Times.Once);
        }

        [Fact]
        public async Task UploadAutoReview_UpdatesRevisionMetadataWithProvidedVersion()
        {
            // Arrange
            var mockFile = CreateMockFormFile("test.json", "dummy content");
            var mockCodeFile = new CodeFile()
            {
                Name = "test",
                Language = "C#",
                PackageName = "TestPackage",
                PackageVersion = "1.0.0"
            };

            var mockReview = new ReviewListItemModel()
            {
                Id = "test-review-id",
                PackageName = "TestPackage",
                Language = "C#",
                IsApproved = false
            };

            var mockApiRevision = new APIRevisionListItemModel()
            {
                Id = "test-revision-id",
                ReviewId = "test-review-id",
                Language = "C#",
                IsApproved = false
            };

            _mockCodeFileManager.Setup(m => m.CreateCodeFileAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<MemoryStream>(), It.IsAny<Stream>(), It.IsAny<string>()))
                .ReturnsAsync(mockCodeFile);

            _mockAutoReviewService.Setup(m => m.CreateAutomaticRevisionAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<CodeFile>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<MemoryStream>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<string>()))
                .ReturnsAsync((mockReview, mockApiRevision));

            _mockApiRevisionsManager.Setup(m => m.UpdateRevisionMetadataAsync(It.IsAny<APIRevisionListItemModel>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(mockApiRevision);

            _mockConfiguration.Setup(c => c["ReviewUrl"]).Returns("https://test.com");

            await _controller.UploadAutoReview(mockFile.Object, "test-label", packageVersion: "2.5.0", setReleaseTag: true);

            _mockApiRevisionsManager.Verify(m => m.UpdateRevisionMetadataAsync(
                mockApiRevision,
                "2.5.0", // Should use the provided version, not the code file's version
                "test-label",
                true), // setReleaseTag = true
                Times.Once);
        }

        [Fact]
        public async Task UploadAutoReview_UsesCodeFileVersionWhenVersionNotProvided()
        {
            var mockFile = CreateMockFormFile("test.json", "dummy content");
            var mockCodeFile = new CodeFile()
            {
                Name = "test",
                Language = "C#",
                PackageName = "TestPackage",
                PackageVersion = "1.0.0"
            };

            var mockReview = new ReviewListItemModel()
            {
                Id = "test-review-id",
                PackageName = "TestPackage",
                Language = "C#",
                IsApproved = false
            };

            var mockApiRevision = new APIRevisionListItemModel()
            {
                Id = "test-revision-id",
                ReviewId = "test-review-id",
                Language = "C#",
                IsApproved = false
            };

            _mockCodeFileManager.Setup(m => m.CreateCodeFileAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<MemoryStream>(), It.IsAny<Stream>(), It.IsAny<string>()))
                .ReturnsAsync(mockCodeFile);

            _mockAutoReviewService.Setup(m => m.CreateAutomaticRevisionAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<CodeFile>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<MemoryStream>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<string>()))
                .ReturnsAsync((mockReview, mockApiRevision));

            _mockApiRevisionsManager.Setup(m => m.UpdateRevisionMetadataAsync(It.IsAny<APIRevisionListItemModel>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(mockApiRevision);

            _mockConfiguration.Setup(c => c["ReviewUrl"]).Returns("https://test.com");

            await _controller.UploadAutoReview(mockFile.Object, "test-label");

            _mockApiRevisionsManager.Verify(m => m.UpdateRevisionMetadataAsync(
                mockApiRevision,
                "1.0.0", // Should use the code file's version
                "test-label",
                false), // setReleaseTag = false by default
                Times.Once);
        }

        [Theory]
        [InlineData("12.27.0", "12.28.0b1", true, false, "revision-stable-12-27")] // Different versions → Create new
        [InlineData("12.27.0", "12.28", true, false, "revision-stable-12-28")] // Different versions → Create new
        [InlineData("12.28.0b1", "12.28.0b1", false, true, "revision-prerelease-12-28-b1")] // Same version → Reuse
        [InlineData("12.27.0", "12.27.0", false, true, "revision-stable-12-27")] // Same version → Reuse
        public async Task CreateApiReview_WithDifferentVersions_CreatesNewRevision_WithSameVersion_ReusesRevision(
            string existingVersion,
            string newVersion,
            bool shouldCreateNewRevision,
            bool shouldReuseRevision,
            string revisionId)
        {
            string packageName = "azure-storage-file-test";
            APIRevisionListItemModel existingRevision = SetupPreReleaseTestScenario(
                existingVersion,
                newVersion,
                packageName,
                revisionId);

            _mockAutoReviewService.Setup(m => m.CreateAutomaticRevisionAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<CodeFile>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<MemoryStream>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<string>()))
                .ReturnsAsync((new ReviewListItemModel
                {
                    Id = "review-id",
                    PackageName = packageName,
                    Language = "Python",
                    IsApproved = false,
                    PackageType = PackageType.client
                },
                shouldCreateNewRevision 
                    ? new APIRevisionListItemModel { Id = "new-revision-id", ReviewId = "review-id", Language = "Python", IsApproved = false }
                    : existingRevision));

            _mockApiRevisionsManager.Setup(m => m.UpdateRevisionMetadataAsync(
                    It.IsAny<APIRevisionListItemModel>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>()))
                .ReturnsAsync((APIRevisionListItemModel r, string v, string l, bool s) =>
                {
                    r.Label = l;
                    if (shouldCreateNewRevision)
                    {
                        r.Files = [new APICodeFileModel { PackageVersion = v }];
                    }

                    return r;
                });

            await _controller.CreateApiReview(
                "12345",
                "packages",
                $"{packageName}-{newVersion}.whl",
                $"{packageName}_python.json",
                "CI Build",
                "Azure/azure-sdk-for-python",
                packageName,
                false,
                "internal",
                newVersion,
                false,
                "client"
            );

            _mockAutoReviewService.Verify(m => m.CreateAutomaticRevisionAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<CodeFile>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<MemoryStream>(),
                    "client",
                    false,
                    It.IsAny<string>()),
                Times.Once);

            if (shouldCreateNewRevision)
            {
                _mockApiRevisionsManager.Verify(m => m.UpdateRevisionMetadataAsync(
                        It.Is<APIRevisionListItemModel>(r => r.Id == "new-revision-id"),
                        newVersion,
                        It.IsAny<string>(),
                        It.IsAny<bool>()),
                    Times.Once,
                    "New revision should have metadata updated");
            }

            if (shouldReuseRevision)
            {
                _mockApiRevisionsManager.Verify(m => m.UpdateRevisionMetadataAsync(
                        It.Is<APIRevisionListItemModel>(r => r.Id == existingRevision.Id),
                        newVersion,
                        It.IsAny<string>(),
                        It.IsAny<bool>()),
                    Times.Once,
                    "Existing revision should have metadata updated (e.g., label)");
            }
        }

        private APIRevisionListItemModel SetupPreReleaseTestScenario(string existingVersion, string newVersion,
            string packageName, string revisionId)
        {
            const string language = "Python";

            CodeFile newCodeFile = new()
            {
                Name = "test", Language = language, PackageName = packageName, PackageVersion = newVersion
            };

            APICodeFileModel existingCodeFile = new()
            {
                Name = "test", Language = language, PackageName = packageName, PackageVersion = existingVersion
            };

            APIRevisionListItemModel existingRevision = new()
            {
                Id = revisionId,
                ReviewId = "review-id",
                Language = language,
                IsApproved = false,
                IsReleased = false,
                CreatedOn = DateTime.UtcNow.AddDays(-1),
                APIRevisionType = APIRevisionType.Automatic,
                Files = new List<APICodeFileModel> { existingCodeFile }
            };

            ReviewListItemModel existingReview = new()
            {
                Id = "review-id",
                PackageName = packageName,
                Language = language,
                IsApproved = false,
                PackageType = PackageType.client
            };

            _mockCodeFileManager.Setup(m => m.GetCodeFileAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<MemoryStream>(), It.IsAny<string>(), null, It.IsAny<string>(), null))
                .ReturnsAsync(newCodeFile);

            _mockReviewManager.Setup(m => m.GetReviewAsync(language, packageName, null))
                .ReturnsAsync(existingReview);

            _mockApiRevisionsManager.Setup(m =>
                    m.GetAPIRevisionsAsync(existingReview.Id, It.IsAny<string>(), It.IsAny<APIRevisionType>()))
                .ReturnsAsync(new List<APIRevisionListItemModel> { existingRevision });

            return existingRevision;
        }


        private Mock<IFormFile> CreateMockFormFile(string fileName, string content)
        {
            var mockFile = new Mock<IFormFile>();
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            writer.Write(content);
            writer.Flush();
            stream.Position = 0;

            mockFile.Setup(f => f.OpenReadStream()).Returns(stream);
            mockFile.Setup(f => f.FileName).Returns(fileName);
            mockFile.Setup(f => f.Length).Returns(stream.Length);
            mockFile.Setup(f => f.ContentType).Returns("application/json");

            return mockFile;
        }

        private void SetupControllerContext()
        {
            // Create a claims principal with the GitHub login claim
            var claims = new List<Claim>
            {
                new Claim("urn:github:login", "testuser")
            };
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            // Set up the HTTP context
            var httpContext = new DefaultHttpContext();
            httpContext.User = claimsPrincipal;

            _controller.ControllerContext = new ControllerContext()
            {
                HttpContext = httpContext
            };
        }

        private class MockLanguageService : LanguageService
        {
            private readonly string _name;
            private readonly bool _usesTreeStyleParser;

            public MockLanguageService(string name, bool usesTreeStyleParser)
            {
                _name = name;
                _usesTreeStyleParser = usesTreeStyleParser;
            }

            public override string Name => _name;
            public override string[] Extensions => new[] { ".json" };
            public override string VersionString => "1.0";
            public override bool CanUpdate(string versionString) => false;
            public override Task<CodeFile> GetCodeFileAsync(string originalName, Stream stream, bool runAnalysis, string crossLanguageMetadata = null) => Task.FromResult<CodeFile>(null);
            public override bool UsesTreeStyleParser => _usesTreeStyleParser;
            public override CodeFile GetReviewGenPendingCodeFile(string fileName) => null;
            public override bool GeneratePipelineRunParams(APIRevisionGenerationPipelineParamModel param) => false;
            public override bool CanConvert(string versionString) => false;
        }
    }
}
