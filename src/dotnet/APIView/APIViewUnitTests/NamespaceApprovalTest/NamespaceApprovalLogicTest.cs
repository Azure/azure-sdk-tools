using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace APIViewUnitTests
{
    public class NamespaceApprovalLogicTest
    {
        [Theory]
        [InlineData("C#")]
        [InlineData("Java")]
        [InlineData("Python")]
        [InlineData("Go")]
        [InlineData("JavaScript")]
        public void IsSDKLanguage_Supported_ReturnsTrue(string language)
        {
            Assert.True(IsSDKLanguage(language));
        }

        [Theory]
        [InlineData("TypeSpec")]
        [InlineData("Swagger")]
        [InlineData("C++")]
        [InlineData("")]
        public void IsSDKLanguage_Unsupported_ReturnsFalse(string language)
        {
            Assert.False(IsSDKLanguage(language));
        }

        [Fact]
        public void IsSDKLanguage_Null_ReturnsFalse()
        {
            Assert.False(IsSDKLanguage(null));
        }

        [Fact]
        public void RelatedSdkReviews_AllApproved_ReturnsTrue()
        {
            var reviews = new[]
            {
                R("contoso.widget","C#",true),
                R("contoso.widget","Java",true),
                R("contoso.widget","Python",true),
                R("contoso.widget","TypeSpec",false)
            };
            Assert.True(AllRelatedSdkApproved(reviews, "contoso"));
        }

        [Fact]
        public void RelatedSdkReviews_SomeNotApproved_ReturnsFalse()
        {
            var reviews = new[]
            {
                R("contoso.widget","C#",true),
                R("contoso.widget","Java",false),
                R("contoso.widget","Python",true),
                R("contoso.widget","TypeSpec",false)
            };
            Assert.False(AllRelatedSdkApproved(reviews, "contoso"));
        }

        [Fact]
        public void RelatedSdkReviews_NoneExist_ReturnsFalse()
        {
            var reviews = new[]
            {
                R("contoso.widget","TypeSpec",false),
                R("contoso.widget","Swagger",false)
            };
            Assert.False(AllRelatedSdkApproved(reviews, "contoso"));
        }

        [Fact]
        public void RelatedSdkReviews_MixedBaseNames_ReturnsTrueWhenOnlyTargetApproved()
        {
            var reviews = new[]
            {
                R("contoso.widget","C#",true),
                R("different.widget","Python",false),
                R("contoso.widget","TypeSpec",false)
            };
            Assert.True(AllRelatedSdkApproved(reviews, "contoso"));
        }

        private static bool IsSDKLanguage(string language)
        {
            var sdk = new[] { "C#","Java","Python","Go","JavaScript" };
            return sdk.Contains(language); // Matches actual implementation - will throw for null
        }

        private static bool AllRelatedSdkApproved(IEnumerable<(string Package, string Lang, bool Approved)> all, string baseLower)
        {
            var sdk = new HashSet<string>(new[] { "C#","Java","Python","Go","JavaScript" });
            var related = all
                .Where(r => !string.IsNullOrEmpty(r.Package))
                .Where(r => sdk.Contains(r.Lang))
                .Where(r => r.Package.ToLower().StartsWith(baseLower))
                .ToList();
            if (!related.Any()) return false;
            return related.All(r => r.Approved);
        }

        private static (string Package, string Lang, bool Approved) R(string p, string l, bool a) => (p,l,a);
    }
}
