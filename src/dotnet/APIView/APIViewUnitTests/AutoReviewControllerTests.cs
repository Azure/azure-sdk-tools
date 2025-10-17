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
            // Act
            var result = Enum.TryParse<PackageType>(packageTypeString, true, out var parsedPackageType);

            // Assert
            result.Should().BeTrue();
            parsedPackageType.Should().Be(expectedPackageType);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("invalid")]
        [InlineData("unknown")]
        public void PackageTypeEnumParsing_WithInvalidValues_ReturnsFalse(string packageTypeString)
        {
            // Act
            var result = Enum.TryParse<PackageType>(packageTypeString, true, out var parsedPackageType);

            // Assert
            result.Should().BeFalse();
            parsedPackageType.Should().Be(default(PackageType));
        }

        [Theory]
        [InlineData("client", PackageType.client)]
        [InlineData("mgmt", PackageType.mgmt)]
        public void PackageTypeEnumParsing_WithStringValues_ProducesCorrectNullableResult(string packageTypeString, PackageType expectedPackageType)
        {
            // Act - simulate the logic from AutoReviewController.CreateAutomaticRevisionAsync
            var parsedPackageType = !string.IsNullOrEmpty(packageTypeString) && Enum.TryParse<PackageType>(packageTypeString, true, out var result) ? (PackageType?)result : null;

            // Assert
            parsedPackageType.Should().NotBeNull();
            parsedPackageType.Value.Should().Be(expectedPackageType);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("invalid")]
        public void PackageTypeEnumParsing_WithInvalidStringValues_ProducesNullResult(string packageTypeString)
        {
            // Act - simulate the logic from AutoReviewController.CreateAutomaticRevisionAsync  
            var parsedPackageType = !string.IsNullOrEmpty(packageTypeString) && Enum.TryParse<PackageType>(packageTypeString, true, out var result) ? (PackageType?)result : null;

            // Assert
            parsedPackageType.Should().BeNull();
        }
    }
}
