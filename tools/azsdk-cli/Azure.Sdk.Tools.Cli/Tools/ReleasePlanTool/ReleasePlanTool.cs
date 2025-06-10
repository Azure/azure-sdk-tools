// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using ModelContextProtocol.Server;
using Octokit;

namespace Azure.Sdk.Tools.Cli.Tools
{
    [Description("Release Plan Tool type that contains tools to connect to Azure DevOps to get release plan work item")]
    [McpServerToolType]
    public partial class ReleasePlanTool(IDevOpsService devOpsService, ITypeSpecHelper typeSpecHelper, ILogger<ReleasePlanTool> logger, IOutputService output, IUserHelper userHelper, IGitHubService githubService) : MCPTool
    {
        //Namespace approval repo details
        private const string namespaceApprovalRepoName = "azure-sdk";
        private const string namespaceApprovalRepoOwner = "Azure";

        // Commands
        private const string getReleasePlanDetailsCommandName = "get";
        private const string createReleasePlanCommandName = "create";
        private const string linkNamespaceApprovalIssueCommandName = "link-namespace-approval";

        // Options
        private readonly Option<int> releasePlanNumberOpt = new(["--release-plan-id",], "Release Plan ID") { IsRequired = false };
        private readonly Option<int> workItemIdOpt = new(["--work-item-id", "-w"], "Work Item ID") { IsRequired = false };
        private readonly Option<string> typeSpecProjectPathOpt = new(["--typespec-path"], "Path to TypeSpec project") { IsRequired = true };
        private readonly Option<string> targetReleaseOpt = new(["--release-month"], "SDK release target month(Month YYYY)");
        private readonly Option<string> serviceTreeIdOpt = new(["--service-tree"], "Service tree ID");
        private readonly Option<string> productTreeIdOpt = new(["--product"], "Product service tree ID");
        private readonly Option<string> apiVersionOpt = new(["--api-version"], "API version");
        private readonly Option<string> pullRequestOpt = new(["--pull-request"], "Api spec pull request URL");
        private readonly Option<string> sdkReleaseTypeOpt = new(["--sdk-type"], "SDK release type: beta or preview");
        private readonly Option<bool> isTestReleasePlanOpt = new(["--test-release"], () => false, "Create release plan in test environment") { IsRequired = false };
        private readonly Option<string> userEmailOpt = new(["--user-email"], "User email for release plan creation") { IsRequired = false };
        private readonly Option<string> namespaceApprovalIssueOpt = new Option<string>(["--namespace-approval-issue"], "Namespace approval issue URL") { IsRequired = true };

        private readonly HashSet<string> supportedLanguages = [
            ".NET","Java","Python","JavaScript","Go"
        ];

        [GeneratedRegex("https:\\/\\/github.com\\/Azure\\/azure-sdk\\/issues\\/([0-9]+)")]
        private static partial Regex NameSpaceIssueUrlRegex();


        [McpServerTool, Description("Get release plan for API spec pull request. This tool should be used only if work item Id is unknown.")]
        public async Task<string> GetReleasePlanForPullRequest(string pullRequestLink)
        {
            try
            {
                List<string> releasePlanList = [];
                var releasePlan = await devOpsService.GetReleasePlanAsync(pullRequestLink);
                var _out = releasePlan == null ? "Failed to get release plan details." :
                    $"Release Plan: {JsonSerializer.Serialize(releasePlan)}";
                return output.Format(_out);
            }
            catch (Exception ex)
            {
                logger.LogError("Failed to get release plan details: {exception}", ex.Message);
                return output.Format($"Failed to get release plan details: {ex.Message}");
            }
        }

