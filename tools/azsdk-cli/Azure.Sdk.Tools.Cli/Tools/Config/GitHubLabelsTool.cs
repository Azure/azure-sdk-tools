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
using Azure.Sdk.Tools.Cli.Tools.Core;


namespace Azure.Sdk.Tools.Cli.Tools.Config
{
    [McpServerToolType, Description("Tools for working with GitHub labels for services")]
    public class GitHubLabelsTool(
        ILogger<GitHubLabelsTool> logger,
        IGitHubService githubService,
        IDevOpsService devOpsService
    ) : MCPMultiCommandTool
    {
        public override CommandGroup[] CommandHierarchy { get; set; } = [
            SharedCommandGroups.Config,
            new("github-label", "GitHub service label commands")
        ];

        //command names
        private const string checkServiceLabelCommandName = "check";
        private const string createServiceLabelCommandName = "create";
        private const string syncAdoCommandName = "sync-ado";

        // MCP Tool Names
        private const string CheckServiceLabelToolName = "azsdk_check_service_label";
        private const string CreateServiceLabelToolName = "azsdk_create_service_label";

        private readonly Argument<string> serviceLabelArg = new("service")
        {
            Description = "Proposed Service name used to create a PR for a new label."
        };

        private readonly Option<string> documentationLinkOpt = new("--link", "-l")
        {
            Description = "Brand documentation link used to create a PR for a new label.",
            Required = true,
        };

        private readonly Option<bool> dryRunOpt = new("--dry-run", "-d")
        {
            Description = "Preview changes without creating Work Items",
            Required = false,
        };

        protected override List<Command> GetCommands() =>
        [
            new(checkServiceLabelCommandName, "Check if a service label exists in the common labels CSV") { serviceLabelArg },
            new(createServiceLabelCommandName, "Creates a PR for a new label given a proposed label and brand documentation") { serviceLabelArg, documentationLinkOpt },
            new(syncAdoCommandName, "Synchronize service labels from the GitHub CSV to Azure DevOps Work Items") { dryRunOpt },
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
                case syncAdoCommandName:
                    var dryRun = parseResult.GetValue(dryRunOpt);
                    return await SyncLabelsToAdo(dryRun);
                default:
                    return new DefaultCommandResponse { ResponseError = $"Unknown command: '{command}'" };
            }
        }

        [McpServerTool(Name = CheckServiceLabelToolName), Description("Checks if a service label exists and returns its details")]
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

        [McpServerTool(Name = CreateServiceLabelToolName), Description("Creates a pull request to add a new service label")]
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
                    logger.LogWarning("Label '{Label}' exists but is not a service label. No action taken.", label);
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
                logger.LogError(ex, "Failed to create pull request for service label '{Label}'", label);

