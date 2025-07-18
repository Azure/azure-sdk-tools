using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers;
using APIViewWeb.Repositories;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace APIViewUnitTests
{
    public class ReviewManagerNamespaceApprovalTests
    {
        [Fact]
        public void IsSDKLanguage_Returns_True_For_Supported_Languages()
        {
            // Test all five supported SDK languages
            Assert.True(IsSDKLanguageStatic("C#"));
            Assert.True(IsSDKLanguageStatic("Java"));
            Assert.True(IsSDKLanguageStatic("Python"));
            Assert.True(IsSDKLanguageStatic("Go"));
            Assert.True(IsSDKLanguageStatic("JavaScript"));
            
            // Test case insensitivity
            Assert.True(IsSDKLanguageStatic("c#"));
            Assert.True(IsSDKLanguageStatic("java"));
            Assert.True(IsSDKLanguageStatic("python"));
            Assert.True(IsSDKLanguageStatic("go"));
            Assert.True(IsSDKLanguageStatic("javascript"));
        }

        [Fact]
        public void IsSDKLanguage_Returns_False_For_Unsupported_Languages()
        {
            Assert.False(IsSDKLanguageStatic("TypeSpec"));
            Assert.False(IsSDKLanguageStatic("Swagger"));
            Assert.False(IsSDKLanguageStatic("C++"));
            Assert.False(IsSDKLanguageStatic("Rust"));
            Assert.False(IsSDKLanguageStatic("Unknown"));
            Assert.False(IsSDKLanguageStatic(""));
            Assert.False(IsSDKLanguageStatic(null));
        }

        [Theory]
        [InlineData("azure.storage.blobs", "azure.storage")]
        [InlineData("contoso.widget", "contoso")]
        [InlineData("microsoft.graph.users", "microsoft.graph")]
        [InlineData("simple", "simple")]
        [InlineData("", "")]
        [InlineData(null, "")]
        public void ExtractPackageBaseName_Returns_Expected_Result(string packageName, string expected)
        {
            var result = ExtractPackageBaseNameStatic(packageName);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void AreAllRelatedSDKReviewsApproved_Returns_True_When_All_SDK_Reviews_Approved()
        {
            // Arrange
            var reviews = new List<ReviewListItemModel>
            {
                CreateReview("contoso.widget", "C#", true),     // Approved SDK
                CreateReview("contoso.widget", "Java", true),   // Approved SDK
                CreateReview("contoso.widget", "Python", true), // Approved SDK
                CreateReview("contoso.widget", "TypeSpec", false) // TypeSpec (not checked)
            };

            // Act
            var result = AreAllRelatedSDKReviewsApprovedStatic(reviews, "contoso");

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void AreAllRelatedSDKReviewsApproved_Returns_False_When_Some_SDK_Reviews_Not_Approved()
        {
            // Arrange
            var reviews = new List<ReviewListItemModel>
            {
                CreateReview("contoso.widget", "C#", true),     // Approved SDK
                CreateReview("contoso.widget", "Java", false),  // Not approved SDK
                CreateReview("contoso.widget", "Python", true), // Approved SDK
                CreateReview("contoso.widget", "TypeSpec", false) // TypeSpec (not checked)
            };

            // Act
            var result = AreAllRelatedSDKReviewsApprovedStatic(reviews, "contoso");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void AreAllRelatedSDKReviewsApproved_Returns_True_When_No_SDK_Reviews_Exist()
        {
            // Arrange
            var reviews = new List<ReviewListItemModel>
            {
                CreateReview("contoso.widget", "TypeSpec", false), // TypeSpec only
                CreateReview("contoso.widget", "Swagger", false)   // Swagger only
            };

            // Act
            var result = AreAllRelatedSDKReviewsApprovedStatic(reviews, "contoso");

            // Assert
            Assert.True(result); // No SDK reviews to check, so return true
        }

        [Fact]
        public void AreAllRelatedSDKReviewsApproved_Handles_Mixed_Package_Names()
        {
            // Arrange
            var reviews = new List<ReviewListItemModel>
            {
                CreateReview("contoso.widget", "C#", true),      // Target package - approved
                CreateReview("contoso.other", "Java", false),    // Different package - ignored
                CreateReview("different.widget", "Python", false), // Different base - ignored
                CreateReview("contoso.widget", "TypeSpec", false)  // TypeSpec (not checked)
            };

            // Act
            var result = AreAllRelatedSDKReviewsApprovedStatic(reviews, "contoso");

            // Assert
            Assert.True(result); // Only one SDK review for contoso.* and it's approved
        }

        private static ReviewListItemModel CreateReview(string packageName, string language, bool isApproved)
        {
            return new ReviewListItemModel
            {
                PackageName = packageName,
                Language = language,
                IsApproved = isApproved
            };
        }

        // Static helper methods that mirror the private methods in ReviewManager
        private static bool IsSDKLanguageStatic(string language)
        {
            if (string.IsNullOrEmpty(language)) return false;

            var supportedSDKLanguages = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "C#", "Java", "Python", "Go", "JavaScript"
            };

            return supportedSDKLanguages.Contains(language);
        }

        private static string ExtractPackageBaseNameStatic(string packageName)
        {
            if (string.IsNullOrEmpty(packageName)) return string.Empty;

            var parts = packageName.Split('.');
            return parts.Length > 1 ? parts[0] : packageName;
        }

        private static bool AreAllRelatedSDKReviewsApprovedStatic(IEnumerable<ReviewListItemModel> allReviews, string packageBaseName)
        {
            var relatedSDKReviews = allReviews
                .Where(r => IsSDKLanguageStatic(r.Language))
                .Where(r => ExtractPackageBaseNameStatic(r.PackageName).Equals(packageBaseName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // If no SDK reviews exist, consider it as "all approved"
            if (!relatedSDKReviews.Any())
                return true;

            return relatedSDKReviews.All(r => r.IsApproved);
        }
    }
}
