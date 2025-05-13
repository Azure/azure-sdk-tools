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

namespace Azure.Sdk.Tools.Cli.Tools.ReleasePlanTool
{
    [Description("Release Plan Tool type that contains tools to connect to Azure DevOps to get release plan work item")]
    [McpServerToolType]
    public class ReleasePlanTool(IDevOpsService _devOpsService, ITypeSpecHelper _helper, ILogger<ReleasePlanTool> _logger)  : MCPTool
    {
        private readonly IDevOpsService devOpsService = _devOpsService;
        private readonly ITypeSpecHelper typeSpecHelper = _helper;
        private readonly ILogger<ReleasePlanTool> logger = _logger;

        // Commands
        private const string getReleasePlanDetailsCommandName = "get-details";
        private const string createReleasePlanCommandName = "create";

        // Options
        private readonly Option<int> workItemIdOpt = new(["--work-item-id", "-w"], "Work Item ID") { IsRequired = true };
        private readonly Option<string> typeSpecProjectPathOpt = new(["--typespec-path"], "Path to TypeSpec project") { IsRequired = true };
        private readonly Option<string> targetReleaseOpt = new(["--release-month"], "SDK release target month(Month YYYY)");
        private readonly Option<string> serviceTreeIdOpt = new(["--service-tree"], "Service tree ID");
        private readonly Option<string> productTreeIdOpt = new(["--product"], "Product service tree ID");
        private readonly Option<string> apiVersionOpt = new(["--api-version"], "API version");
        private readonly Option<string> pullRequestOpt = new(["--pull-request"], "Api spec pull request URL");


        [McpServerTool, Description("Get release plan for API spec pull request. This tool should be used only if work item Id is unknown.")]
        public async Task<string> GetReleasePlan(string pullRequestLink)
        {
            List<string> releasePlanList = [];
            try
            {
                var releasePlan = await devOpsService.GetReleasePlan(pullRequestLink);
                return releasePlan == null ? "Failed to get release plan details." :
                    $"Release Plan: {JsonSerializer.Serialize(releasePlan)}";
            }
            catch (Exception ex)
            {
                logger.LogError($"Failed to get release plan details: {ex.Message}");
                return $"Failed to get release plan details: {ex.Message}";
            }
        }

        public override Command GetCommand()
        {
            Command command = new Command("release-plan");

            var getReleasePlanDetailsCommand = new Command(getReleasePlanDetailsCommandName, "Get details of a release plan") {workItemIdOpt};
            var createReleasePlanCommand = new Command(createReleasePlanCommandName, "Create a release plan") { typeSpecProjectPathOpt, targetReleaseOpt, serviceTreeIdOpt, productTreeIdOpt, apiVersionOpt, pullRequestOpt };

            foreach (var subCommand in new[] { getReleasePlanDetailsCommand, createReleasePlanCommand })
            {
                subCommand.SetHandler(async ctx => { ctx.ExitCode = await HandleCommand(ctx, ctx.GetCancellationToken()); });
                command.AddCommand(subCommand);
            }
            return command;
        }

        // HandleCommand is effectively the actual "worker" for the Tool when looked at from the
        // CLI pov. Each individual function marked with attribute [McpServerTool] will themselves
        // be accessible when this class is loaded into assembly and added to MCP configuration in 
        // HostServerTool.CreateAppBuilder
        public override async Task<int> HandleCommand(InvocationContext ctx, CancellationToken ct)
        {
            var command = ctx.ParseResult.CommandResult.Command.Name;

            switch(command)
            {
                case getReleasePlanDetailsCommandName:
                    var workItemId = ctx.ParseResult.GetValueForOption(workItemIdOpt);
                    var releasePlanDetails = await GetReleasePlanDetails(workItemId);
                    logger.LogInformation($"Release plan details: {releasePlanDetails}");
                    return 0;

                case createReleasePlanCommandName:
                    var typeSpecProjectPath = ctx.ParseResult.GetValueForOption(typeSpecProjectPathOpt);
                    var targetReleaseMonthYear = ctx.ParseResult.GetValueForOption(targetReleaseOpt);
                    var serviceTreeId = ctx.ParseResult.GetValueForOption(serviceTreeIdOpt);
                    var productTreeId = ctx.ParseResult.GetValueForOption(productTreeIdOpt);
                    var specApiVersion = ctx.ParseResult.GetValueForOption(apiVersionOpt);
                    var specPullRequestUrl = ctx.ParseResult.GetValueForOption(pullRequestOpt);
                    var releasePlan = await CreateReleasePlan(typeSpecProjectPath, targetReleaseMonthYear, serviceTreeId, productTreeId, specApiVersion, specPullRequestUrl);
                    logger.LogInformation($"Release plan created: {releasePlan}");
                    return 0;
                default:
                    logger.LogError($"Unknown command: {command}");
                    return 1;
            }
        }

        [McpServerTool, Description("Get Release Plan: Get release plan work item details for a given work item id.")]
        public async Task<string> GetReleasePlanDetails(int workItemId)
        {
            try
            {
                var releasePlan = await devOpsService.GetReleasePlan(workItemId);
                var releasePlanText = releasePlan != null ? JsonSerializer.Serialize(releasePlan) :
                       "Failed to get release plan details.";
                logger.LogInformation($"Release plan details: {releasePlanText}");
                return releasePlanText;
            }
            catch (Exception ex)
            {
                return $"Failed to get release plan details: {ex.Message}";
            }
        }

        [McpServerTool, Description("Create Release Plan work item.")]
        public async Task<string> CreateReleasePlan(string typeSpecProjectPath, string targetReleaseMonthYear, string serviceTreeId, string productTreeId, string specApiVersion, string specPullRequestUrl)
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

                var releasePlan = new ReleasePlan
                {
                    SDKReleaseMonth = targetReleaseMonthYear,
                    ServiceTreeId = serviceTreeId,
                    ProductTreeId = productTreeId,
                    SpecAPIVersion = specApiVersion,
                    SpecType = specType,
                    IsManagementPlane = isMgmt,
                    IsDataPlane = !isMgmt,
                    SpecPullRequests = [specPullRequestUrl]
                };
                var workItem = await devOpsService.CreateReleasePlanWorkItem(releasePlan);
                return workItem != null ? JsonSerializer.Serialize(workItem) : "Failed to create release plan work item.";
            }
            catch (Exception ex)
            {
                return $"Failed to create release plan work item: {ex.Message}";
            }
        }
    }
}
