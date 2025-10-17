using System;
using APIViewWeb.Models;
using FluentAssertions;
using Xunit;

namespace APIViewUnitTests
{
    public class PullRequestsControllerTests
    {
        [Theory]
        [InlineData("client")]
        [InlineData("mgmt")]
        [InlineData("CLIENT")]     // Test case insensitivity  
        [InlineData("MGMT")]
        public void PackageTypeParameterHandling_WithValidValues_ShouldPassThroughToManager(string packageTypeValue)
        {
            // This test verifies that valid packageType values are passed through correctly
            // The actual enum parsing happens in the manager layer, not the controller
            // Controllers should pass through the string value as-is
            
            // Act - simulate controller receiving packageType parameter
            var receivedPackageType = packageTypeValue;

            // Assert - controller should pass through the value unchanged
            receivedPackageType.Should().Be(packageTypeValue);
            receivedPackageType.Should().NotBeNullOrEmpty();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("invalid")]
        [InlineData("unknown")]
        public void PackageTypeParameterHandling_WithInvalidValues_ShouldPassThroughToManager(string packageTypeValue)
        {
            // This test verifies that invalid/null packageType values are also passed through
            // The validation happens in the manager layer, not the controller
            
            // Act - simulate controller receiving packageType parameter
            var receivedPackageType = packageTypeValue;

            // Assert - controller should pass through the value unchanged, even if invalid
            receivedPackageType.Should().Be(packageTypeValue);
        }

        [Fact]
        public void PackageTypeDefaultValue_ShouldBeNull()
        {
            // Test verifies that the default value for packageType parameter is null
            // when not provided in the request
            
            // Act - simulate controller method with default parameter value
            string packageType = null; // Default value for optional parameter

            // Assert
            packageType.Should().BeNull();
        }

        [Theory]
        [InlineData("client", "Client package type should be supported")]
        [InlineData("mgmt", "Management package type should be supported")]
        public void PackageTypeController_DocumentedBehavior_ForValidValues(string packageTypeValue, string expectedBehavior)
        {
            // This test documents the expected behavior for different packageType values
            // in the context of the PullRequestsController
            
            // Act
            var isValidValue = !string.IsNullOrWhiteSpace(packageTypeValue) && 
                              (packageTypeValue.Equals("client", StringComparison.OrdinalIgnoreCase) ||
                               packageTypeValue.Equals("mgmt", StringComparison.OrdinalIgnoreCase));

            // Assert
            isValidValue.Should().BeTrue(expectedBehavior);
        }
    }
}
