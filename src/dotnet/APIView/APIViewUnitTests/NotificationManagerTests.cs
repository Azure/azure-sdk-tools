// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using System.Net.Http;
using Moq;
using Xunit;
using APIViewWeb.Managers;
using APIViewWeb.Repositories;
using APIViewWeb.Services;
using APIViewWeb.Models;
using APIViewWeb.LeanModels;

namespace APIViewUnitTests
{
    public class NotificationManagerTests
    {
        private readonly Mock<ICosmosReviewRepository> _mockReviewRepository;
        private readonly Mock<ICosmosAPIRevisionsRepository> _mockApiRevisionRepository;
        private readonly Mock<ICosmosUserProfileRepository> _mockUserProfileRepository;
        private readonly Mock<UserProfileCache> _mockUserProfileCache;
        private readonly Mock<IEmailTemplateService> _mockEmailTemplateService;
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly Mock<ILogger<NotificationManager>> _mockLogger;
        private readonly TelemetryClient _telemetryClient;

        public NotificationManagerTests()
        {
            _mockReviewRepository = new Mock<ICosmosReviewRepository>();
            _mockApiRevisionRepository = new Mock<ICosmosAPIRevisionsRepository>();
            _mockUserProfileRepository = new Mock<ICosmosUserProfileRepository>();
            _mockUserProfileCache = new Mock<UserProfileCache>(Mock.Of<Microsoft.Extensions.Caching.Memory.IMemoryCache>(), 
                                                              Mock.Of<IUserProfileManager>(), 
                                                              Mock.Of<ILogger<UserProfileCache>>());
            _mockEmailTemplateService = new Mock<IEmailTemplateService>();
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockLogger = new Mock<ILogger<NotificationManager>>();
            
            // Create a minimal TelemetryClient for testing
            var telemetryConfiguration = new TelemetryConfiguration();
            _telemetryClient = new TelemetryClient(telemetryConfiguration);
        }

        [Fact]
        public async Task NotificationManager_WithNullEndpoint_ThrowsUriFormatExceptionOnUse()
        {
            // Arrange
            var config = CreateConfiguration(null);
            var notificationManager = new NotificationManager(
                config,
                _mockReviewRepository.Object,
                _mockApiRevisionRepository.Object,
                _mockUserProfileRepository.Object,
                _mockUserProfileCache.Object,
                _telemetryClient,
                _mockEmailTemplateService.Object,
                _mockHttpClientFactory.Object,
                _mockLogger.Object);

            var review = new ReviewListItemModel { Id = "test-review-id" };
            var revision = new APIRevisionListItemModel { CreatedBy = "testuser" };
            var user = new ClaimsPrincipal();

            // Act & Assert - Should throw UriFormatException when trying to use null endpoint
            await Assert.ThrowsAsync<UriFormatException>(() => 
                notificationManager.NotifySubscribersOnNewRevisionAsync(review, revision, user));
        }

        [Fact]
        public async Task NotificationManager_WithEmptyEndpoint_ThrowsUriFormatExceptionOnUse()
        {
            // Arrange
            var config = CreateConfiguration("");
            var notificationManager = new NotificationManager(
                config,
                _mockReviewRepository.Object,
                _mockApiRevisionRepository.Object,
                _mockUserProfileRepository.Object,
                _mockUserProfileCache.Object,
                _telemetryClient,
                _mockEmailTemplateService.Object,
                _mockHttpClientFactory.Object,
                _mockLogger.Object);

            var review = new ReviewListItemModel { Id = "test-review-id" };
            var revision = new APIRevisionListItemModel { CreatedBy = "testuser" };
            var user = new ClaimsPrincipal();

            // Act & Assert - Should throw UriFormatException when trying to use empty endpoint
            await Assert.ThrowsAsync<UriFormatException>(() => 
                notificationManager.NotifySubscribersOnNewRevisionAsync(review, revision, user));
        }

        [Fact]
        public async Task NotificationManager_WithWhitespaceEndpoint_ThrowsUriFormatExceptionOnUse()
        {
            // Arrange
            var config = CreateConfiguration("   ");
            var notificationManager = new NotificationManager(
                config,
                _mockReviewRepository.Object,
                _mockApiRevisionRepository.Object,
                _mockUserProfileRepository.Object,
                _mockUserProfileCache.Object,
                _telemetryClient,
                _mockEmailTemplateService.Object,
                _mockHttpClientFactory.Object,
                _mockLogger.Object);

            var review = new ReviewListItemModel { Id = "test-review-id" };
            var revision = new APIRevisionListItemModel { CreatedBy = "testuser" };
            var user = new ClaimsPrincipal();

            // Act & Assert - Should throw UriFormatException when trying to use whitespace-only endpoint
            await Assert.ThrowsAsync<UriFormatException>(() => 
                notificationManager.NotifySubscribersOnNewRevisionAsync(review, revision, user));
        }

        [Fact]
        public void NotificationManager_Constructor_WithValidEndpoint_Success()
        {
            // Arrange
            var config = CreateConfiguration("https://apiview.dev");

            // Act & Assert - Constructor should succeed with valid endpoint
            var notificationManager = new NotificationManager(
                config,
                _mockReviewRepository.Object,
                _mockApiRevisionRepository.Object,
                _mockUserProfileRepository.Object,
                _mockUserProfileCache.Object,
                _telemetryClient,
                _mockEmailTemplateService.Object,
                _mockHttpClientFactory.Object,
                _mockLogger.Object);

            Assert.NotNull(notificationManager);
        }

        [Fact]
        public async Task NotifySubscribersOnNewRevisionAsync_WithValidEndpoint_DoesNotThrow()
        {
            // Arrange
            var config = CreateConfiguration("https://apiview.dev");
            var notificationManager = new NotificationManager(
                config,
                _mockReviewRepository.Object,
                _mockApiRevisionRepository.Object,
                _mockUserProfileRepository.Object,
                _mockUserProfileCache.Object,
                _telemetryClient,
                _mockEmailTemplateService.Object,
                _mockHttpClientFactory.Object,
                _mockLogger.Object);

            var review = new ReviewListItemModel { Id = "test-review-id" };
            var revision = new APIRevisionListItemModel { CreatedBy = "testuser" };
            var user = new ClaimsPrincipal();

            // Act & Assert - With valid endpoint, this should not throw UriFormatException
            // The method may still throw other exceptions due to missing email configuration, 
            // but not UriFormatException which is what we're testing for
            try
            {
                await notificationManager.NotifySubscribersOnNewRevisionAsync(review, revision, user);
            }
            catch (UriFormatException)
            {
                Assert.Fail("UriFormatException should not be thrown with valid endpoint");
            }
            catch (Exception)
            {
                // Other exceptions are fine - we're only testing that UriFormatException doesn't occur
            }
        }

        private IConfiguration CreateConfiguration(string apiViewEndpoint)
        {
            var configData = new Dictionary<string, string>();
            
            if (apiViewEndpoint != null)
            {
                configData["APIVIew-Host-Url"] = apiViewEndpoint;
            }

            return new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();
        }
    }
}
