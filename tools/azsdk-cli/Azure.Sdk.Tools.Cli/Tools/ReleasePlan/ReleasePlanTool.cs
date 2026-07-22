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
using Azure.Sdk.Tools.Cli.Services.Notification;
using Azure.Sdk.Tools.Cli.Services.Notification.Templates;
using Azure.Sdk.Tools.Cli.Tools.Core;
using ModelContextProtocol;
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
        HttpClient httpClient,
        INpxHelper npxHelper,
        IRawOutputHelper outputHelper,
        INotificationService notificationService
    ) : MCPMultiCommandTool
    {
        public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.ReleasePlan];

        // Commands
        private const string getReleasePlanDetailsCommandName = "get";
        private const string createReleasePlanCommandName = "create";
        private const string updateReleasePlanCommandName = "update";
        private const string linkNamespaceApprovalIssueCommandName = "link-namespace-approval";
        private const string checkApiReadinessCommandName = "check-api-readiness";
        private const string linkSdkPrCommandName = "link-sdk-pr";
        private const string listOverdueReleasePlansCommandName = "list-overdue";
        private const string updateApiSpecPullRequestCommandName = "update-spec-pr";
        private const string getServiceDetailsCommandName = "get-service-details";
        private const string abandonReleasePlanCommandName = "abandon";
        private const string getKpiAttestationStatusCommandName = "get-kpi-attestation";
        private const string updateReleasePlanTargetCommandName = "update-release-target";

        // MCP Tool Names
        private const string GetReleasePlanForSpecPrToolName = "azsdk_get_release_plan_for_spec_pr";
        private const string GetReleasePlanToolName = "azsdk_get_release_plan";
        private const string CreateReleasePlanToolName = "azsdk_create_release_plan";
        private const string UpdateReleasePlanToolName = "azsdk_update_release_plan";
        private const string UpdateSdkDetailsToolName = "azsdk_update_sdk_details_in_release_plan";
        private const string LinkNamespaceApprovalToolName = "azsdk_link_namespace_approval_issue";
        private const string UpdateLanguageExclusionToolName = "azsdk_update_language_exclusion_justification";
        private const string CheckApiSpecReadyToolName = "azsdk_check_api_spec_ready_for_sdk";
        private const string LinkSdkPullRequestToolName = "azsdk_link_sdk_pull_request_to_release_plan";
        private const string UpdateApiSpecPullRequestToolName = "azsdk_update_api_spec_pull_request_in_release_plan";
        private const string GetServiceDetailsToolName = "azsdk_get_service_details_by_typespec_path";
        private const string AbandonReleasePlanToolName = "azsdk_abandon_release_plan";
        private const string GetKPIAttestationStatusToolName = "azsdk_get_kpi_attestation_status";
        private const string UpdateReleasePlanTargetToolName = "azsdk_update_release_plan_target";

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
            Required = false,
            DefaultValueFactory = _ => string.Empty,
        };

        private readonly Option<string> productTreeIdOpt = new("--product")
        {
            Description = "Product service tree ID",
            Required = false,
            DefaultValueFactory = _ => string.Empty,
        };

        private readonly Option<string> pullRequestOpt = new("--pull-request", "-p")
        {
            Description = "Api spec pull request URL",
            Required = true,
        };

        private readonly Option<string> apiReleaseTypeOpt = new("--api-release-type")
        {
            Description = "API release type. Allowed values: Private Preview, Public Preview, GA",
            Required = true,
        };

        private readonly Option<bool> isTestReleasePlanOpt = new("--test-release")
        {
            Description = "Create release plan in test environment",
            Required = false,
            DefaultValueFactory = _ => false,
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

        // Options specific to update command (optional variants)
        private readonly Option<string> updateTypeSpecProjectPathOpt = new("--typespec-path")
        {
            Description = "Path to TypeSpec project",
            Required = true,
        };

        private readonly Option<string> updateSdkReleaseTypeOpt = new("--sdk-type")
        {
            Description = "SDK release type: beta or stable. If not provided, inferred from API version (preview → beta, otherwise stable).",
            Required = false,
        };

        private readonly Option<string> optionalServiceTreeIdOpt = new("--service-tree")
        {
            Description = "Service tree ID",
            Required = false,
        };

        private readonly Option<string> optionalProductTreeIdOpt = new("--product")
        {
            Description = "Product service tree ID",
            Required = false,
        };

        private readonly Option<ProductType> productTypeOpt = new("--product-type")
        {
            Description = "Product type. Allowed values: Offering, Feature, Sku. Used when the product type cannot be resolved from a triage work item.",
            Required = false,
            DefaultValueFactory = _ => ProductType.Unknown,
        };

        private readonly Option<string> optionalPullRequestOpt = new("--pull-request", "-p")
        {
            Description = "Api spec pull request URL",
            Required = false,
        };

        private readonly Option<string> optionalTypeSpecProjectPathOpt = new("--typespec-path")
        {
            Description = "Path to TypeSpec project",
            Required = false,
        };

        private readonly Option<string> optionalApiReleaseTypeOpt = new("--api-release-type")
        {
            Description = "API release type. Allowed values: Private Preview, Public Preview, GA",
            Required = false,
        };

        private readonly Option<string> kpiProductIdOpt = new("--product")
        {
            Description = "Product service tree ID",
            Required = false,
        };

        private readonly Option<string> releasePlanTypeOpt = new("--release-plan-type")
        {
            Description = "Release plan type: 'Private Preview', 'Public Preview', 'GA'",
            Required = false,
        };

        private readonly Option<string> kpiTypeSpecProjectPathOpt = new("--typespec-path")
        {
            Description = "Path to TypeSpec project",
            Required = false,
        };

        private readonly Option<bool> kpiIsTestReleasePlanOpt = new("--test-release")
        {
            Description = "Use test release plans",
            Required = false,
            DefaultValueFactory = _ => false,
        };

        private const string sdkApexEmail = "azsdkapex@microsoft.com";
        private static readonly string DEFAULT_BRANCH = "main";
        private static readonly string PUBLIC_SPECS_REPO = "azure-rest-api-specs";
        private static readonly string PRIVATE_SPECS_REPO = "azure-rest-api-specs-pr";
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

        internal static readonly HashSet<string> languagesforDataplane = new(System.StringComparer.OrdinalIgnoreCase)
        {
            ".NET", "Java", "Python", "JavaScript"
        };

        internal static readonly HashSet<string> languagesforMgmtplane = new(System.StringComparer.OrdinalIgnoreCase)
        {
            ".NET", "Java", "Python", "JavaScript", "Go"
        };

        // Languages that are supported (allowed) for a data plane release plan. Go is optional for
        // data plane: it must not cause an "unsupported language" failure when present, but it is
        // not part of the mandatory language set (languagesforDataplane) used for exclusion tracking.
        internal static readonly HashSet<string> supportedLanguagesforDataplane = new(System.StringComparer.OrdinalIgnoreCase)
        {
            ".NET", "Java", "Python", "JavaScript", "Go"
        };

        [GeneratedRegex("https:\\/\\/github.com\\/Azure\\/azure-sdk\\/issues\\/([0-9]+)")]
        private static partial Regex NameSpaceIssueUrlRegex();

        [GeneratedRegex("https:\\/\\/github.com\\/Azure\\/azure-rest-api-specs(-pr)?\\/pull\\/[0-9]+\\/?", RegexOptions.IgnoreCase)]
        private static partial Regex PullRequestUrlRegex();

        [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}(-preview)?$")]
        private static partial Regex ApiVersionRegex();

        protected override List<Command> GetCommands() =>
        [
            new McpCommand(getReleasePlanDetailsCommandName, "Get release plan details", GetReleasePlanToolName) { releasePlanNumberOpt, workItemIdOpt, optionalPullRequestOpt, optionalTypeSpecProjectPathOpt, optionalApiReleaseTypeOpt },
            new McpCommand(createReleasePlanCommandName, "Create a release plan", CreateReleasePlanToolName)
            {
                typeSpecProjectPathOpt,
                targetReleaseOpt,
                apiReleaseTypeOpt,
                serviceTreeIdOpt,
                productTreeIdOpt,
                optionalPullRequestOpt,
                isTestReleasePlanOpt,
                forceCreateReleasePlanOpt,
            },
            new McpCommand(linkNamespaceApprovalIssueCommandName, "Link namespace approval issue to release plan", LinkNamespaceApprovalToolName) { workItemIdOpt, namespaceApprovalIssueOpt, },
            new McpCommand(checkApiReadinessCommandName, "Check if API spec is ready to generate SDK", CheckApiSpecReadyToolName) { typeSpecProjectPathOpt, pullRequestNumberOpt, workItemIdOpt, },
            new McpCommand(linkSdkPrCommandName, "Link SDK pull request to release plan", LinkSdkPullRequestToolName) { languageOpt, pullRequestOpt, workItemIdOpt, releasePlanNumberOpt, },
            new McpCommand(listOverdueReleasePlansCommandName, "List in-progress release plans that are past their SDK release deadline") { notifyOwnersOpt, azureSDKEmailerUriOpt, },
            new McpCommand(updateApiSpecPullRequestCommandName, "Update TypeSpec pull request URL in a release plan", UpdateApiSpecPullRequestToolName) { pullRequestOpt, workItemIdOpt, releasePlanNumberOpt, },
            new McpCommand(getServiceDetailsCommandName, "Get service and product details (service tree ID, service ID, package display name) in service tree for TypeSpec project", GetServiceDetailsToolName) { typeSpecProjectOpt, },
            new McpCommand(abandonReleasePlanCommandName, "Abandon a release plan", AbandonReleasePlanToolName) { workItemIdOpt, releasePlanNumberOpt, },
            new McpCommand(getKpiAttestationStatusCommandName, "Get KPI attestation status for a product by product ID and release plan type", GetKPIAttestationStatusToolName) { kpiProductIdOpt, releasePlanTypeOpt, kpiTypeSpecProjectPathOpt, kpiIsTestReleasePlanOpt, },
            new McpCommand(updateReleasePlanCommandName, "Update an existing release plan", UpdateReleasePlanToolName)
            {
                updateTypeSpecProjectPathOpt,
                workItemIdOpt,
                updateSdkReleaseTypeOpt,
                optionalPullRequestOpt,
                optionalServiceTreeIdOpt,
                optionalProductTreeIdOpt,
                productTypeOpt,
            },
            new McpCommand(updateReleasePlanTargetCommandName, "Update the SDK release target month on an existing release plan", UpdateReleasePlanTargetToolName) { workItemIdOpt, targetReleaseOpt, },
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
                    var getPullRequest = commandParser.GetValue(optionalPullRequestOpt);
                    var getTypeSpecPath = commandParser.GetValue(optionalTypeSpecProjectPathOpt);
                    var getApiReleaseType = commandParser.GetValue(optionalApiReleaseTypeOpt);
                    return await GetReleasePlan(releasePlanNumber, workItemId, specPullRequestUrl: getPullRequest, typeSpecProjectPath: getTypeSpecPath, apiReleaseType: getApiReleaseType, ct: ct);

                case createReleasePlanCommandName:
                    var typeSpecProjectPath = commandParser.GetValue(typeSpecProjectPathOpt);
                    var targetReleaseMonthYear = commandParser.GetValue(targetReleaseOpt);
                    var serviceTreeId = commandParser.GetValue(serviceTreeIdOpt);
                    var productTreeId = commandParser.GetValue(productTreeIdOpt);
                    var specPullRequestUrl = commandParser.GetValue(optionalPullRequestOpt);
                    var apiReleaseType = commandParser.GetValue(apiReleaseTypeOpt);
                    var isTestReleasePlan = commandParser.GetValue(isTestReleasePlanOpt);
                    var forceCreateReleasePlan = commandParser.GetValue(forceCreateReleasePlanOpt);
                    return await CreateReleasePlan(
                        null,
                        typeSpecProjectPath,
                        targetReleaseMonthYear,
                        apiReleaseType,
                        specPullRequestUrl: specPullRequestUrl,
                        serviceTreeId: serviceTreeId,
                        productTreeId: productTreeId,
                        isTestReleasePlan: isTestReleasePlan,
                        forceCreateReleasePlan: forceCreateReleasePlan,
                        ct: ct
                    );

                case linkNamespaceApprovalIssueCommandName:
                    return await LinkNamespaceApprovalIssue(commandParser.GetValue(workItemIdOpt), commandParser.GetValue(namespaceApprovalIssueOpt), ct);

                case checkApiReadinessCommandName:
                    return await CheckApiReadyForSDKGeneration(commandParser.GetValue(typeSpecProjectPathOpt), pullRequestNumber: commandParser.GetValue(pullRequestNumberOpt), workItemId: commandParser.GetValue(workItemIdOpt), ct: ct);

                case linkSdkPrCommandName:
                    return await LinkSdkPullRequestToReleasePlan(commandParser.GetValue(languageOpt), commandParser.GetValue(pullRequestOpt), workItemId: commandParser.GetValue(workItemIdOpt), releasePlanId: commandParser.GetValue(releasePlanNumberOpt), ct: ct);

                case listOverdueReleasePlansCommandName:
                    return await ListOverdueReleasePlans(commandParser.GetValue(notifyOwnersOpt), commandParser.GetValue(azureSDKEmailerUriOpt), ct);

                case updateApiSpecPullRequestCommandName:
                    return await UpdateSpecPullRequestInReleasePlan(specPullRequestUrl: commandParser.GetValue(pullRequestOpt), workItemId: commandParser.GetValue(workItemIdOpt), releasePlanId: commandParser.GetValue(releasePlanNumberOpt), ct: ct);

                case getServiceDetailsCommandName:
                    return await GetProductByTypeSpecPath(commandParser.GetValue(typeSpecProjectOpt), ct);
                case abandonReleasePlanCommandName:
                    return await AbandonReleasePlan(workItemId: commandParser.GetValue(workItemIdOpt), releasePlanId: commandParser.GetValue(releasePlanNumberOpt), ct: ct);

                case getKpiAttestationStatusCommandName:
                    return await GetKPIAttestationStatus(commandParser.GetValue(kpiProductIdOpt), commandParser.GetValue(releasePlanTypeOpt), commandParser.GetValue(kpiTypeSpecProjectPathOpt), commandParser.GetValue(kpiIsTestReleasePlanOpt), ct);

                case updateReleasePlanCommandName:
                    return await UpdateReleasePlan(
                        typeSpecProjectPath: commandParser.GetValue(updateTypeSpecProjectPathOpt),
                        workItemId: commandParser.GetValue(workItemIdOpt),
                        sdkReleaseType: commandParser.GetValue(updateSdkReleaseTypeOpt),
                        specPullRequestUrl: commandParser.GetValue(optionalPullRequestOpt),
                        serviceTreeId: commandParser.GetValue(optionalServiceTreeIdOpt),
                        productTreeId: commandParser.GetValue(optionalProductTreeIdOpt),
                        productType: commandParser.GetValue(productTypeOpt),
                        ct: ct
                    );

                case updateReleasePlanTargetCommandName:
                    return await UpdateReleasePlanTarget(
                        workItemId: commandParser.GetValue(workItemIdOpt),
                        targetReleaseMonthYear: commandParser.GetValue(targetReleaseOpt),
                        ct: ct
                    );

                default:
                    logger.LogError("Unknown command: {command}", command);
                    return new DefaultCommandResponse { ResponseError = $"Unknown command: '{command}'" };
            }
        }


        [McpServerTool(Name = GetReleasePlanToolName), Description("Get Release Plan: Get release plan work item details for a given release plan number/Id or work item id. If neither is provided, finds the active release plan by TypeSpec project path or spec PR URL. Optionally filter by API release type (allowed values: Private Preview, Public Preview, GA).")]
        public async Task<ReleasePlanResponse> GetReleasePlan(int releasePlanId = 0, int workItemId = 0, string? specPullRequestUrl = null, string? typeSpecProjectPath = null, string? apiReleaseType = null, CancellationToken ct = default)
        {
            try
            {
                ReleasePlanWorkItem? releasePlan = null;

                // Parse API release type if provided
                ApiReleaseType parsedApiReleaseType = ApiReleaseType.Unknown;
                if (!string.IsNullOrWhiteSpace(apiReleaseType))
                {
                    if (!ApiReleaseTypeExtensions.TryParseFromUserInput(apiReleaseType, out parsedApiReleaseType))
                    {
                        return new ReleasePlanResponse { ResponseError = $"Invalid API release type '{apiReleaseType}'. Allowed values: Private Preview, Public Preview, GA" };
                    }
                }

                // Resolve absolute TypeSpec project path to repo-relative path before any lookup
                if (!string.IsNullOrWhiteSpace(typeSpecProjectPath) && typeSpecHelper.IsValidTypeSpecProjectPath(typeSpecProjectPath))
                {
                    typeSpecProjectPath = typeSpecHelper.GetTypeSpecProjectRelativePath(typeSpecProjectPath);
                }

                if (workItemId != 0)
                {
                    releasePlan = await devOpsService.GetReleasePlanForWorkItemAsync(workItemId, ct);
                }
                else if (releasePlanId != 0)
                {
                    releasePlan = await devOpsService.GetReleasePlanAsync(releasePlanId, ct);
                }
                else if (!string.IsNullOrWhiteSpace(specPullRequestUrl))
                {
                    ValidatePullRequestUrl(specPullRequestUrl);
                    releasePlan = await devOpsService.GetReleasePlanAsync(specPullRequestUrl, parsedApiReleaseType, ct);

                    // Fall back to TypeSpec project path if spec PR lookup failed
                    if (releasePlan == null && !string.IsNullOrWhiteSpace(typeSpecProjectPath))
                    {
                        releasePlan = await devOpsService.GetReleasePlanByTypeSpecProjectPathAsync(typeSpecProjectPath, apiReleaseType: parsedApiReleaseType, ct: ct);
                    }
                }
                else if (!string.IsNullOrWhiteSpace(typeSpecProjectPath))
                {
                    releasePlan = await devOpsService.GetReleasePlanByTypeSpecProjectPathAsync(typeSpecProjectPath, apiReleaseType: parsedApiReleaseType, ct: ct);
                }
                else
                {
                    return new ReleasePlanResponse
                    {
                        ResponseError = "At least one of the following options must be provided: Work item ID, Release plan ID, APi spec pull request or TypeSpec project path."
                    };
                }

                if (releasePlan == null)
                {
                    return new ReleasePlanResponse { ResponseError = "Failed to get release plan details." };
                }

                var response = new ReleasePlanResponse
                {
                    ReleasePlanDetails = releasePlan,
                    Message = "Successfully retrieved release plan."
                };

                // Check API spec readiness if spec PR is linked
                if (!string.IsNullOrEmpty(releasePlan.ActiveSpecPullRequest))
                {
                    if (string.IsNullOrEmpty(releasePlan.APISpecProjectPath))
                    {
                        response.NextSteps = ["Update the release plan to set the TypeSpec project path. TypeSpec project path is required to check API spec readiness."];
                    }
                    else
                    {
                        var prNumber = ParsePullRequestNumberFromUrl(releasePlan.ActiveSpecPullRequest);
                        if (prNumber > 0)
                        {
                            var specReadiness = await CheckApiReadyForSDKGeneration(releasePlan.APISpecProjectPath, prNumber, releasePlan.WorkItemId, ct);
                            if (specReadiness.Status == "Success")
                            {
                                releasePlan.IsSpecApproved = true;
                                // API spec is approved/merged, so the next step is to generate the SDK.
                                // Surface the pipeline-based generation tool explicitly so the agent does
                                // not pick an unrelated tool (e.g. azsdk_get_sdk_pull_request_link).                                // Append so we don't clobber any NextSteps set by earlier logic.
                                (response.NextSteps ??= []).Add("API spec is approved. Run SDK generation for all languages using the azsdk_run_generate_sdk tool.");
                            }
                        }
                    }
                }

                return response;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to get release plan details");
                return new ReleasePlanResponse { ResponseError = $"Failed to get release plan details: {ex.Message}" };
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
        public async Task<ReleaseWorkflowResponse> AbandonReleasePlan(int workItemId = 0, int releasePlanId = 0, CancellationToken ct = default)
        {
            try
            {
                if (workItemId == 0 && releasePlanId == 0)
                {
                    return new ReleaseWorkflowResponse { ResponseError = "Either work item ID or release plan ID must be provided." };
                }

                // Get the release plan to verify it exists
                var releasePlan = workItemId != 0
                    ? await devOpsService.GetReleasePlanForWorkItemAsync(workItemId, ct)
                    : await devOpsService.GetReleasePlanAsync(releasePlanId, ct);

                if (releasePlan == null)
                {
                    return new ReleaseWorkflowResponse { ResponseError = "Failed to find release plan." };
                }

                // Update the work item status to "Abandoned"
                var fieldsToUpdate = new Dictionary<string, string>
                {
                    { "System.State", "Abandoned" }
                };

                var updatedWorkItem = await devOpsService.UpdateWorkItemAsync(releasePlan.WorkItemId, fieldsToUpdate, ct);

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

        /// <summary>
        /// Updates an existing release plan with new details. Finds the release plan by work item ID,
        /// or by TypeSpec project path/spec PR URL if work item ID is not provided.
        /// Runs the @azure-tools/typespec-metadata emitter to resolve package names and updates SDK details.
        /// </summary>
        [McpServerTool(Name = UpdateReleasePlanToolName), Description("Update an existing release plan. Updates spec PR URL, TypeSpec project path, SDK release type, and optionally service/product IDs. " +
            "When a product ID is provided, product name, product lifecycle and product type are resolved from a matching triage work item in Azure DevOps. " +
            "If the product type cannot be determined, provide it via productType (allowed values: Offering, Feature, Sku). " +
            "Runs TypeSpec metadata emitter to resolve package names and updates SDK details. If work item ID is not provided, finds the active release plan by TypeSpec project path or spec PR URL.")]
        public async Task<ReleasePlanResponse> UpdateReleasePlan(string typeSpecProjectPath, string specPullRequestUrl = "", string sdkReleaseType = "", int workItemId = 0, string serviceTreeId = "", string productTreeId = "", ProductType productType = ProductType.Unknown, CancellationToken ct = default)
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

                var supportedReleaseTypes = new[] { "beta", "stable" };
                if (!supportedReleaseTypes.Contains(sdkReleaseType))
                {
                    return new ReleasePlanResponse { ResponseError = $"Invalid SDK release type. Supported release types are: {string.Join(", ", supportedReleaseTypes)}" };
                }

                if (!string.IsNullOrEmpty(specPullRequestUrl))
                {
                    ValidatePullRequestUrl(specPullRequestUrl);
                }

                if (string.IsNullOrEmpty(typeSpecProjectPath))
                {
                    return new ReleasePlanResponse { ResponseError = "TypeSpec project path is required." };
                }

                if (!string.IsNullOrEmpty(serviceTreeId) && !Guid.TryParse(serviceTreeId, out _))
                {
                    return new ReleasePlanResponse { ResponseError = $"Service tree ID '{serviceTreeId}' is not a valid GUID." };
                }

                if (!string.IsNullOrEmpty(productTreeId) && !Guid.TryParse(productTreeId, out _))
                {
                    return new ReleasePlanResponse { ResponseError = $"Product tree ID '{productTreeId}' is not a valid GUID." };
                }

                // Find the release plan
                ReleasePlanWorkItem? releasePlan = null;
                if (workItemId != 0)
                {
                    // The resolver accepts either a Release Plan ID or a work item ID.
                    releasePlan = await devOpsService.ResolveReleasePlanByIdAsync(workItemId, ct);
                }

                // Resolve TypeSpec project relative path
                string specProject;
                bool isMgmt;
                if (typeSpecHelper.IsUrl(typeSpecProjectPath))
                {
                    specProject = typeSpecHelper.GetTypeSpecProjectRelativePathFromUrl(typeSpecProjectPath);
                    isMgmt = typeSpecHelper.IsTypeSpecUrlForMgmtPlane(typeSpecProjectPath);
                }
                else
                {
                    specProject = typeSpecHelper.GetTypeSpecProjectRelativePath(typeSpecProjectPath);
                    isMgmt = typeSpecHelper.IsTypeSpecProjectForMgmtPlane(typeSpecProjectPath);
                }

                if(string.IsNullOrEmpty(specProject))
                {
                    logger.LogWarning("Failed to identify a TypeSpec project path from {typeSpecProjectPath}", typeSpecProjectPath);
                    return new ReleasePlanResponse
                    {
                        ResponseError = $"Could not resolve a TypeSpec project from '{typeSpecProjectPath}'. " +
                            "Provide the absolute path to the TypeSpec project directory, a URL to the TypeSpec project in the azure-rest-api-specs or azure-rest-api-specs-pr repository, " +
                            "or run this command from within a local clone of one of those repositories.",
                        NextSteps = ["Provide the absolute path or a valid azure-rest-api-specs / azure-rest-api-specs-pr URL to the TypeSpec project, or run this command from within a local clone of one of those repositories."]
                    };
                }
                               
                //Checkfor release plan using spec PR first and then using spec path
                if (releasePlan == null && !string.IsNullOrEmpty(specPullRequestUrl))
                {
                    // Try to find by spec PR URL
                    logger.LogInformation("Release plan not found by TypeSpec project path, searching by spec PR URL: {specPullRequestUrl}", specPullRequestUrl);
                    releasePlan = await devOpsService.GetReleasePlanAsync(specPullRequestUrl, ct: ct);
                }

                if (releasePlan == null)
                {
                    // Try to find by TypeSpec project path
                    logger.LogInformation("Work item not found or not provided, searching by TypeSpec project path: {typeSpecProjectPath}", specProject);
                    releasePlan = await devOpsService.GetReleasePlanByTypeSpecProjectPathAsync(specProject, ct: ct);
                }

                if (releasePlan == null)
                {
                    return new ReleasePlanResponse { ResponseError = "No active release plan found. Provide a valid work item ID, TypeSpec project path, or spec PR URL." };
                }

                logger.LogInformation("Found release plan work item {WorkItemId} to update", releasePlan.WorkItemId);

                // Validate spec PR against release type if both are available
                if (!string.IsNullOrEmpty(specPullRequestUrl) && releasePlan.ApiReleaseType != ApiReleaseType.Unknown)
                {
                    ValidateSpecPullRequestForReleaseType(specPullRequestUrl, releasePlan.ApiReleaseType);
                }

                // Update release plan fields
                var fieldsToUpdate = new Dictionary<string, string>
                {
                    { "Custom.SDKtypetobereleased", sdkReleaseType },
                    { "Custom.ApiSpecProjectPath", specProject },
                };

                if (!string.IsNullOrEmpty(serviceTreeId))
                {
                    fieldsToUpdate["Custom.ServiceTreeID"] = serviceTreeId;
                }

                if (!string.IsNullOrEmpty(productTreeId))
                {
                    fieldsToUpdate["Custom.ProductServiceTreeID"] = productTreeId;

                    // Resolve product details (name, lifecycle, type) from a matching triage work item.
                    var triageProductInfo = await devOpsService.GetProductInfoFromTriageWorkItemAsync(productTreeId, ct);

                    var resolvedProductType = triageProductInfo?.ProductType ?? string.Empty;

                    // An explicitly provided product type always takes precedence.
                    if (productType != ProductType.Unknown)
                    {
                        resolvedProductType = productType.ToAdoFieldValue();
                    }

                    // If the product type is still unknown, ask the user to provide it before proceeding.
                    if (ProductTypeExtensions.FromAdoFieldValue(resolvedProductType) == ProductType.Unknown)
                    {
                        logger.LogInformation("Product type could not be determined for product ID {productTreeId}. Requesting product type from user.", productTreeId);
                        return new ReleasePlanResponse
                        {
                            ResponseError = $"Product type could not be determined for product ID '{productTreeId}'. Please provide the product type to update the release plan.",
                            NextSteps = ["Ask the user to provide the product type. Allowed values are: Offering, Feature, Sku. Then re-run the update release plan command/tool with the provided product type."]
                        };
                    }

                    if (triageProductInfo != null)
                    {
                        if (!string.IsNullOrEmpty(triageProductInfo.ProductName))
                        {
                            fieldsToUpdate["Custom.ProductName"] = triageProductInfo.ProductName;
                        }
                        if (!string.IsNullOrEmpty(triageProductInfo.ProductLifecycle))
                        {
                            fieldsToUpdate["Custom.ProductLifecycle"] = triageProductInfo.ProductLifecycle;
                        }
                    }

                    fieldsToUpdate["Custom.ProductType"] = resolvedProductType;
                }

                await devOpsService.UpdateWorkItemAsync(releasePlan.WorkItemId, fieldsToUpdate, ct);
                logger.LogInformation("Updated release plan fields for work item {WorkItemId}", releasePlan.WorkItemId);

                // Update spec PR on the API spec child work item only if provided and different from current
                if (!string.IsNullOrEmpty(specPullRequestUrl) &&
                    (string.IsNullOrEmpty(releasePlan.ActiveSpecPullRequest) || !releasePlan.ActiveSpecPullRequest.Equals(specPullRequestUrl, StringComparison.OrdinalIgnoreCase)))
                {
                    await devOpsService.UpdateSpecPullRequestAsync(releasePlan.WorkItemId, specPullRequestUrl, ct);
                    logger.LogInformation("Updated spec PR URL in release plan {WorkItemId}", releasePlan.WorkItemId);
                }

                // Run TypeSpec metadata emitter to get package names
                List<PackageInfo>? resolvedPackages = null;
                if (!typeSpecHelper.IsUrl(typeSpecProjectPath) && TypeSpecProject.IsValidTypeSpecProjectPath(typeSpecProjectPath))
                {
                    var tspProject = await typeSpecHelper.ParseTypeSpecProjectAsync(typeSpecProjectPath, npxHelper, logger, ct);
                    resolvedPackages = tspProject?.Packages;
                }
                else if (typeSpecHelper.IsUrl(typeSpecProjectPath))
                {
                    logger.LogWarning("Cannot run TypeSpec metadata emitter for URL-based TypeSpec project paths. Skipping emitter.");
                }

                if (resolvedPackages != null && resolvedPackages.Count > 0)
                {
                    logger.LogInformation("Resolved {count} package names from TypeSpec metadata emitter", resolvedPackages.Count);
                    var sdkInfos = resolvedPackages.Select(p => new SDKInfo
                    {
                        Language = p.Language.ToWorkItemString(),
                        PackageName = p.PackageName ?? string.Empty
                    }).ToList();
                    var updated = await devOpsService.UpdateReleasePlanSDKDetailsAsync(releasePlan.WorkItemId, sdkInfos, ct);
                    if (!updated)
                    {
                        logger.LogWarning("Failed to update SDK package details in release plan {WorkItemId}", releasePlan.WorkItemId);
                    }
                }
                else
                {
                    logger.LogWarning("No package names resolved from TypeSpec metadata emitter for {typeSpecProjectPath}", typeSpecProjectPath);
                }

                releasePlan = await devOpsService.GetReleasePlanForWorkItemAsync(releasePlan.WorkItemId, ct);

                if (releasePlan == null)
                {
                    return new ReleasePlanResponse { ResponseError = "Failed to retrieve updated release plan after update." };
                }

                // Check API spec readiness if release plan has a spec PR
                var activeSpecPr = !string.IsNullOrEmpty(releasePlan.ActiveSpecPullRequest) ? releasePlan.ActiveSpecPullRequest : specPullRequestUrl;
                if (!string.IsNullOrEmpty(activeSpecPr))
                {
                    var prNumber = ParsePullRequestNumberFromUrl(activeSpecPr);
                    if (prNumber > 0)
                    {
                        var specReadiness = await CheckApiReadyForSDKGeneration(specProject, prNumber, releasePlan.WorkItemId, ct);
                        if (specReadiness.Status == "Success")
                        {
                            releasePlan.IsSpecApproved = true;
                        }

                        // For private preview release plans, mark as finished if spec PR is merged
                        if (releasePlan.ApiReleaseType == ApiReleaseType.PrivatePreview)
                        {
                            var isPrivateSpec = activeSpecPr.Contains(PRIVATE_SPECS_REPO, StringComparison.OrdinalIgnoreCase);
                            var specRepoName = isPrivateSpec ? PRIVATE_SPECS_REPO : PUBLIC_SPECS_REPO;
                            var specPr = await githubService.GetPullRequestAsync(REPO_OWNER, specRepoName, prNumber, ct);
                            if (specPr?.Merged == true)
                            {
                                await devOpsService.UpdateWorkItemAsync(releasePlan.WorkItemId, new Dictionary<string, string>
                                    {
                                        { "System.State", "Finished" }
                                    }, ct);
                                logger.LogInformation("Private preview release plan {WorkItemId} marked as Finished because spec PR is merged", releasePlan.WorkItemId);
                            }
                        }
                    }
                }

                return new ReleasePlanResponse
                {
                    Message = $"Successfully updated release plan {releasePlan.WorkItemId}.",
                    ReleasePlanDetails = releasePlan,
                    TypeSpecProject = specProject,
                    PackageType = isMgmt ? SdkType.Management : SdkType.Dataplane
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to update release plan");
                return new ReleasePlanResponse { ResponseError = $"Failed to update release plan: {ex.Message}" };
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
                throw new Exception($"Invalid spec pull request URL '{specPullRequestUrl}'. It should be a valid GitHub pull request to azure-rest-api-specs or azure-rest-api-specs-pr repo.");
            }
        }

        private static void ValidateSpecPullRequestForReleaseType(string specPullRequestUrl, ApiReleaseType apiReleaseType)
        {
            var error = apiReleaseType.ValidateSpecPullRequest(specPullRequestUrl);
            if (error != null)
            {
                throw new Exception(error);
            }
        }

        /// <summary>
        /// Extracts the pull request number from a spec pull request URL.
        /// </summary>
        private static int ParsePullRequestNumberFromUrl(string pullRequestUrl)
        {
            if (string.IsNullOrEmpty(pullRequestUrl))
            {
                return 0;
            }

            var match = Regex.Match(pullRequestUrl, @"/pull/(\d+)");
            return match.Success ? int.Parse(match.Groups[1].Value) : 0;
        }

        /// <summary>
        /// Resolves the email address of the spec pull request author by mapping the PR author's
        /// GitHub username to their user profile. Returns an empty string when the author or email
        /// cannot be determined.
        /// </summary>
        private async Task<string> ResolveSpecPullRequestAuthorEmailAsync(string specPullRequestUrl, CancellationToken ct)
        {
            var prNumber = ParsePullRequestNumberFromUrl(specPullRequestUrl);
            if (prNumber <= 0)
            {
                return string.Empty;
            }

            var isPrivateSpec = specPullRequestUrl.Contains(PRIVATE_SPECS_REPO, StringComparison.OrdinalIgnoreCase);
            var specRepoName = isPrivateSpec ? PRIVATE_SPECS_REPO : PUBLIC_SPECS_REPO;
            var specPr = await githubService.GetPullRequestAsync(REPO_OWNER, specRepoName, prNumber, ct);
            var authorLogin = specPr?.User?.Login;
            if (string.IsNullOrEmpty(authorLogin))
            {
                logger.LogWarning("Could not determine spec PR author for '{specPullRequestUrl}'.", specPullRequestUrl);
                return string.Empty;
            }

            logger.LogInformation("Resolving email for spec PR author '{authorLogin}'.", authorLogin);
            var userProfile = await userHelper.GetUserProfile(authorLogin, ct);
            return userProfile?.Aad?.EmailAddress ?? string.Empty;
        }

        private async Task ValidateCreateReleasePlanInputAsync(string typeSpecProjectPath, string serviceTreeId, string productTreeId, string specPullRequestUrl, ApiReleaseType apiReleaseType, CancellationToken ct)
        {
            if (!string.IsNullOrEmpty(specPullRequestUrl))
            {
                ValidatePullRequestUrl(specPullRequestUrl);
            }

            if (string.IsNullOrEmpty(typeSpecProjectPath))
            {
                throw new Exception("TypeSpec project path is empty. Cannot create a release plan without a TypeSpec project root path");
            }

            // Skip filesystem validation for URLs since GetSpecRepoRootPath expects local paths
            // For Private Preview, allow private spec repos
            if (!typeSpecHelper.IsUrl(typeSpecProjectPath) && apiReleaseType != ApiReleaseType.PrivatePreview)
            {
                var repoRoot = typeSpecHelper.GetSpecRepoRootPath(typeSpecProjectPath);

                // When this command is run from a language SDK repository (or any other directory) with a
                // relative path, the path does not resolve within the azure-rest-api-specs(-pr) repository.
                // In that case, guide the user instead of incorrectly reporting a private-repo error.
                bool isSpecRepo = false;
                if (!string.IsNullOrEmpty(repoRoot))
                {
                    try
                    {
                        isSpecRepo = await typeSpecHelper.IsRepoPathForSpecRepoAsync(repoRoot, ct);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        logger.LogDebug(ex, "Failed to determine whether '{RepoRoot}' is an azure-rest-api-specs(-pr) repository; falling back to user guidance.", repoRoot);
                        isSpecRepo = false;
                    }
                }

                if (!isSpecRepo)
                {
                    throw new Exception(
                        $"Could not locate the Azure REST API specs repository (azure-rest-api-specs or azure-rest-api-specs-pr) from the TypeSpec project path '{typeSpecProjectPath}'. " +
                        "If you are running this from a language SDK repository or another directory, provide the absolute path to the TypeSpec project, " +
                        "or run this command from within a local clone of the Azure/azure-rest-api-specs repository.");
                }

                // Ensure a release plan is created only if the API specs pull request is in a public repository.
                if (!await typeSpecHelper.IsRepoPathForPublicSpecRepoAsync(repoRoot, ct))
                {
                    throw new Exception("""
                        SDK generation and release require the API specs pull request to be in the public azure-rest-api-specs repository.
                        Please create a pull request in the public Azure/azure-rest-api-specs repository to move your specs changes to public.
                        A release plan cannot be created for SDK generation using a pull request in a private repository.
                        Use Private Preview API release type if you are working with a private spec repository.
                        """);
                }
            }

            if (!string.IsNullOrWhiteSpace(serviceTreeId) && !Guid.TryParse(serviceTreeId, out _))
            {
                throw new Exception($"Service tree ID '{serviceTreeId}' is not a valid GUID.");
            }

            if (!string.IsNullOrWhiteSpace(productTreeId) && !Guid.TryParse(productTreeId, out _))
            {
                throw new Exception($"Product tree ID '{productTreeId}' is not a valid GUID.");
            }
        }

        [McpServerTool(Name = CreateReleasePlanToolName), Description("Create Release Plan for a TypeSpec project and API release type. API release types support Private Preview, Public Preview, and GA. Service ID and product ID are optional and will be resolved from existing release plans when available.")]
        public async Task<ReleasePlanResponse> CreateReleasePlan(IProgress<ProgressNotificationValue>? progress, string typeSpecProjectPath, string targetReleaseMonthYear, string apiReleaseType, string specPullRequestUrl = "", string serviceTreeId = "", string productTreeId = "", bool isTestReleasePlan = false, bool forceCreateReleasePlan = false, CancellationToken ct = default)
        {
            try
            {         
                // Validate and map API release type
                if (!ApiReleaseTypeExtensions.TryParseFromUserInput(apiReleaseType, out var parsedApiReleaseType))
                {
                    return new ReleasePlanResponse { ResponseError = $"Invalid API release type '{apiReleaseType}'. Supported values are: Private Preview, Public Preview, GA" };
                }

                // SDK release type is always derived from the API release type to prevent
                // a stable SDK release from a preview API version.
                var sdkReleaseType = parsedApiReleaseType.GetDefaultSdkReleaseType();

                await ValidateCreateReleasePlanInputAsync(typeSpecProjectPath, serviceTreeId, productTreeId, specPullRequestUrl, parsedApiReleaseType, ct);

                // Validate spec PR against release type
                ValidateSpecPullRequestForReleaseType(specPullRequestUrl, parsedApiReleaseType);

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

                // Check environment variable to determine if this should be a test release plan
                var isAgentTesting = environmentHelper.GetBooleanVariable("AZSDKTOOLS_AGENT_TESTING", false);
                if (isAgentTesting)
                {
                    isTestReleasePlan = true;
                    logger.LogInformation("AZSDKTOOLS_AGENT_TESTING environment variable is set to true, creating test release plan");
                }

                // Get service and product id from previous release plan
                string productName = Path.GetFileName(specProject);
                string productType = string.Empty;
                string productLifecycle = string.Empty;
                if (string.IsNullOrEmpty(serviceTreeId) || string.IsNullOrEmpty(productTreeId))
                {
                    logger.LogInformation("Service and product id are not available. Checking for a previous release plan with same TypeSpec project {specProject}", specProject);
                    // Get product and service tree Id from existing release plan.
                    var productDetails = await devOpsService.GetProductInfoByTypeSpecProjectPathAsync(specProject, ct);
                    if (productDetails != null)
                    {
                        logger.LogInformation("Found product details for TypeSpec project {specProject} from previous release plans.", specProject);
                        serviceTreeId = string.IsNullOrEmpty(serviceTreeId) ? productDetails?.ServiceId ?? string.Empty : serviceTreeId;
                        productTreeId = string.IsNullOrEmpty(productTreeId) ? productDetails?.ProductServiceTreeId ?? string.Empty : productTreeId;

                        // Copy product name, product type and product lifecycle from the previous release plan
                        if (!string.IsNullOrEmpty(productDetails?.ProductName))
                        {
                            productName = productDetails.ProductName;
                        }
                        productType = productDetails?.ProductType ?? string.Empty;
                        productLifecycle = productDetails?.ProductLifecycle ?? string.Empty;
                    }

                    if (string.IsNullOrEmpty(serviceTreeId) || string.IsNullOrEmpty(productTreeId))
                    {
                        logger.LogInformation("Service and/or product ID could not be resolved from previous release plans for TypeSpec project {specProject}. Creating release plan without unresolved IDs.", specProject);
                    }
                }

                if (!forceCreateReleasePlan)
                { 
                    if (isValidTypeSpec && !string.IsNullOrEmpty(specProject))
                    {
                        logger.LogInformation("Checking for existing in-progress release plan for TypeSpec project: {specProject} with API release type: {apiReleaseType}", specProject, parsedApiReleaseType.ToDisplayLabel());
                        var existingReleasePlan = await devOpsService.GetReleasePlanByTypeSpecProjectPathAsync(specProject, apiReleaseType: parsedApiReleaseType, ct: ct);
                        if (existingReleasePlan != null)
                        {
                            return new ReleasePlanResponse
                            {
                                Message = $"An active release plan already exists for the TypeSpec project: {specProject}. "
                                +  $"Release plan link: {existingReleasePlan.ReleasePlanLink}",
                                ReleasePlanDetails = existingReleasePlan,
                                NextSteps = ["Prompt user to confirm whether to use existing release plan or force create a new release plan."]
                            };
                        }
                    }

                    // Check for existing release plan for the given pull request URL (only if spec PR is provided).
                    if (!string.IsNullOrEmpty(specPullRequestUrl))
                    {
                        logger.LogInformation("Checking for existing release plan for pull request URL: {specPullRequestUrl}", specPullRequestUrl);
                        var existingReleasePlan = await devOpsService.GetReleasePlanAsync(specPullRequestUrl, parsedApiReleaseType, ct);
                        if (existingReleasePlan != null && existingReleasePlan.WorkItemId > 0)
                        {
                            return new ReleasePlanResponse
                            {
                                Message = $"A {parsedApiReleaseType.ToDisplayLabel()} release plan already exists for the pull request: {specPullRequestUrl}. Release plan link: {existingReleasePlan.ReleasePlanLink}",
                                ReleasePlanDetails = existingReleasePlan,
                                NextSteps = ["Prompt user to confirm whether to use existing release plan or force create a new release plan."]
                            };
                        }
                    }
                }

                List<string> warnings = [];
                List<string> nextSteps = [];
                var specType = isValidTypeSpec ? "TypeSpec" : "OpenAPI";
                logger.LogInformation("Attempting to retrieve current user email.");

                var userEmail = "";
                try
                {
                    userEmail = await userHelper.GetUserEmail(ct);
                    logger.LogInformation("User email for release plan submission: {userEmail}", userEmail);
                }
                catch (Exception ex)
                {
                    var warning = "Failed to retrieve user email. Proceeding without user email for release plan submission.";
                    warnings.Add(warning);
                    logger.LogWarning(ex, "Failed to retrieve user email. Proceeding without user email for release plan submission.");
                }

                // Only run the GitHub user ID to email mapping when the current user's email could not be resolved.
                if (string.IsNullOrEmpty(userEmail) && !string.IsNullOrEmpty(specPullRequestUrl))
                {
                    try
                    {
                        logger.LogInformation("User email not available. Attempting to resolve spec PR author email for release plan submission.");
                        userEmail = await ResolveSpecPullRequestAuthorEmailAsync(specPullRequestUrl, ct);
                        logger.LogInformation("Resolved spec PR author email for release plan submission: {userEmail}", userEmail);
                    }
                    catch (Exception ex)
                    {
                        var warning = "Failed to resolve spec PR author email. Proceeding without user email for release plan submission.";
                        warnings.Add(warning);
                        logger.LogWarning(ex, "Failed to resolve spec PR author email. Proceeding without user email for release plan submission.");
                    }
                }

                var productDisplayName = productName;
                var releasePlan = new ReleasePlanWorkItem
                {
                    SDKReleaseMonth = targetReleaseMonthYear,
                    ServiceTreeId = serviceTreeId,
                    ProductTreeId = productTreeId,
                    SpecType = specType,
                    IsManagementPlane = isMgmt,
                    IsDataPlane = !isMgmt,
                    SpecPullRequests = string.IsNullOrEmpty(specPullRequestUrl) ? [] : [specPullRequestUrl],
                    IsTestReleasePlan = isTestReleasePlan,
                    SDKReleaseType = sdkReleaseType,
                    IsCreatedByAgent = true,
                    ReleasePlanSubmittedByEmail = userEmail,
                    APISpecProjectPath = specProject,
                    ProductName = productDisplayName,
                    ProductType = productType,
                    ProductLifecycle = productLifecycle,
                    ApiReleaseType = parsedApiReleaseType
                };

                var reporter = new ProgressReporter(progress, logger, totalSteps: 2, outputHelper);
                reporter.NextStep("Creating a release plan");
                var workItem = await devOpsService.CreateReleasePlanWorkItemAsync(releasePlan, ct);
                if (workItem == null)
                {
                    reporter.NextStep("Failed to create a release plan");
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

                    if (workItem.Fields.TryGetValue("Custom.ReleasePlanID", out var value) && value is int releasePlanId)
                    {
                        releasePlan.ReleasePlanId = releasePlanId;
                    }
                    else
                    {
                        releasePlan.ReleasePlanId = releasePlan.WorkItemId;
                    }
                    
                    // Attempt to update SDK details if the TypeSpec path is a valid local path                    
                    string sdkDetailsMessage = string.Empty;
                    bool isLocalValidTypeSpec = !typeSpecHelper.IsUrl(typeSpecProjectPath) && isValidTypeSpec && Directory.Exists(typeSpecProjectPath);

                    if (isLocalValidTypeSpec && releasePlan.WorkItemId > 0 && parsedApiReleaseType != ApiReleaseType.PrivatePreview)
                    {
                        try
                        {
                            await using (reporter.StartHeartbeat("Parsing TypeSpec project to add SDK details in release plan.", ct))
                            {
                                var sdkDetailsResult = await UpdateSDKDetailsInReleasePlan(releasePlan.WorkItemId, typeSpecProjectPath, ct);
                                if (!string.IsNullOrEmpty(sdkDetailsResult.ResponseError))
                                {
                                    logger.LogWarning("Failed to update SDK details in release plan: {Error}", sdkDetailsResult.ResponseError);
                                    warnings.Add($"Failed to update SDK details in the release plan: {sdkDetailsResult.ResponseError}");
                                    nextSteps.Add("Update SDK details in the release plan.");
                                }
                                else
                                {
                                    sdkDetailsMessage = sdkDetailsResult.Message ?? string.Empty;
                                    if (sdkDetailsResult.NextSteps?.Count > 0)
                                    {
                                        nextSteps.AddRange(sdkDetailsResult.NextSteps);
                                    }
                                }
                            }
                        }
                        catch (Exception sdkEx)
                        {
                            logger.LogWarning(sdkEx, "Failed to update SDK details in release plan");
                            warnings.Add($"Failed to update SDK details in the release plan: {sdkEx.Message}");
                            nextSteps.Add("Update SDK details in the release plan.");
                        }
                    }
                    else
                    {
                        nextSteps.Add("Update SDK details in the release plan.");
                    }

                    var message = new StringBuilder("Created release plan.");
                    if (!string.IsNullOrEmpty(sdkDetailsMessage))
                    {
                        message.AppendLine().Append(sdkDetailsMessage);
                    }

                    reporter.NextStep(message.ToString());

                    // Refresh the release plan and notify the submitter. Both are best-effort and must
                    // never fail release plan creation, so any error here is logged and swallowed.
                    try
                    {
                        //Refresh release plan to get latest details
                        releasePlan = await devOpsService.GetReleasePlanForWorkItemAsync(releasePlan.WorkItemId, ct);

                        // Recipient routing (To/CC) is owned by the email template.
                        // Silently completes when notifications are disabled.
                        var releasePlanEmail = new NewReleasePlanEmail(releasePlan);
                        await notificationService.SendEmailNotificationAsync(releasePlanEmail, ct);
                    }
                    catch (Exception notifyEx)
                    {
                        logger.LogWarning(notifyEx, "Failed to refresh release plan or send release plan notification.");
                    }

                    return new ReleasePlanResponse
                    {
                        Message = message.ToString(),
                        ReleasePlanDetails = releasePlan,
                        Warnings = warnings.Count > 0 ? warnings : null,
                        NextSteps = nextSteps.Count > 0 ? nextSteps : null,
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
            " Provide path to typespec project.")]
        public async Task<DefaultCommandResponse> UpdateSDKDetailsInReleasePlan(int releasePlanWorkItemId, string typeSpecProjectPath, CancellationToken ct)
        {
            try
            {
                if (releasePlanWorkItemId <= 0)
                {
                    return new DefaultCommandResponse { ResponseError = "Invalid release plan ID." };
                }
                
                if (!typeSpecHelper.IsValidTypeSpecProjectPath(typeSpecProjectPath))
                {
                    return new DefaultCommandResponse { ResponseError = $"TypeSpec project path '{typeSpecProjectPath}' is invalid. Provide a TypeSpec project path that contains tspconfig.yaml" };
                }
                logger.LogInformation("Updating SDK details in release plan work item ID: {ReleasePlanWorkItemId}", releasePlanWorkItemId);

                // Parse TypeSpec project to resolve package names
                var typeSpecProject = await typeSpecHelper.ParseTypeSpecProjectAsync(typeSpecProjectPath, npxHelper, logger, ct);
                var resolvedPackages = typeSpecProject?.Packages;
                if (resolvedPackages == null)
                {
                    return new DefaultCommandResponse { ResponseError = $"Failed to parse TypeSpec project at {typeSpecProjectPath}." };
                }

                // Get release plan. The resolver accepts either a Release Plan ID or a work item ID.
                var releasePlan = await devOpsService.ResolveReleasePlanByIdAsync(releasePlanWorkItemId, ct);
                if (releasePlan == null)
                {
                    return new DefaultCommandResponse { ResponseError = $"No release plan found with work item ID {releasePlanWorkItemId}" };
                }

                // The input may have been a Release Plan ID; use the resolved work item ID for writes.
                releasePlanWorkItemId = releasePlan.WorkItemId;

                var requiredLanguages = releasePlan.IsManagementPlane ? languagesforMgmtplane : languagesforDataplane;
                var supportedLanguages = releasePlan.IsManagementPlane ? languagesforMgmtplane : supportedLanguagesforDataplane;

                var resolvedKnownLanguagePackages = resolvedPackages
                    .Where(p => p.Language != SdkLanguage.Unknown)
                    .ToList();

                // Convert resolved packages to SDKInfo list. Empty package names are treated as missing
                // emitter configuration and handled below instead of failing early.
                List<SDKInfo> SdkInfos = resolvedKnownLanguagePackages
                    .Where(p => !string.IsNullOrEmpty(p.PackageName))
                    .Select(p => new SDKInfo
                    {
                        Language = p.Language.ToWorkItemString(),
                        PackageName = p.PackageName!
                    })
                    .ToList();

                // A TypeSpec project may emit packages for languages the release plan does not track
                // (e.g. Rust, C++). Optional languages such as Go for data plane are part of the
                // supported set and must be updated. Skip any other detected language instead of
                // failing, so the tool still updates the supported languages it found.
                var skippedLanguages = resolvedKnownLanguagePackages
                    .Select(pkg => pkg.Language.ToWorkItemString())
                    .Where(lang => !supportedLanguages.Contains(lang))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                SdkInfos = SdkInfos
                    .Where(sdk => supportedLanguages.Contains(sdk.Language))
                    .ToList();

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
                if (SdkInfos.Count > 0)
                {
                    // Update SDK package name and languages in work item
                    var updated = await devOpsService.UpdateReleasePlanSDKDetailsAsync(releasePlanWorkItemId, SdkInfos, ct);
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
                }
                if (skippedLanguages.Any())
                {
                    sb.AppendLine($"Note: The following detected languages are not tracked in the release plan and were skipped: {string.Join(", ", skippedLanguages)}");
                }

                // Check if any required language is missing emitter configuration in the TypeSpec project.
                // Preserve an existing Requested/Approved exclusion status so intentional exclusions are not
                // overwritten by the inferred MissingEmitterConfig state.
                var languagesMissingEmitterConfig = requiredLanguages
                    .Except(SdkInfos.Select(sdk => sdk.Language), StringComparer.OrdinalIgnoreCase)
                    .ToList();
                var existingSdkInfos = releasePlan.SDKInfo ?? [];
                var languagesToMarkMissingEmitterConfig = languagesMissingEmitterConfig
                    .Where(lang => !existingSdkInfos.Any(info =>
                        string.Equals(info.Language, lang, StringComparison.OrdinalIgnoreCase)
                        && (string.Equals(info.ReleaseExclusionStatus, "Requested", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(info.ReleaseExclusionStatus, "Approved", StringComparison.OrdinalIgnoreCase))))
                    .ToList();
                if (languagesToMarkMissingEmitterConfig.Any())
                {
                    logger.LogDebug("Languages missing emitter configuration in TypeSpec project. Work Item: {releasePlanWorkItemId}, languages: {languagesMissingEmitterConfig}", releasePlanWorkItemId, string.Join(", ", languagesToMarkMissingEmitterConfig));
                    sb.AppendLine($"Important: The following languages have missing emitter configuration in the TypeSpec project: [{string.Join(", ", languagesToMarkMissingEmitterConfig)}]. SDK must be released for all required languages: [{string.Join(", ", requiredLanguages)}].");
                    sb.AppendLine("Add the emitter configuration for each missing language in tspconfig.yaml, or request exclusion justification if the language is intentionally excluded.");

                    // Mark languages with missing emitter configuration in the release plan work item.
                    Dictionary<string, string> fieldsToUpdate = [];
                    foreach (var lang in languagesToMarkMissingEmitterConfig)
                    {
                        fieldsToUpdate[$"Custom.ReleaseExclusionStatusFor{DevOpsService.MapLanguageToId(lang)}"] = "MissingEmitterConfig";
                    }
                    await devOpsService.UpdateWorkItemAsync(releasePlanWorkItemId, fieldsToUpdate, ct);
                    logger.LogDebug("Marked languages with missing emitter configuration in release plan work item {releasePlanWorkItemId}.", releasePlanWorkItemId);
                }

                return new DefaultCommandResponse
                {
                    Message = sb.ToString(),
                    NextSteps = languagesToMarkMissingEmitterConfig.Any() ? ["Configure the TypeSpec emitter for missing languages in tspconfig.yaml, or provide a justification for language exclusion."] : []
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to update release plan with SDK details");
                return new DefaultCommandResponse { ResponseError = $"Failed to update release plan with SDK details: {ex.Message}" };
            }
        }

        [McpServerTool(Name = LinkNamespaceApprovalToolName), Description("Link package namespace approval issue to release plan(required only for management plan). This requires GitHub issue URL for the namespace approval request and release plan work item id.")]
        public async Task<DefaultCommandResponse> LinkNamespaceApprovalIssue(int releasePlanWorkItemId, string namespaceApprovalIssue, CancellationToken ct = default)
        {
            try
            {
                if (releasePlanWorkItemId <= 0 || string.IsNullOrEmpty(namespaceApprovalIssue))
                {
                    return "Release plan ID and namespace approval issue are required to verify namespace approval status";
                }

                // Get release plan and verify if it is a management plane release plan before linking namespace approval issue.
                // The resolver accepts either a Release Plan ID or a work item ID.
                var releasePlan = await devOpsService.ResolveReleasePlanByIdAsync(releasePlanWorkItemId, ct);
                if (releasePlan == null)
                {
                    return $"Release plan with ID {releasePlanWorkItemId} not found.";
                }

                // The input may have been a Release Plan ID; use the resolved work item ID for writes.
                releasePlanWorkItemId = releasePlan.WorkItemId;

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
                var issue = await githubService.GetIssueAsync(REPO_OWNER, NAMESPACE_APPROVAL_REPO, issueNumber, ct);
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
                var updated = await devOpsService.LinkNamespaceApprovalIssueAsync(releasePlanWorkItemId, issue.HtmlUrl, ct);
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
        public async Task<DefaultCommandResponse> UpdateLanguageExclusionJustification(int releasePlanWorkItem, string justification, string language = "", CancellationToken ct = default)
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

                // Get release plan. The resolver accepts either a Release Plan ID or a work item ID.
                var releasePlan = await devOpsService.ResolveReleasePlanByIdAsync(releasePlanWorkItem, ct);
                if (releasePlan == null)
                {
                    return new DefaultCommandResponse { ResponseError = $"No release plan found with work item ID {releasePlanWorkItem}" };
                }

                // The input may have been a Release Plan ID; use the resolved work item ID for writes.
                releasePlanWorkItem = releasePlan.WorkItemId;

                // Update language exclusion justification in work item
                Dictionary<string, string> fieldsToUpdate = new()
                {
                    { "Custom.ReleaseExclusionRequestNote", justification }
                };

                if (!string.IsNullOrEmpty(language))
                {
                    fieldsToUpdate[$"Custom.ReleaseExclusionStatusFor{DevOpsService.MapLanguageToId(language)}"] = "Requested";
                }

                var updatedWorkItem = await devOpsService.UpdateWorkItemAsync(releasePlanWorkItem, fieldsToUpdate, ct);
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
                    // The resolver accepts either a Release Plan ID or a work item ID.
                    var releasePlan = await devOpsService.ResolveReleasePlanByIdAsync(workItemId, ct);
                    if (releasePlan != null)
                    {
                        await devOpsService.UpdateApiSpecStatusAsync(releasePlan.WorkItemId, "Approved", ct);
                    }
                    else
                    {
                        logger.LogWarning("Could not find a release plan for ID {workItemId}; skipping API spec status update.", workItemId);
                        response.Details.Add($"Could not find a release plan for ID {workItemId}; the API spec status was not updated in the release plan.");
                        response.NextSteps = ["Verify the release plan ID or work item ID and re-run to update the API spec status."];
                    }
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
                if (!await typeSpecHelper.IsRepoPathForPublicSpecRepoAsync(repoRootPath, ct))
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
                Octokit.PullRequest? pullRequest = pullRequestNumber != 0 ? await githubService.GetPullRequestAsync(REPO_OWNER, PUBLIC_SPECS_REPO, pullRequestNumber, ct) :
                    await githubService.GetPullRequestForBranchAsync(REPO_OWNER, PUBLIC_SPECS_REPO, branchName, ct);
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
        public async Task<ReleaseWorkflowResponse> LinkSdkPullRequestToReleasePlan(string language, string pullRequestUrl, int workItemId = 0, int releasePlanId = 0, CancellationToken ct = default)
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
                var releasePlan = workItemId == 0 ? await devOpsService.GetReleasePlanAsync(releasePlanId, ct) : await devOpsService.GetReleasePlanForWorkItemAsync(workItemId, ct);
                if (releasePlan == null || releasePlan.WorkItemId == 0)
                {
                    response.ResponseError = $"Release plan with ID {releasePlanId} or work item ID {workItemId} is not found.";
                    return response;
                }

                var sdkInfoInRelease = devOpsService.AddSdkInfoInReleasePlanAsync(releasePlan.WorkItemId, language, "", parsedLink.FullUrl, "Completed", ct: ct);
                var releaseInfoInSdk = UpdateSdkPullRequestDescription(parsedLink, releasePlan, ct);

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

        private async Task UpdateSdkPullRequestDescription(ParsedSdkPullRequest parsedUrl, ReleasePlanWorkItem releasePlan, CancellationToken ct)
        {
            var repoOwner = parsedUrl.RepoOwner;
            var repoName = parsedUrl.RepoName;
            var prNumber = parsedUrl.PrNumber;

            var pr = await githubService.GetPullRequestAsync(repoOwner, repoName, prNumber, ct);
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
                await githubService.UpdatePullRequestAsync(repoOwner, repoName, prNumber, pr.Title, appendedBody, pr.State.Value, ct);
            }
            catch (Exception ex)
            {
                // This should not be a hard error when context is not updated in PR description
                logger.LogError(ex, "Failed to update pull request description for {repoOwner}/{repoName}#{prNumber}", repoOwner, repoName, prNumber);
                return;
            }
        }

        public async Task<ReleasePlanListResponse> ListOverdueReleasePlans(bool notifyOwners = false, string emailerUri = "", CancellationToken ct = default)
        {
            try
            {
                if (notifyOwners && string.IsNullOrWhiteSpace(emailerUri))
                {
                    return new ReleasePlanListResponse { ResponseError = "Emailer URI is required when notify owners is enabled." };
                }
                var releasePlans = await devOpsService.ListOverdueReleasePlansAsync(ct);

                if (notifyOwners)
                {
                    await NotifyOwnersOfOverdueReleasePlans(releasePlans, emailerUri, ct);
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

        private async Task NotifyOwnersOfOverdueReleasePlans(List<ReleasePlanWorkItem> releasePlans, string emailerUri, CancellationToken ct)
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

                // Identify SDKs not yet released (skip Go for Data Plane and skip excluded/missing-emitter languages)
                var missingSDKs = releasePlan.SDKInfo
                    .Where(info => (string.IsNullOrEmpty(info.ReleaseStatus) || !string.Equals(info.ReleaseStatus, "Released", StringComparison.OrdinalIgnoreCase))
                             && (releasePlan.IsManagementPlane || !string.Equals(info.Language, "Go", StringComparison.OrdinalIgnoreCase))
                             && !string.Equals(info.ReleaseExclusionStatus, "Requested", StringComparison.OrdinalIgnoreCase)
                             && !string.Equals(info.ReleaseExclusionStatus, "Approved", StringComparison.OrdinalIgnoreCase)
                             && !string.Equals(info.ReleaseExclusionStatus, "MissingEmitterConfig", StringComparison.OrdinalIgnoreCase))
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

                await SendEmailNotification(emailerUri, releaseOwnerEmail, sdkApexEmail, subject, body, ct);
            }
        }

        private async Task SendEmailNotification(string emailerUri, string to, string cc, string subject, string body, CancellationToken ct)
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

                var response = await httpClient.PostAsync(emailerUri, httpContent, ct);
                response.EnsureSuccessStatusCode();

                logger.LogInformation("Successfully sent email - To: {To}, CC: {CC}, Subject: {Subject}", to, cc, subject);
            }
        }

        [McpServerTool(Name = UpdateApiSpecPullRequestToolName), Description("Update TypeSpec pull request URL in a release plan using work item id or release plan id.")]
        public async Task<ReleaseWorkflowResponse> UpdateSpecPullRequestInReleasePlan(string specPullRequestUrl, int workItemId = 0, int releasePlanId = 0, CancellationToken ct = default)
        {
            try
            {
                if (workItemId == 0 && releasePlanId == 0)
                {
                    return new ReleaseWorkflowResponse { ResponseError = "Either work item ID or release plan ID must be provided." };
                }
                ValidatePullRequestUrl(specPullRequestUrl);

                // Get the release plan to check its type
                ReleasePlanWorkItem? releasePlan = null;
                if (releasePlanId > 0)
                {
                    releasePlan = await devOpsService.GetReleasePlanAsync(releasePlanId, ct);
                    if (releasePlan == null)
                    {
                        return new ReleaseWorkflowResponse
                        {
                            ResponseError = $"Release plan with ID {releasePlanId} not found."
                        };
                    }
                    workItemId = releasePlan.WorkItemId;
                }
                else
                {
                    releasePlan = await devOpsService.GetReleasePlanForWorkItemAsync(workItemId, ct);
                }

                // Validate spec PR against release type
                if (releasePlan != null && releasePlan.ApiReleaseType != ApiReleaseType.Unknown)
                {
                    ValidateSpecPullRequestForReleaseType(specPullRequestUrl, releasePlan.ApiReleaseType);
                }

                // Update the spec pull request in the release plan
                var updated = await devOpsService.UpdateSpecPullRequestAsync(workItemId, specPullRequestUrl, ct);

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

        [McpServerTool(Name = UpdateReleasePlanTargetToolName), Description("Update the SDK release target month on an existing release plan.")]
        public async Task<ReleasePlanResponse> UpdateReleasePlanTarget(int workItemId, string targetReleaseMonthYear, CancellationToken ct = default)
        {
            try
            {
                if (workItemId <= 0)
                {
                    return new ReleasePlanResponse { ResponseError = "A valid work item ID must be provided." };
                }

                if (string.IsNullOrWhiteSpace(targetReleaseMonthYear))
                {
                    return new ReleasePlanResponse { ResponseError = "SDK release target month is required." };
                }

                // The resolver accepts either a Release Plan ID or a work item ID.
                var releasePlan = await devOpsService.ResolveReleasePlanByIdAsync(workItemId, ct);
                if (releasePlan == null)
                {
                    return new ReleasePlanResponse { ResponseError = $"No release plan found for work item {workItemId}." };
                }

                var fieldsToUpdate = new Dictionary<string, string>
                {
                    { "Custom.SDKReleasemonth", targetReleaseMonthYear },
                };

                await devOpsService.UpdateWorkItemAsync(releasePlan.WorkItemId, fieldsToUpdate, ct);
                logger.LogInformation("Updated SDK release target month to {targetReleaseMonthYear} for work item {WorkItemId}", targetReleaseMonthYear, releasePlan.WorkItemId);

                releasePlan = await devOpsService.GetReleasePlanForWorkItemAsync(releasePlan.WorkItemId, ct);
                return new ReleasePlanResponse
                {
                    ReleasePlanDetails = releasePlan,
                    Message = $"Successfully updated SDK release target month to {targetReleaseMonthYear} for release plan {releasePlan?.WorkItemId}."
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to update SDK release target month for work item {WorkItemId}", workItemId);
                return new ReleasePlanResponse { ResponseError = $"Failed to update SDK release target month: {ex.Message}" };
            }
        }

        [McpServerTool(Name = GetServiceDetailsToolName), Description("Get service and service tree product details for a product using TypeSpec project path: Get service tree product details (service tree ID, service ID, package display name, product service tree link).")]
        public async Task<ProductInfoResponse> GetProductByTypeSpecPath(string typeSpecProjectPath, CancellationToken ct = default)
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

                var isValidTypeSpec = typeSpecHelper.IsValidTypeSpecProjectPath(typeSpecProjectPath);
                if (!isValidTypeSpec)
                {
                    return new ProductInfoResponse
                    {
                        ResponseError = $"Invalid TypeSpec project path. tspconfig.yaml is not found in the path {typeSpecProjectPath}.",
                        TypeSpecProject = typeSpecProjectPath
                    };
                }
                var specRelativePath = typeSpecHelper.GetTypeSpecProjectRelativePath(typeSpecProjectPath);
                // Get product info from DevOps service
                var productInfo = await devOpsService.GetProductInfoByTypeSpecProjectPathAsync(specRelativePath, ct);

                if (productInfo == null)
                {
                    return new ProductInfoResponse
                    {
                        Message = $"No release plan found for TypeSpec project path: {specRelativePath}",
                        TypeSpecProject = specRelativePath
                    };
                }

                return new ProductInfoResponse
                {
                    ProductInfo = productInfo,
                    Message = "Successfully retrieved product information.",
                    TypeSpecProject = specRelativePath
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

        [McpServerTool(Name = GetKPIAttestationStatusToolName), Description("Get KPI attestation status for release plans with given product tree ID and release plan type. If product ID and release plan type are not provided, a TypeSpec project path can be used to resolve them.")]
        public async Task<ReleasePlanListResponse> GetKPIAttestationStatus(string productId = "", string releasePlanType = "", string typeSpecProjectPath = "", bool isTestReleasePlan = false, CancellationToken ct = default)
        {
            var releasePlans = new List<ReleasePlanWorkItem>();
            try
            {
                ApiReleaseType apiReleaseType = ApiReleaseType.Unknown;
                // Check environment variable to determine if this should be a test release plan
                var isAgentTesting = environmentHelper.GetBooleanVariable("AZSDKTOOLS_AGENT_TESTING", false);
                if (isAgentTesting)
                {
                    isTestReleasePlan = true;
                    logger.LogInformation("AZSDKTOOLS_AGENT_TESTING environment variable is set to true, creating test release plan");
                }

                // If productId or releasePlanType not provided, try to resolve from TypeSpec project path
                if (string.IsNullOrWhiteSpace(productId) || string.IsNullOrWhiteSpace(releasePlanType))
                {
                    if (string.IsNullOrWhiteSpace(typeSpecProjectPath))
                    {
                        return new ReleasePlanListResponse
                        {
                            ResponseError = "Either provide both product ID and release plan type, or provide a TypeSpec project path to resolve them."
                        };
                    }

                    logger.LogInformation("Resolving product ID and release plan type from TypeSpec project path: {typeSpecProjectPath}", typeSpecProjectPath);

                    var releasePlan = await devOpsService.GetReleasePlanByTypeSpecProjectPathAsync(typeSpecProjectPath, includeFinishedPlans: true, ct: ct);
                    if (releasePlan == null)
                    {
                        return new ReleasePlanListResponse
                        {
                            ResponseError = $"No release plan found for TypeSpec project path '{typeSpecProjectPath}'. Cannot resolve product ID and release plan type."
                        };
                    }

                    // Fill these in for logging later on
                    productId = releasePlan.ProductTreeId;
                    apiReleaseType = ApiReleaseTypeExtensions.FromAdoFieldValue(releasePlan.ReleasePlanType);
                    releasePlans.Add(releasePlan);
                }
                else
                {
                    if (!ApiReleaseTypeExtensions.TryParseFromUserInput(releasePlanType, out apiReleaseType))
                    {
                        return new ReleasePlanListResponse
                        {
                            ResponseError = $"Invalid release plan type value '{releasePlanType}'. Supported values are: Private Preview, Public Preview, GA."
                        };
                    }

                    logger.LogInformation("Getting KPI attestation status for product {productId} with release plan type {releasePlanType}", productId, releasePlanType);

                    releasePlans = await devOpsService.GetReleasePlansByProductAndLifecycleAsync(productId, apiReleaseType.ToAdoFieldValue(), isTestReleasePlan, ct);

                    if (releasePlans.Count == 0)
                    {
                        return new ReleasePlanListResponse
                        {
                            Message = $"No release plans found for product '{productId}' with release plan type '{releasePlanType}'. " +
                                      "A release plan must be created and completed before KPI attestation can be fulfilled."
                        };
                    }
                }

                var finishedPlans = releasePlans.Where(rp => rp.Status.Equals("Finished", StringComparison.OrdinalIgnoreCase)).ToList();
                var pendingPlans = releasePlans.Where(rp => !rp.Status.Equals("Finished", StringComparison.OrdinalIgnoreCase)).ToList();
                var attestedPlans = finishedPlans.Where(rp => rp.AttestationStatus.Equals("Completed", StringComparison.OrdinalIgnoreCase)).ToList();

                if (attestedPlans.Count > 0)
                {
                    return new ReleasePlanListResponse
                    {
                        ReleasePlanDetailsList = releasePlans,
                        Message = $"KPI attestation is completed for product '{productId}' ({apiReleaseType.ToDisplayLabel()}). " +
                                  $"Found {attestedPlans.Count} finished release plan(s) with attestation completed."
                    };
                }

                if (finishedPlans.Count > 0)
                {
                    return new ReleasePlanListResponse
                    {
                        ReleasePlanDetailsList = releasePlans,
                        Message = $"Found {finishedPlans.Count} finished release plan(s) for product '{productId}' ({apiReleaseType.ToDisplayLabel()}), " +
                                  "but none have attestation status marked as completed."
                    };
                }

                // No finished plans, only pending
                return new ReleasePlanListResponse
                {
                    ReleasePlanDetailsList = pendingPlans,
                    Message = $"KPI attestation is not yet completed for product '{productId}' ({apiReleaseType.ToDisplayLabel()}). " +
                              $"Found {pendingPlans.Count} release plan(s) in pending status. " +
                              "KPI attestation will be completed once one of these release plans is finished: " +
                              string.Join(", ", pendingPlans.Select(rp => $"'{rp.Title}' (ID: {rp.ReleasePlanId}, Status: {rp.Status})"))
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to get KPI attestation status for product {productId}", productId);
                return new ReleasePlanListResponse
                {
                    ResponseError = $"Failed to get KPI attestation status: {ex.Message}"
                };
            }
        }
    }
}
