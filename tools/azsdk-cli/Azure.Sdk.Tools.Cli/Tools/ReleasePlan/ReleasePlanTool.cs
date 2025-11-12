// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlan;
using Azure.Sdk.Tools.Cli.Services;
using ModelContextProtocol.Server;
using Octokit;

namespace Azure.Sdk.Tools.Cli.Tools.ReleasePlan
{
    [Description("Release Plan Tool type that contains tools to connect to Azure DevOps to get release plan work item")]
    [McpServerToolType]
    public partial class ReleasePlanTool(  // partial class required due to source generated regex
        IDevOpsService devOpsService,
        ITypeSpecHelper typeSpecHelper,
        ILogger<ReleasePlanTool> logger,
        IUserHelper userHelper,
        IGitHubService githubService,
        IEnvironmentHelper environmentHelper
    ) : MCPMultiCommandTool
    {
        public override CommandGroup[] CommandHierarchy { get; set; } = [new("release-plan", "Manage release plans in AzureDevops")];

        // Commands
        private const string getReleasePlanDetailsCommandName = "get";
        private const string createReleasePlanCommandName = "create";
        private const string linkNamespaceApprovalIssueCommandName = "link-namespace-approval";
        private const string sdkBotEmail = "azuresdk@microsoft.com";

        // Options
        private readonly Option<int> releasePlanNumberOpt = new("--release-plan-id")
        {
            Description = "Release Plan ID",
            Required = false,
        };

        private readonly Option<int> workItemIdOpt = new("--work-item-id", "-w")
        {
            Description = "Work Item ID",
            Required = false,
        };

        private readonly Option<string> typeSpecProjectPathOpt = new("--typespec-path")
        {
            Description = "Path to TypeSpec project",
            Required = true,
        };

        private readonly Option<string> targetReleaseOpt = new("--release-month")
        {
            Description = "SDK release target month(Month YYYY)",
            Required = true,
        };

        private readonly Option<string> serviceTreeIdOpt = new("--service-tree")
        {
            Description = "Service tree ID",
            Required = true,
        };

        private readonly Option<string> productTreeIdOpt = new("--product")
        {
            Description = "Product service tree ID",
            Required = true,
        };

        private readonly Option<string> apiVersionOpt = new("--api-version")
        {
            Description = "API version",
            Required = true,
        };

        private readonly Option<string> pullRequestOpt = new("--pull-request")
        {
            Description = "Api spec pull request URL",
            Required = true,
        };

        private readonly Option<string> sdkReleaseTypeOpt = new("--sdk-type")
        {
            Description = "SDK release type: beta or preview",
            Required = true,
        };

        private readonly Option<bool> isTestReleasePlanOpt = new("--test-release")
        {
            Description = "Create release plan in test environment",
            Required = false,
            DefaultValueFactory = _ => false,
        };

        private readonly Option<string> userEmailOpt = new("--user-email")
        {
            Description = "User email for release plan creation",
            Required = false,
        };

        private readonly Option<string> namespaceApprovalIssueOpt = new("--namespace-approval-issue")
        {
            Description = "Namespace approval issue URL",
            Required = true,
        };

        //Namespace approval repo details
        private const string namespaceApprovalRepoName = "azure-sdk";
        private const string namespaceApprovalRepoOwner = "Azure";


        private readonly HashSet<string> languagesforDataplane = [
            ".NET","Java","Python","JavaScript"
        ];

        private readonly HashSet<string> languagesforMgmtplane = [
           ".NET","Java","Python","JavaScript","Go"
       ];

        [GeneratedRegex("https:\\/\\/github.com\\/Azure\\/azure-sdk\\/issues\\/([0-9]+)")]
        private static partial Regex NameSpaceIssueUrlRegex();

        [GeneratedRegex("https:\\/\\/github.com\\/Azure\\/azure-rest-api-specs\\/pull\\/[0-9]+\\/?")]
        private static partial Regex PullRequestUrlRegex();

        [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}(-preview)?$")]
        private static partial Regex ApiVersionRegex();

        protected override List<Command> GetCommands() =>
        [
            new(getReleasePlanDetailsCommandName, "Get release plan details") { workItemIdOpt, releasePlanNumberOpt },
            new(createReleasePlanCommandName, "Create a release plan")
            {
                typeSpecProjectPathOpt,
                targetReleaseOpt,
                serviceTreeIdOpt,
                productTreeIdOpt,
                apiVersionOpt,
                pullRequestOpt,
                sdkReleaseTypeOpt,
                userEmailOpt,
                isTestReleasePlanOpt,
            },
            new(linkNamespaceApprovalIssueCommandName, "Link namespace approval issue to release plan") { workItemIdOpt, namespaceApprovalIssueOpt }
        ];

        public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
        {
            var commandParser = parseResult;
            var command = commandParser.CommandResult.Command.Name;
            switch (command)
            {
                case getReleasePlanDetailsCommandName:
                    var workItemId = commandParser.GetValue(workItemIdOpt);
                    var releasePlanNumber = commandParser.GetValue(releasePlanNumberOpt);
                    return await GetReleasePlan(workItem: workItemId, releasePlanId: releasePlanNumber);

                case createReleasePlanCommandName:
                    var typeSpecProjectPath = commandParser.GetValue(typeSpecProjectPathOpt);
                    var targetReleaseMonthYear = commandParser.GetValue(targetReleaseOpt);
                    var serviceTreeId = commandParser.GetValue(serviceTreeIdOpt);
                    var productTreeId = commandParser.GetValue(productTreeIdOpt);
                    var specApiVersion = commandParser.GetValue(apiVersionOpt);
                    var specPullRequestUrl = commandParser.GetValue(pullRequestOpt);
                    var sdkReleaseType = commandParser.GetValue(sdkReleaseTypeOpt);
                    var isTestReleasePlan = commandParser.GetValue(isTestReleasePlanOpt);
                    var userEmail = commandParser.GetValue(userEmailOpt);
                    return await CreateReleasePlan(
                        typeSpecProjectPath,
                        targetReleaseMonthYear,
                        serviceTreeId,
                        productTreeId,
                        specApiVersion,
                        specPullRequestUrl,
                        sdkReleaseType,
                        userEmail: userEmail,
                        isTestReleasePlan: isTestReleasePlan
                    );

                case linkNamespaceApprovalIssueCommandName:
                    return await LinkNamespaceApprovalIssue(commandParser.GetValue(workItemIdOpt), commandParser.GetValue(namespaceApprovalIssueOpt));

                default:
                    logger.LogError("Unknown command: {command}", command);
                    return new DefaultCommandResponse { ResponseError = $"Unknown command: '{command}'" };
            }
        }


        [McpServerTool(Name = "azsdk_get_release_plan_for_spec_pr"), Description("Get release plan for API spec pull request. This tool should be used only if work item Id is unknown.")]
        public async Task<ReleaseWorkflowResponse> GetReleasePlanForPullRequest(string pullRequestLink)
        {
            var response = new ReleaseWorkflowResponse();

            try
            {
                ValidatePullRequestUrl(pullRequestLink);
                var releasePlan = await devOpsService.GetReleasePlanAsync(pullRequestLink) ?? throw new Exception("No release plan associated with pull request link");
                response.Status = "Success";
                response.Details.Add($"Release Plan: {JsonSerializer.Serialize(releasePlan)}");
                return response;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to get release plan details");
                response.Status = "Failed";
                response.Details.Add($"Failed to get release plan details: {ex.Message}");
                return response;
            }
        }

        [McpServerTool(Name = "azsdk_get_release_plan"), Description("Get Release Plan: Get release plan work item details for a given work item id or release plan Id.")]
        public async Task<ReleaseWorkflowResponse> GetReleasePlan(int workItem = 0, int releasePlanId = 0)
        {
            try
            {
                if (workItem == 0 && releasePlanId == 0)
                {
                    return new ReleaseWorkflowResponse { ResponseError = "Either work item ID or release plan number must be provided." };
                }
                var releasePlan = workItem != 0 ? await devOpsService.GetReleasePlanForWorkItemAsync(workItem) : await devOpsService.GetReleasePlanAsync(releasePlanId);
                if (releasePlan == null)
                {
                    return new ReleaseWorkflowResponse { ResponseError = "Failed to get release plan details." };
                }
                return new ReleaseWorkflowResponse
                {
                    Details = [$"Release Plan: {JsonSerializer.Serialize(releasePlan)}"]
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to get release plan details");
                return new ReleaseWorkflowResponse { ResponseError = $"Failed to get release plan details: {ex.Message}" };
            }
        }

        private void ValidatePullRequestUrl(string specPullRequestUrl)
        {
            if (string.IsNullOrEmpty(specPullRequestUrl))
            {
                throw new Exception("API spec pull request URL is required to create a release plan.");
            }

            var match = PullRequestUrlRegex().Match(specPullRequestUrl);

            if (!match.Success)
            {
                throw new Exception($"Invalid spec pull request URL '{specPullRequestUrl}'. It should be a valid GitHub pull request to azure-rest-api-specs repo.");
            }

        }

        private void ValidateCreateReleasePlanInputAsync(string typeSpecProjectPath, string serviceTreeId, string productTreeId, string specPullRequestUrl, string sdkReleaseType, string specApiVersion)
        {
            ValidatePullRequestUrl(specPullRequestUrl);            

            if (string.IsNullOrEmpty(typeSpecProjectPath))
            {
                throw new Exception("TypeSpec project path is empty. Cannot create a release plan without a TypeSpec project root path");
            }

            var isValidApiVersion = ApiVersionRegex().Match(specApiVersion);

            if (!isValidApiVersion.Success)
            {
                throw new Exception("Invalid API version format. Supported formats are: yyyy-MM-dd or yyyy-MM-dd-preview");
            }

            var supportedReleaseTypes = new[] { "beta", "stable" };
            if (!supportedReleaseTypes.Contains(sdkReleaseType))
            {
                throw new Exception($"Invalid SDK release type. Supported release types are: {string.Join(", ", supportedReleaseTypes)}");
            }

            var repoRoot = typeSpecHelper.GetSpecRepoRootPath(typeSpecProjectPath);

            // Ensure a release plan is created only if the API specs pull request is in a public repository.
            if (!typeSpecHelper.IsRepoPathForPublicSpecRepo(repoRoot))
            {
                throw new Exception("""
                    SDK generation and release require the API specs pull request to be in the public azure-rest-api-specs repository.
                    Please create a pull request in the public Azure/azure-rest-api-specs repository to move your specs changes to public.
                    A release plan cannot be created for SDK generation using a pull request in a private repository.
                    """);
            }

            if (!Guid.TryParse(serviceTreeId, out _))
            {
                throw new Exception($"Service tree ID '{serviceTreeId}' is not a valid GUID.");
            }

            if (!Guid.TryParse(productTreeId, out _))
            {
                throw new Exception($"Product tree ID '{productTreeId}' is not a valid GUID.");
            }
        }

        [McpServerTool(Name = "azsdk_create_release_plan"), Description("Create Release Plan")]
        public async Task<ReleasePlanResponse> CreateReleasePlan(string typeSpecProjectPath, string targetReleaseMonthYear, string serviceTreeId, string productTreeId, string specApiVersion, string specPullRequestUrl, string sdkReleaseType, string userEmail = "", bool isTestReleasePlan = false)
        {
            try
            {
                sdkReleaseType = sdkReleaseType?.ToLower() ?? "";
                var sdkReleaseTypeMappings = new Dictionary<string, string>
                {
                    { "ga", "stable" },
                    { "preview", "beta" }
                };
                if (sdkReleaseTypeMappings.TryGetValue(sdkReleaseType, out var mappedType))
                {
                    sdkReleaseType = mappedType;
                }
                
                ValidateCreateReleasePlanInputAsync(typeSpecProjectPath, serviceTreeId, productTreeId, specPullRequestUrl, sdkReleaseType, specApiVersion);

                // Check for existing release plan for the given pull request URL.
                logger.LogInformation("Checking for existing release plan for pull request URL: {specPullRequestUrl}", specPullRequestUrl);
                var existingReleasePlan = await devOpsService.GetReleasePlanAsync(specPullRequestUrl);
                if (existingReleasePlan != null && existingReleasePlan.WorkItemId > 0)
                {
                    return new ReleasePlanResponse
                    {
                        Message = $"Release plan already exists for the pull request: {specPullRequestUrl}. Release plan link: {existingReleasePlan.ReleasePlanLink}",
                        ReleasePlanDetails = existingReleasePlan
                    };                    
                }

                // Check environment variable to determine if this should be a test release plan
                var isAgentTesting = environmentHelper.GetBooleanVariable("AZSDKTOOLS_AGENT_TESTING", false);
                if (isAgentTesting)
                {
                    isTestReleasePlan = true;
                    logger.LogInformation("AZSDKTOOLS_AGENT_TESTING environment variable is set to true, creating test release plan");
                }

                var specType = typeSpecHelper.IsValidTypeSpecProjectPath(typeSpecProjectPath) ? "TypeSpec" : "OpenAPI";
                var isMgmt = typeSpecHelper.IsTypeSpecProjectForMgmtPlane(typeSpecProjectPath);
                var specProject = typeSpecHelper.GetTypeSpecProjectRelativePath(typeSpecProjectPath);
                logger.LogInformation("Attempting to retrieve current user email.");

                var email = await userHelper.GetUserEmail();
                if (email != sdkBotEmail)
                {
                    userEmail = email;
                    logger.LogInformation("Using current user email to submit release plan: {userEmail}", userEmail);
                }
                else if (string.IsNullOrEmpty(userEmail))
                {
                    throw new InvalidOperationException("Cannot create release plan using SDK bot email. Please provide a valid user email address.");
                }

                logger.LogInformation("User email for release plan submission: {userEmail}", userEmail);

                var releasePlan = new ReleasePlanDetails
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
                    return new ReleasePlanResponse
                    { 
                        ResponseError = "Failed to create release plan work item.",
                        TypeSpecProject = specProject,
                        PackageType = isMgmt? SdkType.Management : SdkType.Dataplane
                    };
                }
                else
                {
                    if (workItem.Id is int workItemId)
                    {
                        releasePlan.WorkItemId = workItemId;
                    }

                    if (workItem.Fields.TryGetValue("Custom.ReleasePlanId", out var value) && value is int releasePlanId)
                    {
                        releasePlan.ReleasePlanId = releasePlanId;
                    }

                    if (workItem.Fields.TryGetValue("Custom.ReleasePlanLink", out value) && value is string releasePlanLink)
                    {
                        releasePlan.ReleasePlanLink = releasePlanLink;
                    }

                    return new ReleasePlanResponse
                    {
                        Message = "Release plan is being created",
                        ReleasePlanDetails = releasePlan,
                        NextSteps = [$"Get release plan from `workItem`, work item value: {releasePlan.WorkItemId}"],
                        TypeSpecProject = specProject,
                        PackageType = isMgmt ? SdkType.Management : SdkType.Dataplane
                    };
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to create release plan work item");
                return new ReleasePlanResponse { ResponseError = $"Failed to create release plan work item: {ex.Message}" };
            }
        }

        [McpServerTool(Name = "azsdk_update_sdk_details_in_release_plan"), Description("Update the SDK details in the release plan work item. This tool is called to update SDK language and package name in the release plan work item." +
            " sdkDetails parameter is a JSON of list of SDKInfo and each SDKInfo contains Language and PackageName as properties.")]
        public async Task<DefaultCommandResponse> UpdateSDKDetailsInReleasePlan(int releasePlanWorkItemId, string sdkDetails)
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
                logger.LogInformation("Updating SDK details in release plan work item ID: {ReleasePlanWorkItemId}", releasePlanWorkItemId);
                logger.LogDebug("SDK details to update: {SdkDetails}", sdkDetails);
                // Fix for CS8600: Ensure sdkDetails is not null before deserialization
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                List<SDKInfo>? SdkInfos = JsonSerializer.Deserialize<List<SDKInfo>>(sdkDetails, options);
                if (SdkInfos == null)
                {
                    return "Failed to deserialize SDK details.";
                }

                // Get release plan
                var releasePlan = await devOpsService.GetReleasePlanForWorkItemAsync(releasePlanWorkItemId);
                if (releasePlan == null)
                {
                    return new DefaultCommandResponse { ResponseError = $"No release plan found with work item ID {releasePlanWorkItemId}" };
                }

                var supportedLanguages = releasePlan.IsManagementPlane ? languagesforMgmtplane : languagesforDataplane;
                // Validate SDK language name
                if (SdkInfos.Any(sdk => !supportedLanguages.Contains(sdk.Language, StringComparer.OrdinalIgnoreCase)))
                {
                    return $"Unsupported SDK language found. Supported languages are: {string.Join(", ", supportedLanguages)}";
                }

                // Validate SDK Package names
                var languagePrefixMap = new Dictionary<string, string>
                (StringComparer.OrdinalIgnoreCase)
                {
                    { "JavaScript", "@azure/" },
                    { "Go", "sdk/" },
                };

                var invalidSdks = SdkInfos.Where(sdk => languagePrefixMap.TryGetValue(sdk.Language, out var prefix) && !sdk.PackageName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
                if (invalidSdks.Any())
                {
                    var errorDetails = string.Join("; ", invalidSdks.Select(sdk => $"{sdk.Language} -> {sdk.PackageName}"));
                    var prefixRules = string.Join(", ", languagePrefixMap.Select(kvp => $"{kvp.Key}: starts with {kvp.Value}"));
                    return new DefaultCommandResponse
                    {
                        ResponseError = $"Unsupported package name(s) detected: {errorDetails}. Package names must follow these rules: {prefixRules}",
                        NextSteps = ["Prompt the user to update the package name to match the required prefix for its language."]
                    };
                }
                
                StringBuilder sb = new();
                // Update SDK package name and languages in work item
                var updated = await devOpsService.UpdateReleasePlanSDKDetailsAsync(releasePlanWorkItemId, SdkInfos);
                if (!updated)
                {
                    return new DefaultCommandResponse { ResponseError = "Failed to update release plan with SDK details." };
                }
                else
                {
                    sb.Append("Updated SDK details in release plan.").AppendLine();
                    foreach (var sdk in SdkInfos)
                    {
                        sb.AppendLine($"Language: {sdk.Language}, Package name: {sdk.PackageName}");
                    }
                }

                // Check if any language is excluded
                var excludedLanguages = supportedLanguages.Except(SdkInfos.Select(sdk => sdk.Language), StringComparer.OrdinalIgnoreCase);
                if (excludedLanguages.Any())
                {
                    logger.LogDebug("Languages excluded in release plan. Work Item: {releasePlanWorkItemId}, languages: {excludedLanguages}", releasePlanWorkItemId, string.Join(", ", excludedLanguages));
                    sb.AppendLine($"Important: The following languages were excluded in the release plan. SDK must be released for all languages. [{string.Join(", ", supportedLanguages)}]");
                    sb.AppendLine("Explanation is required for any language exclusion. Please provide a justification for each excluded language.");

                    // Mark excluded language as 'Requested' in the release plan work item.
                    Dictionary<string, string> fieldsToUpdate = [];
                    foreach (var lang in excludedLanguages)
                    {
                        fieldsToUpdate[$"Custom.ReleaseExclusionStatusFor{DevOpsService.MapLanguageToId(lang)}"] = "Requested";
                    }
                    await devOpsService.UpdateWorkItemAsync(releasePlanWorkItemId, fieldsToUpdate);
                    logger.LogDebug("Marked excluded languages as 'Requested' in release plan work item {releasePlanWorkItemId}.", releasePlanWorkItemId);
                }

                return new DefaultCommandResponse
                {
                    Message = sb.ToString(),
                    NextSteps = excludedLanguages.Any() && string.IsNullOrEmpty(releasePlan.LanguageExclusionRequesterNote) ? ["Prompt the user for justification for excluded languages and update it in the release plan."] : []
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to update release plan with SDK details");
                return new DefaultCommandResponse { ResponseError = $"Failed to update release plan with SDK details: {ex.Message}" };
            }
        }

        [McpServerTool(Name = "azsdk_link_namespace_approval_issue"), Description("Link package namespace approval issue to release plan(required only for management plan). This requires GitHub issue URL for the namespace approval request and release plan work item id.")]
        public async Task<DefaultCommandResponse> LinkNamespaceApprovalIssue(int releasePlanWorkItemId, string namespaceApprovalIssue)
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
                if (issue == null)
                {
                    return $"Failed to verify approval status. Namespace approval issue #{namespaceApprovalIssue} not found in {namespaceApprovalRepoOwner}/{namespaceApprovalRepoName}.";
                }

                // Verify if issue has label 'mgmt-namespace-review'
                if (!issue.Labels.Any(label => label.Name.Equals("mgmt-namespace-review", StringComparison.OrdinalIgnoreCase)))
                {
                    return $"Namespace approval issue #{namespaceApprovalIssue} does not have the required 'mgmt-namespace-review' label.";
                }

                // Verify if issue is closed
                StringBuilder response = new();
                if (issue.State == ItemState.Open)
                {
                    response.Append($"Namespace approval is still pending. Please check {issue.HtmlUrl} for more details.");
                }
                else
                {
                    response.Append($"Package namespace has been approved.");
                }

                var failed = false;
                var updated = await devOpsService.LinkNamespaceApprovalIssueAsync(releasePlanWorkItemId, issue.HtmlUrl);
                if (!updated)
                {
                    failed = true;
                    response.Append("Failed to link namespace approval issue to release plan.");
                }
                else
                {
                    response.Append("Successfully linked the namespace approval issue to release plan");
                }

                if (failed)
                {
                    return new DefaultCommandResponse { ResponseError = response.ToString() };
                }

                return response.ToString();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to verify package namespace approval");
                return new DefaultCommandResponse
                {
                    ResponseError = $"Failed to verify package namespace approval: {ex.Message}"
                };
            }
        }

