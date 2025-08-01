// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Contract;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses;
using Azure.Sdk.Tools.Cli.Helpers;
using System.Text;
using CsvHelper;
using Microsoft.Extensions.Logging;
using System.Text.Json;


namespace Azure.Sdk.Tools.Cli.Tools
{

    [McpServerToolType, Description("Tools for working with GitHub service labels from the Azure SDK Tools common-labels.csv")]
    public class GitHubLabelsTool(
        ILogger<GitHubLabelsTool> logger,
        IOutputService output,
        ILabelHelper labelHelper,
        IGitHubService githubService
    ) : MCPTool
    {
        private const string serviceLabelColorCode = "e99695"; // color code for service labels in common-labels.csv

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
                    output.Output(createdPRResult.ToString());
                    return;
                default:
                    SetFailure();
                    output.OutputError($"Unknown command: '{command}'");
                    return;
            }
        }

        [McpServerTool(Name = "CheckServiceLabel"), Description("Checks if a service label exists in the common-labels.csv and returns its details if found.")]
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
                    Status = "Error",
                    Label = serviceLabel
                };
            }
        }

        public async Task<LabelHelper.ServiceLabelStatus> getServiceLabelInfo(string serviceLabel)
        {
            try
            {
                logger.LogInformation($"Checking service label: {serviceLabel}");

                var contents = await githubService.GetContentsAsync("Azure", "azure-sdk-tools", "tools/github/data/common-labels.csv");
                if (contents == null || contents.Count == 0)
                {
                    throw new InvalidOperationException("Could not retrieve common-labels.csv file");
                }

                // Get the first (and should be only) file content
                var csvContent = contents[0].Content;
                if (string.IsNullOrEmpty(csvContent))
                {
                    throw new InvalidOperationException("common-labels.csv file is empty");
                }

                var result = labelHelper.CheckServiceLabel(csvContent, serviceLabel);

                return result;
            }
            catch (Exception ex)
            {
                SetFailure();
                logger.LogError(ex, "Error occurred while checking service label: {serviceLabel}", serviceLabel);
                throw;
            }
        }

        [McpServerTool(Name = "CreateServiceLabel"), Description("Creates a pull request to add a new service label to the common-labels.csv.")]
        public async Task<ServiceLabelResponse> CreateServiceLabel(string label, string link)
        {
            try
            {
                var normalizedLabel = labelHelper.NormalizeLabel(label);

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
                        ResponseError = "Label exists but is not a service label"
                    };
                }

                var branchResult = await githubService.CreateBranchAsync("Azure", "azure-sdk-tools", $"add_service_label_{normalizedLabel}", "main");
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
                var csvContent = await githubService.GetContentsAsync("Azure", "azure-sdk-tools", "tools/github/data/common-labels.csv");

                if (csvContent == null || csvContent.Count == 0)
                {
                    throw new InvalidOperationException("Could not retrieve common-labels.csv file");
                }

                var csvContentString = csvContent[0].Content;

                var updatedFile = labelHelper.CreateServiceLabel(csvContentString, label); // Contains updated CSV content with the new service label added

                await githubService.UpdateFileAsync("Azure", "azure-sdk-tools", "tools/github/data/common-labels.csv", $"Adding {label}", updatedFile, csvContent.First().Sha, $"add_service_label_{normalizedLabel}");

                // Create the pull request
                var result = await githubService.CreatePullRequestAsync(
                    repoName: "azure-sdk-tools",
                    repoOwner: "Azure",
                    baseBranch: "main",
                    headBranch: $"add_service_label_{normalizedLabel}",
                    title: $"Add service label: {label}",
                    body: $"This PR adds the service label '{label}' to the repository. Documentation link: {link}"
                );

                logger.LogInformation($"Service label '{label}' pull request created successfully. Result: {string.Join(", ", result)}");

                // Extract the pull request URL from the result
                var pullRequestUrl = ExtractPullRequestUrl(result);

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

        private static string? ExtractPullRequestUrl(List<string> responses)
        {
            // Look for a response containing "Pull request URL:"
            foreach (var response in responses)
            {
                if (response.Contains("Pull request URL:"))
                {
                    var urlStart = response.IndexOf("https://github.com");
                    if (urlStart >= 0)
                    {
                        return response.Substring(urlStart).Trim();
                    }
                }
            }
            return null;
        }
    }
}
