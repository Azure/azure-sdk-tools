// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.Feedback;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.Feedback;

[TestFixture]
public class FeedbackToolTests
{
    private FeedbackTool _feedbackTool;
    private TestLogger<FeedbackTool> _logger;

    [SetUp]
    public void Setup()
    {
        _logger = new TestLogger<FeedbackTool>();
        _feedbackTool = new FeedbackTool(_logger);
    }

    [Test]
    public async Task CollectFeedback_WithNoUserInput_ReturnsNotSubmitted()
    {
        var result = await _feedbackTool.CollectFeedback(
            agentSummary: "Test summary",
            userFeedback: null,
            issues: null
        );

        Assert.That(result.FeedbackSubmitted, Is.False);
        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.Message, Does.Contain("No feedback provided"));
    }

    [Test]
    public async Task CollectFeedback_WithEmptyUserInput_ReturnsNotSubmitted()
    {
        var result = await _feedbackTool.CollectFeedback(
            agentSummary: "Test summary",
            userFeedback: "   ",
            issues: new List<string>()
        );

        Assert.That(result.FeedbackSubmitted, Is.False);
        Assert.That(result.ResponseError, Is.Null);
    }

    [Test]
    public async Task CollectFeedback_WithUserFeedback_ReturnsSubmitted()
    {
        var result = await _feedbackTool.CollectFeedback(
            agentSummary: "Completed task successfully",
            userFeedback: "Great session!"
        );

        Assert.That(result.FeedbackSubmitted, Is.True);
        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.UserFeedback, Is.EqualTo("Great session!"));
        Assert.That(result.AgentSummary, Is.EqualTo("Completed task successfully"));
        Assert.That(result.Repository, Is.Not.Null);
        Assert.That(result.SessionId, Is.Not.Null);
    }

    [Test]
    public async Task CollectFeedback_WithIssues_ReturnsSubmitted()
    {
        var issues = new List<string> { "Bug 1", "Bug 2" };
        var result = await _feedbackTool.CollectFeedback(
            agentSummary: "Completed task",
            userFeedback: null,
            issues: issues
        );

        Assert.That(result.FeedbackSubmitted, Is.True);
        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.IssuesReported, Has.Count.EqualTo(2));
        Assert.That(result.IssuesReported, Does.Contain("Bug 1"));
        Assert.That(result.IssuesReported, Does.Contain("Bug 2"));
    }

    [Test]
    public async Task CollectFeedback_WithCustomSessionId_UsesProvidedId()
    {
        var customSessionId = "custom-session-123";
        var result = await _feedbackTool.CollectFeedback(
            agentSummary: "Test",
            userFeedback: "Feedback",
            sessionId: customSessionId
        );

        Assert.That(result.SessionId, Is.EqualTo(customSessionId));
    }

    [Test]
    public async Task CollectFeedback_WithCustomRepository_UsesProvidedRepo()
    {
        var customRepo = "custom-repo";
        var result = await _feedbackTool.CollectFeedback(
            agentSummary: "Test",
            userFeedback: "Feedback",
            repository: customRepo
        );

        Assert.That(result.Repository, Is.EqualTo(customRepo));
    }

    [Test]
    public async Task CollectFeedback_AutoDetectsRepository_ReturnsSubmitted()
    {
        var result = await _feedbackTool.CollectFeedback(
            agentSummary: "Test",
            userFeedback: "Feedback"
        );

        Assert.That(result.Repository, Is.Not.Null);
        Assert.That(result.FeedbackSubmitted, Is.True);
    }

    [Test]
    public async Task CollectFeedback_WithAllParameters_ReturnsCompleteResponse()
    {
        var agentSummary = "Completed 3 tasks successfully";
        var userFeedback = "Excellent work!";
        var issues = new List<string> { "Minor UI issue", "Performance lag" };
        var sessionId = "session-456";
        var repository = "azure-sdk-tools";

        var result = await _feedbackTool.CollectFeedback(
            agentSummary: agentSummary,
            userFeedback: userFeedback,
            issues: issues,
            sessionId: sessionId,
            repository: repository
        );

        Assert.That(result.FeedbackSubmitted, Is.True);
        Assert.That(result.AgentSummary, Is.EqualTo(agentSummary));
        Assert.That(result.UserFeedback, Is.EqualTo(userFeedback));
        Assert.That(result.IssuesReported, Is.EqualTo(issues));
        Assert.That(result.SessionId, Is.EqualTo(sessionId));
        Assert.That(result.Repository, Is.EqualTo(repository));
        Assert.That(result.Message, Does.Contain("Thank you"));
    }

    [Test]
    public async Task RequestFeedback_ReturnsGuidance()
    {
        var agentSummary = "Task completed";
        var result = await _feedbackTool.RequestFeedback(agentSummary);

        Assert.That(result.FeedbackSubmitted, Is.False);
        Assert.That(result.AgentSummary, Is.EqualTo(agentSummary));
        Assert.That(result.Message, Does.Contain("Session Summary"));
        Assert.That(result.Message, Does.Contain("azsdk_feedback_submit"));
        Assert.That(result.ResponseError, Is.Null);
    }

    [Test]
    public void FeedbackResponse_Format_WithAllData_ProducesCorrectOutput()
    {
        var response = new FeedbackResponse
        {
            FeedbackSubmitted = true,
            SessionId = "test-123",
            AgentSummary = "Summary text",
            UserFeedback = "Feedback text",
            IssuesReported = new List<string> { "Issue 1", "Issue 2" },
            Repository = "test-repo",
            Message = "Success"
        };

        var output = response.ToString();

        Assert.That(output, Does.Contain("Success"));
        Assert.That(output, Does.Contain("test-123"));
        Assert.That(output, Does.Contain("Summary text"));
        Assert.That(output, Does.Contain("Feedback text"));
        Assert.That(output, Does.Contain("Issue 1"));
        Assert.That(output, Does.Contain("Issue 2"));
        Assert.That(output, Does.Contain("test-repo"));
    }

    [Test]
    public void FeedbackResponse_Format_WhenNotSubmitted_ShowsNotSubmitted()
    {
        var response = new FeedbackResponse
        {
            FeedbackSubmitted = false
        };

        var output = response.ToString();

        Assert.That(output, Does.Contain("Feedback not submitted"));
    }
}
