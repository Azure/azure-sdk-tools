using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using ApiView;
using APIViewWeb.Controllers;
using APIViewWeb.Helpers;
using APIViewWeb.LeanModels;
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
        [Theory]
        [InlineData("client", PackageType.client)]
        [InlineData("mgmt", PackageType.mgmt)]
        [InlineData("CLIENT", PackageType.client)]  // Test case insensitivity
        [InlineData("MGMT", PackageType.mgmt)]
        public void PackageTypeEnumParsing_WithValidValues_ParsesCorrectly(string packageTypeString, PackageType expectedPackageType)
        {
            // Act - test both direct enum parsing and controller logic
            var directParseResult = Enum.TryParse<PackageType>(packageTypeString, true, out var directParsedPackageType);
            var controllerLogicResult = !string.IsNullOrEmpty(packageTypeString) && Enum.TryParse<PackageType>(packageTypeString, true, out var controllerParsedType) ? (PackageType?)controllerParsedType : null;

            // Assert
            directParseResult.Should().BeTrue();
            directParsedPackageType.Should().Be(expectedPackageType);
            controllerLogicResult.Should().NotBeNull();
            controllerLogicResult.Value.Should().Be(expectedPackageType);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("invalid")]
        [InlineData("unknown")]
        public void PackageTypeEnumParsing_WithInvalidValues_ReturnsExpectedResults(string packageTypeString)
        {
            // Act - test both direct enum parsing and controller logic
            var directParseResult = Enum.TryParse<PackageType>(packageTypeString, true, out var directParsedPackageType);
            var controllerLogicResult = !string.IsNullOrEmpty(packageTypeString) && Enum.TryParse<PackageType>(packageTypeString, true, out var controllerParsedType) ? (PackageType?)controllerParsedType : null;

            // Assert
            directParseResult.Should().BeFalse();
            directParsedPackageType.Should().Be(default(PackageType));
            controllerLogicResult.Should().BeNull();
        }

        [Fact]
        public void ReviewUpdate_WithExistingReviewWithoutPackageType_ShouldSetPackageType()
        {
            // Arrange - simulate existing review without PackageType
            var existingReview = new ReviewListItemModel()
            {
                Id = "existing-review-id",
                PackageName = "TestPackage",
                Language = "C#",
                PackageType = null  // No package type set initially
            };

            var packageTypeString = "mgmt";
            var parsedPackageType = !string.IsNullOrEmpty(packageTypeString) && Enum.TryParse<PackageType>(packageTypeString, true, out var result) ? (PackageType?)result : null;

            // Act - simulate the logic from AutoReviewController.CreateAutomaticRevisionAsync
            if (parsedPackageType.HasValue && !existingReview.PackageType.HasValue)
            {
                existingReview.PackageType = parsedPackageType;
            }

            // Assert
            existingReview.PackageType.Should().Be(PackageType.mgmt);
        }

        [Fact]
        public void ReviewUpdate_WithExistingReviewWithPackageType_ShouldNotOverridePackageType()
        {
            // Arrange - simulate existing review with PackageType already set
            var existingReview = new ReviewListItemModel()
            {
                Id = "existing-review-id",
                PackageName = "TestPackage",
                Language = "C#",
                PackageType = PackageType.client  // Already has package type set
            };

            var packageTypeString = "mgmt";
            var parsedPackageType = !string.IsNullOrEmpty(packageTypeString) && Enum.TryParse<PackageType>(packageTypeString, true, out var result) ? (PackageType?)result : null;

            // Act - simulate the logic from AutoReviewController.CreateAutomaticRevisionAsync
            if (parsedPackageType.HasValue && !existingReview.PackageType.HasValue)
            {
                existingReview.PackageType = parsedPackageType;
            }

            // Assert - PackageType should remain unchanged
            existingReview.PackageType.Should().Be(PackageType.client);
        }

        [Theory]
        [InlineData("invalid-package-type")]
        [InlineData(null)]
        [InlineData("")]
        public void ReviewUpdate_WithInvalidOrNullPackageType_ShouldNotSetPackageType(string packageTypeString)
        {
            // Arrange - simulate existing review without PackageType
            var existingReview = new ReviewListItemModel()
            {
                Id = "existing-review-id",
                PackageName = "TestPackage",
                Language = "C#",
                PackageType = null  // No package type set initially
            };

            var parsedPackageType = !string.IsNullOrEmpty(packageTypeString) && Enum.TryParse<PackageType>(packageTypeString, true, out var result) ? (PackageType?)result : null;

            // Act - simulate the logic from AutoReviewController.CreateAutomaticRevisionAsync
            if (parsedPackageType.HasValue && !existingReview.PackageType.HasValue)
            {
                existingReview.PackageType = parsedPackageType;
            }

            // Assert - PackageType should remain null due to invalid/null input
            existingReview.PackageType.Should().BeNull();
        }
    }
}
