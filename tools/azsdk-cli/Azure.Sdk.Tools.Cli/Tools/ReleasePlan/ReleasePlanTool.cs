// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlan;
using Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlanList;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tools.Core;
using ModelContextProtocol.Server;
using Octokit;

namespace Azure.Sdk.Tools.Cli.Tools.ReleasePlan
{
    [Description("Release Plan Tool type that contains tools to connect to Azure DevOps to get release plan work item")]
    [McpServerToolType]
    public partial class ReleasePlanTool(  // partial class required due to source generated regex
        IDevOpsService devOpsService,
        IGitHelper gitHelper,
        ITypeSpecHelper typeSpecHelper,
        ILogger<ReleasePlanTool> logger,
        IUserHelper userHelper,
        IGitHubService githubService,
        IEnvironmentHelper environmentHelper,
        IInputSanitizer inputSanitizer,
        HttpClient httpClient
    ) : MCPMultiCommandTool
    {
        public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.ReleasePlan];

        // Commands
        private const string getReleasePlanDetailsCommandName = "get";
        private const string createReleasePlanCommandName = "create";
        private const string linkNamespaceApprovalIssueCommandName = "link-namespace-approval";
        private const string checkApiReadinessCommandName = "check-api-readiness";
        private const string linkSdkPrCommandName = "link-sdk-pr";
        private const string listOverdueReleasePlansCommandName = "list-overdue";
        private const string updateApiSpecPullRequestCommandName = "update-spec-pr";
        private const string getServiceDetailsCommandName = "get-service-details";
        private const string abandonReleasePlanCommandName = "abandon";

        // MCP Tool Names
        private const string GetReleasePlanForSpecPrToolName = "azsdk_get_release_plan_for_spec_pr";
        private const string GetReleasePlanToolName = "azsdk_get_release_plan";
        private const string CreateReleasePlanToolName = "azsdk_create_release_plan";
        private const string UpdateSdkDetailsToolName = "azsdk_update_sdk_details_in_release_plan";
        private const string LinkNamespaceApprovalToolName = "azsdk_link_namespace_approval_issue";
        private const string UpdateLanguageExclusionToolName = "azsdk_update_language_exclusion_justification";
        private const string CheckApiSpecReadyToolName = "azsdk_check_api_spec_ready_for_sdk";
        private const string LinkSdkPullRequestToolName = "azsdk_link_sdk_pull_request_to_release_plan";
        private const string UpdateApiSpecPullRequestToolName = "azsdk_update_api_spec_pull_request_in_release_plan";
        private const string GetServiceDetailsToolName = "azsdk_get_service_details_by_typespec_path";
        private const string AbandonReleasePlanToolName = "azsdk_abandon_release_plan";

        // Options
        private readonly Option<int> releasePlanNumberOpt = new("--release-plan-id", "--release-plan")
        {
            Description = "Release Plan ID",
            Required = false,
        };

        private readonly Option<int> workItemIdOpt = new("--work-item-id", "--workitem-id", "-w")
        {
            Description = "Work Item ID",
            Required = false,
        };

        private readonly Option<string> typeSpecProjectPathOpt = new("--typespec-path")
        {
            Description = "Path to TypeSpec project",
            Required = true,
        };

