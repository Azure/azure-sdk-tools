using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Sdk.Tools.CodeownersUtils.Parsing;
using Azure.Sdk.Tools.GitHubEventProcessor.Constants;
using Azure.Sdk.Tools.GitHubEventProcessor.EventProcessing;
using Azure.Sdk.Tools.GitHubEventProcessor.GitHubPayload;
using Azure.Sdk.Tools.GitHubEventProcessor.Utils;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace Azure.Sdk.Tools.GitHubEventProcessor.Tests.Static
{
    [TestFixture]
    [Parallelizable(ParallelScope.Children)]
    public class IssueProcessingTests : ProcessingTestBase
    {
        // With the updated Initial Triage rule I need to be able to set the following information in order to test
        // all of the code paths.
        // 1. mockGitHubEventClient.UserHasPermissionsReturn - whether or not the user has write or admin permissions
        // 2. mockGitHubEventClient.IsUserMemberOfOrgReturn - whether or not the user is a member of the Azure org
        // 3. mockGitHubEventClient.OwnerCanBeAssignedToIssueInRepoReturn - whether or not a given owner can be assigned to an issue
        // 4. AIReturnLabels - change to string, if null no labels returned otherwise split string into a list
        // 5. RuleState ruleState - Whether or not the InitialIssueAssignment rule is on or off
        // 6. RuleState ruleStateServiceAttention - Whether or not the ServiceAttention rule is on or off
        //
        // CODEOWNERS changes necessary to test
        // The CODEOWNERS file needs entries that match the AIReturnLabels, in theory the AI Issue Triage Service will return one
        // ServiceLabel and one other label. If the rule is going to process the Service Label returned needs to match one
        // entry in the CODEOWNERS file. The AzureSdkOwners for that CODEOWNERS entry will need to have
        // OwnerCanBeAssignedToIssueInRepoReturn set. Some entries will need AzureSdkOwners with and without assignment permission
        // and other entries will be without AzureSdkOwners entirely.
        //
        // Labels being added (this entire thing only matters if there are no assinees, no labels and the AI Issue Triage Service returns
        // labels)
        // Suggestions and comments being added, if provided by the AI Issue Triage Service.
        // If there are no valid AzureSdkOwners (meaning there are none or none of the AzureSdkOwners can be assigned to an issue)
        // AND there are ServiceOwners for the ServiceLabel
        // AND the ServiceAttention rule is enabled
        // add ServiceAttention label, process ServiceAttention rule and add NeedsTeamAttention label
        // ELSE
        // add NeedsTeamTriage label
        //
        // If the creator of the Issue is not a member of the Azure org and does not have write or admin permissions
        // add CustomerReported and Question labels
        //
        // The ServiceAttention just adds a comment @ mentioning everyone on the ServiceOwners list from CODEOWNERS that
        // matches the ServiceLabel entry returned from the AI Issue Triage Service.
        // 

        /// <summary>
        /// Note: FakeUser1 is the owner that submitted issue in InitialIssueTriage_issue_opened_no_labels_no_assignee.json
        /// </summary>
        /// <param name="rule">String, RulesConstants for the rule being tested</param>
        /// <param name="payloadFile">JSon payload file for the event being tested</param>
        /// <param name="ruleState">Whether or not the InitialIssueTriage rule is on/off</param>
        /// <param name="serviceAttentionRuleState">Whether or not the ServiceAttention rule is on/off</param>
        /// <param name="AIServiceReturnsLabels">The label(s) for the AI Issue Triage Service to "return"or null if none</param>
        /// <param name="AIServiceAnswer">The answer provided by the AI Issue Triage Service or null if none</param>
        /// <param name="AIServiceAnswerType">The answer type provided by the AI Issue Triage Service or null if none</param>
        /// <param name="ownersWithAssignPermission">The owners, from the appropriate CODEOWNERS entry, with assign permission or null if none</param>
        /// <param name="hasCodeownersEntry">Whether or not to expect a codeowners entry for the labels returned.</param>
        /// <param name="isMemberOfOrg">Whether or not the owner that created the issue is a member of Azure</param>
        /// <param name="hasWriteOrAdmin">Whether or not the owner that created the issue has write or admin</param>
        [Category("static")]
        [NonParallelizable] // All the tests use the same CODEOWNERS file
        // Scenario: Everything turned off, nothing should process
        // Expected: There should be no updates
        [TestCase(RulesConstants.InitialIssueTriage,
                  "Tests.JsonEventPayloads/InitialIssueTriage_issue_opened_no_labels_no_assignee.json",
                  RuleState.Off,
                  RuleState.Off,
                  null, // labels returned from the AI Issue Triage Service,
                  null, // answer returned from the AI Issue Triage Service
                  null, // answer type returned from the AI Issue Triage Service
                  null, // owners with permission to be assigned to issues
                  false,
                  false,
                  false)]
        // Scenario: No labels returned from AI Issue Triage Service
        //           isMemberOfOrg and hasWriteOrAdmin both set to false.
        // Expected: NeedsTriage, CustomerReported and Question labels added to the Issue
        [TestCase(RulesConstants.InitialIssueTriage,
                  "Tests.JsonEventPayloads/InitialIssueTriage_issue_opened_no_labels_no_assignee.json",
                  RuleState.On,
                  RuleState.On,
                  null, // labels returned from the AI Issue Triage Service
                  null, // answer returned from the AI Issue Triage Service
                  null, // answer type returned from the AI Issue Triage Service
                  null, // owners with permission to be assigned to issues
                  false,
                  false,
                  false)]
        // Scenario: Labels returned from AI Issue Triage Service
        //           isMemberOfOrg and hasWriteOrAdmin both set to false.
        // Expected: CustomerReported, Question, and only predicted labels added to the Issue
        [TestCase(RulesConstants.InitialIssueTriage,
                  "Tests.JsonEventPayloads/InitialIssueTriage_issue_opened_no_labels_no_assignee.json",
                  RuleState.On,
                  RuleState.On,
                  "FakeLabel666", // labels returned from the AI Issue Triage Service
                  null, // answer returned from the AI Issue Triage Service
                  null, // answer type returned from the AI Issue Triage Service
                  null, // owners with permission to be assigned to issues
                  false,
                  false,
                  false)]
        // Scenario: No labels returned from AI Issue Triage Service
        //           isMemberOfOrg is true and hasWriteOrAdmin is false
        // Expected: Only NeedsTriage label added to the Issue
        [TestCase(RulesConstants.InitialIssueTriage,
                  "Tests.JsonEventPayloads/InitialIssueTriage_issue_opened_no_labels_no_assignee.json",
                  RuleState.On,
                  RuleState.On,
                  null, // labels returned from the AI Issue Triage Service
                  null, // answer returned from the AI Issue Triage Service
                  null, // answer type returned from the AI Issue Triage service
                  null, // owners with permission to be assigned to issues
                  false,
                  true,
                  false)]
        // Scenario: No labels returned from AI Issue Triage Service
        //           isMemberOfOrg is false and hasWriteOrAdmin is true
        // Expected: Only NeedsTriage label added to the Issue
        [TestCase(RulesConstants.InitialIssueTriage,
                  "Tests.JsonEventPayloads/InitialIssueTriage_issue_opened_no_labels_no_assignee.json",
                  RuleState.On,
                  RuleState.On,
                  null, // labels returned from the AI Issue Triage Service
                  null, // answer returned from the AI Issue Triage Service
                  null, // answer type returned from the AI Issue Triage Service
                  null, // owners with permission to be assigned to issues
                  false,
                  false,
                  true)]
        /* From here on out, the creator will have isMemberOfOrg and hasWriteOrAdmin set to true. Those scenarios were already tested */
        // Scenario: The AI Issue Triage Service doesn't have a matching CODEOWNERS entry
        // Expected: The label is added to the issue along with NeedsTeamTriage
        [TestCase(RulesConstants.InitialIssueTriage,
                  "Tests.JsonEventPayloads/InitialIssueTriage_issue_opened_no_labels_no_assignee.json",
                  RuleState.On,
                  RuleState.On,
                  "FakeLabel666", // labels returned from the AI Issue Triage Service
                  null, // answer returned from the AI Issue Triage Service
                  null, // answer type returned from the AI Issue Triage Service
                  null, // owners with permission to be assigned to issues
                  false, // Has CODEOWNERS entry
                  true,
                  true)]
        // Scenario: The AI Issue Triage Service returned label has a matching CODEOWNERS entry with no AzureSdkOwners.
        //           ServiceAttention rule is Off.
        // Expected: The label is added to the issue along with NeedsTeamTriage
        [TestCase(RulesConstants.InitialIssueTriage,
                  "Tests.JsonEventPayloads/InitialIssueTriage_issue_opened_no_labels_no_assignee.json",
                  RuleState.On,
                  RuleState.Off,
                  "FakeLabel1", // labels returned from the AI Issue Triage Service
                  null, // answer returned from the AI Issue Triage Service
                  null, // answer type returned from the AI Issue Triage Service
                  null, // owners with permission to be assigned to issues
                  true, // Has CODEOWNERS entry
                  true,
                  true)]
        // Scenario: TThe AI Issue Triage Service returned label has a matching CODEOWNERS entry with a single AzureSdkOwner.
        //           ServiceAttention rule is Off.
        //           The AzureSdkOwner does not have issue assignment permissions.
        // Expected: The label is added to the issue along with NeedsTeamTriage
        [TestCase(RulesConstants.InitialIssueTriage,
                  "Tests.JsonEventPayloads/InitialIssueTriage_issue_opened_no_labels_no_assignee.json",
                  RuleState.On,
                  RuleState.Off,
                  "FakeLabel2", // labels returned from the AI Issue Triage Service
                  null, // answer returned from the AI Issue Triage Service
                  null, // answer type returned from the AI Issue Triage Service
                  null, // owners with permission to be assigned to issues
                  true, // Has CODEOWNERS entry
                  true,
                  true)]
        // Scenario: The AI Issue Triage Service returned label has a matching CODEOWNERS entry with a single AzureSdkOwner.
        //           ServiceAttention rule is On.
        //           The AzureSdkOwner does not have issue assignment permissions.
        // Expected: The label is added to the issue.
        //           ServiceAttention is added to the issue and the ServiceAttention rule executed.
        //           A comment is created via running the ServiceAttention rule.
        [TestCase(RulesConstants.InitialIssueTriage,
                  "Tests.JsonEventPayloads/InitialIssueTriage_issue_opened_no_labels_no_assignee.json",
                  RuleState.On,
                  RuleState.On,
                  "FakeLabel2", // labels returned from the AI Issue Triage Service
                  null, // answer returned from the AI Issue Triage Service
                  null, // answer type returned from the AI Issue Triage Service
                  null, // owners with permission to be assigned to issues
                  true, // Has CODEOWNERS entry
                  true,
                  true)]
        // Scenario: The AI Issue Triage Service returned label has a matching CODEOWNERS entry with a single AzureSdkOwner.
        //           ServiceAttention rule is On (doesn't matter when there's an AzureSdkOwner valid for assignment).
        //           The AzureSdkOwner has issue assignment permissions.
        // Expected: The label is added to the issue.
        //           The AzureSdkOwner, FakeUser1, is assigned to the issue
        //           NeedsTeamAttention is added to the issue since there is a valid owner
        //           A comment is added thanking the creator, tagging and routing to the team
        [TestCase(RulesConstants.InitialIssueTriage,
                  "Tests.JsonEventPayloads/InitialIssueTriage_issue_opened_no_labels_no_assignee.json",
                  RuleState.On,
                  RuleState.On,
                  "FakeLabel2", // labels returned from the AI Issue Triage Service
                  null, // answer returned from the AI Issue Triage Service
                  null, // answer type returned from the AI Issue Triage Service
                  "FakeUser1", // owners with permission to be assigned to issues
                  true, // Has CODEOWNERS entry
                  true,
                  true)]
        // Scenario: The AI Issue Triage Service returned label has a matching CODEOWNERS entry with a single AzureSdkOwner.
        //           ServiceAttention rule is On (doesn't matter when there's an AzureSdkOwner valid for assignment).
        //           The AzureSdkOwner has issue assignment permissions. Suggestion is provided by the service.
        // Expected: The label is added to the issue.
        //           The AzureSdkOwner, FakeUser1, is assigned to the issue
        //           NeedsTeamAttention is added to the issue since there is a valid owner
        //           A comment is added thanking the creator, tagging and routing to the team
        //           A second comment is made adding the suggestion provided.
        [TestCase(RulesConstants.InitialIssueTriage,
                  "Tests.JsonEventPayloads/InitialIssueTriage_issue_opened_no_labels_no_assignee.json",
                  RuleState.On,
                  RuleState.On,
                  "FakeLabel2", // labels returned from the AI Issue Triage Service
                  "FakeAnswer", // answer returned from the AI Issue Triage Service
                  "suggestion", // answer type returned from the AI Issue Triage Service
                  "FakeUser1", // owners with permission to be assigned to issues
                  true, // Has CODEOWNERS entry
                  true,
                  true)]
        // Scenario: The AI Issue Triage Service returned label has a matching CODEOWNERS entry with a single AzureSdkOwner.
        //           ServiceAttention rule is On (doesn't matter when there's an AzureSdkOwner valid for assignment).
        //           The AzureSdkOwner has issue assignment permissions. Suggestion is provided by the service.
        // Expected: The label is added to the issue.
        //           The AzureSdkOwner, FakeUser1, is assigned to the issue
        //           NeedsTeamAttention is added to the issue since there is a valid owner
        //           A comment is added thanking the creator, tagging and routing to the team
        //           A second comment is made adding the solution provided.
        //           issue-addressed label added to the issue.
        [TestCase(RulesConstants.InitialIssueTriage,
                  "Tests.JsonEventPayloads/InitialIssueTriage_issue_opened_no_labels_no_assignee.json",
                  RuleState.On,
                  RuleState.On,
                  "FakeLabel2", // labels returned from the AI Issue Triage Service
                  "FakeAnswer", // answer returned from the AI Issue Triage Service
                  "solution", // answer type returned from the AI Issue Triage Service
                  "FakeUser1", // owners with permission to be assigned to issues
                  true, // Has CODEOWNERS entry
                  true,
                  true)]
        // Scenario: The AI Issue Triage Service returned label has a matching CODEOWNERS entry with a single AzureSdkOwner.
        //           ServiceAttention rule is On (doesn't matter when there's an AzureSdkOwner valid for assignment).
        //           The AzureSdkOwner has issue assignment permissions. Answer type is empty.
        // Expected: The label is added to the issue.
        //           The AzureSdkOwner, FakeUser1, is assigned to the issue
        //           NeedsTeamAttention is added to the issue since there is a valid owner
        //           A comment is added thanking the creator, tagging and routing to the team
        [TestCase(RulesConstants.InitialIssueTriage,
                  "Tests.JsonEventPayloads/InitialIssueTriage_issue_opened_no_labels_no_assignee.json",
                  RuleState.On,
                  RuleState.On,
                  "FakeLabel2", // labels returned from the AI Issue Triage Service
                  "FakeAnswer", // answer returned from the AI Issue Triage Service
                  "", // answer type returned from the AI Issue Triage Service
                  "FakeUser1", // owners with permission to be assigned to issues
                  true, // Has CODEOWNERS entry
                  true,
                  true)]
        // Scenario: The AI Issue Triage Service returned label has a matching CODEOWNERS entry with a single AzureSdkOwner.
        //           ServiceAttention rule is On (doesn't matter when there's an AzureSdkOwner valid for assignment).
        //           The AzureSdkOwner has issue assignment permissions. Answer is empty.
        // Expected: The label is added to the issue.
        //           The AzureSdkOwner, FakeUser1, is assigned to the issue
        //           NeedsTeamAttention is added to the issue since there is a valid owner
        //           A comment is added thanking the creator, tagging and routing to the team
        [TestCase(RulesConstants.InitialIssueTriage,
                  "Tests.JsonEventPayloads/InitialIssueTriage_issue_opened_no_labels_no_assignee.json",
                  RuleState.On,
                  RuleState.On,
                  "FakeLabel2", // labels returned from the AI Issue Triage Service
                  "", // answer returned from the AI Issue Triage Service
                  "solution", // answer type returned from the AI Issue Triage Service
                  "FakeUser1", // owners with permission to be assigned to issues
                  true, // Has CODEOWNERS entry
                  true,
                  true)]
        // Scenario: The AI Issue Triage Service returned label has a matching CODEOWNERS entry with a multiple AzureSdkOwners.
        //           ServiceAttention rule is Off.
        //           None of the AzureSdkOwners have issue assignment permissions.
        // Expected: The label is added to the issue
        //           NeedsTeamTriage is added to the issue since there is no a valid owner and ServiceAttention isn't On
        [TestCase(RulesConstants.InitialIssueTriage,
                  "Tests.JsonEventPayloads/InitialIssueTriage_issue_opened_no_labels_no_assignee.json",
                  RuleState.On,
                  RuleState.Off,
                  "FakeLabel3", // labels returned from the AI Issue Triage Service
                  null, // answer returned from the AI Issue Triage Service
                  null, // answer type returned from the AI Issue Triage Service
                  null, // owners with permission to be assigned to issues
                  true, // Has CODEOWNERS entry
                  true,
                  true)]
        // Scenario: The AI Issue Triage Service returned label has a matching CODEOWNERS entry with a multiple AzureSdkOwners.
        //           ServiceAttention rule is On.
        //           None of the AzureSdkOwners have issue assignment permissions.
        // Expected: The label is added to the issue
        //           ServiceAttention is added to the issue and the ServiceAttention rule processed.
        //           ServiceAttention rule adds a comment to the issue.
        [TestCase(RulesConstants.InitialIssueTriage,
                  "Tests.JsonEventPayloads/InitialIssueTriage_issue_opened_no_labels_no_assignee.json",
                  RuleState.On,
                  RuleState.On,
                  "FakeLabel3", // labels returned from the AI Issue Triage Service
                  null, // answer returned from the AI Issue Triage Service
                  null, // answer type returned from the AI Issue Triage Service
                  null, // owners with permission to be assigned to issues
                  true, // Has CODEOWNERS entry
                  true,
                  true)]
        // Scenario: The AI Issue Triage Service returned label has a matching CODEOWNERS entry with a multiple AzureSdkOwners.
        //           ServiceAttention rule is On.
        //           Only one of the AzureSdkOwners have issue assignment permissions.
        // Expected: The label is added to the issue
        //           The AzureSdkOwner with permission is assigned to the issue
        //           A comment is added @ mentioning all of the AzureSdkOwners
        //           A second comment is added thanking the creator, tagging and routing to the team
        //           NeedsTeamTriage is added to the issue
        [TestCase(RulesConstants.InitialIssueTriage,
                  "Tests.JsonEventPayloads/InitialIssueTriage_issue_opened_no_labels_no_assignee.json",
                  RuleState.On,
                  RuleState.On,
                  "FakeLabel3", // labels returned from the AI Issue Triage Service
                  null, // answer returned from the AI Issue Triage Service
                  null, // answer type returned from the AI Issue Triage Service
                  "FakeUser5", // owners with permission to be assigned to issues
                  true, // Has CODEOWNERS entry
                  true,
                  true)]
        // Scenario: The AI Issue Triage Service returned label has a matching CODEOWNERS entry with multiple AzureSdkOwners which
        //           are pulled from source path/owners.
        //           Both AzureSdkOwners have assignment permissions.
        // Expected: The label is added to the issue.
        //           One of the AzureSdkOWners with permission is assigned to the issue
        //           A comment is added @ mentioning all of the AzureSdkOwners
        //           A second comment is added thanking the creator, tagging and routing to the team
        //           NeedsTeamTriage is added to the issue
        [TestCase(RulesConstants.InitialIssueTriage,
                  "Tests.JsonEventPayloads/InitialIssueTriage_issue_opened_no_labels_no_assignee.json",
                  RuleState.On,
                  RuleState.On,
                  "FakeLabel4", // labels returned from the AI Issue Triage Service
                  null, // answer returned from the AI Issue Triage Service
                  null, // answer type returned from the AI Issue Triage Service
                  "FakeUser5,FakeUser6", // owners with permission to be assigned to issues
                  true, // Has CODEOWNERS entry
                  true,
                  true)]
        // Scenario: The AI Issue Triage Service returned label has a matching CODEOWNERS entry with multiple AzureSdkOwners which
        //           are pulled from source path/owners.
        //           None of AzureSdkOwners have assignment permissions.
        // Expected: The label is added to the issue
        //           ServiceAttention is added to the issue and the ServiceAttention rule processed.
        //           ServiceAttention rule adds a comment to the issue.
        [TestCase(RulesConstants.InitialIssueTriage,
                  "Tests.JsonEventPayloads/InitialIssueTriage_issue_opened_no_labels_no_assignee.json",
                  RuleState.On,
                  RuleState.On,
                  "FakeLabel4", // labels returned from the AI Issue Triage Service
                  null, // answer returned from the AI Issue Triage Service
                  null, // answer type returned from the AI Issue Triage Service
                  null, // owners with permission to be assigned to issues
                  true, // Has CODEOWNERS entry
                  true,
                  true)]
        // Scenario: The AI Issue Triage Service returned label has a matching CODEOWNERS entry with multiple AzureSdkOwners which
        //           are pulled from source path/owners. Suggestion is added.
        //           None of AzureSdkOwners have assignment permissions.
        // Expected: The label is added to the issue
        //           ServiceAttention is added to the issue and the ServiceAttention rule processed.
        //           ServiceAttention rule adds a comment to the issue.
        //           Suggestion comment is added.
        [TestCase(RulesConstants.InitialIssueTriage,
                  "Tests.JsonEventPayloads/InitialIssueTriage_issue_opened_no_labels_no_assignee.json",
                  RuleState.On,
                  RuleState.On,
                  "FakeLabel4", // labels returned from the AI Issue Triage Service
                  "FakeAnswer", // answer returned from the AI Issue Triage Service
                  "suggestion", // answer type returned from the AI Issue Triage Service
                  null, // owners with permission to be assigned to issues
                  true, // Has CODEOWNERS entry
                  true,
                  true)]
        // Scenario: The AI Issue Triage Service returned label has a matching CODEOWNERS entry with multiple AzureSdkOwners which
        //           are pulled from source path/owners. Suggestion is added.
        //           None of AzureSdkOwners have assignment permissions.
        // Expected: The label is added to the issue
        //           ServiceAttention is added to the issue and the ServiceAttention rule processed.
        //           ServiceAttention rule adds a comment to the issue.
        //           Solution comment is added
        //           Issue-addressed label added.
        [TestCase(RulesConstants.InitialIssueTriage,
                  "Tests.JsonEventPayloads/InitialIssueTriage_issue_opened_no_labels_no_assignee.json",
                  RuleState.On,
                  RuleState.On,
                  "FakeLabel4", // labels returned from the AI Issue Triage Service
                  "FakeAnswer", // answer returned from the AI Issue Triage Service
                  "solution", // answer type returned from the AI Issue Triage Service
                  null, // owners with permission to be assigned to issues
                  true, // Has CODEOWNERS entry
                  true,
                  true)]
        // Scenario: The AI Issue Triage Service returned label has a matching CODEOWNERS entry that matches both labels returned.
        //           One of of AzureSdkOwners will have assignment permissions.
        // Expected: The labels are added to the issue
        //           One of the AzureSdkOWners with permission is assigned to the issue
        //           A comment is added @ mentioning all of the AzureSdkOwners
        //           A second comment is added thanking the creator, tagging and routing to the team
        //           NeedsTeamTriage is added to the issue
        [TestCase(RulesConstants.InitialIssueTriage,
                  "Tests.JsonEventPayloads/InitialIssueTriage_issue_opened_no_labels_no_assignee.json",
                  RuleState.On,
                  RuleState.On,
                  "Client, FakeLabel4", // labels returned from the AI Issue Triage Service
                  null, // answer returned from the AI Issue Triage Service
                  null, // answer type returned from the AI Issue Triage Service
                  "FakeUser6", // owners with permission to be assigned to issues
                  true, // Has CODEOWNERS entry
                  true,
                  true)]
        // Scenario: The AI Issue Triage Service returned label has a matching CODEOWNERS entry that matches only the Category label
        //           One of of AzureSdkOwners will have assignment permissions.
        // Expected: The labels are added to the issue
        //           One of the AzureSdkOWners with permission is assigned to the issue
        //           A comment is added @ mentioning all of the AzureSdkOwners
        //           A second comment is added thanking the creator, tagging and routing to the team
        //           NeedsTeamTriage is added to the issue
        [TestCase(RulesConstants.InitialIssueTriage,
                  "Tests.JsonEventPayloads/InitialIssueTriage_issue_opened_no_labels_no_assignee.json",
                  RuleState.On,
                  RuleState.On,
                  "Client, FakeLabel6", // labels returned from the AI Issue Triage Service, there is no FakeLabel6 only Client will match
                  null, // answer returned from the AI Issue Triage Service
                  null, // answer type returned from the AI Issue Triage Service
                  "FakeUser1", // owners with permission to be assigned to issues
                  true, // Has CODEOWNERS entry
                  true,
                  true)]
        // Scenario: The AI Issue Triage Service returned label has a matching CODEOWNERS entry that matches both labels returned.
        //           One of of AzureSdkOwners will have assignment permissions.
        // Expected: The labels are added to the issue
        //           One of the AzureSdkOWners with permission is assigned to the issue
        //           A comment is added @ mentioning all of the AzureSdkOwners
        //           A second comment is added thanking the creator, tagging and routing to the team
        //           NeedsTeamTriage is added to the issue
        [TestCase(RulesConstants.InitialIssueTriage,
                  "Tests.JsonEventPayloads/InitialIssueTriage_issue_opened_no_labels_no_assignee.json",
                  RuleState.On,
                  RuleState.On,
                  "Client, FakeLabel4", // labels returned from the AI Issue Triage Service
                  null, // answer returned from the AI Issue Triage Service
                  null, // answer type returned from the AI Issue Triage Service
                  "FakeUser6", // owners with permission to be assigned to issues
                  true, // Has CODEOWNERS entry
                  true,
                  true)]
        // Scenario: The AI Issue Triage Service has a matching CODEOWNERS entry that matches both labels returned.
        //           One of of AzureSdkOwners will have assignment permissions.
        // Expected: The labels are added to the issue
        //           One of the AzureSdkOWners with permission is assigned to the issue
        //           A comment is added @ mentioning all of the AzureSdkOwners
        //           A second comment is added thanking the creator, tagging and routing to the team
        //           NeedsTeamTriage is added to the issue
        [TestCase(RulesConstants.InitialIssueTriage,
                  "Tests.JsonEventPayloads/InitialIssueTriage_issue_opened_no_labels_no_assignee.json",
                  RuleState.On,
                  RuleState.On,
                  "Client, FakeLabel4", // labels returned from the AI Issue Triage Service
                  null, // answer returned from the AI Issue Triage Service
                  null, // answer type returned from the AI Issue Triage Service
                  "FakeUser6", // owners with permission to be assigned to issues
                  true, // Has CODEOWNERS entry
                  true,
                  true)]
        // Scenario: The AI Issue Triage Service returned label has a matching CODEOWNERS entry that matches both labels returned.
        //           One of of AzureSdkOwners will have assignment permissions. Suggestion is also provided by the service.
        // Expected: The labels are added to the issue
        //           One of the AzureSdkOWners with permission is assigned to the issue
        //           A comment is added @ mentioning all of the AzureSdkOwners
        //           A second comment is added thanking the creator, tagging and routing to the team
        //           A third comment is added with a suggestion
        //           NeedsTeamTriage is added to the issue
        [TestCase(RulesConstants.InitialIssueTriage,
                  "Tests.JsonEventPayloads/InitialIssueTriage_issue_opened_no_labels_no_assignee.json",
                  RuleState.On,
                  RuleState.On,
                  "Client, FakeLabel4", // labels returned from the AI Issue Triage Service
                  "FakeAnswer", // answer returned from the AI Issue Triage Service
                  "suggestion", // answer type returned from the AI Issue Triage Service
                  "FakeUser6", // owners with permission to be assigned to issues
                  true, // Has CODEOWNERS entry
                  true,
                  true)]
        // Scenario: The AI Issue Triage Service returned label has a matching CODEOWNERS entry that matches both labels returned.
        //           One of of AzureSdkOwners will have assignment permissions. Solution is also provided by the service.
        // Expected: The labels are added to the issue
        //           One of the AzureSdkOWners with permission is assigned to the issue
        //           A comment is added @ mentioning all of the AzureSdkOwners
        //           A second comment is added thanking the creator, tagging and routing to the team
        //           A third comment is added with a solution
        //           NeedsTeamTriage is added to the issue
        //           issue-addressed is added to the issue
        [TestCase(RulesConstants.InitialIssueTriage,
                  "Tests.JsonEventPayloads/InitialIssueTriage_issue_opened_no_labels_no_assignee.json",
                  RuleState.On,
                  RuleState.On,
                  "Client, FakeLabel4", // labels returned from the AI Issue Triage Service
                  "FakeAnswer", // answer returned from the AI Issue Triage Service
                  "solution", // answer type returned from the AI Issue Triage Service
                  "FakeUser6", // owners with permission to be assigned to issues
                  true, // Has CODEOWNERS entry
                  true,
                  true)]
        public async Task TestInitialIssueTriage(string rule,
                                                 string payloadFile,
                                                 RuleState ruleState,
                                                 RuleState serviceAttentionRuleState,
                                                 string AIServiceLabels,
                                                 string AIServiceAnswer,
                                                 string AIServiceAnswerType,
                                                 string ownersWithAssignPermission,
                                                 bool hasCodeownersEntry,
                                                 bool isMemberOfOrg,
                                                 bool hasWriteOrAdmin)
        {
            // CODEOWNERS has blocks setup for different scenarios. The ServiceLabel to return from the AI Service needs
            // to be a parameter so the correct block is selected.
            CodeOwnerUtils.ResetCodeOwnerEntries();
            CodeOwnerUtils.codeOwnersFilePathOverride = "Tests.FakeCodeowners/InitialIssueTriage_CODEOWNERS";

            var mockGitHubEventClient = new MockGitHubEventClient(OrgConstants.ProductHeaderName);
            mockGitHubEventClient.RulesConfiguration.Rules[rule] = ruleState;
            mockGitHubEventClient.RulesConfiguration.Rules[RulesConstants.ServiceAttention] = serviceAttentionRuleState;
            mockGitHubEventClient.UserHasPermissionsReturn = hasWriteOrAdmin;
            mockGitHubEventClient.IsUserMemberOfOrgReturn = isMemberOfOrg;
            var rawJson = TestHelpers.GetTestEventPayload(payloadFile);
            var issueEventPayload = SimpleJsonSerializer.Deserialize<IssueEventGitHubPayload>(rawJson);
            var issueProcessor = CreateIssueProcessingInstance();

            // If there are labels to be returned from the AI Issue Triage Service, parse them and ensure the mock's
            // AILabelServiceReturn is set. Each label gets added to the issue which means each label = 1 update.
            List<string> expectedLabels = new List<string>();
            if (AIServiceLabels != null)
            {
                expectedLabels.AddRange(AIServiceLabels.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList());
                mockGitHubEventClient.AIServiceLabels.AddRange(expectedLabels);
            }

            if (AIServiceAnswer != null)
            {
                mockGitHubEventClient.AIServiceAnswer = AIServiceAnswer;
            }
            if (AIServiceAnswerType != null)
            {
                mockGitHubEventClient.AIServiceAnswerType = AIServiceAnswerType;
            }

            // Set the owners with assign permission. If there are none, and there are ServiceOwners, then the
            // ServiceAttention label will get added and the rule executed if it's turned on for the repository.
            if (ownersWithAssignPermission != null)
            {
                mockGitHubEventClient.OwnersWithAssignPermission.AddRange(ownersWithAssignPermission.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList());
            }

            await issueProcessor.InitialIssueTriage(mockGitHubEventClient, issueEventPayload);
            var totalUpdates = await mockGitHubEventClient.ProcessPendingUpdates(issueEventPayload.Repository.Id, issueEventPayload.Issue.Number);

            // Verify the RuleChecks
            Assert.That(mockGitHubEventClient.RulesConfiguration.RuleEnabled(rule), Is.EqualTo(ruleState == RuleState.On), $"Rule '{rule}' enabled should have been {ruleState == RuleState.On} but RuleEnabled returned {ruleState != RuleState.On}.'");
            Assert.That(mockGitHubEventClient.RulesConfiguration.RuleEnabled(RulesConstants.ServiceAttention), Is.EqualTo(serviceAttentionRuleState == RuleState.On), $"Rule '{RulesConstants.ServiceAttention}' enabled should have been {serviceAttentionRuleState == RuleState.On} but RuleEnabled returned {serviceAttentionRuleState != RuleState.On}.'");
            if (ruleState == RuleState.Off)
            {
                Assert.That(totalUpdates, Is.EqualTo(0), $"{rule} is {ruleState} and should not have produced any updates but had {totalUpdates} updates.");
            }
            else
            {
                // The creator org/permission check is done regardless of whether or not the AI Issue Triage Service returnes labels.
                // If the user is not part of the Azure org AND they don't have write or admin collaborator permissions
                // then customer-reported and question labels should be added to the issue.
                if (!isMemberOfOrg && !hasWriteOrAdmin)
                {
                    Assert.That(mockGitHubEventClient.GetLabelsToAdd(), Does.Contain(TriageLabelConstants.CustomerReported), $"Labels to add should contain {TriageLabelConstants.CustomerReported} which it should when the user is not part of the org and doesn't have write/admin collaborator permissions.");
                    Assert.That(mockGitHubEventClient.GetLabelsToAdd(), Does.Contain(TriageLabelConstants.Question), $"Labels to add should contain {TriageLabelConstants.Question} which it should when the user is not part of the org and doesn't have write/admin collaborator permissions.");
                }
                else
                {
                    Assert.That(mockGitHubEventClient.GetLabelsToAdd(), Does.Not.Contain(TriageLabelConstants.CustomerReported), $"Labels to add contains {TriageLabelConstants.CustomerReported} and shouldn't when the user is part of the org or has write/admin collaborator permissions.");
                    Assert.That(mockGitHubEventClient.GetLabelsToAdd(), Does.Not.Contain(TriageLabelConstants.Question), $"Labels to add contains {TriageLabelConstants.Question} and shouldn't when the user is part of the org or has write/admin collaborator permissions.");
                }

                // If there are no labels being returned from the AI Issue Triage Service, the only processing is whether or not the creator
                // is a member of the Azure org or has Write or Admin permissions
                if (expectedLabels.Count == 0)
                {
                    Assert.That(mockGitHubEventClient.GetLabelsToAdd(), Does.Contain(TriageLabelConstants.NeedsTriage), $"Labels to add should contain {TriageLabelConstants.NeedsTriage} when the AI Label service has no suggested labels.");
                }
                // else the AI Issue Triage Service returned labels to add
                else
                {
                    // Verify the labels returned by the AI service have been added to the issue
                    foreach (string label in expectedLabels)
                    {
                        Assert.That(mockGitHubEventClient.GetLabelsToAdd(), Does.Contain(label), $"Labels to add should contain {label} which was returned by the AI service and should have been added.");
                    }

                    // If there is no CODEOWNERS entry there will be no AzureSdkOwners or ServiceOwners which means
                    // that LabelConstants.NeedsTeamTriage should be on the issue. In theory, this is the only path
                    // that should add NeedsTeamTriage. The reason being is that this will only happen if there are
                    // no ServiceOwners and no AzureSdkOwners which can only happen if there's no entry. A metadata
                    // block with a ServiceLabel needs to have ServiceOwners directly or end in a source path/owners
                    // line otherwise the block is malformed and will get thrown away. 
                    if (!hasCodeownersEntry)
                    {
                        Assert.That(mockGitHubEventClient.GetLabelsToAdd(), Does.Contain(TriageLabelConstants.NeedsTeamTriage), $"With no CODEOWNERS entry {TriageLabelConstants.NeedsTeamTriage} should have been added to the issue.");
                    }
                    // There is a CODEOWNERS entry for the ServiceLabel returned from the AI Issue Triage Service
                    else
                    {
                        // First, check whether or not there are AzureSdkOwners with assign permissions. If so,
                        // then one of them needs to be assigned as the owner of the issue. Verify that only
                        // one owner is being assigned to the issue and it's one of the ones in the list of
                        // owners with issue assign permission.
                        if (mockGitHubEventClient.OwnersWithAssignPermission.Count > 0)
                        {
                            var githubIssueAssignment = mockGitHubEventClient.GetGitHubIssueAssignment();
                            Assert.That(githubIssueAssignment.Assignees.Count, Is.EqualTo(1), $"There should only be a single owner assigned to an issue but {githubIssueAssignment.Assignees.Count} owners were assigned. Assignees={string.Join(",", githubIssueAssignment.Assignees)}.");
                            // Verify that the assignee is one of the owners from the list with assign permissions.
                            bool ownerFromOwnersWithPermList = mockGitHubEventClient.OwnersWithAssignPermission.Contains(githubIssueAssignment.Assignees[0], StringComparer.OrdinalIgnoreCase);
                            Assert.That(ownerFromOwnersWithPermList, Is.True, $"The owner assigned to the issue, {githubIssueAssignment.Assignees[0]}, was not in the list of owners with assign permission, {string.Join(",", mockGitHubEventClient.OwnersWithAssignPermission)}");

                            Assert.That(mockGitHubEventClient.GetLabelsToAdd(), Does.Contain(TriageLabelConstants.NeedsTeamAttention), $"With a valid AzureSdkOwner to assign to the issue the {TriageLabelConstants.NeedsTeamAttention} should have been added to the issue.");

                            // If there is more than one AzureSdkOwner, a comment will also be created to @ mention all of the owners
                            // and a second comment will be created thanking the issue creator for their feedback.
                            // However given a suggestion/solution it will create a comment with its contents instead
                            // of the thanks noted above
                            CodeownersEntry codeownersEntry = CodeOwnerUtils.GetCodeownersEntryForLabelList(expectedLabels);
                            // With a single AzureSdkOwner there should only be one comment thanking the creator for feedback and tagging and routing the issue
                            // Or suggestion/solution
                            if (codeownersEntry.AzureSdkOwners.Count == 1)
                            {
                                Assert.That(mockGitHubEventClient.GetComments().Count, Is.EqualTo(1), $"With only one AzureSdkOwner there should only be one comment thanking the creator, tagging and routing but {mockGitHubEventClient.GetComments().Count} comments were created.");
                            }
                            // With more than one AzureSdkOWner there should be two comments. The first is an @ mention and the second is the same one thanking the creator
                            // or it ignores thanking and gives the suggestion/solution
                            else if (codeownersEntry.AzureSdkOwners.Count > 1)
                            {
                                Assert.That(mockGitHubEventClient.GetComments().Count, Is.EqualTo(2), $"With multiple AzureSdkOwners there should only be two comments. One @ mentioning everyone on the list and the other thanking the creator, tagging and routing but {mockGitHubEventClient.GetComments().Count} comments were created.");
                            }
                            // If there are OwnersWithAssignPermission but no AzureSdkOwners then the test scenario wasn't written correctly.
                            else
                            {
                                Assert.That(false, $"OwnersWithAssignPermission was > 0 but the label(s), {string.Join(",", expectedLabels)}, has no AzureSdkOwners in it's CODEOWNERS entry. Please verify the test was written correctly.");
                            }


                            // When solution is provided by the AI Issue Triage Service it should add the label issue-addressed.
                            // and has valid answer
                            if (AIServiceAnswerType == "solution" && !string.IsNullOrEmpty(AIServiceAnswer))
                            {
                                Assert.That(mockGitHubEventClient.GetLabelsToAdd(), Does.Contain(TriageLabelConstants.IssueAddressed), $"Labels to add should contain {TriageLabelConstants.IssueAddressed} when Solution is provided.");
                            }
                        }
                        // No AzureSdkOwners with assign permission means that there's no valid assignees and if there are ServiceOwners (which their has to be
                        // at this point since the no CODEOWNERS entry case is checked first) it'll run the ServiceAttention rule if its enabled.
                        else
                        {
                            if (serviceAttentionRuleState == RuleState.Off)
                            {
                                Assert.That(mockGitHubEventClient.GetLabelsToAdd(), Does.Contain(TriageLabelConstants.NeedsTeamTriage), $"With no valid AzureSdkOwners and the ServiceAttention rule being disabled, {TriageLabelConstants.NeedsTeamTriage} should have been added to the issue.");
                            }
                            // else, the ServiceAttention rule will which creates a comment that ends with @ mentioning the service owners
                            // and NeedsTeamAttention is added to the issue
                            else
                            {
                                Assert.That(mockGitHubEventClient.GetLabelsToAdd(), Does.Contain(TriageLabelConstants.NeedsTeamAttention), $"With no valid AzureSdkOwners but valid ServiceOwners and ServiceAttention rule being enabled, {TriageLabelConstants.NeedsTeamAttention} should have been added to the issue.");

                                // If only labels are predicted
                                if (AIServiceAnswerType == null && AIServiceLabels != null)
                                {
                                    Assert.That(mockGitHubEventClient.GetComments().Count, Is.EqualTo(1), $"With no AzureSdkOwners and the ServiceAttention rule being enabled, there should only be one comment created by the ServiceAttention rule but {mockGitHubEventClient.GetComments().Count} comments were created.");
                                }
                                else
                                {
                                    Assert.That(mockGitHubEventClient.GetComments().Count, Is.EqualTo(2), $"With no AzureSdkOwners, the ServiceAttention rule being enabled, and Suggestion/Solution provided by the AI Issue Triage Service, there should be two comment created by the ServiceAttention rule but {mockGitHubEventClient.GetComments().Count} comments were created.");
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// ManualIssueTriage requires 2 labeled event payloads to test.
        /// 1. Labeled payload needs needs to be one where needs-triage is being added which should cause no change.
        /// 2. Needs-triage needs to be already on the issues when another label is added. This should cause an update
        ///    where the needs-triage label is removed. This is also the payload used to check when the rule is Off.
        /// Trigger: issues labeled
        /// Conditions: Issue is open
        ///             Issue has "needs-triage" label
        ///             Label being added is NOT "needs-triage"
        /// Resulting Action: Remove "needs-triage" label
        /// </summary>
        /// <param name="rule">String, RulesConstants for the rule being tested</param>
        /// <param name="payloadFile">JSon payload file for the event being tested</param>
        /// <param name="ruleState">Whether or not the rule is on/off</param>
        /// <param name="labelAddedIsNeedsTriage">Whether or not the payload's labeled event is adding needs-triage</param>
        [Category("static")]
        [TestCase(RulesConstants.ManualIssueTriage, "Tests.JsonEventPayloads/ManualIssueTriage_issue_labeled_needs-triage.json", RuleState.On, true)]
        [TestCase(RulesConstants.ManualIssueTriage, "Tests.JsonEventPayloads/ManualIssueTriage_issue_labeled_not_needs-triage.json", RuleState.On, false)]
        [TestCase(RulesConstants.ManualIssueTriage, "Tests.JsonEventPayloads/ManualIssueTriage_issue_labeled_not_needs-triage.json", RuleState.Off, false)]
        public async Task TestManualIssueTriage(string rule, string payloadFile, RuleState ruleState, bool labelAddedIsNeedsTriage)
        {
            var mockGitHubEventClient = new MockGitHubEventClient(OrgConstants.ProductHeaderName);
            mockGitHubEventClient.RulesConfiguration.Rules[rule] = ruleState;
            var rawJson = TestHelpers.GetTestEventPayload(payloadFile);
            var issueEventPayload = SimpleJsonSerializer.Deserialize<IssueEventGitHubPayload>(rawJson);
            var issueProcessing = CreateIssueProcessingInstance();
            issueProcessing.ManualIssueTriage(mockGitHubEventClient, issueEventPayload);

            // Verify the RuleCheck
            Assert.That(mockGitHubEventClient.RulesConfiguration.RuleEnabled(rule), Is.EqualTo(ruleState == RuleState.On), $"Rule '{rule}' enabled should have been {ruleState == RuleState.On} but RuleEnabled returned {ruleState != RuleState.On}.'");

            var totalUpdates = await mockGitHubEventClient.ProcessPendingUpdates(issueEventPayload.Repository.Id, issueEventPayload.Issue.Number);
            if (RuleState.On == ruleState)
            {
                // If the label being added is NeedsTriage, there should be no updates
                if (labelAddedIsNeedsTriage)
                {
                    Assert.That(totalUpdates, Is.EqualTo(0), $"The label being added was {TriageLabelConstants.NeedsTriage} and should not have produced any updates.");
                }
                else
                {
                    // There should be one update, an IssueUpdate with the NoRecentActivity label removed
                    Assert.That(totalUpdates, Is.EqualTo(1), $"The number of updates should have been 1 but was instead, {totalUpdates}");
                    // Verify that NeedsTriage was removed
                    Assert.That(mockGitHubEventClient.GetLabelsToRemove(), Does.Contain(TriageLabelConstants.NeedsTriage), $"Labels to remove should contain {TriageLabelConstants.NeedsTriage} and does not.");
                }
            }
            else
            {
                Assert.That(totalUpdates, Is.EqualTo(0), $"{rule} is {ruleState} and should not have produced any updates.");
            }
        }

        /// <summary>
        /// Service Attention requires two different CODEOWNERS files to test, one that has parties to mention
        /// for ServiceAttention and one that doesn't. When there are no parties to mention, there should be
        /// no updates.
        /// Trigger: issue labeled
        /// Conditions: Issue is open
        ///             Label being added is "Service Attention"
        /// Resulting Action: Add issue comment "Thanks for the feedback! We are routing this to the appropriate team for follow-up. cc ${mentionees}."
        /// </summary>
        /// <param name="rule">String, RulesConstants for the rule being tested</param>
        /// <param name="payloadFile">JSon payload file for the event being tested</param>
        /// <param name="ruleState">Whether or not the rule is on/off</param>
        /// <param name="hasPartiesToMentionForServiceAttention">Determines whether to load the codeowners with parties to mention or the one without</param>
        [Category("static")]
        [NonParallelizable]
        [TestCase(RulesConstants.ServiceAttention, "Tests.JsonEventPayloads/ServiceAttention_issue_labeled.json", "Tests.FakeCodeowners/ServiceAttention_has_CODEOWNERS", RuleState.Off, null)]
        [TestCase(RulesConstants.ServiceAttention, "Tests.JsonEventPayloads/ServiceAttention_issue_labeled.json", "Tests.FakeCodeowners/ServiceAttention_has_CODEOWNERS", RuleState.On, "@FakeUser4 @FakeUser9")]
        [TestCase(RulesConstants.ServiceAttention, "Tests.JsonEventPayloads/ServiceAttention_issue_labeled.json", "Tests.FakeCodeowners/ServiceAttention_has_CODEOWNERS_SingleLabelMatch", RuleState.On, "@FakeUser9")]
        [TestCase(RulesConstants.ServiceAttention, "Tests.JsonEventPayloads/ServiceAttention_issue_labeled.json", "Tests.FakeCodeowners/ServiceAttention_does_not_have_CODEOWNERS", RuleState.On, null)]
        public async Task TestServiceAttention(string rule, string payloadFile, string codeownersFile, RuleState ruleState, string expectedNames)
        {
            var mockGitHubEventClient = new MockGitHubEventClient(OrgConstants.ProductHeaderName);
            mockGitHubEventClient.RulesConfiguration.Rules[rule] = ruleState;
            var rawJson = TestHelpers.GetTestEventPayload(payloadFile);
            var issueEventPayload = SimpleJsonSerializer.Deserialize<IssueEventGitHubPayload>(rawJson);

            CodeOwnerUtils.ResetCodeOwnerEntries();
            CodeOwnerUtils.codeOwnersFilePathOverride = codeownersFile;

            var issueProcessing = CreateIssueProcessingInstance();
            issueProcessing.ServiceAttention(mockGitHubEventClient, issueEventPayload);

            // Verify the RuleCheck
            Assert.That(mockGitHubEventClient.RulesConfiguration.RuleEnabled(rule), Is.EqualTo(ruleState == RuleState.On), $"Rule '{rule}' enabled should have been {ruleState == RuleState.On} but RuleEnabled returned {ruleState != RuleState.On}.'");

            var totalUpdates = await mockGitHubEventClient.ProcessPendingUpdates(issueEventPayload.Repository.Id, issueEventPayload.Issue.Number);
            if (RuleState.On == ruleState)
            {
                if (null != expectedNames)
                {
                    // "Thanks for the feedback! We are routing this to the appropriate team for follow-up. cc @FakeUser1 @FakeUser11 @FakeUser4 @FakeUser14 @FakeUser24 @FakeUser9."
                    // There should be one update, a comment
                    Assert.That(totalUpdates, Is.EqualTo(1), $"The number of updates should have been 1 but was instead, {totalUpdates}");
                    // Verify that a single comment was created
                    Assert.That(mockGitHubEventClient.GetComments().Count, Is.EqualTo(1), $"{rule} should have produced a single comment.");
                    string comment = mockGitHubEventClient.GetComments()[0].Comment;
                    Assert.That(comment.Contains(expectedNames), Is.True, $"Comment should have contained expected names {expectedNames} but did not. Full comment={comment}");
                }
                else
                {
                    Assert.That(totalUpdates, Is.EqualTo(0), $"With no parties to mention for Service Attention, the rule should not have produced any updates.");
                }
            }
            else
            {
                Assert.That(totalUpdates, Is.EqualTo(0), $"{rule} is {ruleState} and should not have produced any updates.");
            }
        }

        /// <summary>
        /// Test ManualTriageAfterExternalAssignment rule enabled/disabled, with a payload that would cause updates when enabled.
        /// Verify all the expected updates when enabled and no updates when disabled.
        /// Trigger: issue unlabeled
        /// Conditions: Issue is open
        ///             Has "customer-reported" label
        ///             Issue is unassigned
        ///             Label removed is "Service Attention"
        ///             (in other words if both labels are on the issue and one is removed, this
        ///             shouldn't process)
        /// Resulting Action: Add "needs-team-triage" label
        /// </summary>
        /// <param name="rule">String, RulesConstants for the rule being tested</param>
        /// <param name="payloadFile">JSon payload file for the event being tested</param>
        /// <param name="ruleState">Whether or not the rule is on/off</param>
        /// <param name="alreadyHasNeedsTeamTriage">Whether or not the payload already has the needs-team-triage label</param>
        [Category("static")]
        [TestCase(RulesConstants.ManualTriageAfterExternalAssignment, "Tests.JsonEventPayloads/ManualTriageAfterExternalAssignment_issue_unlabeled_service_attention.json", RuleState.Off, false, false)]
        [TestCase(RulesConstants.ManualTriageAfterExternalAssignment, "Tests.JsonEventPayloads/ManualTriageAfterExternalAssignment_issue_unlabeled_service_attention.json", RuleState.On, false, false)]
        [TestCase(RulesConstants.ManualTriageAfterExternalAssignment, "Tests.JsonEventPayloads/ManualTriageAfterExternalAssignment_issue_unlabeled_service_attention_no_assignees.json", RuleState.On, false, true)]
        [TestCase(RulesConstants.ManualTriageAfterExternalAssignment, "Tests.JsonEventPayloads/ManualTriageAfterExternalAssignment_issue_unlabeled_has_needs-team-triage.json", RuleState.On, true, false)]
        public async Task TestManualTriageAfterExternalAssignment(string rule, string payloadFile, RuleState ruleState, bool alreadyHasNeedsTeamTriage, bool shouldAddLabel)
        {
            var mockGitHubEventClient = new MockGitHubEventClient(OrgConstants.ProductHeaderName);
            mockGitHubEventClient.RulesConfiguration.Rules[rule] = ruleState;
            var rawJson = TestHelpers.GetTestEventPayload(payloadFile);
            var issueEventPayload = SimpleJsonSerializer.Deserialize<IssueEventGitHubPayload>(rawJson);
            IssueProcessing issueProcessing = CreateIssueProcessingInstance();
            issueProcessing.ManualTriageAfterExternalAssignment(mockGitHubEventClient, issueEventPayload);

            // Verify the RuleCheck
            Assert.That(mockGitHubEventClient.RulesConfiguration.RuleEnabled(rule), Is.EqualTo(ruleState == RuleState.On), $"Rule '{rule}' enabled should have been {ruleState == RuleState.On} but RuleEnabled returned {ruleState != RuleState.On}.'");

            var totalUpdates = await mockGitHubEventClient.ProcessPendingUpdates(issueEventPayload.Repository.Id, issueEventPayload.Issue.Number);
            if (RuleState.On == ruleState)
            {
                // If issue already has needs-team-triage there should be no updates
                if (alreadyHasNeedsTeamTriage)
                {
                    Assert.That(totalUpdates, Is.EqualTo(0), $"The issue already has {TriageLabelConstants.NeedsTeamTriage} and should not have produced any updates.");
                }
                else
                {
                    if (shouldAddLabel)
                    {
                        // There should be one update, the label NoRecentActivity should have been added
                        Assert.That(totalUpdates, Is.EqualTo(1), $"The number of updates should have been 1 but was instead, {totalUpdates}");
                        // Verify that NeedsTeamTriage was added
                        Assert.That(mockGitHubEventClient.GetLabelsToAdd().Contains(TriageLabelConstants.NeedsTeamTriage), Is.True, $"Labels to add should contain {TriageLabelConstants.NeedsTeamTriage} and does not.");
                    }
                    else
                    {
                        Assert.That(totalUpdates, Is.EqualTo(0), $"The issue only 1 of {TriageLabelConstants.ServiceAttention}. With the other still being on the issue there should have been no updates.");
                    }
                }
            }
            else
            {
                Assert.That(totalUpdates, Is.EqualTo(0), $"{rule} is {ruleState} and should not have produced any updates.");
            }
        }

        /// <summary>
        /// Test ResetIssueActivity rule enabled/disabled, with a payload that would cause updates when enabled.
        /// Verify all the expected updates when enabled and no updates when disabled.
        /// Trigger: issue reopened/edited
        /// Conditions: Issue is open OR Issue is being reopened
        ///             Issue has "no-recent-activity" label
        ///             User modifying the issue is NOT a known bot
        /// </summary>
        /// <param name="rule">String, RulesConstants for the rule being tested</param>
        /// <param name="payloadFile">JSon payload file for the event being tested</param>
        /// <param name="ruleState">Whether or not the rule is on/off</param>
        [Category("static")]
        [TestCase(RulesConstants.ResetIssueActivity, "Tests.JsonEventPayloads/ResetIssueActivity_issue_edited.json", RuleState.Off)]
        [TestCase(RulesConstants.ResetIssueActivity, "Tests.JsonEventPayloads/ResetIssueActivity_issue_edited.json", RuleState.On)]
        [TestCase(RulesConstants.ResetIssueActivity, "Tests.JsonEventPayloads/ResetIssueActivity_issue_reopened.json", RuleState.Off)]
        [TestCase(RulesConstants.ResetIssueActivity, "Tests.JsonEventPayloads/ResetIssueActivity_issue_reopened.json", RuleState.On)]
        public async Task TestResetIssueActivity(string rule, string payloadFile, RuleState ruleState)
        {
            var mockGitHubEventClient = new MockGitHubEventClient(OrgConstants.ProductHeaderName);
            mockGitHubEventClient.RulesConfiguration.Rules[rule] = ruleState;
            var rawJson = TestHelpers.GetTestEventPayload(payloadFile);
            var issueEventPayload = SimpleJsonSerializer.Deserialize<IssueEventGitHubPayload>(rawJson);
            var issueProcessing = CreateIssueProcessingInstance();
            issueProcessing.ResetIssueActivity(mockGitHubEventClient, issueEventPayload);

            // Verify the RuleCheck
            Assert.That(mockGitHubEventClient.RulesConfiguration.RuleEnabled(rule), Is.EqualTo(ruleState == RuleState.On), $"Rule '{rule}' enabled should have been {ruleState == RuleState.On} but RuleEnabled returned {ruleState != RuleState.On}.'");

            var totalUpdates = await mockGitHubEventClient.ProcessPendingUpdates(issueEventPayload.Repository.Id, issueEventPayload.Issue.Number);
            if (RuleState.On == ruleState)
            {
                // There should be one update, the NoRecentActivity label removed
                Assert.That(totalUpdates, Is.EqualTo(1), $"The number of updates should have been 1 but was instead, {totalUpdates}");

                // Verify that NoRecentActivity was removed
                Assert.That(mockGitHubEventClient.GetLabelsToRemove().Contains(TriageLabelConstants.NoRecentActivity), Is.True, $"Labels to remove should contain {TriageLabelConstants.NoRecentActivity} and does not.");
            }
            else
            {
                Assert.That(totalUpdates, Is.EqualTo(0), $"{rule} is {ruleState} and should not have produced any updates.");
            }
        }

        /// <summary>
        /// Test RequireAttentionForNonMilestone rule enabled/disabled, with a payload that would cause updates when enabled.
        /// Verify all the expected updates when enabled and no updates when disabled.
        /// Trigger: issue labeled/unlabeled
        /// Conditions: Issue is open
        ///             Issue has label "customer-reported"
        ///             Issue does NOT have label "needs-team-attention"
        ///             Issue does NOT have label "needs-triage"
        ///             Issue does NOT have label "needs-team-triage"
        ///             Issue does NOT have label "needs-author-feedback"
        ///             Issue does NOT have label "issue-addressed"
        ///             Issue is not in a milestone
        /// Resulting Action: Add "needs-team-attention" label
        /// </summary>
        /// <param name="rule">String, RulesConstants for the rule being tested</param>
        /// <param name="payloadFile">JSon payload file for the event being tested</param>
        /// <param name="ruleState">Whether or not the rule is on/off</param>
        [Category("static")]
        [TestCase(RulesConstants.RequireAttentionForNonMilestone, "Tests.JsonEventPayloads/RequireAttentionForNonMilestone_issue_labeled.json", RuleState.Off)]
        [TestCase(RulesConstants.RequireAttentionForNonMilestone, "Tests.JsonEventPayloads/RequireAttentionForNonMilestone_issue_labeled.json", RuleState.On)]
        [TestCase(RulesConstants.RequireAttentionForNonMilestone, "Tests.JsonEventPayloads/RequireAttentionForNonMilestone_issue_unlabeled.json", RuleState.Off)]
        [TestCase(RulesConstants.RequireAttentionForNonMilestone, "Tests.JsonEventPayloads/RequireAttentionForNonMilestone_issue_unlabeled.json", RuleState.On)]
        public async Task TestRequireAttentionForNonMilestone(string rule, string payloadFile, RuleState ruleState)
        {
            var mockGitHubEventClient = new MockGitHubEventClient(OrgConstants.ProductHeaderName);
            mockGitHubEventClient.RulesConfiguration.Rules[rule] = ruleState;
            var rawJson = TestHelpers.GetTestEventPayload(payloadFile);
            var issueEventPayload = SimpleJsonSerializer.Deserialize<IssueEventGitHubPayload>(rawJson);
            var issueProcessing = CreateIssueProcessingInstance();
            issueProcessing.RequireAttentionForNonMilestone(mockGitHubEventClient, issueEventPayload);

            // Verify the RuleCheck
            Assert.That(mockGitHubEventClient.RulesConfiguration.RuleEnabled(rule), Is.EqualTo(ruleState == RuleState.On), $"Rule '{rule}' enabled should have been {ruleState == RuleState.On} but RuleEnabled returned {ruleState != RuleState.On}.'");

            var totalUpdates = await mockGitHubEventClient.ProcessPendingUpdates(issueEventPayload.Repository.Id, issueEventPayload.Issue.Number);
            if (RuleState.On == ruleState)
            {
                // There should be one update, the NeedsTeamAttention label added
                Assert.That(totalUpdates, Is.EqualTo(1), $"The number of updates should have been 1 but was instead, {totalUpdates}");

                // Verify that NeedsTeamAttention was added
                Assert.That(mockGitHubEventClient.GetLabelsToAdd().Contains(TriageLabelConstants.NeedsTeamAttention), Is.True, $"Lables to add should contain {TriageLabelConstants.NeedsTeamAttention} and does not.");
            }
            else
            {
                Assert.That(totalUpdates, Is.EqualTo(0), $"{rule} is {ruleState} and should not have produced any updates.");
            }
        }

        /// <summary>
        /// Test AuthorFeedbackNeeded rule enabled/disabled, with a payload that would cause updates when enabled.
        /// Verify all the expected updates when enabled and no updates when disabled.
        /// Trigger: issue labeled
        /// Conditions: Issue is open
        ///             Label added is "needs-author-feedback"
        /// Resulting Action:
        ///             Remove "needs-triage" label
        ///             Remove "needs-team-triage" label
        ///             Remove "needs-team-attention" label
        /// </summary>
        /// <param name="rule">String, RulesConstants for the rule being tested</param>
        /// <param name="payloadFile">JSon payload file for the event being tested</param>
        /// <param name="ruleState">Whether or not the rule is on/off</param>
        /// <param name="hasLabelsToRemove">Whether or not the payload Issue contains the labels that are being removed</param>
        [Category("static")]
        [TestCase(RulesConstants.AuthorFeedbackNeeded, "Tests.JsonEventPayloads/AuthorFeedbackNeeded_issue_labeled_with_labels_to_remove.json", RuleState.Off, true)]
        [TestCase(RulesConstants.AuthorFeedbackNeeded, "Tests.JsonEventPayloads/AuthorFeedbackNeeded_issue_labeled_with_labels_to_remove.json", RuleState.On, true)]
        [TestCase(RulesConstants.AuthorFeedbackNeeded, "Tests.JsonEventPayloads/AuthorFeedbackNeeded_issue_labeled_nothing_to_remove.json", RuleState.Off, false)]
        public async Task TestAuthorFeedbackNeeded(string rule, string payloadFile, RuleState ruleState, bool hasLabelsToRemove)
        {
            var mockGitHubEventClient = new MockGitHubEventClient(OrgConstants.ProductHeaderName);
            mockGitHubEventClient.RulesConfiguration.Rules[rule] = ruleState;
            var rawJson = TestHelpers.GetTestEventPayload(payloadFile);
            var issueEventPayload = SimpleJsonSerializer.Deserialize<IssueEventGitHubPayload>(rawJson);
            var issueProcessing = CreateIssueProcessingInstance();
            issueProcessing.AuthorFeedbackNeeded(mockGitHubEventClient, issueEventPayload);

            // Verify the RuleCheck
            Assert.That(mockGitHubEventClient.RulesConfiguration.RuleEnabled(rule), Is.EqualTo(ruleState == RuleState.On), $"Rule '{rule}' enabled should have been {ruleState == RuleState.On} but RuleEnabled returned {ruleState != RuleState.On}.'");

            var totalUpdates = await mockGitHubEventClient.ProcessPendingUpdates(issueEventPayload.Repository.Id, issueEventPayload.Issue.Number);
            if (RuleState.On == ruleState)
            {
                // If there are no labels to remove, there should be 1 update which is a comment
                if (!hasLabelsToRemove)
                {
                    Assert.That(totalUpdates, Is.EqualTo(1), $"With no labels to remove there only be 1 update, a comment.");
                }
                else
                {
                    // There should be 4 updates, NeedsTriage, NeedsTeamTriage and NeedsTeamAttention labels removed and a comment added
                    Assert.That(totalUpdates, Is.EqualTo(4), $"The number of updates should have been 4 but was instead, {totalUpdates}");

                    // Verify that NeedsTriage, NeedsTeamTriage and NeedsTeamAttention were removed
                    Assert.That(mockGitHubEventClient.GetLabelsToRemove().Contains(TriageLabelConstants.NeedsTriage), Is.True, $"Labels to remove should contain {TriageLabelConstants.NeedsTriage} and does not.");
                    Assert.That(mockGitHubEventClient.GetLabelsToRemove().Contains(TriageLabelConstants.NeedsTeamTriage), Is.True, $"Labels to remove should contain {TriageLabelConstants.NeedsTeamTriage} and does not.");
                    Assert.That(mockGitHubEventClient.GetLabelsToRemove().Contains(TriageLabelConstants.NeedsTeamAttention), Is.True, $"Labels to remove should contain {TriageLabelConstants.NeedsTeamAttention} and does not.");
                }

                // The comment should be created regardless
                int numComments = mockGitHubEventClient.GetComments().Count;
                Assert.That(numComments, Is.EqualTo(1), $"There should have been one comment created but instead there were {numComments} created.");

            }
            else
            {
                Assert.That(totalUpdates, Is.EqualTo(0), $"{rule} is {ruleState} and should not have produced any updates.");
            }
        }

        /// <summary>
        /// Test IssueAddressed rule enabled/disabled, with a payload that would cause updates when enabled.
        /// Verify all the expected updates when enabled and no updates when disabled.
        /// Trigger: issue labeled
        /// Conditions: Issue is open
        ///             Label added is "issue-addressed"
        /// Resulting Action:
        ///     Remove "needs-triage" label if it exists
        ///     Remove "needs-team-triage" label
        ///     Remove "needs-team-attention" label
        ///     Remove "needs-author-feedback" label
        ///     Remove "no-recent-activity" label
        ///     Add issue comment
        /// </summary>
        /// <param name="rule">String, RulesConstants for the rule being tested</param>
        /// <param name="payloadFile">JSon payload file for the event being tested</param>
        /// <param name="ruleState">Whether or not the rule is on/off</param>
        /// <param name="hasLabelsToRemove">Whether or not the payload Issue contains the labels that are being removed</param>
        [Category("static")]
        [TestCase(RulesConstants.IssueAddressed, "Tests.JsonEventPayloads/IssueAddressed_issue_labeled_with_labels_to_remove.json", RuleState.Off, true)]
        [TestCase(RulesConstants.IssueAddressed, "Tests.JsonEventPayloads/IssueAddressed_issue_labeled_with_labels_to_remove.json", RuleState.On, true)]
        [TestCase(RulesConstants.IssueAddressed, "Tests.JsonEventPayloads/IssueAddressed_issue_labeled_nothing_to_remove.json", RuleState.On, false)]
        public async Task TestIssueAddressed(string rule, string payloadFile, RuleState ruleState, bool hasLabelsToRemove)
        {
            var mockGitHubEventClient = new MockGitHubEventClient(OrgConstants.ProductHeaderName);
            mockGitHubEventClient.RulesConfiguration.Rules[rule] = ruleState;
            var rawJson = TestHelpers.GetTestEventPayload(payloadFile);
            var issueEventPayload = SimpleJsonSerializer.Deserialize<IssueEventGitHubPayload>(rawJson);
            var issueProcessing = CreateIssueProcessingInstance();
            issueProcessing.IssueAddressed(mockGitHubEventClient, issueEventPayload);

            // Verify the RuleCheck
            Assert.That(mockGitHubEventClient.RulesConfiguration.RuleEnabled(rule), Is.EqualTo(ruleState == RuleState.On), $"Rule '{rule}' enabled should have been {ruleState == RuleState.On} but RuleEnabled returned {ruleState != RuleState.On}.'");

            var totalUpdates = await mockGitHubEventClient.ProcessPendingUpdates(issueEventPayload.Repository.Id, issueEventPayload.Issue.Number);
            if (RuleState.On == ruleState)
            {
                // If the label being added is NeedsTriage, there should be no updates
                if (!hasLabelsToRemove)
                {
                    Assert.That(totalUpdates, Is.EqualTo(1), $"With none of the labels to remove being on the Issue, there should still be 1 update from an added comment.");
                }
                else
                {
                    // There should be one comment and up to 5 labels removed, the no-recent-activity and all needs-* labels removed
                    // (the test payload has them all but real events will only remove the ones that are there)
                    Assert.That(totalUpdates, Is.EqualTo(6), $"The number of updates should have been 2 but was instead, {totalUpdates}");

                    // Verify that NeedsTriage was added to the remove list
                    Assert.That(mockGitHubEventClient.GetLabelsToRemove().Contains(TriageLabelConstants.NeedsTriage), Is.True, $"Labels to remove should contain {TriageLabelConstants.NeedsTriage} and does not.");
                    // Verify that NeedsTeamTriage was added to the remove list
                    Assert.That(mockGitHubEventClient.GetLabelsToRemove().Contains(TriageLabelConstants.NeedsTeamTriage), Is.True, $"Labels to remove should contain {TriageLabelConstants.NeedsTeamTriage} and does not.");
                    // Verify that NeedsTeamAttention was added to the remove list
                    Assert.That(mockGitHubEventClient.GetLabelsToRemove().Contains(TriageLabelConstants.NeedsTeamAttention), Is.True, $"Labels to remove should contain {TriageLabelConstants.NeedsTeamAttention} and does not.");
                    // Verify that NeedsAuthorFeedback was added to the remove list
                    Assert.That(mockGitHubEventClient.GetLabelsToRemove().Contains(TriageLabelConstants.NeedsAuthorFeedback), Is.True, $"Labels to remove should contain {TriageLabelConstants.NeedsAuthorFeedback} and does not.");
                    // Verify that NoRecentActivity was added to the remove list
                    Assert.That(mockGitHubEventClient.GetLabelsToRemove().Contains(TriageLabelConstants.NoRecentActivity), Is.True, $"Labels to remove should contain {TriageLabelConstants.NoRecentActivity} and does not.");
                }
                // Regardless of whether or not there were labels to remove, a single comment should be created.
                Assert.That(mockGitHubEventClient.GetComments().Count, Is.EqualTo(1), $"{rule} should have produced a single comment.");
            }
            else
            {
                Assert.That(totalUpdates, Is.EqualTo(0), $"{rule} is {ruleState} and should not have produced any updates.");
            }
        }

        /// <summary>
        /// Test IssueAddressedReset rule enabled/disabled, with a payload that would cause updates when enabled.
        /// Verify all the expected updates when enabled and no updates when disabled.
        /// Trigger: issue labeled
        /// Conditions: Issue is open
        ///             Issue has label "issue-addressed"
        ///             Label added is any one of:
        ///                 "needs-team-attention"
        ///                 "needs-author-feedback"
        ///                 "Service Attention"
        ///                 "needs-triage"
        ///                 "needs-team-triage"
        /// Resulting Action:
        ///     Remove "issue-addressed" label
        /// </summary>
        /// <param name="rule">String, RulesConstants for the rule being tested</param>
        /// <param name="payloadFile">JSon payload file for the event being tested</param>
        /// <param name="ruleState">Whether or not the rule is on/off</param>
        [Category("static")]
        [TestCase(RulesConstants.IssueAddressedReset, "Tests.JsonEventPayloads/IssueAddressedReset_issue_labeled_needs-author-feedack.json", RuleState.On)]
        [TestCase(RulesConstants.IssueAddressedReset, "Tests.JsonEventPayloads/IssueAddressedReset_issue_labeled_needs-team-attention.json", RuleState.On)]
        [TestCase(RulesConstants.IssueAddressedReset, "Tests.JsonEventPayloads/IssueAddressedReset_issue_labeled_needs-team-triage.json", RuleState.On)]
        [TestCase(RulesConstants.IssueAddressedReset, "Tests.JsonEventPayloads/IssueAddressedReset_issue_labeled_needs-triage.json", RuleState.On)]
        [TestCase(RulesConstants.IssueAddressedReset, "Tests.JsonEventPayloads/IssueAddressedReset_issue_labeled_service_attention.json", RuleState.On)]
        public async Task TestIssueAddressedReset(string rule, string payloadFile, RuleState ruleState)
        {
            var mockGitHubEventClient = new MockGitHubEventClient(OrgConstants.ProductHeaderName);
            mockGitHubEventClient.RulesConfiguration.Rules[rule] = ruleState;
            var rawJson = TestHelpers.GetTestEventPayload(payloadFile);
            var issueEventPayload = SimpleJsonSerializer.Deserialize<IssueEventGitHubPayload>(rawJson);
            var issueProcessing = CreateIssueProcessingInstance();
            issueProcessing.IssueAddressedReset(mockGitHubEventClient, issueEventPayload);

            // Verify the RuleCheck
            Assert.That(mockGitHubEventClient.RulesConfiguration.RuleEnabled(rule), Is.EqualTo(ruleState == RuleState.On), $"Rule '{rule}' enabled should have been {ruleState == RuleState.On} but RuleEnabled returned {ruleState != RuleState.On}.'");

            var totalUpdates = await mockGitHubEventClient.ProcessPendingUpdates(issueEventPayload.Repository.Id, issueEventPayload.Issue.Number);
            if (RuleState.On == ruleState)
            {
                // There should be one update, the NoRecentActivity label removed
                Assert.That(totalUpdates, Is.EqualTo(1), $"The number of updates should have been 1 but was instead, {totalUpdates}");

                // Verify that IssueAddressed was added to the remove list
                Assert.That(mockGitHubEventClient.GetLabelsToRemove().Contains(TriageLabelConstants.IssueAddressed), Is.True, $"Labels to remove should contain {TriageLabelConstants.IssueAddressed} and does not.");
            }
            else
            {
                Assert.That(totalUpdates, Is.EqualTo(0), $"{rule} is {ruleState} and should not have produced any updates.");
            }
        }

        private static IssueProcessing CreateIssueProcessingInstance()
        {
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<IssueProcessing>();
            return new IssueProcessing(logger);
        }
    }
}
