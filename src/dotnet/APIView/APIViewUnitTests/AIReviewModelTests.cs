using System.Text.Json;
using APIViewWeb.Models;
using Xunit;

namespace APIViewUnitTests;

public class AIReviewModelTests
{
    [Fact]
    public void AIReviewComment_DeserializesFromRuleIds_Successfully()
    {
        string json = """
                      {
                          "rule_ids": ["rule1", "rule2", "rule3"],
                          "line_no": 42,
                          "bad_code": "bad code example",
                          "suggestion": "good code suggestion",
                          "comment": "This is a test comment",
                          "source": "test_source"
                      }
                      """;

        AIReviewComment result = JsonSerializer.Deserialize<AIReviewComment>(json);

        Assert.NotNull(result);
        Assert.Equal(3, result.GuidelineIds.Count);
        Assert.Contains("rule1", result.GuidelineIds);
        Assert.Contains("rule2", result.GuidelineIds);
        Assert.Contains("rule3", result.GuidelineIds);
        Assert.Equal(42, result.LineNo);
        Assert.Equal("bad code example", result.Code);
        Assert.Equal("good code suggestion", result.Suggestion);
        Assert.Equal("This is a test comment", result.Comment);
        Assert.Equal("test_source", result.Source);
    }

    [Fact]
    public void AIReviewComment_DeserializesFromGuidelineIds_Successfully()
    {
        string json = """
                      {
                          "guideline_ids": ["guideline1", "guideline2", "guideline3"],
                          "line_no": 24,
                          "bad_code": "another bad code example",
                          "suggestion": "another good suggestion",
                          "comment": "This is another test comment",
                          "source": "another_test_source"
                      }
                      """;

        AIReviewComment result = JsonSerializer.Deserialize<AIReviewComment>(json);

        Assert.NotNull(result);
        Assert.Equal(3, result.GuidelineIds.Count);
        Assert.Contains("guideline1", result.GuidelineIds);
        Assert.Contains("guideline2", result.GuidelineIds);
        Assert.Contains("guideline3", result.GuidelineIds);
        Assert.Equal(24, result.LineNo);
        Assert.Equal("another bad code example", result.Code);
        Assert.Equal("another good suggestion", result.Suggestion);
        Assert.Equal("This is another test comment", result.Comment);
        Assert.Equal("another_test_source", result.Source);
    }
}
