using System.Collections.Generic;
using System.Threading.Tasks;
using APIViewWeb.LeanModels;
using APIViewWeb.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace APIViewUnitTests
{
    public class EmailTemplateServiceTests
    {
        private readonly EmailTemplateService _service;

        public EmailTemplateServiceTests()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["APIVIew-Host-Url"] = "https://apiview.test"
                })
                .Build();

            _service = new EmailTemplateService(configuration);
        }

        [Fact]
        public async Task GetNamespaceReviewRequestEmailAsync_WithReviewsAndNotes_RendersExpectedSections()
        {
            var reviews = new[]
            {
                new ReviewListItemModel { Id = "java-review", Language = "Java", PackageName = "azure-ai-test" },
                new ReviewListItemModel { Id = "python-review", Language = "Python", PackageName = "azure-ai-test" },
            };

            var content = await _service.GetNamespaceReviewRequestEmailAsync(
                "Azure.AI.Test",
                "https://apiview.test/Assemblies/Review/typespec-review",
                reviews,
                "Please prioritize this review.");

            content.Should().Contain("Namespace Review Request");
            content.Should().Contain("Azure.AI.Test");
            content.Should().Contain("https://apiview.test/Assemblies/Review/typespec-review");
            content.Should().Contain("Java:");
            content.Should().Contain("Python:");
            content.Should().Contain("https://apiview.test/Assemblies/Review/java-review");
            content.Should().Contain("https://apiview.test/Assemblies/Review/python-review");
            content.Should().Contain("Additional Notes:");
            content.Should().Contain("Please prioritize this review.");
            content.Should().NotContain("{{PackageName}}");
            content.Should().NotContain("{{LanguageLinks}}");
            content.Should().NotContain("{{NotesSection}}");
        }

        [Fact]
        public async Task GetNamespaceReviewRequestEmailAsync_WithHtmlInput_EncodesDynamicValues()
        {
            var reviews = new[]
            {
                new ReviewListItemModel
                {
                    Id = "id-with-unsafe-<tag>",
                    Language = "C# <unsafe>",
                    PackageName = "pkg & \"quoted\""
                },
            };

            var content = await _service.GetNamespaceReviewRequestEmailAsync(
                "Azure.<AI>",
                "https://apiview.test/Assemblies/Review/review?<script>",
                reviews,
                "notes <b>bold</b>");

            content.Should().Contain("Azure.&lt;AI&gt;");
            content.Should().Contain("C# &lt;unsafe&gt;:");
            content.Should().Contain("pkg &amp; &quot;quoted&quot;");
            content.Should().Contain("notes &lt;b&gt;bold&lt;/b&gt;");
            content.Should().Contain("id-with-unsafe-&lt;tag&gt;");
            content.Should().NotContain("<script>");
        }

        [Fact]
        public async Task GetNamespaceReviewApprovedEmailAsync_WithNoReviews_UsesFallbackMessage()
        {
            var content = await _service.GetNamespaceReviewApprovedEmailAsync(
                "Azure.AI.Test",
                "https://apiview.test/Assemblies/Review/typespec-review",
                new List<ReviewListItemModel>());

            content.Should().Contain("Namespace Review Approved");
            content.Should().Contain("No language-specific package names available yet.");
            content.Should().NotContain("{{LanguageViews}}");
        }

        [Fact]
        public async Task GetNamespaceReviewRequestEmailAsync_WithoutNotes_DoesNotRenderNotesSection()
        {
            var content = await _service.GetNamespaceReviewRequestEmailAsync(
                "Azure.AI.Test",
                "https://apiview.test/Assemblies/Review/typespec-review",
                new List<ReviewListItemModel>(),
                null);

            content.Should().NotContain("Additional Notes:");
            content.Should().Contain("No language-specific reviews available yet.");
        }
    }
}
