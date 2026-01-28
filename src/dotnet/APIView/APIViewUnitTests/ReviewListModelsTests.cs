using APIViewWeb.LeanModels;
using Xunit;

namespace APIViewWeb.Tests
{
    public class ReviewListModelsTests
    {
        [Theory]
        [InlineData("Source Branch:main", null, "main")]
        [InlineData("Source Branch:feature/test-branch", "", "feature/test-branch")]
        [InlineData("Source Branch:  release/v1.0  ", null, "release/v1.0")] // Trims whitespace
        public void SourceBranch_ExtractsFromLabel_WhenSourceBranchIsNullOrEmpty(string label, string sourceBranch, string expected)
        {
            var model = new APIRevisionListItemModel { Label = label, SourceBranch = sourceBranch };
            Assert.Equal(expected, model.SourceBranch);
        }

        [Theory]
        [InlineData("Something else entirely")]
        [InlineData(null)]
        [InlineData("")]
        public void SourceBranch_ReturnsNull_WhenLabelInvalid(string label)
        {
            var model = new APIRevisionListItemModel { Label = label };
            Assert.Null(model.SourceBranch);
        }

        [Fact]
        public void SourceBranch_PrefersDirectValue_OverLabelExtraction()
        {
            var model = new APIRevisionListItemModel
            {
                Label = "Source Branch:feature/test",
                SourceBranch = "direct-value"
            };
            Assert.Equal("direct-value", model.SourceBranch);
        }
    }
}
