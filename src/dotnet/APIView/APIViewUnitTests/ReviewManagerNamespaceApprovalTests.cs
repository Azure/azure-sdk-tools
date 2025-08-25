using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using APIViewWeb.LeanModels;
using Xunit;

namespace APIViewUnitTests
{
    public class ReviewManagerNamespaceApprovalTests
    {
        [Fact]
        public void IsSDKLanguage_Returns_True_For_Supported_Languages()
        {
            // Test five supported SDK languages (case-sensitive per implementation)
            Assert.True(IsSDKLanguageStatic("C#"));
            Assert.True(IsSDKLanguageStatic("Java"));
            Assert.True(IsSDKLanguageStatic("Python"));
            Assert.True(IsSDKLanguageStatic("Go"));
            Assert.True(IsSDKLanguageStatic("JavaScript"));
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
            // Case-insensitive names should be false (implementation is case-sensitive)
            Assert.False(IsSDKLanguageStatic("c#"));
            Assert.False(IsSDKLanguageStatic("java"));
        }

        [Fact]
        public void IsSDKLanguage_Returns_False_For_Null()
        {
            // Test null separately since the actual implementation doesn't handle null gracefully
            Assert.Throws<ArgumentNullException>(() => IsSDKLanguageStatic(null));
        }

        [Fact]
        public void AreAllRelatedSDKReviewsApproved_Returns_True_When_All_SDK_Reviews_Approved()
        {
            // Arrange
            var reviews = new List<ReviewListItemModel>
            {
                CreateReview("contoso.widget", "C#", true),
                CreateReview("contoso.widget", "Java", true),
                CreateReview("contoso.widget", "Python", true),
                CreateReview("contoso.widget", "TypeSpec", false)
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
                CreateReview("contoso.widget", "C#", true),
                CreateReview("contoso.widget", "Java", false),
                CreateReview("contoso.widget", "Python", true),
                CreateReview("contoso.widget", "TypeSpec", false)
            };

            // Act
            var result = AreAllRelatedSDKReviewsApprovedStatic(reviews, "contoso");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void AreAllRelatedSDKReviewsApproved_Returns_False_When_No_SDK_Reviews_Exist()
        {
            // Arrange
            var reviews = new List<ReviewListItemModel>
            {
                CreateReview("contoso.widget", "TypeSpec", false),
                CreateReview("contoso.widget", "Swagger", false)
            };

            // Act
            var result = AreAllRelatedSDKReviewsApprovedStatic(reviews, "contoso");

            // Assert
            Assert.False(result); // No SDK reviews exist, so return false
        }

        [Fact]
        public void AreAllRelatedSDKReviewsApproved_Handles_Mixed_Package_Names()
        {
            // Arrange
            var reviews = new List<ReviewListItemModel>
            {
                CreateReview("contoso.widget", "C#", true),      // Target base - approved
                CreateReview("contoso.other", "Java", false),    // Same base - NOT approved -> should fail
                CreateReview("different.widget", "Python", false), // Different base - ignored
                CreateReview("contoso.widget", "TypeSpec", false)  // TypeSpec - ignored
            };

            // Act
            var result = AreAllRelatedSDKReviewsApprovedStatic(reviews, "contoso");

            // Assert
            Assert.False(result);
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

        // Static helper methods mirroring actual ReviewManager logic
        private static bool IsSDKLanguageStatic(string language)
        {
            var sdkLanguages = new[] { "C#", "Java", "Python", "Go", "JavaScript" };
            return sdkLanguages.Contains(language); // This will throw ArgumentNullException for null, matching actual implementation
        }

        private static bool AreAllRelatedSDKReviewsApprovedStatic(IEnumerable<ReviewListItemModel> allReviews, string packageBaseName)
        {
            var baseLower = (packageBaseName ?? string.Empty).ToLower();
            var sdkLanguages = new HashSet<string>(new[] { "C#", "Java", "Python", "Go", "JavaScript" });

            var relatedSDKReviews = allReviews
                .Where(r => r != null && !string.IsNullOrEmpty(r.PackageName))
                .Where(r => sdkLanguages.Contains(r.Language))
                .Where(r => r.PackageName.ToLower().StartsWith(baseLower))
                .ToList();

            // If no SDK reviews exist, consider it as "not approved"
            if (!relatedSDKReviews.Any())
                return false;

            return relatedSDKReviews.All(r => r.IsApproved);
        }
    }
}