        private readonly Option<string> typeSpecProjectOpt = new("--typespec-project")
        {
            Description = "TypeSpec project path to find product information",
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

        private readonly Option<string> pullRequestOpt = new("--pull-request", "--url")
        {
            Description = "Api spec pull request URL",
            Required = true,
        };

        private readonly Option<string> sdkReleaseTypeOpt = new("--sdk-type")
        {
            Description = "SDK release type: beta or stable",
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

        private readonly Option<bool> forceCreateReleasePlanOpt = new("--force")
        {
            Description = "Force creation of release plan even if one already exists",
            Required = true,
        };

        private readonly Option<string> languageOpt = new("--language")
        {
            Description = "SDK language, Options[.NET, Java, JavaScript, Go, Python]",
            Required = true,
        };

        private readonly Option<int> pullRequestNumberOpt = new("--pr")
        {
            Description = "Pull request number",
            Required = false,
        };

        private readonly Option<bool> notifyOwnersOpt = new("--notify-owners")
        {
            Description = "Send email notification to owners of overdue release plans",
            Required = false,
        };

        private readonly Option<string> azureSDKEmailerUriOpt = new("--emailer-uri")
        {
            Description = "The Uri of the app used to send email notifications",
            Required = false,
        };

        private const string sdkBotEmail = "azuresdk@microsoft.com";
        private const string sdkApexEmail = "azsdkapex@microsoft.com";
        private static readonly string DEFAULT_BRANCH = "main";
        private static readonly string PUBLIC_SPECS_REPO = "azure-rest-api-specs";
        private static readonly string NAMESPACE_APPROVAL_REPO = "azure-sdk";
        private static readonly string REPO_OWNER = "Azure";
        public static readonly string ARM_SIGN_OFF_LABEL = "ARMSignedOff";
        public static readonly string API_STEWARDSHIP_APPROVAL = "APIStewardshipBoard-SignedOff";
        public static readonly HashSet<string> SUPPORTED_LANGUAGES = new()
        {
            "python",
            ".net",
            "javascript",
            "java",
            "go"
        };

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
            new McpCommand(getReleasePlanDetailsCommandName, "Get release plan details", GetReleasePlanToolName) { workItemIdOpt, releasePlanNumberOpt },
            new McpCommand(createReleasePlanCommandName, "Create a release plan", CreateReleasePlanToolName)
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
                forceCreateReleasePlanOpt,
            },
            new McpCommand(linkNamespaceApprovalIssueCommandName, "Link namespace approval issue to release plan", LinkNamespaceApprovalToolName) { workItemIdOpt, namespaceApprovalIssueOpt, },
            new McpCommand(checkApiReadinessCommandName, "Check if API spec is ready to generate SDK", CheckApiSpecReadyToolName) { typeSpecProjectPathOpt, pullRequestNumberOpt, workItemIdOpt, },
            new McpCommand(linkSdkPrCommandName, "Link SDK pull request to release plan", LinkSdkPullRequestToolName) { languageOpt, pullRequestOpt, workItemIdOpt, releasePlanNumberOpt, },
            new McpCommand(listOverdueReleasePlansCommandName, "List in-progress release plans that are past their SDK release deadline") { notifyOwnersOpt, azureSDKEmailerUriOpt, },
            new McpCommand(updateApiSpecPullRequestCommandName, "Update TypeSpec pull request URL in a release plan", UpdateApiSpecPullRequestToolName) { pullRequestOpt, workItemIdOpt, releasePlanNumberOpt, },
            new McpCommand(getServiceDetailsCommandName, "Get service and product details (service tree ID, service ID, package display name) in service tree for TypeSpec project", GetServiceDetailsToolName) { typeSpecProjectOpt, },
            new McpCommand(abandonReleasePlanCommandName, "Abandon a release plan", AbandonReleasePlanToolName) { workItemIdOpt, releasePlanNumberOpt, }
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
                    var forceCreateReleasePlan = commandParser.GetValue(forceCreateReleasePlanOpt);
                    return await CreateReleasePlan(
                        typeSpecProjectPath,
                        targetReleaseMonthYear,
                        serviceTreeId,
                        productTreeId,
                        specApiVersion,
                        specPullRequestUrl,
                        sdkReleaseType,
                        userEmail: userEmail,
                        isTestReleasePlan: isTestReleasePlan,
                        forceCreateReleasePlan: forceCreateReleasePlan
                    );

                case linkNamespaceApprovalIssueCommandName:
                    return await LinkNamespaceApprovalIssue(commandParser.GetValue(workItemIdOpt), commandParser.GetValue(namespaceApprovalIssueOpt));

                case checkApiReadinessCommandName:
                    return await CheckApiReadyForSDKGeneration(commandParser.GetValue(typeSpecProjectPathOpt), pullRequestNumber: commandParser.GetValue(pullRequestNumberOpt), workItemId: commandParser.GetValue(workItemIdOpt));

                case linkSdkPrCommandName:
                    return await LinkSdkPullRequestToReleasePlan(commandParser.GetValue(languageOpt), commandParser.GetValue(pullRequestOpt), workItemId: commandParser.GetValue(workItemIdOpt), releasePlanId: commandParser.GetValue(releasePlanNumberOpt));

                case listOverdueReleasePlansCommandName:
                    return await ListOverdueReleasePlans(commandParser.GetValue(notifyOwnersOpt), commandParser.GetValue(azureSDKEmailerUriOpt));

                case updateApiSpecPullRequestCommandName:
                    return await UpdateSpecPullRequestInReleasePlan(specPullRequestUrl: commandParser.GetValue(pullRequestOpt), workItemId: commandParser.GetValue(workItemIdOpt), releasePlanId: commandParser.GetValue(releasePlanNumberOpt));

                case getServiceDetailsCommandName:
                    return await GetProductByTypeSpecPath(commandParser.GetValue(typeSpecProjectOpt));
                case abandonReleasePlanCommandName:
                    return await AbandonReleasePlan(workItemId: commandParser.GetValue(workItemIdOpt), releasePlanId: commandParser.GetValue(releasePlanNumberOpt));

                default:
                    logger.LogError("Unknown command: {command}", command);
                    return new DefaultCommandResponse { ResponseError = $"Unknown command: '{command}'" };
            }
        }


        [McpServerTool(Name = GetReleasePlanForSpecPrToolName), Description("Get release plan for API spec pull request. This tool should be used only if work item Id is unknown.")]
        public async Task<ReleaseWorkflowResponse> GetReleasePlanForPullRequest(string pullRequestLink)
        {
            try
            {
                ValidatePullRequestUrl(pullRequestLink);
                var releasePlan = await devOpsService.GetReleasePlanAsync(pullRequestLink) ?? throw new Exception("No release plan associated with pull request link");
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

        [McpServerTool(Name = GetReleasePlanToolName), Description("Get Release Plan: Get release plan work item details for a given work item id or release plan Id.")]
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

        /// <summary>
        /// Abandons a release plan by updating its status to 'Abandoned'.
        /// </summary>
        /// <param name="workItemId">The work item ID of the release plan (optional).</param>
        /// <param name="releasePlanId">The release plan ID (optional).</param>
        /// <returns>A response indicating success or failure of the operation.</returns>
        /// <remarks>
        /// Either workItemId or releasePlanId must be provided. If both are provided, workItemId takes precedence.
        /// </remarks>
        [McpServerTool(Name = AbandonReleasePlanToolName), Description("Abandon a release plan by work item ID or release plan ID. Updates the release plan status to 'Abandoned'.")]
        public async Task<ReleaseWorkflowResponse> AbandonReleasePlan(int workItemId = 0, int releasePlanId = 0)
        {
            try
            {
                if (workItemId == 0 && releasePlanId == 0)
                {
                    return new ReleaseWorkflowResponse { ResponseError = "Either work item ID or release plan ID must be provided." };
                }

                // Get the release plan to verify it exists
                var releasePlan = workItemId != 0
                    ? await devOpsService.GetReleasePlanForWorkItemAsync(workItemId)
                    : await devOpsService.GetReleasePlanAsync(releasePlanId);

                if (releasePlan == null)
                {
                    return new ReleaseWorkflowResponse { ResponseError = "Failed to find release plan." };
                }

                // Update the work item status to "Abandoned"
                var fieldsToUpdate = new Dictionary<string, string>
                {
                    { "System.State", "Abandoned" }
                };

                var updatedWorkItem = await devOpsService.UpdateWorkItemAsync(releasePlan.WorkItemId, fieldsToUpdate);

                if (updatedWorkItem == null)
                {
                    logger.LogError("Failed to abandon release plan {WorkItemId}: work item update returned null", releasePlan.WorkItemId);
                    return new ReleaseWorkflowResponse
                    {
                        ResponseError = $"Failed to abandon release plan {releasePlan.WorkItemId}: work item update failed."
                    };
                }
                logger.LogInformation("Successfully abandoned release plan {WorkItemId}", releasePlan.WorkItemId);

                return new ReleaseWorkflowResponse
                {
                    Status = "Success",
                    Details = [$"Release plan {releasePlan.WorkItemId} has been successfully abandoned."]
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to abandon release plan");
                return new ReleaseWorkflowResponse { ResponseError = $"Failed to abandon release plan: {ex.Message}" };
            }
        }

        private void ValidatePullRequestUrl(string specPullRequestUrl)
        {
            if (string.IsNullOrEmpty(specPullRequestUrl))
            {
                throw new Exception("API spec pull request URL is required for this release plan operation.");
            }

            var match = PullRequestUrlRegex().Match(specPullRequestUrl);

            if (!match.Success)
            {
                throw new Exception($"Invalid spec pull request URL '{specPullRequestUrl}'. It should be a valid GitHub pull request to azure-rest-api-specs repo.");
            }
        }

        private async Task ValidateCreateReleasePlanInputAsync(string typeSpecProjectPath, string serviceTreeId, string productTreeId, string specPullRequestUrl, string sdkReleaseType, string specApiVersion)
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

            // Skip filesystem validation for URLs since GetSpecRepoRootPath expects local paths
            if (!typeSpecHelper.IsUrl(typeSpecProjectPath))
            {
                var repoRoot = typeSpecHelper.GetSpecRepoRootPath(typeSpecProjectPath);

                // Ensure a release plan is created only if the API specs pull request is in a public repository.
                if (!await typeSpecHelper.IsRepoPathForPublicSpecRepoAsync(repoRoot))
                {
                    throw new Exception("""
                        SDK generation and release require the API specs pull request to be in the public azure-rest-api-specs repository.
                        Please create a pull request in the public Azure/azure-rest-api-specs repository to move your specs changes to public.
                        A release plan cannot be created for SDK generation using a pull request in a private repository.
                        """);
                }
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

        [McpServerTool(Name = CreateReleasePlanToolName), Description("Create Release Plan")]
        public async Task<ReleasePlanResponse> CreateReleasePlan(string typeSpecProjectPath, string targetReleaseMonthYear, string serviceTreeId, string productTreeId, string specApiVersion, string specPullRequestUrl, string sdkReleaseType, string userEmail = "", bool isTestReleasePlan = false, bool forceCreateReleasePlan = false)
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

                await ValidateCreateReleasePlanInputAsync(typeSpecProjectPath, serviceTreeId, productTreeId, specPullRequestUrl, sdkReleaseType, specApiVersion);

                // Check environment variable to determine if this should be a test release plan
                var isAgentTesting = environmentHelper.GetBooleanVariable("AZSDKTOOLS_AGENT_TESTING", false);
                if (isAgentTesting)
                {
                    isTestReleasePlan = true;
                    logger.LogInformation("AZSDKTOOLS_AGENT_TESTING environment variable is set to true, creating test release plan");
                }

                if (!forceCreateReleasePlan)
                {
                    // Check for existing release plan for the given pull request URL.
                    logger.LogInformation("Checking for existing release plan for pull request URL: {specPullRequestUrl}", specPullRequestUrl);
                    var existingReleasePlan = await devOpsService.GetReleasePlanAsync(specPullRequestUrl);
                    if (existingReleasePlan != null && existingReleasePlan.WorkItemId > 0)
                    {
                        return new ReleasePlanResponse
                        {
                            Message = $"Release plan already exists for the pull request: {specPullRequestUrl}. Release plan link: {existingReleasePlan.ReleasePlanLink}",
                            ReleasePlanDetails = existingReleasePlan,
                            NextSteps = ["Prompt user to confirm whether to use existing release plan or force create a new release plan."]
                        };
                    }

                    logger.LogInformation("Checking for existing release plans for product: {productTreeId}", productTreeId);
                    var existingReleasePlans = await devOpsService.GetReleasePlansForProductAsync(productTreeId, specApiVersion, sdkReleaseType, isTestReleasePlan);
                    if (existingReleasePlans.Any())
                    {
                        return new ReleasePlanResponse
                        {
                            Message = $"An active release plan already exists for the product: {productTreeId}. "
                            +  $"Release plan link(s): {string.Join("\n ", existingReleasePlans.Select(p => p.ReleasePlanLink))}",
                            ReleasePlanDetails = existingReleasePlans[0],
                            NextSteps = ["Prompt user to confirm whether to use existing release plan or force create a new release plan."]
                        };
                    }
                }

                // Handle both URLs and local paths for TypeSpec projects
                bool isValidTypeSpec;
                bool isMgmt;
                string specProject;

                if (typeSpecHelper.IsUrl(typeSpecProjectPath))
                {
                    // URL path
                    isValidTypeSpec = typeSpecHelper.IsValidTypeSpecProjectUrl(typeSpecProjectPath);
                    isMgmt = typeSpecHelper.IsTypeSpecUrlForMgmtPlane(typeSpecProjectPath);
                    specProject = typeSpecHelper.GetTypeSpecProjectRelativePathFromUrl(typeSpecProjectPath);
                }
                else
                {
                    // Local file path
                    isValidTypeSpec = typeSpecHelper.IsValidTypeSpecProjectPath(typeSpecProjectPath);
                    isMgmt = typeSpecHelper.IsTypeSpecProjectForMgmtPlane(typeSpecProjectPath);
                    specProject = typeSpecHelper.GetTypeSpecProjectRelativePath(typeSpecProjectPath);
                }

                var specType = isValidTypeSpec ? "TypeSpec" : "OpenAPI";
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

                var releasePlan = new ReleasePlanWorkItem
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
                    ReleasePlanSubmittedByEmail = userEmail,
                    APISpecProjectPath = specProject
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

        [McpServerTool(Name = UpdateSdkDetailsToolName), Description("Update the SDK details in the release plan work item. This tool is called to update SDK language and package name in the release plan work item." +
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

        [McpServerTool(Name = LinkNamespaceApprovalToolName), Description("Link package namespace approval issue to release plan(required only for management plan). This requires GitHub issue URL for the namespace approval request and release plan work item id.")]
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
                var issue = await githubService.GetIssueAsync(REPO_OWNER, NAMESPACE_APPROVAL_REPO, issueNumber);
                if (issue == null)
                {
                    return $"Failed to verify approval status. Namespace approval issue #{namespaceApprovalIssue} not found in {REPO_OWNER}/{NAMESPACE_APPROVAL_REPO}.";
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

        [McpServerTool(Name = UpdateLanguageExclusionToolName), Description("Update language exclusion justification in release plan work item. This tool is called to update justification for excluded languages in the release plan. " +
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

        [McpServerTool(Name = CheckApiSpecReadyToolName), Description("Checks whether a TypeSpec API spec is ready to generate SDK. Provide a pull request number and path to TypeSpec project json as params.")]
        public async Task<ReleaseWorkflowResponse> CheckApiReadyForSDKGeneration(string typeSpecProjectRoot, int pullRequestNumber, int workItemId = 0, CancellationToken ct = default)
        {
            try
            {
                var response = await IsSpecReadyToGenerateSDKAsync(typeSpecProjectRoot, pullRequestNumber, ct);
                if (workItemId != 0 && response.Status == "Success")
                {
                    await devOpsService.UpdateApiSpecStatusAsync(workItemId, "Approved");
                }
                return response;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to check if API spec is ready for SDK generation");
                return new ReleaseWorkflowResponse
                {
                    ResponseError = $"Failed to check if API spec is ready for SDK generation: {ex.Message}",
                };
            }
        }

        private async Task<ReleaseWorkflowResponse> IsSpecReadyToGenerateSDKAsync(string typeSpecProjectRoot, int pullRequestNumber, CancellationToken ct)
        {
            var response = new ReleaseWorkflowResponse()
            {
                Status = "Failed"
            };

            try
            {
                if (string.IsNullOrEmpty(typeSpecProjectRoot) && pullRequestNumber == 0)
                {
                    response.Details.Add("Invalid value for both TypeSpec project root and pull request number. Provide at least the TypeSpec project root path for modified project or provide a pull request number.");
                    return response;
                }

                // Get current branch name
                var repoRootPath = typeSpecHelper.GetSpecRepoRootPath(typeSpecProjectRoot);
                var branchName = await gitHelper.GetBranchNameAsync(repoRootPath, ct: ct);

                // Check if current repo is private or public repo
                if (!await typeSpecHelper.IsRepoPathForPublicSpecRepoAsync(repoRootPath))
                {
                    response.Details.AddRange([
                        $"Current repo root path '{repoRootPath}' is not a GitHub clone of 'Azure/azure-rest-api-specs' repo. SDK can be generated only if your TypeSpec changes are in public Azure/azure-rest-api-specs repo. ",
                        "Create a pull request in public repo Azure/azure-rest-api-specs for your TypeSpec changes to get your TypeSpec ready."
                        ]);
                    return response;
                }

                if (!typeSpecHelper.IsValidTypeSpecProjectPath(typeSpecProjectRoot))
                {
                    response.Details.Add($"TypeSpec project path '{typeSpecProjectRoot}' is invalid. Provide a TypeSpec project path that contains tspconfig.yaml");
                    return response;
                }
                response.TypeSpecProject = typeSpecHelper.GetTypeSpecProjectRelativePath(typeSpecProjectRoot);

                // if current branch name is main then ask user to provide pull request number if they have or switch to the branch they have created for TypeSpec changes.
                if (branchName.Equals(DEFAULT_BRANCH))
                {
                    response.Details.Add($"The current branch is '{DEFAULT_BRANCH}', which is not recommended for development. Please switch to a branch containing your TypeSpec project changes or create a new branch if none exists.");
                    return response;
                }

                // Get pull request details
                Octokit.PullRequest? pullRequest = pullRequestNumber != 0 ? await githubService.GetPullRequestAsync(REPO_OWNER, PUBLIC_SPECS_REPO, pullRequestNumber) :
                    await githubService.GetPullRequestForBranchAsync(REPO_OWNER, PUBLIC_SPECS_REPO, branchName);
                if (pullRequest == null)
                {
                    response.Details.Add($"Pull request is not found in {REPO_OWNER}/{PUBLIC_SPECS_REPO} for your TypeSpec changes.");
                    if (pullRequestNumber == 0)
                    {
                        response.Details.Add("Do you have a pull request created for your TypeSpec changes? If not, make TypeSpec changes for your API specification and create a pull request.");
                    }
                    else
                    {
                        response.Details.Add($"Pull request {pullRequestNumber} is not valid. Please provide a valid pull request number to check the status.");
                    }
                    return response;
                }

                // Pull request is not targeted to main branch
                if (!string.IsNullOrEmpty(pullRequest.Base?.Ref) && !pullRequest.Base.Ref.Equals(DEFAULT_BRANCH))
                {
                    response.Details.Add($"Pull request {pullRequest.Number} merges changes to '{pullRequest.Base?.Ref}' branch. SDK can be generated only from a pull request with {DEFAULT_BRANCH} branch as target. Create a pull request for your changes with '{DEFAULT_BRANCH}' branch as target.");
                    return response;
                }

                // PR closed without merging changes
                if (pullRequest.State == Octokit.ItemState.Closed && !pullRequest.Merged)
                {
                    response.Details.Add($"Pull request {pullRequest.Number} is in closed status without merging changes to main branch. SDK can not be generated from closed PR. Create a pull request for your changes with '{DEFAULT_BRANCH}' branch as target.");
                    return response;
                }

                var isMgmtPlane = typeSpecHelper.IsTypeSpecProjectForMgmtPlane(typeSpecProjectRoot);
                response.PackageType = isMgmtPlane ? SdkType.Management : SdkType.Dataplane;
                // Check if ARM or API stewardship approval is present if PR is not in merged status
                // Check ARM approval label is present on the management pull request
                if (!pullRequest.Merged && isMgmtPlane && (pullRequest.Labels == null || !pullRequest.Labels.Any(l => l.Name.Equals(ARM_SIGN_OFF_LABEL))))
                {
                    response.Details.Add($"Pull request {pullRequest.Number} does not have ARM approval. Your API spec changes are not ready to generate SDK. Please check pull request details to get more information on next step for your pull request");
                    return response;
                }

                // Check if API stewardship approval label is present on the data plane pull request
                if (!pullRequest.Merged && !isMgmtPlane && (pullRequest.Labels == null || !pullRequest.Labels.Any(l => l.Name.Equals(API_STEWARDSHIP_APPROVAL))))
                {
                    response.Details.Add($"Pull request {pullRequest.Number} does not have API stewardship approval. Your API spec changes are not ready to generate SDK. Please check pull request details to get more information on next step for your pull request");
                    return response;
                }

                var approvalLabel = isMgmtPlane ? ARM_SIGN_OFF_LABEL : API_STEWARDSHIP_APPROVAL;
                response.Details.Add($"Pull request {pullRequest.Number} has {approvalLabel} or it is in merged status. Your API spec changes are ready to generate SDK. Please make sure you have a release plan created for the pull request.");
                response.Status = "Success";
                return response;
            }
            catch (Exception ex)
            {
                response.Status = "Failed";
                response.Details.Add($"Failed to check if TypeSpec is ready for SDK generation. Error: {ex.Message}");
                return response;
            }
        }

        public static bool IsValidLanguage(string language)
        {
            return SUPPORTED_LANGUAGES.Contains(language.ToLower());
        }
        private static string GetRepoName(string language)
        {
            return language.ToLower() switch
            {
                ".net" => "azure-sdk-for-net",
                "javascript" => "azure-sdk-for-js",
                _ => $"azure-sdk-for-{language.ToLower()}"
            };
        }

        [McpServerTool(Name = LinkSdkPullRequestToolName), Description("Link SDK pull request to release plan work item")]
        public async Task<ReleaseWorkflowResponse> LinkSdkPullRequestToReleasePlan(string language, string pullRequestUrl, int workItemId = 0, int releasePlanId = 0)
        {
            try
            {
                var response = new ReleaseWorkflowResponse();
                language = inputSanitizer.SanitizeLanguage(language);
                response.SetLanguage(language);

                // Verify language and get repo name
                if (!IsValidLanguage(language))
                {
                    response.ResponseError = $"Unsupported language to link pull request. Supported languages: {string.Join(", ", SUPPORTED_LANGUAGES)}";
                    return response;
                }
                // work item Id or release plan Id is required to link SDK pull request to release plan
                if (workItemId == 0 && releasePlanId == 0)
                {
                    response.ResponseError = "Either work item ID or release plan ID is required to link SDK pull request to release plan.";
                    return response;
                }
                // Verify SDK pull request URL
                if (string.IsNullOrEmpty(pullRequestUrl))
                {
                    response.ResponseError = "SDK pull request URL is required to link it to release plan.";
                    return response;
                }

                // Parse just the pull request link from input
                var repoName = GetRepoName(language);
                var parsedLink = DevOpsService.ParseSDKPullRequestUrl(pullRequestUrl);
                if (!parsedLink.FullUrl.Contains(repoName))
                {
                    response.ResponseError = $"Invalid pull request link. Provide a pull request link in SDK repo {repoName}";
                    return response;
                }

                // Add PR to release plan
                var releasePlan = workItemId == 0 ? await devOpsService.GetReleasePlanAsync(releasePlanId) : await devOpsService.GetReleasePlanForWorkItemAsync(workItemId);
                if (releasePlan == null || releasePlan.WorkItemId == 0)
                {
                    response.ResponseError = $"Release plan with ID {releasePlanId} or work item ID {workItemId} is not found.";
                    return response;
                }

                var sdkInfoInRelease = devOpsService.AddSdkInfoInReleasePlanAsync(releasePlan.WorkItemId, language, "", parsedLink.FullUrl, "Completed");
                var releaseInfoInSdk = UpdateSdkPullRequestDescription(parsedLink, releasePlan);

                await Task.WhenAll(sdkInfoInRelease, releaseInfoInSdk);
                if (releasePlan.IsManagementPlane)
                {
                    response.PackageType = SdkType.Management;
                }
                else if (releasePlan.IsDataPlane)
                {
                    response.PackageType = SdkType.Dataplane;
                }
                response.Details.Add($"Successfully linked pull request to release plan {releasePlan.ReleasePlanId}, work item id {releasePlan.WorkItemId}, and updated PR description.");
                return response;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to link SDK pull request to release plan work item");
                return new() { ResponseError = $"Failed to link SDK pull request to release plan work item, Error: {ex.Message}" };
            }
        }

        private async Task UpdateSdkPullRequestDescription(ParsedSdkPullRequest parsedUrl, ReleasePlanWorkItem releasePlan)
        {
            var repoOwner = parsedUrl.RepoOwner;
            var repoName = parsedUrl.RepoName;
            var prNumber = parsedUrl.PrNumber;

            var pr = await githubService.GetPullRequestAsync(repoOwner, repoName, prNumber);
            if (pr == null)
            {
                throw new InvalidOperationException($"Failed to fetch pull request {repoOwner}/{repoName}#{prNumber}");
            }

            // Check if the PR body already contains the release plan link (main indicator)
            var header = "## Release Plan Details";
            if (!string.IsNullOrEmpty(pr.Body) && pr.Body.Contains(header, StringComparison.OrdinalIgnoreCase))
            {
                // If already contains release plan info, just return without doing anything
                return;
            }

            var linksBuilder = new StringBuilder(header);
            linksBuilder.AppendLine();
            linksBuilder.AppendLine($"- Release Plan: {releasePlan.ReleasePlanLink}");
            linksBuilder.AppendLine($"- Work Item Link: {releasePlan.WorkItemHtmlUrl}");
            linksBuilder.AppendLine($"- Spec Pull Request: {releasePlan.ActiveSpecPullRequest}");
            linksBuilder.Append($"- Spec API version: {releasePlan.SpecAPIVersion}");

            var links = linksBuilder.ToString();
            var appendedBody = string.IsNullOrEmpty(pr.Body)
                ? links
                : $"{pr.Body}\n{links}";
            try
            {
                await githubService.UpdatePullRequestAsync(repoOwner, repoName, prNumber, pr.Title, appendedBody, pr.State.Value);
            }
            catch (Exception ex)
            {
                // This should not be a hard error when context is not updated in PR description
                logger.LogError(ex, "Failed to update pull request description for {repoOwner}/{repoName}#{prNumber}", repoOwner, repoName, prNumber);
                return;
            }
        }

        public async Task<ReleasePlanListResponse> ListOverdueReleasePlans(bool notifyOwners = false, string emailerUri = "")
        {
            try
            {
                if (notifyOwners && string.IsNullOrWhiteSpace(emailerUri))
                {
                    return new ReleasePlanListResponse { ResponseError = "Emailer URI is required when notify owners is enabled." };
                }
                var releasePlans = await devOpsService.ListOverdueReleasePlansAsync();

                if (notifyOwners)
                {
                    await NotifyOwnersOfOverdueReleasePlans(releasePlans, emailerUri);
                }

                return new ReleasePlanListResponse
                {
                    Message = "List of overdue Release plans:",
                    ReleasePlanDetailsList = releasePlans
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error retrieving overdue release plans");
                return new ReleasePlanListResponse { ResponseError = $"An error occurred while retrieving overdue release plans: {ex.Message}" };
            }
        }

        private async Task NotifyOwnersOfOverdueReleasePlans(List<ReleasePlanWorkItem> releasePlans, string emailerUri)
        {
            const string subject = "Action Required: Azure SDKs Not Yet Published for Your Release Plan";

            foreach (var releasePlan in releasePlans)
            {
                var releaseOwnerEmail = releasePlan.ReleasePlanSubmittedByEmail;

                // Validate email address
                if (string.IsNullOrWhiteSpace(releaseOwnerEmail) || !Regex.IsMatch(releaseOwnerEmail, @"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase))
                {
                    logger.LogWarning("Skipped notification for Release Plan ID {WorkItemId}: invalid email '{Email}'",
                        releasePlan.WorkItemId, releaseOwnerEmail);
                    continue;
                }

                var releaseOwnerName = releasePlan.Owner;
                var plane = releasePlan.IsManagementPlane ? "Management Plane" : "Data Plane";
                var releasePlanLink = releasePlan.ReleasePlanLink;
                var releasePlanDate = releasePlan.SDKReleaseMonth;

                // Identify SDKs not yet released (skip Go for Data Plane and skip excluded languages)
                var missingSDKs = releasePlan.SDKInfo
                    .Where(info => (string.IsNullOrEmpty(info.ReleaseStatus) || !string.Equals(info.ReleaseStatus, "Released", StringComparison.OrdinalIgnoreCase))
                             && (releasePlan.IsManagementPlane || !string.Equals(info.Language, "Go", StringComparison.OrdinalIgnoreCase))
                             && !string.Equals(info.ReleaseExclusionStatus, "Requested", StringComparison.OrdinalIgnoreCase)
                             && !string.Equals(info.ReleaseExclusionStatus, "Approved", StringComparison.OrdinalIgnoreCase))
                    .Select(info => info.Language)
                    .ToList();

                var body = $"""
                    <html>
                    <body>
                        <p>Hello {releaseOwnerName},</p>
                        <p>Our automation has detected that one or more Azure SDKs generated for your release plan have not yet been published to the required language package managers.</p>
                        <ul>
                            <li><strong>Azure SDK Type:</strong> {plane}</li>
                            <li><strong>SDKs not yet published:</strong> {string.Join(", ", missingSDKs)}</li>
                            <li><strong>Release Plan:</strong> <a href="{releasePlanLink}">{releasePlanLink}</a></li>
                            <li><strong>Release Plan Target Release Date:</strong> {releasePlanDate}</li>
                        </ul>
                        <p>Per Azure SDK release requirements, all Tier 1 language SDKs must be <strong>published to their respective package managers</strong> before a release plan can be marked as complete.</p>
                        <p>Until the missing SDKs are published:</p>
                        <ul>
                            <li>The release plan cannot be completed in Release Planner.</li>
                            <li>If this release is in scope for CPEX, Cloud Lifecycle phase KPIs for Public Preview or GA will remain incomplete.</li>
                        </ul>
                        <p><strong>Required actions:</strong></p>
                        <ol>
                            <li>Publish the missing SDKs to their respective package managers, or</li>
                            <li>Update the target release date in the release plan, or</li>
                            <li>If publication is not intended, file an approved exception: <a href="https://eng.ms/docs/products/azure-developer-experience/onboard/request-exception">https://eng.ms/docs/products/azure-developer-experience/onboard/request-exception</a></li>
                        </ol>
                        <p>Once publication is complete, this status will clear automatically. Thank you for helping maintain consistent, complete Azure SDK releases across all mandatory Tier 1 languages.</p>
                        <p>Best regards,</p>
                        <p>Azure SDK PM Team</p>
                    </body>
                    </html>
                """;

                await SendEmailNotification(emailerUri, releaseOwnerEmail, sdkApexEmail, subject, body);
            }
        }

        private async Task SendEmailNotification(string emailerUri, string to, string cc, string subject, string body)
        {
            var emailPayload = new
            {
                EmailTo = to,
                CC = cc,
                Subject = subject,
                Body = body
            };

            var jsonContent = JsonSerializer.Serialize(emailPayload);

            using (var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json"))
            {
                logger.LogInformation("Sending Email - To: {To}, CC: {CC}, Subject: {Subject}", to, cc, subject);

                var response = await httpClient.PostAsync(emailerUri, httpContent);
                response.EnsureSuccessStatusCode();

                logger.LogInformation("Successfully sent email - To: {To}, CC: {CC}, Subject: {Subject}", to, cc, subject);
            }
        }

        [McpServerTool(Name = UpdateApiSpecPullRequestToolName), Description("Update TypeSpec pull request URL in a release plan using work item id or release plan id.")]
        public async Task<ReleaseWorkflowResponse> UpdateSpecPullRequestInReleasePlan(string specPullRequestUrl, int workItemId = 0, int releasePlanId = 0)
        {
            try
            {
                if (workItemId == 0 && releasePlanId == 0)
                {
                    return new ReleaseWorkflowResponse { ResponseError = "Either work item ID or release plan ID must be provided." };
                }
                ValidatePullRequestUrl(specPullRequestUrl);

                // Get work item ID from release plan ID if needed
                if (workItemId == 0)
                {
                    var releasePlan = await devOpsService.GetReleasePlanAsync(releasePlanId);
                    if (releasePlan == null)
                    {
                        return new ReleaseWorkflowResponse
                        {
                            ResponseError = $"Release plan with ID {releasePlanId} not found."
                        };
                    }
                    workItemId = releasePlan.WorkItemId;
                }

                // Update the spec pull request in the release plan
                var updated = await devOpsService.UpdateSpecPullRequestAsync(workItemId, specPullRequestUrl);

                if (!updated)
                {
                    return new ReleaseWorkflowResponse
                    {
                        ResponseError = "Failed to update TypeSpec pull request URL in release plan."
                    };
                }

                return new ReleaseWorkflowResponse
                {
                    Status = "Success",
                    Details =
                    [
                        $"Successfully updated spec pull request URL to {specPullRequestUrl} in release plan."
                    ],
                    NextSteps =
                    [
                        "SDK generation should be triggered to regenerate SDK using the new spec pull request.",
                        "Generate SDK for each language listed in the release plan."
                    ]
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to update TypeSpec pull request URL in release plan.");
                return new ReleaseWorkflowResponse
                {
                    ResponseError = $"Failed to update TypeSpec pull request URL in release plan: {ex.Message}",
                };
            }
        }

        [McpServerTool(Name = GetServiceDetailsToolName), Description("Get service and service tree product details for a product using TypeSpec project path: Get service tree product details (service tree ID, service ID, package display name, product service tree link).")]
        public async Task<ProductInfoResponse> GetProductByTypeSpecPath(string typeSpecProjectPath)
        {
            try
            {
                logger.LogInformation("Finding product information for TypeSpec project path: {typeSpecProjectPath}", typeSpecProjectPath);

                // Validate input
                if (string.IsNullOrWhiteSpace(typeSpecProjectPath))
                {
                    return new ProductInfoResponse
                    {
                        ResponseError = "TypeSpec project path cannot be empty.",
                        TypeSpecProject = typeSpecProjectPath
                    };
                }

                // Get product info from DevOps service
                var productInfo = await devOpsService.GetProductInfoByTypeSpecProjectPathAsync(typeSpecProjectPath);

                if (productInfo == null)
                {
                    return new ProductInfoResponse
                    {
                        Message = $"No release plan found for TypeSpec project path: {typeSpecProjectPath}",
                        TypeSpecProject = typeSpecProjectPath
                    };
                }

                return new ProductInfoResponse
                {
                    ProductInfo = productInfo,
                    Message = "Successfully retrieved product information.",
                    TypeSpecProject = typeSpecProjectPath
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to find product information for TypeSpec project path: {typeSpecProjectPath}", typeSpecProjectPath);
                return new ProductInfoResponse
                {
                    ResponseError = $"Failed to find product information: {ex.Message}",
                    TypeSpecProject = typeSpecProjectPath
                };
            }
        }
    }
}
