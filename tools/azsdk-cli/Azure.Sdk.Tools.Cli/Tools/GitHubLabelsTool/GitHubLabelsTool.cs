// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Configuration;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models.Responses;
using Azure.Sdk.Tools.Cli.Services;


namespace Azure.Sdk.Tools.Cli.Tools
{
    [McpServerToolType, Description("Tools for working with GitHub labels for services")]
    public class GitHubLabelsTool(
        ILogger<GitHubLabelsTool> logger,
        IOutputHelper output,
        IGitHubService githubService
    ) : MCPTool
    {
        //command names
        private const string checkServiceLabelCommandName = "check-service-label";
        private const string createServiceLabelCommandName = "create-service-label";

        // Command options
        private readonly Option<string> serviceLabelOpt = new(["--service", "-s"], "Proposed Service name used to create a PR for a new label.") { IsRequired = true };
        private readonly Option<string> documentationLinkOpt = new(["--link", "-l"], "Brand documentation link used to create a PR for a new label.") { IsRequired = true };

        public override Command GetCommand()
        {
            var command = new Command("github-labels", "GitHub service labels tools");
            var subCommands = new[]
            {
                new Command(checkServiceLabelCommandName, "Check if a service label exists in the common labels CSV") { serviceLabelOpt },
                new Command(createServiceLabelCommandName, "Creates a PR for a new label given a proposed label and brand documentation.") { serviceLabelOpt, documentationLinkOpt },
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
            var command = ctx.ParseResult.CommandResult.Command.Name;
            var commandParser = ctx.ParseResult;

            switch (command)
            {
                case checkServiceLabelCommandName:
                    var serviceLabel = commandParser.GetValueForOption(serviceLabelOpt);
                    var result = await CheckServiceLabel(serviceLabel);
                    ctx.ExitCode = ExitCode;
                    output.Output(result);
                    return;
                case createServiceLabelCommandName:
                    var proposedServiceLabel = commandParser.GetValueForOption(serviceLabelOpt);
                    var documentationLink = commandParser.GetValueForOption(documentationLinkOpt);
                    var createdPRResult = await CreateServiceLabel(proposedServiceLabel, documentationLink ?? "");
                    ctx.ExitCode = ExitCode;
                    output.Output(createdPRResult);
                    return;
                default:
                    SetFailure();
                    output.OutputError($"Unknown command: '{command}'");
                    return;
            }
        }

        [McpServerTool(Name = "CheckServiceLabel"), Description("Checks if a service label exists and returns its details")]
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
                SetFailure();
                logger.LogError(ex, "Error occurred while checking service label: {serviceLabel}", serviceLabel);
                return new ServiceLabelResponse
                {
                    ResponseError = $"Error occurred while checking service label: {serviceLabel}: {ex.Message}",
                };
            }
        }

        private async Task<LabelHelper.ServiceLabelStatus> getServiceLabelInfo(string serviceLabel)
        {
            logger.LogInformation($"Checking service label: {serviceLabel}");

            var csvContents = await githubService.GetContentsSingleAsync(Constants.AZURE_OWNER_PATH, Constants.AZURE_SDK_TOOLS_PATH, Constants.AZURE_COMMON_LABELS_PATH);

            var result = LabelHelper.CheckServiceLabel(csvContents.Content, serviceLabel);

            return result;
        }

        [McpServerTool(Name = "CreateServiceLabel"), Description("Creates a pull request to add a new service label")]
        public async Task<ServiceLabelResponse> CreateServiceLabel(string label, string link)
        {
            try
            {
                var normalizedLabel = LabelHelper.NormalizeLabel(label);

                var checkResult = await getServiceLabelInfo(normalizedLabel);

                // Create a new branch
                if (checkResult == LabelHelper.ServiceLabelStatus.Exists)
                {
                    logger.LogInformation($"Service label '{label}' already exists. No action taken.");
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
                logger.LogInformation($"Branch creation result: {branchResult}");

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

                logger.LogInformation($"Creating new service label: {label}. Documentation link: {link}");


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
                    title: $"Add service label: {label}",
                    body: $"This PR adds the service label '{label}' to the repository. Documentation link: {link}"
                );

                logger.LogInformation($"Service label '{label}' pull request created successfully. Result: {string.Join(", ", result)}");

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
                SetFailure();
                logger.LogError(ex, $"Failed to create pull request for service label '{label}': {ex.Message}");

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
