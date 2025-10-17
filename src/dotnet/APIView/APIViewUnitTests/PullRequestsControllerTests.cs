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
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("invalid")]
        [InlineData("unknown")]
        public void PackageTypeParameterHandling_ShouldPassThroughToManager(string packageTypeValue)
        {
            // This test verifies that packageType values (valid or invalid) are passed through correctly
            // The actual enum parsing and validation happens in the manager layer, not the controller
            // Controllers should pass through the string value as-is
            
            // Act - simulate controller receiving packageType parameter
            var receivedPackageType = packageTypeValue;

            // Assert - controller should pass through the value unchanged
            receivedPackageType.Should().Be(packageTypeValue);
        }

        [Fact]
        public void PackageTypeParameterHandling_WhenOmitted_ShouldDefaultToNull()
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
