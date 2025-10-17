using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using ApiView;
using APIViewWeb;
using APIViewWeb.Controllers;
using APIViewWeb.Helpers;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace APIViewUnitTests
{
    public class AutoReviewControllerTests
    {
        private readonly Mock<IAuthorizationService> _mockAuthorizationService;
        private readonly Mock<ICodeFileManager> _mockCodeFileManager;
        private readonly Mock<IReviewManager> _mockReviewManager;
        private readonly Mock<IAPIRevisionsManager> _mockApiRevisionsManager;
        private readonly Mock<ICommentsManager> _mockCommentsManager;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly List<LanguageService> _languageServices;
        private readonly AutoReviewController _controller;

        public AutoReviewControllerTests()
        {
            _mockAuthorizationService = new Mock<IAuthorizationService>();
            _mockCodeFileManager = new Mock<ICodeFileManager>();
            _mockReviewManager = new Mock<IReviewManager>();
            _mockApiRevisionsManager = new Mock<IAPIRevisionsManager>();
            _mockCommentsManager = new Mock<ICommentsManager>();
            _mockConfiguration = new Mock<IConfiguration>();
            _languageServices = new List<LanguageService>
            {
                new MockLanguageService("C#", false)
            };

            _controller = new AutoReviewController(
                _mockAuthorizationService.Object,
                _mockCodeFileManager.Object,
                _mockReviewManager.Object,
                _mockApiRevisionsManager.Object,
                _mockCommentsManager.Object,
                _mockConfiguration.Object,
                _languageServices);

            // Set up the HTTP context with a mock user principal
            SetupControllerContext();
        }

        [Theory]
        [InlineData("client")]
        [InlineData("mgmt")]
        [InlineData("CLIENT")]
        [InlineData("MGMT")]
        public async Task UploadAutoReview_WithValidPackageType_CreatesNewReviewWithCorrectPackageType(string packageTypeValue)
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

            var mockApiRevision = new APIRevisionListItemModel()
            {
                Id = "test-revision-id",
                ReviewId = "test-review-id",
                Language = "C#",
                IsApproved = false
            };

            var expectedPackageType = Enum.Parse<PackageType>(packageTypeValue, true);

            // Setup mocks for new review creation scenario
            _mockCodeFileManager.Setup(m => m.CreateCodeFileAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<MemoryStream>(), It.IsAny<Stream>(), It.IsAny<string>()))
                .ReturnsAsync(mockCodeFile);

            _mockReviewManager.Setup(m => m.GetReviewAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool?>()))
                .ReturnsAsync((ReviewListItemModel)null); // No existing review

            _mockReviewManager.Setup(m => m.CreateReviewAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<PackageType?>()))
                .ReturnsAsync(new ReviewListItemModel() 
                { 
                    Id = "test-review-id", 
                    PackageName = "TestPackage", 
                    Language = "C#",
                    IsApproved = false,
                    PackageType = expectedPackageType
                });

            _mockApiRevisionsManager.Setup(m => m.CreateAPIRevisionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<APIRevisionType>(), It.IsAny<string>(), It.IsAny<MemoryStream>(), It.IsAny<CodeFile>(), It.IsAny<string>(), It.IsAny<int?>()))
                .ReturnsAsync(mockApiRevision);

            _mockApiRevisionsManager.Setup(m => m.UpdateRevisionMetadataAsync(It.IsAny<APIRevisionListItemModel>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(mockApiRevision);

            _mockConfiguration.Setup(c => c["ReviewUrl"]).Returns("https://test.com");

            // Act
            var result = await _controller.UploadAutoReview(mockFile.Object, "test-label", packageType: packageTypeValue);

            // Assert
            result.Should().NotBeNull();
            
            // UploadAutoReview returns ObjectResult with 202 status and review URL
            var objectResult = result.Should().BeOfType<ObjectResult>().Which;
            objectResult.StatusCode.Should().Be(StatusCodes.Status202Accepted);
            objectResult.Value.Should().NotBeNull();
            objectResult.Value.Should().BeOfType<string>().Which.Should().Contain("/Assemblies/Review/");

            // Verify that CreateReviewAsync was called with the correct parsed PackageType
            _mockReviewManager.Verify(m => m.CreateReviewAsync(
                "TestPackage", 
                "C#", 
                false, 
                It.Is<PackageType?>(pt => pt.HasValue && pt.Value == expectedPackageType)), 
                Times.Once);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("invalid")]
        public async Task UploadAutoReview_WithInvalidPackageType_CreatesReviewWithNullPackageType(string packageTypeValue)
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

            var mockApiRevision = new APIRevisionListItemModel()
            {
                Id = "test-revision-id",
                ReviewId = "test-review-id",
                Language = "C#",
                IsApproved = false
            };

            // Setup mocks for new review creation scenario
            _mockCodeFileManager.Setup(m => m.CreateCodeFileAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<MemoryStream>(), It.IsAny<Stream>(), It.IsAny<string>()))
                .ReturnsAsync(mockCodeFile);

            _mockReviewManager.Setup(m => m.GetReviewAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool?>()))
                .ReturnsAsync((ReviewListItemModel)null); // No existing review

            _mockReviewManager.Setup(m => m.CreateReviewAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<PackageType?>()))
                .ReturnsAsync(new ReviewListItemModel() 
                { 
                    Id = "test-review-id", 
                    PackageName = "TestPackage", 
                    Language = "C#",
                    IsApproved = false,
                    PackageType = null
                });

            _mockApiRevisionsManager.Setup(m => m.CreateAPIRevisionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<APIRevisionType>(), It.IsAny<string>(), It.IsAny<MemoryStream>(), It.IsAny<CodeFile>(), It.IsAny<string>(), It.IsAny<int?>()))
                .ReturnsAsync(mockApiRevision);

            _mockApiRevisionsManager.Setup(m => m.UpdateRevisionMetadataAsync(It.IsAny<APIRevisionListItemModel>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(mockApiRevision);

            _mockConfiguration.Setup(c => c["ReviewUrl"]).Returns("https://test.com");

            // Act
            var result = await _controller.UploadAutoReview(mockFile.Object, "test-label", packageType: packageTypeValue);

            // Assert
            result.Should().NotBeNull();
            
            // UploadAutoReview returns ObjectResult with 202 status and review URL
            var objectResult = result.Should().BeOfType<ObjectResult>().Which;
            objectResult.StatusCode.Should().Be(StatusCodes.Status202Accepted);
            objectResult.Value.Should().NotBeNull();
            objectResult.Value.Should().BeOfType<string>();

            // Verify that CreateReviewAsync was called with null PackageType for invalid values
            _mockReviewManager.Verify(m => m.CreateReviewAsync(
                "TestPackage", 
                "C#", 
                false, 
                It.Is<PackageType?>(pt => !pt.HasValue)), 
                Times.Once);
        }

        [Fact]
        public async Task UploadAutoReview_WithExistingReviewWithoutPackageType_UpdatesReviewPackageType()
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

            var existingReview = new ReviewListItemModel()
            {
                Id = "existing-review-id",
                PackageName = "TestPackage",
                Language = "C#",
                IsApproved = false,
                PackageType = null  // No package type set initially
            };

            var mockApiRevision = new APIRevisionListItemModel()
            {
                Id = "test-revision-id",
                ReviewId = "existing-review-id",
                Language = "C#",
                IsApproved = false
            };

            // Setup mocks for existing review scenario
            _mockCodeFileManager.Setup(m => m.CreateCodeFileAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<MemoryStream>(), It.IsAny<Stream>(), It.IsAny<string>()))
                .ReturnsAsync(mockCodeFile);

            _mockReviewManager.Setup(m => m.GetReviewAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool?>()))
                .ReturnsAsync(existingReview);

            _mockReviewManager.Setup(m => m.UpdateReviewAsync(It.IsAny<ReviewListItemModel>()))
                .ReturnsAsync(existingReview);

            _mockApiRevisionsManager.Setup(m => m.GetAPIRevisionsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<APIRevisionType>()))
                .ReturnsAsync(new List<APIRevisionListItemModel>());

            _mockApiRevisionsManager.Setup(m => m.CreateAPIRevisionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<APIRevisionType>(), It.IsAny<string>(), It.IsAny<MemoryStream>(), It.IsAny<CodeFile>(), It.IsAny<string>(), It.IsAny<int?>()))
                .ReturnsAsync(mockApiRevision);

            _mockApiRevisionsManager.Setup(m => m.UpdateRevisionMetadataAsync(It.IsAny<APIRevisionListItemModel>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(mockApiRevision);

            _mockConfiguration.Setup(c => c["ReviewUrl"]).Returns("https://test.com");

            // Act
            var result = await _controller.UploadAutoReview(mockFile.Object, "test-label", packageType: "mgmt");

            // Assert
            result.Should().NotBeNull();
            
            // UploadAutoReview returns ObjectResult with 202 status and review URL
            var objectResult = result.Should().BeOfType<ObjectResult>().Which;
            objectResult.StatusCode.Should().Be(StatusCodes.Status202Accepted);
            objectResult.Value.Should().NotBeNull();
            objectResult.Value.Should().BeOfType<string>();

            // Verify that UpdateReviewAsync was called (indicating the review was updated)
            _mockReviewManager.Verify(m => m.UpdateReviewAsync(
                It.Is<ReviewListItemModel>(r => r.PackageType == PackageType.mgmt)), 
                Times.Once);
        }

        [Fact]
        public async Task UploadAutoReview_WithExistingReviewWithPackageType_DoesNotOverridePackageType()
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

            var existingReview = new ReviewListItemModel()
            {
                Id = "existing-review-id",
                PackageName = "TestPackage",
                Language = "C#",
                IsApproved = false,
                PackageType = PackageType.client  // Already has package type set
            };

            var mockApiRevision = new APIRevisionListItemModel()
            {
                Id = "test-revision-id",
                ReviewId = "existing-review-id",
                Language = "C#",
                IsApproved = false
            };

            // Setup mocks for existing review scenario
            _mockCodeFileManager.Setup(m => m.CreateCodeFileAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<MemoryStream>(), It.IsAny<Stream>(), It.IsAny<string>()))
                .ReturnsAsync(mockCodeFile);

            _mockReviewManager.Setup(m => m.GetReviewAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool?>()))
                .ReturnsAsync(existingReview);

            _mockApiRevisionsManager.Setup(m => m.GetAPIRevisionsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<APIRevisionType>()))
                .ReturnsAsync(new List<APIRevisionListItemModel>());

            _mockApiRevisionsManager.Setup(m => m.CreateAPIRevisionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<APIRevisionType>(), It.IsAny<string>(), It.IsAny<MemoryStream>(), It.IsAny<CodeFile>(), It.IsAny<string>(), It.IsAny<int?>()))
                .ReturnsAsync(mockApiRevision);

            _mockApiRevisionsManager.Setup(m => m.UpdateRevisionMetadataAsync(It.IsAny<APIRevisionListItemModel>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(mockApiRevision);

            _mockConfiguration.Setup(c => c["ReviewUrl"]).Returns("https://test.com");

            // Act
            var result = await _controller.UploadAutoReview(mockFile.Object, "test-label", packageType: "mgmt");

            // Assert
            result.Should().NotBeNull();
            
            // UploadAutoReview returns ObjectResult with 202 status and review URL
            var objectResult = result.Should().BeOfType<ObjectResult>().Which;
            objectResult.StatusCode.Should().Be(StatusCodes.Status202Accepted);
            objectResult.Value.Should().NotBeNull();
            objectResult.Value.Should().BeOfType<string>();

            // Verify that UpdateReviewAsync was NOT called since PackageType was already set
            _mockReviewManager.Verify(m => m.UpdateReviewAsync(It.IsAny<ReviewListItemModel>()), Times.Never);
        }

        [Fact]
        public async Task UploadAutoReview_WithNullFile_ReturnsInternalServerError()
        {
            // Act
            var result = await _controller.UploadAutoReview(null, "test-label", packageType: "client");

            // Assert
            result.Should().NotBeNull();
            result.Should().BeOfType<StatusCodeResult>().Which.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);

            // Verify no manager methods were called
            _mockCodeFileManager.Verify(m => m.CreateCodeFileAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<MemoryStream>(), It.IsAny<Stream>(), It.IsAny<string>()), Times.Never);
            _mockReviewManager.Verify(m => m.CreateReviewAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<PackageType?>()), Times.Never);
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
            public override Task<CodeFile> GetCodeFileAsync(string originalName, Stream stream, bool runAnalysis) => Task.FromResult<CodeFile>(null);
            public override bool UsesTreeStyleParser => _usesTreeStyleParser;
            public override CodeFile GetReviewGenPendingCodeFile(string fileName) => null;
            public override bool GeneratePipelineRunParams(APIRevisionGenerationPipelineParamModel param) => false;
            public override bool CanConvert(string versionString) => false;
        }
    }
}
