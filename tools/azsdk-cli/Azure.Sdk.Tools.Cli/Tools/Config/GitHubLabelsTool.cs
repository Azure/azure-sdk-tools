// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.ComponentModel;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Configuration;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses;
using Azure.Sdk.Tools.Cli.Services;


namespace Azure.Sdk.Tools.Cli.Tools.GitHub
{
    [McpServerToolType, Description("Tools for working with GitHub labels for services")]
    public class GitHubLabelsTool(
        ILogger<GitHubLabelsTool> logger,
        IGitHubService githubService
    ) : MCPMultiCommandTool
    {
        public override CommandGroup[] CommandHierarchy { get; set; } = [
            SharedCommandGroups.Config,
            new("github-label", "GitHub service label commands")
        ];

        //command names
        private const string checkServiceLabelCommandName = "check";
        private const string createServiceLabelCommandName = "create";

        private readonly Argument<string> serviceLabelArg = new("service")
        {
            Description = "Proposed Service name used to create a PR for a new label."
        };

        private readonly Option<string> documentationLinkOpt = new("--link", "-l")
        {
            Description = "Brand documentation link used to create a PR for a new label.",
            Required = true,
        };

        protected override List<Command> GetCommands() =>
        [
            new(checkServiceLabelCommandName, "Check if a service label exists in the common labels CSV") { serviceLabelArg },
            new(createServiceLabelCommandName, "Creates a PR for a new label given a proposed label and brand documentation") { serviceLabelArg, documentationLinkOpt },
        ];

        public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
        {
            var command = parseResult.CommandResult.Command.Name;

            switch (command)
            {
                case checkServiceLabelCommandName:
                    var serviceLabel = parseResult.GetValue(serviceLabelArg);
                    return await CheckServiceLabel(serviceLabel);
                case createServiceLabelCommandName:
                    var proposedServiceLabel = parseResult.GetValue(serviceLabelArg);
                    var documentationLink = parseResult.GetValue(documentationLinkOpt);
                    return await CreateServiceLabel(proposedServiceLabel, documentationLink ?? "");
                default:
                    return new DefaultCommandResponse { ResponseError = $"Unknown command: '{command}'" };
            }
        }

        [McpServerTool(Name = "azsdk_check_service_label"), Description("Checks if a service label exists and returns its details")]
        public async Task<ServiceLabelResponse> CheckServiceLabel(string serviceLabel)
        {
            try
            {
                var labelStatus = (await getServiceLabelInfo(serviceLabel)).ToString();
                return new ServiceLabelResponse
                {
                    Status = labelStatus,
                    Label = serviceLabel
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while checking service label: {serviceLabel}", serviceLabel);
                return new ServiceLabelResponse
                {
                    ResponseError = $"Error occurred while checking service label: {serviceLabel}: {ex.Message}",
                };
            }
        }

        private async Task<LabelHelper.ServiceLabelStatus> getServiceLabelInfo(string serviceLabel)
        {
            var csvContents = await githubService.GetContentsSingleAsync(Constants.AZURE_OWNER_PATH, Constants.AZURE_SDK_TOOLS_PATH, Constants.AZURE_COMMON_LABELS_PATH);

            var result = LabelHelper.CheckServiceLabel(csvContents.Content, serviceLabel);
            if (result == LabelHelper.ServiceLabelStatus.Exists)
            {
                return result;
            }

            var pullRequests = await githubService.SearchPullRequestsByTitleAsync("Azure", "azure-sdk-tools", "Service Label");
            if (LabelHelper.CheckServiceLabelInReview(pullRequests, serviceLabel))
            {
                return LabelHelper.ServiceLabelStatus.InReview;
            }
            return result;
        }

        [McpServerTool(Name = "azsdk_create_service_label"), Description("Creates a pull request to add a new service label")]
        public async Task<ServiceLabelResponse> CreateServiceLabel(string label, string link)
        {
            try
            {
                var normalizedLabel = LabelHelper.NormalizeLabel(label);

                var checkResult = await getServiceLabelInfo(normalizedLabel);

                // Create a new branch
                if (checkResult == LabelHelper.ServiceLabelStatus.Exists)
                {
                    return new ServiceLabelResponse
                    {
                        Status = "AlreadyExists",
                        Label = label
                    };
                }
                else if (checkResult == LabelHelper.ServiceLabelStatus.NotAServiceLabel)
                {
                    logger.LogWarning($"Label '{label}' exists but is not a service label. No action taken.");
                    return new ServiceLabelResponse
                    {
                        Status = "NotAServiceLabel",
                        Label = label,
                    };
                }

                var branchResult = await githubService.CreateBranchAsync(Constants.AZURE_OWNER_PATH, Constants.AZURE_SDK_TOOLS_PATH, $"add_service_label_{normalizedLabel}", "main");

                // If branch already exists, return early with the compare URL
                if (branchResult == CreateBranchStatus.AlreadyExists)
                {
                    return new ServiceLabelResponse
                    {
                        Status = "BranchExists",
                        Label = label,
                        PullRequestUrl = $"https://github.com/Azure/azure-sdk-tools/compare/main...add_service_label_{normalizedLabel}"
                    };
                }

                // Update the common-labels.csv file
                var csvContent = await githubService.GetContentsSingleAsync(Constants.AZURE_OWNER_PATH, Constants.AZURE_SDK_TOOLS_PATH, Constants.AZURE_COMMON_LABELS_PATH);

                var updatedFile = LabelHelper.CreateServiceLabel(csvContent.Content, label); // Contains updated CSV content with the new service label added

                await githubService.UpdateFileAsync(Constants.AZURE_OWNER_PATH, Constants.AZURE_SDK_TOOLS_PATH, Constants.AZURE_COMMON_LABELS_PATH, $"Adding {label}", updatedFile, csvContent.Sha, $"add_service_label_{normalizedLabel}");

                // Create the pull request
                var result = await githubService.CreatePullRequestAsync(
                    repoName: Constants.AZURE_SDK_TOOLS_PATH,
                    repoOwner: Constants.AZURE_OWNER_PATH,
                    baseBranch: "main",
                    headBranch: $"add_service_label_{normalizedLabel}",
                    title: $"[Service Label] Add service label: {label}",
                    body: $"This PR adds the service label '{label}' to the repository. Documentation link: {link}",
                    draft: true
                );

                // Extract the pull request URL from the result
                var pullRequestUrl = result.Url;

                return new ServiceLabelResponse
                {
                    Status = "Success",
                    Label = label,
                    PullRequestUrl = pullRequestUrl
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to create pull request for service label '{label}': {error}", label, ex.Message);

                return new ServiceLabelResponse
                {
                    Status = "Failed",
                    Label = label,
                    ResponseError = ex.Message
                };
            }
        }
    }
}
