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
        public void NotificationManager_Constructor_WithNullEndpoint_ThrowsInvalidOperationException()
        {
            // Arrange
            var config = CreateConfiguration(null);

            // Act & Assert - Constructor should throw InvalidOperationException due to null endpoint
            var exception = Assert.Throws<InvalidOperationException>(() => new NotificationManager(
                config,
                _mockReviewRepository.Object,
                _mockApiRevisionRepository.Object,
                _mockUserProfileRepository.Object,
                _mockUserProfileCache.Object,
                _telemetryClient,
                _mockEmailTemplateService.Object,
                _mockHttpClientFactory.Object,
                _mockLogger.Object));

            Assert.Contains("APIVIew-Host-Url", exception.Message);
            Assert.Contains("required but not provided", exception.Message);
        }

        [Fact]
        public void NotificationManager_Constructor_WithEmptyEndpoint_ThrowsInvalidOperationException()
        {
            // Arrange
            var config = CreateConfiguration("");

            // Act & Assert - Constructor should throw InvalidOperationException due to empty endpoint
            var exception = Assert.Throws<InvalidOperationException>(() => new NotificationManager(
                config,
                _mockReviewRepository.Object,
                _mockApiRevisionRepository.Object,
                _mockUserProfileRepository.Object,
                _mockUserProfileCache.Object,
                _telemetryClient,
                _mockEmailTemplateService.Object,
                _mockHttpClientFactory.Object,
                _mockLogger.Object));

            Assert.Contains("APIVIew-Host-Url", exception.Message);
            Assert.Contains("required but not provided", exception.Message);
        }

        [Fact]
        public void NotificationManager_Constructor_WithWhitespaceEndpoint_ThrowsInvalidOperationException()
        {
            // Arrange
            var config = CreateConfiguration("   ");

            // Act & Assert - Constructor should throw InvalidOperationException due to whitespace-only endpoint
            var exception = Assert.Throws<InvalidOperationException>(() => new NotificationManager(
                config,
                _mockReviewRepository.Object,
                _mockApiRevisionRepository.Object,
                _mockUserProfileRepository.Object,
                _mockUserProfileCache.Object,
                _telemetryClient,
                _mockEmailTemplateService.Object,
                _mockHttpClientFactory.Object,
                _mockLogger.Object));

            Assert.Contains("APIVIew-Host-Url", exception.Message);
            Assert.Contains("required but not provided", exception.Message);
        }

        [Fact]
        public void NotificationManager_Constructor_WithValidEndpoint_Success()
        {
            // Arrange
            var config = CreateConfiguration("https://apiview.dev");

            // Act & Assert - Should not throw with valid endpoint
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

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\t")]
        [InlineData("\n")]
        public void NotificationManager_Constructor_WithInvalidEndpoint_ThrowsInvalidOperationException(string invalidEndpoint)
        {
            // Arrange
            var config = CreateConfiguration(invalidEndpoint);

            // Act & Assert - Constructor should throw InvalidOperationException with any invalid endpoint
            var exception = Assert.Throws<InvalidOperationException>(() => new NotificationManager(
                config,
                _mockReviewRepository.Object,
                _mockApiRevisionRepository.Object,
                _mockUserProfileRepository.Object,
                _mockUserProfileCache.Object,
                _telemetryClient,
                _mockEmailTemplateService.Object,
                _mockHttpClientFactory.Object,
                _mockLogger.Object));

            Assert.Contains("APIVIew-Host-Url", exception.Message);
            Assert.Contains("required but not provided", exception.Message);
        }

        [Theory]
        [InlineData("https://apiview.dev")]
        [InlineData("http://localhost:5000")]
        [InlineData("https://apiview.azure.com")]
        [InlineData("https://example.com/path")]
        public void NotificationManager_Constructor_WithValidEndpoints_Success(string validEndpoint)
        {
            // Arrange
            var config = CreateConfiguration(validEndpoint);

            // Act & Assert - Should construct successfully with any valid endpoint
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