        [McpServerTool(Name = "azsdk_update_language_exclusion_justification"), Description("Update language exclusion justification in release plan work item. This tool is called to update justification for excluded languages in the release plan. " +
            "Optionally pass a language name to explicitly request exclusion for a specific language.")]
        public async Task<DefaultCommandResponse> UpdateLanguageExclusionJustification(int releasePlanWorkItem, string justification, string language = "")
        {
            try
            {
                if (releasePlanWorkItem <= 0)
                {
                    return "Invalid release plan work item ID.";
                }
                if (string.IsNullOrEmpty(justification))
                {
                    return "No justification provided to update the release plan.";
                }

                // Get release plan
                var releasePlan = await devOpsService.GetReleasePlanForWorkItemAsync(releasePlanWorkItem);
                if (releasePlan == null)
                {
                    return new DefaultCommandResponse { ResponseError = $"No release plan found with work item ID {releasePlanWorkItem}" };
                }

                // Update language exclusion justification in work item
                Dictionary<string, string> fieldsToUpdate = new()
                {
                    { "Custom.ReleaseExclusionRequestNote", justification }
                };

                if (!string.IsNullOrEmpty(language))
                {
                    fieldsToUpdate[$"Custom.ReleaseExclusionStatusFor{DevOpsService.MapLanguageToId(language)}"] = "Requested";
                }

                var updatedWorkItem = await devOpsService.UpdateWorkItemAsync(releasePlanWorkItem, fieldsToUpdate);
                if (updatedWorkItem == null)
                {
                    return new DefaultCommandResponse { ResponseError = $"Failed to update the language exclusion justification in release plan work item {releasePlanWorkItem}." };
                }
                else
                {
                    return new DefaultCommandResponse
                    {
                        Message = "Updated language exclusion justification in release plan.",
                        NextSteps = []
                    };
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to update release plan with language exclusion justification in release plan work item {ReleasePlanWorkItem}", releasePlanWorkItem);
                return new DefaultCommandResponse { ResponseError = $"Failed to update release plan with language exclusion justification: {ex.Message}" };
            }
        }
    }
}
