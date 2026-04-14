// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
#if DEBUG
using Moq;
using Octokit;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.SLA;
using Azure.Sdk.Tools.Cli.Models.Responses.SLA;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.SLA;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.SLA
{
    [TestFixture]
    public class SLAStatusToolTests
    {
        private Mock<IGitHubService> _mockGitHub;
        private ISLAConfigProvider _config;
        private SLAMetricsService _metricsService;
        private SLAStatusTool _tool;

        [SetUp]
        public void Setup()
        {
            _mockGitHub = new Mock<IGitHubService>();
            _config = new SLAConfigProvider();
            _metricsService = new SLAMetricsService(
                _mockGitHub.Object,
                _config,
                new TestLogger<SLAMetricsService>());
            _tool = new SLAStatusTool(
                _metricsService,
                new TestLogger<SLAStatusTool>());
        }

        // ========================
        // Helper methods to build mock Octokit objects
        // ========================

        private static User CreateTestUser(string login)
        {
            return new User(
                url: "", htmlUrl: "", avatarUrl: "", id: 0, login: login, nodeId: "",
                bio: "", siteAdmin: false, blog: "",
                createdAt: default, updatedAt: default,
                followers: 0, following: 0, hireable: null, email: "",
                publicRepos: 0, publicGists: 0,
                totalPrivateRepos: 0, ownedPrivateRepos: 0,
                diskUsage: 0, collaborators: 0, plan: null, privateGists: 0,
                company: "", location: "", name: "",
                suspendedAt: null, ldapDistinguishedName: "", permissions: null
            );
        }

        private static Issue CreateIssue(int number, string title, DateTimeOffset createdAt,
            ItemState state, List<string> labelNames, string? assignee = null,
            DateTimeOffset? closedAt = null)
        {
            var labels = labelNames.Select(name =>
                new Label(0, "", name, "", "", "", false)).ToList();

            var user = CreateTestUser("external-user");
            var assigneeUser = assignee != null ? CreateTestUser(assignee) : null;

            return new Issue(
                url: $"https://api.github.com/repos/Azure/test-repo/issues/{number}",
                htmlUrl: $"https://github.com/Azure/test-repo/issues/{number}",
                commentsUrl: "",
                eventsUrl: "",
                number: number,
                state: state,
                title: title,
                body: "",
                closedBy: null,
                user: user,
                labels: labels.AsReadOnly(),
                assignee: assigneeUser,
                assignees: null,
                milestone: null,
                comments: 0,
                pullRequest: null,
                closedAt: closedAt,
                createdAt: createdAt,
                updatedAt: createdAt,
                id: number,
                nodeId: "",
                locked: false,
                repository: null,
                reactions: null,
                activeLockReason: null,
                stateReason: null
            );
        }

        private static IssueComment CreateComment(DateTimeOffset createdAt,
            string authorLogin, AuthorAssociation authorAssociation)
        {
            var user = CreateTestUser(authorLogin);

            return new IssueComment(
                id: 1,
                nodeId: "",
                url: "",
                htmlUrl: "",
                body: "test comment",
                createdAt: createdAt,
                updatedAt: createdAt,
                user: user,
                reactions: null,
                authorAssociation: authorAssociation
            );
        }

        // ========================
        // No issues scenario
        // ========================

        [Test]
        public async Task GetSLAStatus_NoIssues_ReturnsEmptyMetrics()
        {
            _mockGitHub.Setup(s => s.ListIssuesForSLAAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<DateTimeOffset>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Issue>().AsReadOnly());

            var result = await _tool.GetSLAStatus("TestService", "test-repo");

            Assert.That(result.Service, Is.EqualTo("TestService"));
            Assert.That(result.TotalOpenIssues, Is.EqualTo(0));
            Assert.That(result.FirstQuestionResponse.TotalTracked, Is.EqualTo(0));
            Assert.That(result.BugResolution.TotalTracked, Is.EqualTo(0));
            Assert.That(result.QuestionResolution.TotalTracked, Is.EqualTo(0));
            Assert.That(result.ApproachingBreaches, Is.Null);
            Assert.That(result.BreachedIssues, Is.Null);
        }

        // ========================
        // FQR (First Question Response) tests
        // ========================

        [Test]
        public async Task FQR_TeamResponseWithinSLA_CountsAsCompliant()
        {
            // Issue created 5 business days ago, team responded after 2 business days
            var issueCreated = DateTimeOffset.UtcNow.AddDays(-7);
            var commentCreated = issueCreated.AddDays(2);

            var issue = CreateIssue(1, "Test issue", issueCreated, ItemState.Open,
                ["customer-reported", "TestService"]);

            _mockGitHub.Setup(s => s.ListIssuesForSLAAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<DateTimeOffset>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Issue> { issue }.AsReadOnly());

            _mockGitHub.Setup(s => s.GetIssueCommentsAsync(
                    It.IsAny<string>(), It.IsAny<string>(), 1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<IssueComment>
                {
                    CreateComment(commentCreated, "team-member", AuthorAssociation.Member)
                }.AsReadOnly());

            var result = await _tool.GetSLAStatus("TestService", "test-repo");

            Assert.That(result.FirstQuestionResponse.TotalTracked, Is.EqualTo(1));
            Assert.That(result.FirstQuestionResponse.WithinSLA, Is.EqualTo(1));
            Assert.That(result.FirstQuestionResponse.Breached, Is.EqualTo(0));
            Assert.That(result.FirstQuestionResponse.CompliancePercent, Is.EqualTo(100));
        }

        [Test]
        public async Task FQR_NoTeamResponse_PastThreshold_CountsAsBreached()
        {
            var issueCreated = DateTimeOffset.UtcNow.AddDays(-14);

            var issue = CreateIssue(1, "Unanswered issue", issueCreated, ItemState.Open,
                ["customer-reported", "TestService"]);

            _mockGitHub.Setup(s => s.ListIssuesForSLAAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<DateTimeOffset>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Issue> { issue }.AsReadOnly());

            _mockGitHub.Setup(s => s.GetIssueCommentsAsync(
                    It.IsAny<string>(), It.IsAny<string>(), 1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<IssueComment>().AsReadOnly());

            var result = await _tool.GetSLAStatus("TestService", "test-repo");

            Assert.That(result.FirstQuestionResponse.TotalTracked, Is.EqualTo(1));
            Assert.That(result.FirstQuestionResponse.Breached, Is.EqualTo(1));
            Assert.That(result.FirstQuestionResponse.WithinSLA, Is.EqualTo(0));
            Assert.That(result.BreachedIssues, Is.Not.Null);
            Assert.That(result.BreachedIssues!.Any(i => i.SLAMetricType == "fqr"), Is.True);
        }

        [Test]
        public async Task FQR_BotCommentIgnored_OnlyTeamMemberCounts()
        {
            var issueCreated = DateTimeOffset.UtcNow.AddDays(-14);

            var issue = CreateIssue(1, "Bot-only issue", issueCreated, ItemState.Open,
                ["customer-reported", "TestService"]);

            _mockGitHub.Setup(s => s.ListIssuesForSLAAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<DateTimeOffset>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Issue> { issue }.AsReadOnly());

            _mockGitHub.Setup(s => s.GetIssueCommentsAsync(
                    It.IsAny<string>(), It.IsAny<string>(), 1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<IssueComment>
                {
                    CreateComment(issueCreated.AddHours(1), "azure-sdk[bot]", AuthorAssociation.Member)
                }.AsReadOnly());

            var result = await _tool.GetSLAStatus("TestService", "test-repo");

            Assert.That(result.FirstQuestionResponse.Breached, Is.EqualTo(1));
            Assert.That(result.FirstQuestionResponse.WithinSLA, Is.EqualTo(0));
        }

        [Test]
        public async Task FQR_ExternalUserComment_DoesNotCountAsResponse()
        {
            var issueCreated = DateTimeOffset.UtcNow.AddDays(-14);

            var issue = CreateIssue(1, "External comment issue", issueCreated, ItemState.Open,
                ["customer-reported", "TestService"]);

            _mockGitHub.Setup(s => s.ListIssuesForSLAAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<DateTimeOffset>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Issue> { issue }.AsReadOnly());

            _mockGitHub.Setup(s => s.GetIssueCommentsAsync(
                    It.IsAny<string>(), It.IsAny<string>(), 1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<IssueComment>
                {
                    CreateComment(issueCreated.AddHours(1), "random-user", AuthorAssociation.None)
                }.AsReadOnly());

            var result = await _tool.GetSLAStatus("TestService", "test-repo");

            Assert.That(result.FirstQuestionResponse.Breached, Is.EqualTo(1));
        }

        [Test]
        public async Task FQR_ClosedWithoutResponse_CountsAsBreached()
        {
            var issueCreated = DateTimeOffset.UtcNow.AddDays(-30);

            var issue = CreateIssue(1, "Closed silently", issueCreated, ItemState.Closed,
                ["customer-reported", "TestService"], closedAt: issueCreated.AddDays(5));

            _mockGitHub.Setup(s => s.ListIssuesForSLAAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<DateTimeOffset>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Issue> { issue }.AsReadOnly());

            _mockGitHub.Setup(s => s.GetIssueCommentsAsync(
                    It.IsAny<string>(), It.IsAny<string>(), 1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<IssueComment>().AsReadOnly());

            var result = await _tool.GetSLAStatus("TestService", "test-repo", includeClosed: true);

            Assert.That(result.FirstQuestionResponse.Breached, Is.EqualTo(1));
        }

        // ========================
        // Bug Resolution tests
        // ========================

        [Test]
        public async Task BugResolution_ClosedWithinThreshold_CountsAsCompliant()
        {
            var issueCreated = DateTimeOffset.UtcNow.AddDays(-60);

            var issue = CreateIssue(1, "Fixed bug", issueCreated, ItemState.Closed,
                ["bug", "TestService"], closedAt: issueCreated.AddDays(30));

            _mockGitHub.Setup(s => s.ListIssuesForSLAAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<DateTimeOffset>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Issue> { issue }.AsReadOnly());

            var result = await _tool.GetSLAStatus("TestService", "test-repo", includeClosed: true);

            Assert.That(result.BugResolution.TotalTracked, Is.EqualTo(1));
            Assert.That(result.BugResolution.WithinSLA, Is.EqualTo(1));
            Assert.That(result.BugResolution.CompliancePercent, Is.EqualTo(100));
        }

        [Test]
        public async Task BugResolution_OpenBeyondThreshold_CountsAsBreached()
        {
            var issueCreated = DateTimeOffset.UtcNow.AddDays(-100);

            var issue = CreateIssue(1, "Old open bug", issueCreated, ItemState.Open,
                ["bug", "TestService"]);

            _mockGitHub.Setup(s => s.ListIssuesForSLAAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<DateTimeOffset>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Issue> { issue }.AsReadOnly());

            var result = await _tool.GetSLAStatus("TestService", "test-repo");

            Assert.That(result.BugResolution.TotalTracked, Is.EqualTo(1));
            Assert.That(result.BugResolution.Breached, Is.EqualTo(1));
            Assert.That(result.BreachedIssues, Is.Not.Null);
            Assert.That(result.BreachedIssues!.Any(i => i.SLAMetricType == "bug_resolution"), Is.True);
        }

        // ========================
        // Question Resolution tests
        // ========================

        [Test]
        public async Task QuestionResolution_OpenApproachingThreshold_FlaggedAsApproaching()
        {
            var issueCreated = DateTimeOffset.UtcNow.AddDays(-10);

            var issue = CreateIssue(1, "Approaching question", issueCreated, ItemState.Open,
                ["question", "TestService"]);

            _mockGitHub.Setup(s => s.ListIssuesForSLAAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<DateTimeOffset>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Issue> { issue }.AsReadOnly());

            var result = await _tool.GetSLAStatus("TestService", "test-repo");

            Assert.That(result.QuestionResolution.Approaching, Is.EqualTo(1));
            Assert.That(result.ApproachingBreaches, Is.Not.Null);
            Assert.That(result.ApproachingBreaches!.Any(i => i.SLAMetricType == "question_resolution"), Is.True);
        }

        // ========================
        // Issue-addressed exclusion
        // ========================

        [Test]
        public async Task IssueAddressed_OpenIssue_ExcludedFromTracking()
        {
            var issue = CreateIssue(1, "Addressed issue", DateTimeOffset.UtcNow.AddDays(-30),
                ItemState.Open, ["customer-reported", "issue-addressed", "TestService"]);

            _mockGitHub.Setup(s => s.ListIssuesForSLAAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<DateTimeOffset>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Issue> { issue }.AsReadOnly());

            var result = await _tool.GetSLAStatus("TestService", "test-repo");

            Assert.That(result.FirstQuestionResponse.TotalTracked, Is.EqualTo(0));
        }

        // ========================
        // Multi-category issue
        // ========================

        [Test]
        public async Task Issue_WithMultipleLabels_TrackedInMultipleBuckets()
        {
            var issueCreated = DateTimeOffset.UtcNow.AddDays(-5);

            var issue = CreateIssue(1, "Customer bug", issueCreated, ItemState.Open,
                ["customer-reported", "bug", "TestService"], "assignee1");

            _mockGitHub.Setup(s => s.ListIssuesForSLAAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<DateTimeOffset>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Issue> { issue }.AsReadOnly());

            _mockGitHub.Setup(s => s.GetIssueCommentsAsync(
                    It.IsAny<string>(), It.IsAny<string>(), 1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<IssueComment>
                {
                    CreateComment(issueCreated.AddDays(1), "team-dev", AuthorAssociation.Collaborator)
                }.AsReadOnly());

            var result = await _tool.GetSLAStatus("TestService", "test-repo");

            Assert.That(result.FirstQuestionResponse.TotalTracked, Is.EqualTo(1));
            Assert.That(result.BugResolution.TotalTracked, Is.EqualTo(1));
        }

        // ========================
        // Error handling
        // ========================

        [Test]
        public async Task GetSLAStatus_GitHubAPIFailure_ReturnsPartialResult()
        {
            _mockGitHub.Setup(s => s.ListIssuesForSLAAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<DateTimeOffset>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("API rate limit exceeded"));

            var result = await _tool.GetSLAStatus("TestService", "test-repo");

            // Per-repo errors are caught — returns partial result, not an exception
            Assert.That(result.Service, Is.EqualTo("TestService"));
            Assert.That(result.TotalOpenIssues, Is.EqualTo(0));
        }

        // ========================
        // Response formatting
        // ========================

        [Test]
        public async Task Format_ProducesReadableOutput()
        {
            var issueCreated = DateTimeOffset.UtcNow.AddDays(-100);

            var issue = CreateIssue(42, "Old bug title", issueCreated, ItemState.Open,
                ["bug", "TestService"], "owner1");

            _mockGitHub.Setup(s => s.ListIssuesForSLAAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<DateTimeOffset>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Issue> { issue }.AsReadOnly());

            var result = await _tool.GetSLAStatus("TestService", "test-repo");
            var output = result.ToString();

            Assert.That(output, Does.Contain("SLA Status: TestService"));
            Assert.That(output, Does.Contain("test-repo"));
            Assert.That(output, Does.Contain("Breached"));
            Assert.That(output, Does.Contain("#42"));
            Assert.That(output, Does.Contain("owner1"));
        }
    }

    // ========================
    // BusinessDayCalculator tests
    // ========================

    [TestFixture]
    public class BusinessDayCalculatorTests
    {
        [Test]
        public void CountBusinessDays_MondayToNextMonday_Returns5()
        {
            var monday = new DateTimeOffset(2026, 3, 23, 0, 0, 0, TimeSpan.Zero);
            var nextMonday = monday.AddDays(7);

            Assert.That(BusinessDayCalculator.CountBusinessDays(monday, nextMonday), Is.EqualTo(5));
        }

        [Test]
        public void CountBusinessDays_FridayToMonday_Returns1()
        {
            var friday = new DateTimeOffset(2026, 3, 27, 0, 0, 0, TimeSpan.Zero);
            var monday = friday.AddDays(3);

            Assert.That(BusinessDayCalculator.CountBusinessDays(friday, monday), Is.EqualTo(1));
        }

        [Test]
        public void CountBusinessDays_SameDay_ReturnsZero()
        {
            var date = new DateTimeOffset(2026, 3, 25, 10, 0, 0, TimeSpan.Zero);

            Assert.That(BusinessDayCalculator.CountBusinessDays(date, date), Is.EqualTo(0));
        }

        [Test]
        public void CountBusinessDays_EndBeforeStart_ReturnsZero()
        {
            var start = new DateTimeOffset(2026, 3, 25, 0, 0, 0, TimeSpan.Zero);

            Assert.That(BusinessDayCalculator.CountBusinessDays(start, start.AddDays(-5)), Is.EqualTo(0));
        }

        [Test]
        public void CountBusinessDays_WeekendOnly_ReturnsZero()
        {
            var saturday = new DateTimeOffset(2026, 3, 28, 0, 0, 0, TimeSpan.Zero);

            Assert.That(BusinessDayCalculator.CountBusinessDays(saturday, saturday.AddDays(2)), Is.EqualTo(0));
        }

        [Test]
        public void CountBusinessDays_TwoWeeks_Returns10()
        {
            var monday = new DateTimeOffset(2026, 3, 23, 0, 0, 0, TimeSpan.Zero);

            Assert.That(BusinessDayCalculator.CountBusinessDays(monday, monday.AddDays(14)), Is.EqualTo(10));
        }

        [Test]
        public void AddBusinessDays_FromMonday_Add3_ReturnsThursday()
        {
            var monday = new DateTimeOffset(2026, 3, 23, 0, 0, 0, TimeSpan.Zero);

            Assert.That(BusinessDayCalculator.AddBusinessDays(monday, 3).DayOfWeek, Is.EqualTo(DayOfWeek.Thursday));
        }

        [Test]
        public void AddBusinessDays_FromFriday_Add1_ReturnsMonday()
        {
            var friday = new DateTimeOffset(2026, 3, 27, 0, 0, 0, TimeSpan.Zero);

            Assert.That(BusinessDayCalculator.AddBusinessDays(friday, 1).DayOfWeek, Is.EqualTo(DayOfWeek.Monday));
        }
    }
}
#endif
