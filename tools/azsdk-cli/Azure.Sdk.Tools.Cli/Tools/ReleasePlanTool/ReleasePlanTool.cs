// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools
{
    [Description("Release Plan Tool type that contains tools to connect to Azure DevOps to get release plan work item")]
    [McpServerToolType]
    public class ReleasePlanTool(IDevOpsService devOpsService, ITypeSpecHelper typeSpecHelper, ILogger<ReleasePlanTool> logger, IOutputService output) : MCPTool
    {
        // Commands
        private const string getReleasePlanDetailsCommandName = "get";
        private const string createReleasePlanCommandName = "create";

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
        private readonly Option<bool> isTestReleasePlanOpt = new(["--test-release"], () => false, "Create release plan in test environment") { IsRequired = false};


        [McpServerTool, Description("Get release plan for API spec pull request. This tool should be used only if work item Id is unknown.")]
        public async Task<string> GetReleasePlanForPullRequest(string pullRequestLink)
        {
            try
            {
                List<string> releasePlanList = [];
                var releasePlan = await devOpsService.GetReleasePlan(pullRequestLink);
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
                new Command(createReleasePlanCommandName, "Create a release plan") { typeSpecProjectPathOpt, targetReleaseOpt, serviceTreeIdOpt, productTreeIdOpt, apiVersionOpt, pullRequestOpt, sdkReleaseTypeOpt, isTestReleasePlanOpt }
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
                    var releasePlanDetails = await GetReleasePlan(workItem: workItemId, releasePlanId: releasePlanNumber);
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
                    var releasePlan = await CreateReleasePlan(typeSpecProjectPath, targetReleaseMonthYear, serviceTreeId, productTreeId, specApiVersion, specPullRequestUrl, sdkReleaseType, isTestReleasePlan);
                    output.Output($"Release plan created: {releasePlan}");
                    return;
                default:
                    logger.LogError("Unknown command: {command}", command);
                    SetFailure();
                    return;
            }
        }

        [McpServerTool, Description("Get Release Plan: Get release plan work item details for a given work item id or release plan Id.")]
        public async Task<string> GetReleasePlan(int workItem = 0, int releasePlanId = 0)
        {
            try
            {
                if (workItem == 0 && releasePlanId == 0)
                {
                    return "Either work item ID or release plan number must be provided.";
                }
                var releasePlan = workItem != 0 ? await devOpsService.GetReleasePlanForWorkItem(workItem) : await devOpsService.GetReleasePlan(releasePlanId);
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
        public async Task<string> CreateReleasePlan(string typeSpecProjectPath, string targetReleaseMonthYear, string serviceTreeId, string productTreeId, string specApiVersion, string specPullRequestUrl, string sdkReleaseType, bool isTestReleasePlan = false)
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
                };
                var workItem = await devOpsService.CreateReleasePlanWorkItem(releasePlan);
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
    }
}
