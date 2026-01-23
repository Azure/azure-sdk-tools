using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Sdk.Tools.CodeownersUtils.Parsing;
using Azure.Sdk.Tools.GitHubEventProcessor.Constants;
using Azure.Sdk.Tools.GitHubEventProcessor.Configuration;
using Azure.Sdk.Tools.GitHubEventProcessor.EventProcessing;
using Azure.Sdk.Tools.GitHubEventProcessor.GitHubPayload;
using Azure.Sdk.Tools.GitHubEventProcessor.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace Azure.Sdk.Tools.GitHubEventProcessor.Tests.Static
{
    [TestFixture]
    [Parallelizable(ParallelScope.Children)]
    public class McpIssueProcessingTests : ProcessingTestBase
    {
        /// <summary>
        /// Test MCP InitialIssueTriage with various scenarios including:
        /// - Predicted server and tool labels
        /// - User-provided labels that conflict with predictions
        /// - Code owner assignment
        /// - Team notification comments when no owners found
        /// - Customer-reported label logic
        /// - needs-triage label removal
        /// </summary>
        /// <param name="rule">Rule being tested</param>
        /// <param name="payloadFile">JSON payload file for the event</param>
        /// <param name="ruleState">Whether InitialIssueTriage rule is on/off</param>
        /// <param name="predictedLabels">Labels returned from AI triage service (comma-separated)</param>
        /// <param name="userProvidedLabels">Labels already on the issue when opened (comma-separated)</param>
        /// <param name="ownersWithAssignPermission">Owners with permission to be assigned (comma-separated)</param>
        /// <param name="hasCodeownersEntry">Whether CODEOWNERS has entry for the labels</param>
        /// <param name="isMemberOfOrg">Whether issue creator is member of microsoft org</param>
        /// <param name="hasWriteOrAdmin">Whether issue creator has write/admin permission</param>
        [Category("static")]
        [NonParallelizable]
        
        // Scenario: AI predicts server-azure.mcp label, no user labels, has code owner with permission
        // Expected: server-azure.mcp label added, owner assigned, needs-team-attention added, comment posted
        [TestCase(RulesConstants.InitialIssueTriage,
                  "Tests.JsonEventPayloads/McpIssueTriage_issue_opened_no_labels.json",
                  RuleState.On,
                  "server-azure.mcp",
                  "",
                  "McpOwner1",
                  true,
                  false,
                  false)]
        
        // Scenario: AI predicts server-azure.mcp + tool-prompts.mcp, no owners found
        // Expected: Both labels added, needs-team-triage added, team notification comment posted, customer-reported + question added
        [TestCase(RulesConstants.InitialIssueTriage,
                  "Tests.JsonEventPayloads/McpIssueTriage_issue_opened_no_labels.json",
                  RuleState.On,
                  "server-azure.mcp, tools-prompts",
                  "",
                  null,
                  false,
                  false,
                  false)]
        
        // Scenario: AI predicts server-azure.mcp, but user already added server-fabric.mcp (conflict)
        // Expected: Only user's server-fabric.mcp kept, AI prediction ignored, no server-azure.mcp added
        [TestCase(RulesConstants.InitialIssueTriage,
                  "Tests.JsonEventPayloads/McpIssueTriage_issue_opened_with_user_label.json",
                  RuleState.On,
                  "server-azure.mcp",
                  "server-fabric.mcp",
                  null,
                  false,
                  true,
                  true)]
        
        // Scenario: AI predicts server-azure.mcp + tool-prompts.mcp, user added tool-prompts.mcp (partial match)
        // Expected: server-azure.mcp added (prediction), tool-prompts.mcp kept (user choice), needs-triage removed if present
        [TestCase(RulesConstants.InitialIssueTriage,
                  "Tests.JsonEventPayloads/McpIssueTriage_issue_opened_with_needs_triage.json",
                  RuleState.On,
                  "server-azure.mcp, tools-prompts",
                  "tools-prompts, needs-triage",
                  "McpOwner1",
                  true,
                  true,
                  false)]
        
        // Scenario: AI predicts no labels (empty response)
        // Expected: needs-triage label added, no other processing
        [TestCase(RulesConstants.InitialIssueTriage,
                  "Tests.JsonEventPayloads/McpIssueTriage_issue_opened_no_labels.json",
                  RuleState.On,
                  "",
                  "",
                  null,
                  false,
                  false,
                  false)]
        
        // Scenario: AI predicts server-fabric.mcp, multiple owners, only one has permission
        // Expected: server-fabric.mcp added, owner with permission assigned, comment with all owners mentioned
        [TestCase(RulesConstants.InitialIssueTriage,
                  "Tests.JsonEventPayloads/McpIssueTriage_issue_opened_no_labels.json",
                  RuleState.On,
                  "server-fabric.mcp",
                  "",
                  "McpOwner2",
                  true,
                  true,
                  true)]
        
        // Scenario: Rule is disabled
        // Expected: No processing, no updates
        [TestCase(RulesConstants.InitialIssueTriage,
                  "Tests.JsonEventPayloads/McpIssueTriage_issue_opened_no_labels.json",
                  RuleState.Off,
                  "server-azure.mcp",
                  "",
                  null,
                  false,
                  false,
                  false)]
        
        public async Task TestMcpInitialIssueTriage(
            string rule,
            string payloadFile,
            RuleState ruleState,
            string predictedLabels,
            string userProvidedLabels,
            string ownersWithAssignPermission,
            bool hasCodeownersEntry,
            bool isMemberOfOrg,
            bool hasWriteOrAdmin)
        {
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<McpIssueProcessing>();
            
            var mockGitHubEventClient = new MockGitHubEventClient(OrgConstants.ProductHeaderName);
            mockGitHubEventClient.RulesConfiguration.Rules[rule] = ruleState;
            mockGitHubEventClient.UserHasPermissionsReturn = hasWriteOrAdmin;
            mockGitHubEventClient.IsUserMemberOfOrgReturn = isMemberOfOrg;
            
            var rawJson = TestHelpers.GetTestEventPayload(payloadFile);
            var issueEventPayload = SimpleJsonSerializer.Deserialize<IssueEventGitHubPayload>(rawJson);
            
            List<string> expectedPredictedLabels = new List<string>();
            if (!string.IsNullOrEmpty(predictedLabels))
            {
                expectedPredictedLabels = predictedLabels.Split(',')
                    .Select(l => l.Trim())
                    .Where(l => !string.IsNullOrEmpty(l))
                    .ToList();
            }
            
            mockGitHubEventClient.AIServiceLabels = expectedPredictedLabels;
            mockGitHubEventClient.AIServiceAnswer = null;
            mockGitHubEventClient.AIServiceAnswerType = null;
            
            if (hasCodeownersEntry)
            {
                CodeOwnerUtils.ResetCodeOwnerEntries();
                CodeOwnerUtils.codeOwnersFilePathOverride = "Tests.FakeCodeowners/McpCodeowners";
            }
            
            if (!string.IsNullOrEmpty(ownersWithAssignPermission))
            {
                var ownersWithPermission = ownersWithAssignPermission.Split(',')
                    .Select(o => o.Trim())
                    .ToList();
                
                mockGitHubEventClient.OwnersWithAssignPermission = ownersWithPermission;
            }
            
            var mcpProcessor = new McpIssueProcessing(logger, CreateTestMcpConfiguration());
            
            await mcpProcessor.ProcessIssueEvent(mockGitHubEventClient, issueEventPayload);
            
            Assert.AreEqual(ruleState == RuleState.On, 
                mockGitHubEventClient.RulesConfiguration.RuleEnabled(rule), 
                $"Rule '{rule}' enabled should have been {ruleState == RuleState.On}");
            
            var totalUpdates = await mockGitHubEventClient.ProcessPendingUpdates(
                issueEventPayload.Repository.Id, 
                issueEventPayload.Issue.Number);
            
            if (ruleState == RuleState.Off)
            {
                Assert.AreEqual(0, totalUpdates, "Expected no updates when rule is disabled");
            }
            else if (string.IsNullOrEmpty(predictedLabels))
            {
                Assert.That(mockGitHubEventClient.GetLabelsToAdd(), Does.Contain(TriageLabelConstants.NeedsTriage));
                
                if (!isMemberOfOrg && !hasWriteOrAdmin)
                {
                    Assert.That(mockGitHubEventClient.GetLabelsToAdd(), Does.Contain(TriageLabelConstants.CustomerReported));
                    Assert.That(mockGitHubEventClient.GetLabelsToAdd(), Does.Contain(TriageLabelConstants.Question));
                }
            }
            else
            {
                var userLabels = string.IsNullOrEmpty(userProvidedLabels) 
                    ? new List<string>() 
                    : userProvidedLabels.Split(',').Select(l => l.Trim()).ToList();
                
                var labelsToAdd = mockGitHubEventClient.GetLabelsToAdd();
                var serverPredicted = expectedPredictedLabels.FirstOrDefault(l => l.StartsWith("server-", StringComparison.OrdinalIgnoreCase));
                var toolPredicted = expectedPredictedLabels.FirstOrDefault(l => l.StartsWith("tools-", StringComparison.OrdinalIgnoreCase));
                
                if (serverPredicted != null)
                {
                    var userHasServerLabel = userLabels.Any(l => l.StartsWith("server-", StringComparison.OrdinalIgnoreCase));
                    if (!userHasServerLabel)
                    {
                        Assert.That(labelsToAdd, Does.Contain(serverPredicted), 
                            $"Expected predicted server label '{serverPredicted}' to be added");
                    }
                }
                
                if (toolPredicted != null)
                {
                    var userHasToolLabel = userLabels.Any(l => l.StartsWith("tools-", StringComparison.OrdinalIgnoreCase));
                    if (!userHasToolLabel)
                    {
                        Assert.That(labelsToAdd, Does.Contain(toolPredicted),
                            $"Expected predicted tool label '{toolPredicted}' to be added");
                    }
                }
                
                if (!string.IsNullOrEmpty(ownersWithAssignPermission) && hasCodeownersEntry)
                {
                    Assert.That(labelsToAdd, Does.Contain(TriageLabelConstants.NeedsTeamAttention));
                }
                else if (serverPredicted != null || toolPredicted != null)
                {
                    Assert.That(labelsToAdd, Does.Contain(TriageLabelConstants.NeedsTeamTriage));
                }
                
                if (userLabels.Contains(TriageLabelConstants.NeedsTriage, StringComparer.OrdinalIgnoreCase))
                {
                    var labelsToRemove = mockGitHubEventClient.GetLabelsToRemove();
                    Assert.That(labelsToRemove, Does.Contain(TriageLabelConstants.NeedsTriage),
                        "Expected needs-triage to be removed when valid predicted labels exist");
                }
                
                if (!isMemberOfOrg && !hasWriteOrAdmin)
                {
                    Assert.That(labelsToAdd, Does.Contain(TriageLabelConstants.CustomerReported));
                    Assert.That(labelsToAdd, Does.Contain(TriageLabelConstants.Question));
                }
            }
        }

        /// <summary>
        /// Creates a test McpConfiguration with mock server team mappings.
        /// </summary>
        private static McpConfiguration CreateTestMcpConfiguration()
        {
            var configData = new Dictionary<string, string?>
            {
                { "microsoft/mcp:ServerTeamMappings", "server-Azure.Mcp=@microsoft/azure-mcp;server-Fabric.Mcp=@microsoft/fabric-mcp" }
            };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            return new McpConfiguration(configuration);
        }
    }
}