                return new ServiceLabelResponse
                {
                    Status = "Failed",
                    Label = label,
                    ResponseError = ex.Message
                };
            }
        }

        /// <summary>
        /// Synchronizes service labels from the GitHub CSV to Azure DevOps Work Items.
        /// This is a CLI-only command (no MCP exposure).
        /// </summary>
        /// <param name="dryRun">If true, preview changes without creating Work Items</param>
        /// <returns>GitHubLabelSyncResponse with details of the sync operation</returns>
        public async Task<GitHubLabelSyncResponse> SyncLabelsToAdo(bool dryRun)
        {
            var response = new GitHubLabelSyncResponse { DryRun = dryRun };

            try
            {
                // 1. Fetch service labels from CSV
                logger.LogInformation("Fetching service labels from CSV...");
                var csvContents = await githubService.GetContentsSingleAsync(
                    Constants.AZURE_OWNER_PATH,
                    Constants.AZURE_SDK_TOOLS_PATH,
                    Constants.AZURE_COMMON_LABELS_PATH);

                var csvContent = csvContents.Content;
                var serviceLabels = LabelHelper.GetAllServiceLabels(csvContent);
                logger.LogInformation("Found {count} service labels in CSV", serviceLabels.Count);

                // Check for duplicate labels in CSV
                if (LabelHelper.TryFindDuplicateLabels(serviceLabels, out var duplicateCsvLabels))
                {
                    foreach (var duplicateLabel in duplicateCsvLabels)
                    {
                        response.SyncErrors.Add(new GitHubLabelSyncError
                        {
                            ErrorType = GitHubLabelSyncErrorType.DuplicateCsvLabel,
                            Label = duplicateLabel,
                            Details = "Label appears multiple times in the CSV file"
                        });
                    }
                }

                // 2. Fetch existing ADO Work Items
                logger.LogInformation("Fetching existing Label work items from Azure DevOps...");
                List<GitHubLableWorkItem> existingWorkItems;
                try
                {
                    existingWorkItems = await devOpsService.GetGitHubLableWorkItemsAsync();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to fetch Label Work Items from Azure DevOps");
                    response.ResponseError = $"GetGitHubLableWorkItemsAsync failed: {ex.Message}.";
                    return response;
                }

                logger.LogInformation("Found {count} existing Label work items in ADO", existingWorkItems.Count);

                // Check for duplicate Work Items in ADO (same Custom.Label value)
                var labelToWorkItems = existingWorkItems
                    .GroupBy(wi => wi.Label, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

                foreach (var kvp in labelToWorkItems.Where(kvp => kvp.Value.Count > 1))
                {
                    response.SyncErrors.Add(new GitHubLabelSyncError
                    {
                        ErrorType = GitHubLabelSyncErrorType.DuplicateAdoWorkItem,
                        Label = kvp.Key,
                        Details = $"Multiple work items exist with the same label: {string.Join(", ", kvp.Value.Select(wi => wi.WorkItemId))}"
                    });
                }

                // Check for orphaned Work Items (Work Items referencing labels not in CSV)
                var serviceLabelSet = new HashSet<string>(serviceLabels, StringComparer.OrdinalIgnoreCase);
                foreach (var workItem in existingWorkItems)
                {
                    if (!serviceLabelSet.Contains(workItem.Label))
                    {
                        response.SyncErrors.Add(new GitHubLabelSyncError
                        {
                            ErrorType = GitHubLabelSyncErrorType.OrphanedWorkItem,
                            Label = workItem.Label,
                            Details = $"Work item {workItem.WorkItemId} references a label not found in the CSV"
                        });
                    }
                }

                // 3. Sync logic - determine which labels need Work Items created then create them
                var existingLabels = new HashSet<string>(existingWorkItems.Select(wi => wi.Label), StringComparer.OrdinalIgnoreCase);
                foreach (var label in serviceLabels)
                {
                    if (existingLabels.Contains(label))
                    {
                        var existingWorkItem = existingWorkItems.First(wi => wi.Label.Equals(label, StringComparison.OrdinalIgnoreCase));
                        response.ExistingWorkItems.Add(existingWorkItem);
                        continue;
                    }

                    if (dryRun)
                    {
                        // In dry-run mode, just report what would be created
                        response.CreatedWorkItems.Add(new GitHubLableWorkItem
                        {
                            Label = label,
                            WorkItemId = 0,
                            WorkItemUrl = "(would be created)"
                        });
                        continue;
                    }

                    try
                    {
                        var createdWorkItem = await devOpsService.CreateGitHubLableWorkItemAsync(label);
                        response.CreatedWorkItems.Add(createdWorkItem);
                        logger.LogInformation("Created work item {id} for label '{label}'", createdWorkItem.WorkItemId, label);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to create work item for label '{label}'", label);
                        response.SyncErrors.Add(new GitHubLabelSyncError
                        {
                            ErrorType = GitHubLabelSyncErrorType.AdoApiError,
                            Label = label,
                            Details = $"Failed to create work item: {ex.Message}"
                        });
                    }
                }

                logger.LogInformation("Sync completed. Existing: {existing}, Created: {created}, Errors: {errors}",
                    response.ExistingWorkItems.Count, response.CreatedWorkItems.Count, response.SyncErrors.Count);

                return response;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to sync labels to ADO");
                response.ResponseError = $"Failed to sync labels: {ex.Message}";
                return response;
            }
        }
    }
}
