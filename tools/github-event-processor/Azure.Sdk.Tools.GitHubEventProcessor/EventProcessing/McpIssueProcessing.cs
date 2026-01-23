#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Sdk.Tools.CodeownersUtils.Parsing;
using Azure.Sdk.Tools.GitHubEventProcessor.Constants;
using Azure.Sdk.Tools.GitHubEventProcessor.Configuration;
using Azure.Sdk.Tools.GitHubEventProcessor.GitHubPayload;
using Azure.Sdk.Tools.GitHubEventProcessor.Utils;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.GitHubEventProcessor.EventProcessing
{
    public class McpIssueProcessing : IssueProcessing
    {
        private readonly ILogger<McpIssueProcessing> _logger;
        private readonly McpConfiguration _mcpConfiguration;

        public McpIssueProcessing(ILogger<McpIssueProcessing> logger, McpConfiguration mcpConfiguration)
        {
            _logger = logger;
            _mcpConfiguration = mcpConfiguration;
        }
        
        public override async Task InitialIssueTriage(GitHubEventClient gitHubEventClient, IssueEventGitHubPayload issueEventPayload)
        {

            _logger.LogInformation("Starting MCP issue triage for issue #{IssueNumber}", issueEventPayload.Issue.Number);

            if (!gitHubEventClient.RulesConfiguration.RuleEnabled(RulesConstants.InitialIssueTriage))
            {
                _logger.LogDebug("InitialIssueTriage rule disabled");
                return;
            }

            if (issueEventPayload.Action != ActionConstants.Opened)
            {
                _logger.LogDebug("Issue action {Action} is not 'opened'", issueEventPayload.Action);
                return;
            }

            bool isCustomerReported = await GetCustomerReportedLabel(gitHubEventClient, issueEventPayload);

            var userLabels = issueEventPayload.Issue.Labels
                .Select(label => label.Name)
                .ToList();

            IssueTriageResponse triageOutput = await gitHubEventClient.QueryAIIssueTriageService(
                issueEventPayload, 
                true, 
                false); 

            var usePredictedLabels = EvaluatePredictedLabelsForMcp( userLabels, triageOutput.Labels);
            var finalLabels = new HashSet<string>();

            if (usePredictedLabels.UsePredictedServer && usePredictedLabels.PredictedServerLabel != null)
            {
                gitHubEventClient.AddLabel(usePredictedLabels.PredictedServerLabel);
                finalLabels.Add(usePredictedLabels.PredictedServerLabel);
            }

            if (usePredictedLabels.UsePredictedTool && usePredictedLabels.PredictedToolLabel != null)
            {
                gitHubEventClient.AddLabel(usePredictedLabels.PredictedToolLabel);
                finalLabels.Add(usePredictedLabels.PredictedToolLabel);
            }

            // Check if user already has any valid MCP labels (server or tool)
            bool userHasValidMcpLabel = userLabels.Any(IsServerLabel) || userLabels.Any(IsToolLabel);

            if (!usePredictedLabels.UsePredictedServer && !usePredictedLabels.UsePredictedTool && !userHasValidMcpLabel)
            {
                gitHubEventClient.AddLabel(TriageLabelConstants.NeedsTriage); 
                return;
            }

            if ((usePredictedLabels.UsePredictedServer || usePredictedLabels.UsePredictedTool) &&
                userLabels.Contains(TriageLabelConstants.NeedsTriage, StringComparer.OrdinalIgnoreCase))
            {
                gitHubEventClient.RemoveLabel(TriageLabelConstants.NeedsTriage);
            }

            if (userLabels != null)
            {
                foreach (var label in userLabels) 
                {
                    finalLabels.Add(label);
                }
            }

            bool hasValidAssignee = await AssignCodeOwnerAsync(
                gitHubEventClient, 
                issueEventPayload, 
                finalLabels.ToList());

            if (!hasValidAssignee) 
            {
                gitHubEventClient.AddLabel(TriageLabelConstants.NeedsTeamTriage);

                if (usePredictedLabels.UsePredictedServer && 
                    usePredictedLabels.PredictedServerLabel != null)
                {
                    CreateServerTeamComment(gitHubEventClient, issueEventPayload, usePredictedLabels.PredictedServerLabel, triageOutput);
                }
            }
            else
            {
                gitHubEventClient.AddLabel(TriageLabelConstants.NeedsTeamAttention);
            }           
        }

        private async Task<bool> AssignCodeOwnerAsync(
            GitHubEventClient gitHubEventClient,
            IssueEventGitHubPayload issueEventPayload,
            List<string> finalLabels)
        {
            bool hasValidAssignee = false;

            if (issueEventPayload.Issue.Assignees != null && issueEventPayload.Issue.Assignees.Count > 0)
            {
                hasValidAssignee = true;
                var existingAssignees = issueEventPayload.Issue.Assignees.Select(a => $"@{a.Login}");
                string assigneesMention = string.Join(", ", existingAssignees);
                string issueComment = $"Thanks for your feedback! {assigneesMention} {(issueEventPayload.Issue.Assignees.Count == 1 ? "is" : "are")} looking into it.";
                gitHubEventClient.CreateComment(issueEventPayload.Repository.Id, issueEventPayload.Issue.Number, issueComment);
                _logger.LogInformation("Issue #{IssueNumber} already has assignee(s): {Assignees}", 
                    issueEventPayload.Issue.Number, string.Join(", ", issueEventPayload.Issue.Assignees.Select(a => a.Login)));
                return hasValidAssignee;
            }

            CodeownersEntry codeOwnersEntry = CodeOwnerUtils.GetCodeownersEntryForLabelList(finalLabels);
            
            if (codeOwnersEntry.ServiceOwners.Count > 0)
            {
                if (codeOwnersEntry.ServiceOwners.Count == 1)
                {
                    if (await gitHubEventClient.OwnerCanBeAssignedToIssuesInRepo(
                        issueEventPayload.Repository.Owner.Login, 
                        issueEventPayload.Repository.Name,
                        codeOwnersEntry.ServiceOwners[0]))
                    {
                        hasValidAssignee = true;
                        gitHubEventClient.AssignOwnerToIssue(
                            issueEventPayload.Repository.Owner.Login, 
                            issueEventPayload.Repository.Name, 
                            codeOwnersEntry.ServiceOwners[0]);
                        CreateComment(codeOwnersEntry, gitHubEventClient, issueEventPayload);

                    }
                    else
                    {
                        _logger.LogWarning("{Owner} is the only owner in the ServiceOwners for service label(s) {Labels}, but cannot be assigned as an issue owner in this repository", codeOwnersEntry.ServiceOwners[0], string.Join(",", finalLabels));
                    } 
                }

                else
                {
                    var rnd = new Random();
                    var randomMcpOwners = codeOwnersEntry.ServiceOwners.OrderBy(item => rnd.Next(0, codeOwnersEntry.ServiceOwners.Count));
                    foreach (string mcpOwner in randomMcpOwners)
                    {
                        if (await gitHubEventClient.OwnerCanBeAssignedToIssuesInRepo(
                        issueEventPayload.Repository.Owner.Login,
                        issueEventPayload.Repository.Name,
                        mcpOwner))
                        {
                            hasValidAssignee = true;
                            gitHubEventClient.AssignOwnerToIssue(issueEventPayload.Repository.Owner.Login,
                            issueEventPayload.Repository.Name, mcpOwner);
                            CreateComment(codeOwnersEntry, gitHubEventClient, issueEventPayload);
        
                            break;
                        }
                        else
                        {
                            _logger.LogWarning("{Owner} is an MCP owner for service labels {Labels} but cannot be assigned as an issue owner in this repository", 
                                mcpOwner, string.Join(",", finalLabels));
                        }
                    }
                }
            }

            return hasValidAssignee;
        }

        private void CreateComment(CodeownersEntry codeOwnersEntry, GitHubEventClient gitHubEventClient, IssueEventGitHubPayload issueEventPayload)
        {
            string mcpOwnersAtMention = CodeOwnerUtils.CreateAtMentionForOwnerList(codeOwnersEntry.ServiceOwners);
            string issueComment = $"Thanks for the feedback! We are routing this to the appropriate team for follow-up. cc {mcpOwnersAtMention}.";
            gitHubEventClient.CreateComment(issueEventPayload.Repository.Id, issueEventPayload.Issue.Number, issueComment);
        }

        private void CreateServerTeamComment(
            GitHubEventClient gitHubEventClient, 
            IssueEventGitHubPayload issueEventPayload, 
            string serverLabel,
            IssueTriageResponse triageOutput)
        {
            string? teamMention = GetTeamMentionForServerLabel(serverLabel);
            
            string issueComment = teamMention != null 
                ? $"Thanks for the feedback! We are routing this to the appropriate team for follow-up. cc {teamMention}." 
                : "Thanks for the feedback! We are routing this to the appropriate team for follow-up.";
            
            gitHubEventClient.CreateComment(issueEventPayload.Repository.Id, issueEventPayload.Issue.Number, issueComment);
        }

        private string? GetTeamMentionForServerLabel(string serverLabel)
        {
            return _mcpConfiguration.GetTeamMentionForServerLabel(serverLabel);
        }

        private static McpPredictedLabelDecision EvaluatePredictedLabelsForMcp(IEnumerable<string> userLabels, IEnumerable<string> predictedLabels)
        {
            if(predictedLabels == null || !predictedLabels.Any())
            {
                return new McpPredictedLabelDecision(false, false, null, null);
            }

            userLabels ??= Enumerable.Empty<string>();

            var userServerLabel = userLabels.Where(IsServerLabel).ToList();
            var userToolLabels = userLabels.Where(IsToolLabel).ToList();

            string? predictedServerLabel = predictedLabels.FirstOrDefault(IsServerLabel);
            string? predictedToolLabel   = predictedLabels.FirstOrDefault(IsToolLabel);

            bool isPredictedServerLabel = predictedServerLabel != null && 
            (!userServerLabel.Any() || 
            userServerLabel.Any(label => label.Equals(predictedServerLabel, StringComparison.OrdinalIgnoreCase)));

            bool isPredictedToolLabel = predictedToolLabel != null && 
            (!userToolLabels.Any() || 
            userToolLabels.Any(label => label.Equals(predictedToolLabel, StringComparison.OrdinalIgnoreCase)));

            return new McpPredictedLabelDecision
            (isPredictedServerLabel, isPredictedToolLabel, predictedServerLabel, predictedToolLabel);

        }

        private static bool IsServerLabel(string label)
        {
            return label.StartsWith(ConfigConstants.McpServerLabelPrefix, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsToolLabel(string label)
        {
            return label.StartsWith(ConfigConstants.McpToolLabelPrefix, StringComparison.OrdinalIgnoreCase) ||
            ConfigConstants.McpOtherLabels.Any(otherLabel => label.Equals(otherLabel, StringComparison.OrdinalIgnoreCase));
        }

        private async Task<bool> GetCustomerReportedLabel(GitHubEventClient gitHubEventClient, IssueEventGitHubPayload issueEventPayload)
        {
            string login = issueEventPayload.Issue.User.Login;
            bool isMemberOfOrg = await gitHubEventClient.IsUserMemberOfOrg(OrgConstants.Azure, login);
            
            if (isMemberOfOrg) return false;
        
            var hasAdminOrWritePermission = await gitHubEventClient.DoesUserHaveAdminOrWritePermission(
                issueEventPayload.Repository.Id, login);

            if (hasAdminOrWritePermission) return false;

            gitHubEventClient.AddLabel(TriageLabelConstants.CustomerReported);
            gitHubEventClient.AddLabel(TriageLabelConstants.Question);
            return true;
        }

        private sealed record McpPredictedLabelDecision(
            bool UsePredictedServer,
            bool UsePredictedTool,
            string? PredictedServerLabel = null,
            string? PredictedToolLabel = null
        );
    }
}