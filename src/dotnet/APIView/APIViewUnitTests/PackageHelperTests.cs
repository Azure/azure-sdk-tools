// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using APIViewWeb.Helpers;
using Xunit;

namespace APIViewUnitTests
{
    public class PackageHelperTests
    {
        #region ClassifyPackageType Tests

        [Theory]
        // Management Plane - TypeSpec pattern: /^Azure\.ResourceManager\./
        [InlineData("Azure.ResourceManager.AAA", "C#", PackageType.Management)] // Example from spec
        [InlineData("Azure.ResourceManager.Storage", "C#", PackageType.Management)]
        [InlineData("Azure.ResourceManager.Compute", "C#", PackageType.Management)]
        [InlineData("Azure.ResourceManager.Network.Security", "C#", PackageType.Management)]
        // Data Plane - TypeSpec pattern: starts with "Azure."
        [InlineData("Azure.aaa", "C#", PackageType.Data)] // Example from spec
        [InlineData("Azure.Storage.Blobs", "C#", PackageType.Data)]
        [InlineData("Azure.Core", "C#", PackageType.Data)]
        [InlineData("Azure.Identity", "C#", PackageType.Data)]
        [InlineData("Azure.KeyVault.Secrets", "C#", PackageType.Data)]
        // Invalid patterns - should return Unknown
        [InlineData("Microsoft.Azure.Something", "C#", PackageType.Unknown)] // Different prefix
        [InlineData("SomeOther.Package", "C#", PackageType.Unknown)] // Different pattern
        [InlineData("azure.storage", "C#", PackageType.Unknown)] // Case sensitive - lowercase
        [InlineData("AZURE.Storage", "C#", PackageType.Unknown)] // Case sensitive - uppercase
        public void ClassifyPackageType_CSharp_ReturnsCorrectType(string packageName, string language, PackageType expected)
        {
            // Act
            var result = PackageHelper.ClassifyPackageType(packageName, language);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        // Management Plane - TypeSpec pattern: /^azure-mgmt(-[a-z]+){1,2}$/
        [InlineData("azure-mgmt-aaa", "Python", PackageType.Management)] // Example from spec
        [InlineData("azure-mgmt-storage", "Python", PackageType.Management)] // 1 segment
        [InlineData("azure-mgmt-compute-vm", "Python", PackageType.Management)] // 2 segments
        [InlineData("azure-mgmt-network-security", "Python", PackageType.Management)] // 2 segments
        // Data Plane - TypeSpec pattern: /^azure(-[a-z]+){1,3}$/
        [InlineData("azure-aaa-bbb-ccc", "Python", PackageType.Data)] // Example from spec (3 segments)
        [InlineData("azure-storage", "Python", PackageType.Data)] // 1 segment
        [InlineData("azure-keyvault-secrets", "Python", PackageType.Data)] // 2 segments
        [InlineData("azure-identity-cache-persistent", "Python", PackageType.Data)] // 3 segments
        [InlineData("azure-core", "Python", PackageType.Data)] // 1 segment
        // Invalid patterns - should return Unknown
        [InlineData("azure-mgmt-compute-vm-extra", "Python", PackageType.Unknown)] // Too many segments for mgmt (3 instead of max 2)
        [InlineData("azure-storage-blob-container-extra", "Python", PackageType.Unknown)] // Too many segments for data (4 instead of max 3)
        [InlineData("azure", "Python", PackageType.Unknown)] // Missing required segment
        [InlineData("azure-STORAGE", "Python", PackageType.Unknown)] // Uppercase not allowed
        [InlineData("azure-mgmt-COMPUTE", "Python", PackageType.Unknown)] // Uppercase not allowed
        [InlineData("some-other-package", "Python", PackageType.Unknown)] // Different pattern
        [InlineData("microsoft-azure-storage", "Python", PackageType.Unknown)] // Different prefix
        public void ClassifyPackageType_Python_ReturnsCorrectType(string packageName, string language, PackageType expected)
        {
            // Act
            var result = PackageHelper.ClassifyPackageType(packageName, language);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        // Management Plane - TypeSpec pattern: /^azure-resourcemanager-[^\/]+$/
        [InlineData("azure-resourcemanager-servicename", "Java", PackageType.Management)] // Example from spec
        [InlineData("azure-resourcemanager-storage", "Java", PackageType.Management)]
        [InlineData("azure-resourcemanager-compute", "Java", PackageType.Management)]
        [InlineData("azure-resourcemanager-network", "Java", PackageType.Management)]
        // Data Plane - TypeSpec pattern: /^azure(-\w+)+$/
        [InlineData("azure-aaa", "Java", PackageType.Data)] // Example from spec
        [InlineData("azure-storage-blob", "Java", PackageType.Data)]
        [InlineData("azure-keyvault-secrets", "Java", PackageType.Data)]
        [InlineData("azure-identity", "Java", PackageType.Data)]
        [InlineData("azure-core-http", "Java", PackageType.Data)]
        // Invalid patterns - should return Unknown
        [InlineData("azure-resourcemanager-foo/bar", "Java", PackageType.Unknown)] // Contains slash
        [InlineData("azure-resourcemanager-", "Java", PackageType.Unknown)] // Incomplete management
        [InlineData("azure", "Java", PackageType.Unknown)] // No dash segments for data
        [InlineData("com.azure:azure-storage", "Java", PackageType.Unknown)] // Different format
        [InlineData("microsoft-azure-storage", "Java", PackageType.Unknown)] // Different prefix
        [InlineData("Azure-storage", "Java", PackageType.Unknown)] // Wrong case
        public void ClassifyPackageType_Java_ReturnsCorrectType(string packageName, string language, PackageType expected)
        {
            // Act
            var result = PackageHelper.ClassifyPackageType(packageName, language);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        // Management Plane - package-dir pattern: /^arm-[^\/]+$/ and package name pattern: /^\@azure\/arm(?:-[a-z]+)+$/
        [InlineData("@azure/arm-aaa-bbb", "JavaScript", PackageType.Management)] // Matches package name spec
        [InlineData("@azure/arm-storage", "JavaScript", PackageType.Management)]
        [InlineData("@azure/arm-compute-management", "JavaScript", PackageType.Management)]
        [InlineData("arm-aaa-bbb", "JavaScript", PackageType.Management)] // Example from package-dir spec
        [InlineData("arm-network", "JavaScript", PackageType.Management)] // Without @azure/ scope
        [InlineData("arm-keyvault-admin", "JavaScript", PackageType.Management)]
        // Data Plane - package-dir pattern: /^(?:[a-z]+-)+rest$/ and package name pattern: /^\@azure-rest\/[a-z]+(?:-[a-z]+)*$/
        [InlineData("@azure-rest/aaa-bbb", "JavaScript", PackageType.Data)] // Matches package name spec
        [InlineData("@azure-rest/storage-blob", "JavaScript", PackageType.Data)]
        [InlineData("@azure-rest/keyvault", "JavaScript", PackageType.Data)]
        [InlineData("storage-blob-rest", "JavaScript", PackageType.Data)]
        [InlineData("identity-cache-rest", "JavaScript", PackageType.Data)]
        [InlineData("communication-common-rest", "JavaScript", PackageType.Data)]
        // Invalid patterns - should return Unknown
        [InlineData("@azure/storage-blob", "JavaScript", PackageType.Unknown)] // Doesn't match either pattern
        [InlineData("@azure/core-auth", "JavaScript", PackageType.Unknown)] // Doesn't match either pattern
        [InlineData("@azure/arm-storage/nested", "JavaScript", PackageType.Unknown)] // Contains slash
        [InlineData("@azure/ARM-storage", "JavaScript", PackageType.Unknown)] // Uppercase not allowed
        [InlineData("@azure-rest/AAA-bbb", "JavaScript", PackageType.Unknown)] // Uppercase not allowed
        [InlineData("some-other-package", "JavaScript", PackageType.Unknown)] // Different pattern
        [InlineData("@microsoft/azure-storage", "JavaScript", PackageType.Unknown)] // Different scope
        [InlineData("arm", "JavaScript", PackageType.Unknown)] // Too short for management pattern
        [InlineData("rest", "JavaScript", PackageType.Unknown)] // Too short for data pattern
        public void ClassifyPackageType_JavaScript_ReturnsCorrectType(string packageName, string language, PackageType expected)
        {
            // Act
            var result = PackageHelper.ClassifyPackageType(packageName, language);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        // Management Plane - service-dir: /^(\{output-dir\}\/)?sdk\/resourcemanager\/[^\/]*$/ and emitter-output-dir: /^(\{output-dir\}\/)?(\{service-dir\}|sdk\/resourcemanager\/)[^\/]*\/arm.*/
        [InlineData("github.com/Azure/azure-sdk-for-go/sdk/resourcemanager/contoso/armcontoso", "Go", PackageType.Management)] // Example from spec
        [InlineData("{output-dir}/sdk/resourcemanager/contoso", "Go", PackageType.Management)] // Example from spec
        [InlineData("{service-dir}/armcontoso", "Go", PackageType.Management)] // Example from spec
        [InlineData("sdk/resourcemanager/storage", "Go", PackageType.Management)]
        [InlineData("github.com/Azure/azure-sdk-for-go/sdk/resourcemanager/compute", "Go", PackageType.Management)]
        [InlineData("sdk/resourcemanager/network/armnetwork", "Go", PackageType.Management)]
        // Data Plane - service-dir: /^(\{output-dir\}\/)?sdk\/.*$/ and emitter-output-dir: /^(\{output-dir\}\/)?(\{service-dir\}|sdk\/).*\/az.*/
        [InlineData("github.com/Azure/azure-sdk-for-go/sdk/contosowidget/azmanager", "Go", PackageType.Data)] // Example from spec
        [InlineData("{output-dir}/sdk/contosowidget", "Go", PackageType.Data)] // Example from spec
        [InlineData("{service-dir}/azmanager", "Go", PackageType.Data)] // Example from spec
        [InlineData("github.com/Azure/azure-sdk-for-go/sdk/storage/azblob", "Go", PackageType.Data)]
        [InlineData("sdk/keyvault/azkeyvault", "Go", PackageType.Data)]
        [InlineData("{output-dir}/sdk/communication", "Go", PackageType.Data)]
        // Edge cases - should return Unknown when patterns don't match
        [InlineData("some-other-package", "Go", PackageType.Unknown)] // Doesn't match any pattern
        [InlineData("randompackage", "Go", PackageType.Unknown)] // Doesn't match any pattern
        [InlineData("github.com/other/random-package", "Go", PackageType.Unknown)] // Different repo
        [InlineData("microsoft/azure-sdk", "Go", PackageType.Unknown)] // Different pattern
        public void ClassifyPackageType_Go_ReturnsCorrectType(string packageName, string language, PackageType expected)
        {
            // Act
            var result = PackageHelper.ClassifyPackageType(packageName, language);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(null, "C#")]
        [InlineData("", "C#")]
        [InlineData("   ", "C#")]
        public void ClassifyPackageType_NullOrEmptyPackageName_ReturnsUnknown(string packageName, string language)
        {
            // Act
            var result = PackageHelper.ClassifyPackageType(packageName, language);

            // Assert
            Assert.Equal(PackageType.Unknown, result);
        }

        [Theory]
        [InlineData("Azure.Storage", null)]
        [InlineData("Azure.Storage", "")]
        [InlineData("Azure.Storage", "   ")]
        [InlineData("Azure.Storage", "UnsupportedLanguage")]
        public void ClassifyPackageType_InvalidLanguage_ReturnsUnknown(string packageName, string language)
        {
            // Act
            var result = PackageHelper.ClassifyPackageType(packageName, language);

            // Assert
            Assert.Equal(PackageType.Unknown, result);
        }



        #endregion
    }
}
