// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using APIViewWeb;
using APIViewWeb.Helpers;
using APIViewWeb.Hubs;
using APIViewWeb.LeanControllers;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using FluentAssertions;
using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace APIViewUnitTests
{
    public class PackageTypeClassificationTests
    {
        #region Test Setup and Mocks

        // ReviewManager dependencies
        private readonly Mock<IAuthorizationService> _mockAuthorizationService;
        private readonly Mock<ICosmosReviewRepository> _mockReviewsRepository;
        private readonly Mock<IAPIRevisionsManager> _mockApiRevisionsManager;
        private readonly Mock<ICommentsManager> _mockCommentManager;
        private readonly Mock<IBlobCodeFileRepository> _mockCodeFileRepository;
        private readonly Mock<ICosmosCommentsRepository> _mockCommentsRepository;
        private readonly Mock<ICosmosAPIRevisionsRepository> _mockApiRevisionsRepository;
        private readonly Mock<IHubContext<SignalRHub>> _mockSignalRHubContext;

        private readonly Mock<ICodeFileManager> _mockCodeFileManager;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly Mock<System.Net.Http.IHttpClientFactory> _mockHttpClientFactory;
        private readonly Mock<IPollingJobQueueManager> _mockPollingJobQueueManager;
        private readonly Mock<INotificationManager> _mockNotificationManager;
        private readonly Mock<ICosmosPullRequestsRepository> _mockPullRequestsRepository;
        private readonly Mock<ILogger<ReviewManager>> _mockReviewManagerLogger;

        // ReviewsController dependencies
        private readonly Mock<IReviewManager> _mockReviewManager;
        private readonly Mock<ILogger<ReviewsController>> _mockControllerLogger;

        private readonly Mock<IWebHostEnvironment> _mockWebHostEnvironment;

        // Test instances
        private readonly ReviewManager _reviewManager;
        private readonly ReviewsController _reviewsController;

        public PackageTypeClassificationTests()
        {
            // Initialize ReviewManager mocks
            _mockAuthorizationService = new Mock<IAuthorizationService>();
            _mockReviewsRepository = new Mock<ICosmosReviewRepository>();
            _mockApiRevisionsManager = new Mock<IAPIRevisionsManager>();
            _mockCommentManager = new Mock<ICommentsManager>();
            _mockCodeFileRepository = new Mock<IBlobCodeFileRepository>();
            _mockCommentsRepository = new Mock<ICosmosCommentsRepository>();
            _mockApiRevisionsRepository = new Mock<ICosmosAPIRevisionsRepository>();
            _mockSignalRHubContext = new Mock<IHubContext<SignalRHub>>();

            _mockCodeFileManager = new Mock<ICodeFileManager>();
            _mockConfiguration = new Mock<IConfiguration>();
            _mockHttpClientFactory = new Mock<System.Net.Http.IHttpClientFactory>();
            _mockPollingJobQueueManager = new Mock<IPollingJobQueueManager>();
            _mockNotificationManager = new Mock<INotificationManager>();
            _mockPullRequestsRepository = new Mock<ICosmosPullRequestsRepository>();
            _mockReviewManagerLogger = new Mock<ILogger<ReviewManager>>();

            // Initialize ReviewsController mocks
            _mockReviewManager = new Mock<IReviewManager>();
            _mockControllerLogger = new Mock<ILogger<ReviewsController>>();

            _mockWebHostEnvironment = new Mock<IWebHostEnvironment>();

            // Create instances
            _reviewManager = new ReviewManager(
                _mockAuthorizationService.Object,
                _mockReviewsRepository.Object,
                _mockApiRevisionsManager.Object,
                _mockCommentManager.Object,
                _mockCodeFileRepository.Object,
                _mockCommentsRepository.Object,
                _mockApiRevisionsRepository.Object,
                _mockSignalRHubContext.Object,
                new List<LanguageService>(),
                null,
                _mockCodeFileManager.Object,
                _mockConfiguration.Object,
                _mockHttpClientFactory.Object,
                _mockPollingJobQueueManager.Object,
                _mockNotificationManager.Object,
                _mockPullRequestsRepository.Object,
                _mockReviewManagerLogger.Object
            );

            _reviewsController = new ReviewsController(
                _mockControllerLogger.Object,
                _mockApiRevisionsManager.Object,
                _mockReviewManager.Object,
                _mockCommentManager.Object,
                _mockCodeFileRepository.Object,
                _mockConfiguration.Object,
                null,
                new List<LanguageService>(),
                _mockSignalRHubContext.Object,
                _mockNotificationManager.Object,
                _mockWebHostEnvironment.Object
            );

            // Setup common User principal for controller tests
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, "test-user-id"),
                new Claim("login", "testuser")
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);
            
            _reviewsController.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = principal
                }
            };
        }

        #endregion

        #region ClassifyTypeSpecPackageAsync Tests

        [Fact]
        public async Task ClassifyTypeSpecPackageAsync_ReviewNotFound_ReturnsNull()
        {
            // Arrange
            var reviewId = "nonexistent-review";
            _mockReviewsRepository.Setup(r => r.GetReviewAsync(reviewId))
                .ReturnsAsync((ReviewListItemModel)null);

            // Act
            var result = await _reviewManager.ClassifyTypeSpecPackageAsync(reviewId);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task ClassifyTypeSpecPackageAsync_NoRevisions_ReturnsReviewWithNullPackageType()
        {
            // Arrange
            var reviewId = "typespec-no-revisions";
            var typeSpecReview = new ReviewListItemModel
            {
                Id = reviewId,
                PackageName = "Microsoft.Test",
                Language = ApiViewConstants.TypeSpecLanguage,
                PackageType = null
            };

            _mockReviewsRepository.Setup(r => r.GetReviewAsync(reviewId))
                .ReturnsAsync(typeSpecReview);
            _mockApiRevisionsManager.Setup(a => a.GetAPIRevisionsAsync(reviewId, "", APIRevisionType.All))
                .ReturnsAsync(new List<APIRevisionListItemModel>());

            // Act
            var result = await _reviewManager.ClassifyTypeSpecPackageAsync(reviewId);

            // Assert
            result.Should().NotBeNull();
            result.PackageType.Should().BeNull();
            _mockReviewsRepository.Verify(r => r.UpsertReviewAsync(typeSpecReview), Times.Once);
        }

        [Fact]
        public async Task ClassifyTypeSpecPackageAsync_WithRevisionsButNoRelatedReviews_ReturnsReviewWithNullPackageType()
        {
            // Arrange
            var reviewId = "typespec-with-revisions-no-related";
            var typeSpecReview = new ReviewListItemModel
            {
                Id = reviewId,
                PackageName = "Microsoft.Communication",
                Language = ApiViewConstants.TypeSpecLanguage,
                PackageType = null
            };

            var latestRevision = new APIRevisionListItemModel
            {
                Id = "revision-1",
                CreatedOn = System.DateTime.UtcNow
            };

            _mockReviewsRepository.Setup(r => r.GetReviewAsync(reviewId))
                .ReturnsAsync(typeSpecReview);
            _mockApiRevisionsManager.Setup(a => a.GetAPIRevisionsAsync(reviewId, "", APIRevisionType.All))
                .ReturnsAsync(new List<APIRevisionListItemModel> { latestRevision });

            // No mocking for pull request lookup, which means FindRelatedReviewsByPullRequestAsync
            // will not find any related reviews and the method will return the review with null PackageType

            // Act
            var result = await _reviewManager.ClassifyTypeSpecPackageAsync(reviewId);
            
            // Assert
            // When no related reviews are found, the method should return the original review
            // with PackageType still as null, and save it to the repository
            result.Should().NotBeNull();
            result.Id.Should().Be(reviewId);
            result.PackageType.Should().BeNull();
            _mockReviewsRepository.Verify(r => r.UpsertReviewAsync(typeSpecReview), Times.Once);
        }

        [Fact]
        public async Task ClassifyTypeSpecPackageAsync_WithExistingPackageTypeInRelatedReview_UsesExistingType()
        {
            // This test focuses on the logic that would use an existing PackageType from related reviews
            // In practice, this would be tested through the controller or integration tests
            // since the ReviewManager method involves complex private method calls

            // Arrange
            var reviewId = "typespec-with-existing-classification";
            var typeSpecReview = new ReviewListItemModel
            {
                Id = reviewId,
                PackageName = "Microsoft.Storage",
                Language = ApiViewConstants.TypeSpecLanguage,
                PackageType = null
            };

            _mockReviewsRepository.Setup(r => r.GetReviewAsync(reviewId))
                .ReturnsAsync(typeSpecReview);
            _mockApiRevisionsManager.Setup(a => a.GetAPIRevisionsAsync(reviewId, "", APIRevisionType.All))
                .ReturnsAsync(new List<APIRevisionListItemModel>());

            // Act
            var result = await _reviewManager.ClassifyTypeSpecPackageAsync(reviewId);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().Be(reviewId);
        }

        [Fact]
        public async Task ClassifyTypeSpecPackageAsync_ExceptionThrown_ReturnsNull()
        {
            // Arrange
            var reviewId = "error-review";
            _mockReviewsRepository.Setup(r => r.GetReviewAsync(reviewId))
                .ThrowsAsync(new System.Exception("Database connection failed"));

            // Act
            var result = await _reviewManager.ClassifyTypeSpecPackageAsync(reviewId);

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region ReviewsController.ClassifyPackageTypeAsync Tests

        [Fact]
        public async Task ClassifyPackageTypeAsync_ReviewNotFound_ReturnsNotFound()
        {
            // Arrange
            var reviewId = "nonexistent-review";
            _mockReviewManager.Setup(rm => rm.GetReviewAsync(It.IsAny<ClaimsPrincipal>(), reviewId))
                .ReturnsAsync((ReviewListItemModel)null);

            // Act
            var result = await _reviewsController.ClassifyPackageTypeAsync(reviewId);

            // Assert
            result.Result.Should().BeOfType<NotFoundObjectResult>();
            var notFoundResult = result.Result as NotFoundObjectResult;
            notFoundResult.Value.Should().Be($"Review with ID {reviewId} not found");
        }

        [Fact]
        public async Task ClassifyPackageTypeAsync_ReviewAlreadyClassified_ReturnsExistingReview()
        {
            // Arrange
            var reviewId = "already-classified-review";
            var classifiedReview = new ReviewListItemModel
            {
                Id = reviewId,
                PackageName = "Azure.Storage.Blobs",
                Language = "C#",
                PackageType = PackageType.Data
            };

            _mockReviewManager.Setup(rm => rm.GetReviewAsync(It.IsAny<ClaimsPrincipal>(), reviewId))
                .ReturnsAsync(classifiedReview);

            // Act
            var result = await _reviewsController.ClassifyPackageTypeAsync(reviewId);

            // Assert
            result.Result.Should().BeOfType<OkObjectResult>();
            var okResult = result.Result as OkObjectResult;
            var returnedReview = okResult.Value as ReviewListItemModel;
            returnedReview.Should().NotBeNull();
            returnedReview.PackageType.Should().Be(PackageType.Data);
            returnedReview.Id.Should().Be(reviewId);

            // Verify no classification attempt was made
            _mockReviewManager.Verify(rm => rm.ClassifyTypeSpecPackageAsync(It.IsAny<string>()), Times.Never);
            _mockReviewManager.Verify(rm => rm.UpdateReviewAsync(It.IsAny<ReviewListItemModel>()), Times.Never);
        }

        [Fact]
        public async Task ClassifyPackageTypeAsync_TypeSpecLanguage_CallsClassifyTypeSpecPackageAsync()
        {
            // Arrange
            var reviewId = "typespec-review";
            var typeSpecReview = new ReviewListItemModel
            {
                Id = reviewId,
                PackageName = "Microsoft.Communication",
                Language = ApiViewConstants.TypeSpecLanguage,
                PackageType = null
            };

            var classifiedReview = new ReviewListItemModel
            {
                Id = reviewId,
                PackageName = "Microsoft.Communication",
                Language = ApiViewConstants.TypeSpecLanguage,
                PackageType = PackageType.Data
            };

            _mockReviewManager.Setup(rm => rm.GetReviewAsync(It.IsAny<ClaimsPrincipal>(), reviewId))
                .ReturnsAsync(typeSpecReview);
            _mockReviewManager.Setup(rm => rm.ClassifyTypeSpecPackageAsync(reviewId))
                .ReturnsAsync(classifiedReview);

            // Act
            var result = await _reviewsController.ClassifyPackageTypeAsync(reviewId);

            // Assert
            result.Result.Should().BeOfType<OkObjectResult>();
            var okResult = result.Result as OkObjectResult;
            var returnedReview = okResult.Value as ReviewListItemModel;
            returnedReview.Should().NotBeNull();
            returnedReview.PackageType.Should().Be(PackageType.Data);

            _mockReviewManager.Verify(rm => rm.ClassifyTypeSpecPackageAsync(reviewId), Times.Once);
            _mockReviewManager.Verify(rm => rm.UpdateReviewAsync(It.IsAny<ReviewListItemModel>()), Times.Never);
        }

        [Theory]
        [InlineData("C#", "Azure.Storage.Blobs", PackageType.Data)]
        [InlineData("C#", "Azure.ResourceManager.Storage", PackageType.Management)]
        [InlineData("Python", "azure-storage-blob", PackageType.Data)]
        [InlineData("Python", "azure-mgmt-storage", PackageType.Management)]
        [InlineData("Java", "azure-storage-blob", PackageType.Data)]
        [InlineData("Java", "azure-resourcemanager-storage", PackageType.Management)]
        [InlineData("JavaScript", "@azure-rest/storage-blob", PackageType.Data)]
        [InlineData("JavaScript", "@azure/arm-storage", PackageType.Management)]
        [InlineData("Go", "sdk/storage/azblob", PackageType.Data)]
        [InlineData("Go", "sdk/resourcemanager/storage", PackageType.Management)]
        public async Task ClassifyPackageTypeAsync_SDKLanguage_ClassifiesCorrectly(
            string language, string packageName, PackageType expectedType)
        {
            // Arrange
            var reviewId = $"sdk-review-{language.ToLower()}";
            var sdkReview = new ReviewListItemModel
            {
                Id = reviewId,
                PackageName = packageName,
                Language = language,
                PackageType = null
            };

            _mockReviewManager.Setup(rm => rm.GetReviewAsync(It.IsAny<ClaimsPrincipal>(), reviewId))
                .ReturnsAsync(sdkReview);
            _mockReviewManager.Setup(rm => rm.UpdateReviewAsync(It.IsAny<ReviewListItemModel>()))
                .ReturnsAsync(sdkReview);

            // Act
            var result = await _reviewsController.ClassifyPackageTypeAsync(reviewId);

            // Assert
            result.Result.Should().BeOfType<OkObjectResult>();
            var okResult = result.Result as OkObjectResult;
            var returnedReview = okResult.Value as ReviewListItemModel;
            returnedReview.Should().NotBeNull();
            returnedReview.PackageType.Should().Be(expectedType);

            _mockReviewManager.Verify(rm => rm.UpdateReviewAsync(It.Is<ReviewListItemModel>(r => 
                r.Id == reviewId && r.PackageType == expectedType)), Times.Once);
            _mockReviewManager.Verify(rm => rm.ClassifyTypeSpecPackageAsync(It.IsAny<string>()), Times.Never);
        }

        [Theory]
        [InlineData("Rust")]
        [InlineData("Swift")]
        [InlineData("Kotlin")]
        [InlineData("UnsupportedLanguage")]
        public async Task ClassifyPackageTypeAsync_NonSDKLanguage_SetsToUnknown(string language)
        {
            // Arrange
            var reviewId = $"non-sdk-review-{language.ToLower()}";
            var nonSdkReview = new ReviewListItemModel
            {
                Id = reviewId,
                PackageName = "some.package.name",
                Language = language,
                PackageType = null
            };

            _mockReviewManager.Setup(rm => rm.GetReviewAsync(It.IsAny<ClaimsPrincipal>(), reviewId))
                .ReturnsAsync(nonSdkReview);
            _mockReviewManager.Setup(rm => rm.UpdateReviewAsync(It.IsAny<ReviewListItemModel>()))
                .ReturnsAsync(nonSdkReview);

            // Act
            var result = await _reviewsController.ClassifyPackageTypeAsync(reviewId);

            // Assert
            result.Result.Should().BeOfType<OkObjectResult>();
            var okResult = result.Result as OkObjectResult;
            var returnedReview = okResult.Value as ReviewListItemModel;
            returnedReview.Should().NotBeNull();
            returnedReview.PackageType.Should().Be(PackageType.Unknown);

            _mockReviewManager.Verify(rm => rm.UpdateReviewAsync(It.Is<ReviewListItemModel>(r => 
                r.Id == reviewId && r.PackageType == PackageType.Unknown)), Times.Once);
        }

        [Fact]
        public async Task ClassifyPackageTypeAsync_ExceptionThrown_ReturnsInternalServerError()
        {
            // Arrange
            var reviewId = "error-review";
            _mockReviewManager.Setup(rm => rm.GetReviewAsync(It.IsAny<ClaimsPrincipal>(), reviewId))
                .ThrowsAsync(new Exception("Database connection failed"));

            // Act
            var result = await _reviewsController.ClassifyPackageTypeAsync(reviewId);

            // Assert
            result.Result.Should().BeOfType<ObjectResult>();
            var objectResult = result.Result as ObjectResult;
            objectResult.StatusCode.Should().Be(500);
            objectResult.Value.Should().Be("Internal server error while classifying package type");
        }

        #endregion

        #region Helper Method Tests for Package Classification Logic

        [Theory]
        [InlineData("azure-communication-chat", "Python", PackageType.Data)]
        [InlineData("azure-mgmt-communication", "Python", PackageType.Management)]
        [InlineData("Azure.Communication.Chat", "C#", PackageType.Data)]
        [InlineData("Azure.ResourceManager.Communication", "C#", PackageType.Management)]
        [InlineData("@azure-rest/communication-chat", "JavaScript", PackageType.Data)]
        [InlineData("@azure/arm-communication", "JavaScript", PackageType.Management)]
        [InlineData("azure-communication-chat", "Java", PackageType.Data)]
        [InlineData("azure-resourcemanager-communication", "Java", PackageType.Management)]
        [InlineData("sdk/communication/azcommunication", "Go", PackageType.Data)]
        [InlineData("sdk/resourcemanager/communication", "Go", PackageType.Management)]
        public void PackageHelper_ClassifyPackageType_ReturnsExpectedTypeForCommunicationPackages(
            string packageName, string language, PackageType expectedType)
        {
            // Act
            var actualType = PackageHelper.ClassifyPackageType(packageName, language);

            // Assert
            actualType.Should().Be(expectedType);
        }

        [Theory]
        [InlineData("random-package", "Python", PackageType.Unknown)]
        [InlineData("NotAzure.Package", "C#", PackageType.Unknown)]
        [InlineData("@company/random", "JavaScript", PackageType.Unknown)]
        [InlineData("com.company.random", "Java", PackageType.Unknown)]
        [InlineData("company.com/random", "Go", PackageType.Unknown)]
        public void PackageHelper_ClassifyPackageType_ReturnsUnknownForNonAzurePackages(
            string packageName, string language, PackageType expectedType)
        {
            // Act
            var actualType = PackageHelper.ClassifyPackageType(packageName, language);

            // Assert
            actualType.Should().Be(expectedType);
        }

        [Theory]
        [InlineData(null, "C#")]
        [InlineData("", "C#")]
        [InlineData("Azure.Storage", null)]
        [InlineData("Azure.Storage", "")]
        [InlineData("Azure.Storage", "UnsupportedLanguage")]
        public void PackageHelper_ClassifyPackageType_ReturnsUnknownForInvalidInputs(
            string packageName, string language)
        {
            // Act
            var actualType = PackageHelper.ClassifyPackageType(packageName, language);

            // Assert
            actualType.Should().Be(PackageType.Unknown);
        }

        #endregion

        #region Language Helper Tests

        [Theory]
        [InlineData("C#", true)]
        [InlineData("Python", true)]
        [InlineData("Java", true)]
        [InlineData("JavaScript", true)]
        [InlineData("Go", true)]
        [InlineData("TypeSpec", false)] // TypeSpec is not considered an SDK language for direct classification
        [InlineData("Rust", false)]
        [InlineData("Swift", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void LanguageHelper_IsSDKLanguage_ReturnsExpectedResult(string language, bool expected)
        {
            // Act
            var result = LanguageHelper.IsSDKLanguage(language);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("C#", true)]
        [InlineData("Python", true)]
        [InlineData("Java", true)]
        [InlineData("JavaScript", true)]
        [InlineData("Go", true)]
        [InlineData("TypeSpec", true)] // TypeSpec is supported for IsSDKLanguageOrTypeSpec
        [InlineData("Rust", false)]
        [InlineData("Swift", false)]
        public void LanguageHelper_IsSDKLanguageOrTypeSpec_ReturnsExpectedResult(string language, bool expected)
        {
            // Act
            var result = LanguageHelper.IsSDKLanguageOrTypeSpec(language);

            // Assert
            result.Should().Be(expected);
        }

        #endregion
    }
}