        public override Command GetCommand()
        {
            Command command = new("release-plan");
            var subCommands = new[]
            {
                new Command(getReleasePlanDetailsCommandName, "Get release plan details") {workItemIdOpt, releasePlanNumberOpt},
                new Command(createReleasePlanCommandName, "Create a release plan") { typeSpecProjectPathOpt, targetReleaseOpt, serviceTreeIdOpt, productTreeIdOpt, apiVersionOpt, pullRequestOpt, sdkReleaseTypeOpt, userEmailOpt, isTestReleasePlanOpt },
                new Command(linkNamespaceApprovalIssueCommandName, "Link namespace approval issue to release plan") { workItemIdOpt, namespaceApprovalIssueOpt }
            };

            foreach (var subCommand in subCommands)
            {
                subCommand.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });
                command.AddCommand(subCommand);
            }
            return command;
        }

        public override async Task HandleCommand(InvocationContext ctx, CancellationToken ct)
        {
            var commandParser = ctx.ParseResult;
            var command = commandParser.CommandResult.Command.Name;
            switch (command)
            {
                case getReleasePlanDetailsCommandName:
                    var workItemId = commandParser.GetValueForOption(workItemIdOpt);
                    var releasePlanNumber = commandParser.GetValueForOption(releasePlanNumberOpt);
                    var releasePlanDetails = await GetReleasePlanAsync(workItem: workItemId, releasePlanId: releasePlanNumber);
                    output.Output($"Release plan details: {releasePlanDetails}");
                    return;

                case createReleasePlanCommandName:
                    var typeSpecProjectPath = commandParser.GetValueForOption(typeSpecProjectPathOpt);
                    var targetReleaseMonthYear = commandParser.GetValueForOption(targetReleaseOpt);
                    var serviceTreeId = commandParser.GetValueForOption(serviceTreeIdOpt);
                    var productTreeId = commandParser.GetValueForOption(productTreeIdOpt);
                    var specApiVersion = commandParser.GetValueForOption(apiVersionOpt);
                    var specPullRequestUrl = commandParser.GetValueForOption(pullRequestOpt);
                    var sdkReleaseType = commandParser.GetValueForOption(sdkReleaseTypeOpt);
                    var isTestReleasePlan = commandParser.GetValueForOption(isTestReleasePlanOpt);
                    var userEmail = commandParser.GetValueForOption(userEmailOpt);
                    var releasePlan = await CreateReleasePlan(typeSpecProjectPath, targetReleaseMonthYear, serviceTreeId, productTreeId, specApiVersion, specPullRequestUrl, sdkReleaseType, userEmail: userEmail, isTestReleasePlan: isTestReleasePlan);
                    output.Output($"Release plan created: {releasePlan}");
                    return;

                case linkNamespaceApprovalIssueCommandName:
                    var linkResponse = await LinkNamespaceApprovalIssueAsync(commandParser.GetValueForOption(workItemIdOpt), commandParser.GetValueForOption(namespaceApprovalIssueOpt));
                    output.Output($"Link namespace approval issue response: {linkResponse}");
                    return;

                default:
                    logger.LogError("Unknown command: {command}", command);
                    SetFailure();
                    return;
            }
        }

        [McpServerTool, Description("Get Release Plan: Get release plan work item details for a given work item id or release plan Id.")]
        public async Task<string> GetReleasePlanAsync(int workItem = 0, int releasePlanId = 0)
        {
            try
            {
                if (workItem == 0 && releasePlanId == 0)
                {
                    return "Either work item ID or release plan number must be provided.";
                }
                var releasePlan = workItem != 0 ? await devOpsService.GetReleasePlanForWorkItemAsync(workItem) : await devOpsService.GetReleasePlanAsync(releasePlanId);
                var releasePlanText = releasePlan != null ? JsonSerializer.Serialize(releasePlan) :
                       "Failed to get release plan details.";
                return releasePlanText;
            }
            catch (Exception ex)
            {
                SetFailure();
                return $"Failed to get release plan details: {ex.Message}";
            }
        }

        [McpServerTool, Description("Create Release Plan work item.")]
        public async Task<string> CreateReleasePlan(string typeSpecProjectPath, string targetReleaseMonthYear, string serviceTreeId, string productTreeId, string specApiVersion, string specPullRequestUrl, string sdkReleaseType, string userEmail = "", bool isTestReleasePlan = false)
        {
            try
            {
                if (string.IsNullOrEmpty(typeSpecProjectPath))
                {
                    throw new Exception("TypeSpec project path is empty. Cannot create a release plan without a TypeSpec project root path");
                }

                var specType = typeSpecHelper.IsValidTypeSpecProjectPath(typeSpecProjectPath) ? "TypeSpec" : "OpenAPI";
                var isMgmt = typeSpecHelper.IsTypeSpecProjectForMgmtPlane(typeSpecProjectPath);
                var repoRoot = typeSpecHelper.GetSpecRepoRootPath(typeSpecProjectPath);

                // Ensure a release plan is created only if the API specs pull request is in a public repository.
                if (!typeSpecHelper.IsRepoPathForPublicSpecRepo(repoRoot))
                {
                    return """
                        SDK generation and release require the API specs pull request to be in the public azure-rest-api-specs repository.
                        Please create a pull request in the public Azure/azure-rest-api-specs repository to move your specs changes to public.
                        A release plan cannot be created for SDK generation using a pull request in a private repository.
                        """;
                }

                sdkReleaseType = sdkReleaseType?.ToLower() ?? "";
                var supportedReleaseTypes = new[] { "beta", "stable" };
                if (!supportedReleaseTypes.Contains(sdkReleaseType))
                {
                    return $"Invalid SDK release type. Supported release types are: {string.Join(", ", supportedReleaseTypes)}";
                }

                if (string.IsNullOrEmpty(userEmail))
                {
                    logger.LogInformation("User email not provided. Attempting to retrieve current user email.");
                    userEmail = await userHelper.GetUserEmail();
                    logger.LogInformation("User email not provided. Using current user email to submit release plan: {userEmail}", userEmail);
                }

                var releasePlan = new ReleasePlan
                {
                    SDKReleaseMonth = targetReleaseMonthYear,
                    ServiceTreeId = serviceTreeId,
                    ProductTreeId = productTreeId,
                    SpecAPIVersion = specApiVersion,
                    SpecType = specType,
                    IsManagementPlane = isMgmt,
                    IsDataPlane = !isMgmt,
                    SpecPullRequests = [specPullRequestUrl],
                    IsTestReleasePlan = isTestReleasePlan,
                    SDKReleaseType = sdkReleaseType,
                    IsCreatedByAgent = true,
                    ReleasePlanSubmittedByEmail = userEmail
                };
                var workItem = await devOpsService.CreateReleasePlanWorkItemAsync(releasePlan);
                if (workItem == null)
                {
                    SetFailure();
                    return "Failed to create release plan work item.";
                }
                else
                {
                    return output.Format(workItem);
                }
            }
            catch (Exception ex)
            {
                SetFailure();
                return $"Failed to create release plan work item: {ex.Message}";
            }
        }

        [McpServerTool, Description("Update the SDK details in the release plan work item. This tool is called to update SDK language and package name in the release plan work item." +
            " sdkDetails parameter is a JSON of list of SDKInfo and each SDKInfo contains Language and PackageName as properties.")]
        public async Task<string> UpdateSDKDetailsInReleasePlan(int releasePlanWorkItemId, string sdkDetails)
        {
            try
            {
                if (releasePlanWorkItemId <= 0)
                {
                    return "Invalid release plan ID.";
                }

                if (string.IsNullOrEmpty(sdkDetails))
                {
                    return "No SDK information provided to update the release plan.";
                }

                // Fix for CS8600: Ensure sdkDetails is not null before deserialization
                List<SDKInfo>? SdkInfos = JsonSerializer.Deserialize<List<SDKInfo>>(sdkDetails);
                if (SdkInfos == null)
                {
                    return "Failed to deserialize SDK details.";
                }

                // Validate SDK language name
                if (SdkInfos.Any(sdk => !supportedLanguages.Contains(sdk.Language, StringComparer.OrdinalIgnoreCase)))
                {
                    return $"Unsupported SDK language found. Supported languages are: {string.Join(", ", supportedLanguages)}";
                }

                var updated = await devOpsService.UpdateReleasePlanSDKDetailsAsync(releasePlanWorkItemId, SdkInfos);
                if (!updated)
                {
                    SetFailure();
                    return "Failed to update release plan with SDK details.";
                }
                else
                {
                    StringBuilder sb = new("Updated SDK details in release plan.");
                    sb.AppendLine();
                    foreach (var sdk in SdkInfos)
                    {
                        sb.AppendLine($"Language: {sdk.Language}, Package name: {sdk.PackageName}");
                    }
                    return output.Format(sb.ToString());
                }
            }
            catch (Exception ex)
            {
                SetFailure();
                return $"Failed to update release plan with SDK details: {ex.Message}";
            }
        }

        [McpServerTool, Description("Link package namespace approval issue to release plan(required only for management plan). This requires GitHub issue URL for the namespace approval request and release plan work item id.")]
        public async Task<string> LinkNamespaceApprovalIssueAsync(int releasePlanWorkItemId, string namespaceApprovalIssue)
        {
            try
            {
                if (releasePlanWorkItemId <= 0 || string.IsNullOrEmpty(namespaceApprovalIssue))
                {
                    return "Release plan ID and namespace approval issue are required to verify namespace approval status";
                }

                // Get release plan and verify if it is a management plane release plan before linking namespace approval issue
                var releasePlan = await devOpsService.GetReleasePlanForWorkItemAsync(releasePlanWorkItemId);
                if (releasePlan == null)
                {
                    return $"Release plan with ID {releasePlanWorkItemId} not found.";
                }

                if (!releasePlan.IsManagementPlane)
                {
                    return "Namespace approval is only required for management plane release plans. This release plan is not for management plane.";
                }

                var match = NameSpaceIssueUrlRegex().Match(namespaceApprovalIssue);
                // Check if the namespace approval issue is a valid GitHub issue number
                if (!match.Success)
                {
                    return $"Invalid namespace approval issue '{namespaceApprovalIssue}'. It should be a valid GitHub issue in Azure/azure-sdk repo.";
                }
                // Get issue number from the match
                var issueNumber = int.Parse(match.Groups[1].Value);
                var issue = await githubService.GetIssueAsync(namespaceApprovalRepoOwner, namespaceApprovalRepoName, issueNumber);
                if(issue == null)
                {
                    return $"Failed to verify approval status. Namespace approval issue #{namespaceApprovalIssue} not found in {namespaceApprovalRepoOwner}/{namespaceApprovalRepoName}.";
                }

                // Verify if issue has label 'mgmt-namespace-review'
                if (!issue.Labels.Any(label => label.Name.Equals("mgmt-namespace-review", StringComparison.OrdinalIgnoreCase)))
                {
                    return $"Namespace approval issue #{namespaceApprovalIssue} does not have the required 'mgmt-namespace-review' label.";
                }

                // Verify if issue is closed
                StringBuilder response = new ();
                if (issue.State == ItemState.Open)
                {
                    response.Append($"Namespace approval is still pending. Please check {issue.HtmlUrl} for more details.");
                }
                else
                {
                    response.Append($"Package namespace has been approved.");
                }

                var updated = await devOpsService.LinkNamespaceApprovalIssueAsync(releasePlanWorkItemId, issue.HtmlUrl);
                if (!updated)
                {
                    SetFailure();
                    response.Append("Failed to link namespace approval issue to release plan.");
                }
                else
                {
                    response.Append("Successfully linked the namespace approval issue to release plan");
                }
                return output.Format(response.ToString());
            }
            catch (Exception ex)
            {
                SetFailure();
                return $"Failed to verify package namespace approval: {ex.Message}";
            }
        }
    }
}
