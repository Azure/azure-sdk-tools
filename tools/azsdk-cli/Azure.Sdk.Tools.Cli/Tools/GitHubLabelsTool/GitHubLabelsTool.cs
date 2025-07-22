// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Contract;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Helpers;
using System.Text;


namespace Azure.Sdk.Tools.Cli.Tools
{

    [McpServerToolType, Description("Tools for working with GitHub service labels from the Azure SDK common labels CSV file")]
    public class GitHubLabelsTool(ILogger<GitHubLabelsTool> logger, IOutputService output, ILabelHelper labelHelper, IGitHubService githubService) : MCPTool
    {
        private const string serviceLabelColorCode = "e99695"; // color code for service labels in common-labels.csv

        //command names
        private const string checkServiceLabelCommandName = "check-service-label";
        private const string createServiceLabelCommandName = "create-service-label";

        // Command options
        private readonly Option<string> proposedServiceLabelOpt = new(["--service", "-s"], "Proposed Service name used to create a PR for a new label.") { IsRequired = true };
        private readonly Option<string> documentationLinkOpt = new(["--link", "-l"], "Brand documentation link used to create a PR for a new label.") { IsRequired = true };

        private readonly Argument<string> _serviceLabelArg = new Argument<string>(
            name: "service-label",
            description: "The service label to check in the common labels CSV"
        )
        {
            Arity = ArgumentArity.ExactlyOne // only one service label is expected
        };

        public override Command GetCommand()
        {
            var command = new Command("github-labels", "GitHub service labels tools");
            var subCommands = new[]
            {
                new Command(checkServiceLabelCommandName, "Check if a service label exists in the common labels CSV") { _serviceLabelArg },
                new Command(createServiceLabelCommandName, "Creates a PR for a new label given a proposed label and brand documentation.") { proposedServiceLabelOpt, documentationLinkOpt },
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
                    var serviceLabel = commandParser.GetValueForArgument(_serviceLabelArg);
                    var result = await CheckServiceLabel(serviceLabel);
                    ctx.ExitCode = ExitCode;
                    output.Output(result);
                    return;
                case createServiceLabelCommandName:
                    var proposedServiceLabel = commandParser.GetValueForOption(proposedServiceLabelOpt);
                    var documentationLink = commandParser.GetValueForOption(documentationLinkOpt);
                    var createdPRLink = await CreateServiceLabel(proposedServiceLabel, documentationLink ?? ""); // Should probably just return the created PR link.
                    output.Output($"Create service label result: {createdPRLink}");
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
                return new ServiceLabelResponse
                {
                    ServiceLabel = serviceLabel,
                    Found = result != null,
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while checking service label: {serviceLabel}", serviceLabel);
                SetFailure();
                return new ServiceLabelResponse
                {
                    ServiceLabel = serviceLabel,
                    Found = false,
                    ResponseError = $"Error occurred while checking service label '{serviceLabel}': {ex.Message}"
                };
            }
        }

        [McpServerTool(Name = "CreateServiceLabel"), Description("Creates a pull request to add a new service label to the common-labels.csv.")]
        public async Task<string> CreateServiceLabel(string label, string link)
        {
            try
            {
                logger.LogInformation($"Creating new service label: {label}. Documentation link: {link}"); // Is this documentation or branding link?

                var result = await githubService.CreatePullRequestAsync(
                    repoName: "azure-sdk-tools",
                    repoOwner: "Azure",
                    baseBranch: "main",
                    // This is not sufficiently unique
                    headBranch: $"add-service-label-{label}",
                    title: $"Add service label: {label}",
                    body: $"This PR adds the service label '{label}' to the repository. Documentation link: {link}",

                    // TODO: Before merge, make this not draft
                    draft: true
                );

                logger.LogInformation($"Service label '{label}' pull request created successfully. Result: {string.Join(", ", result)}");

                var response = new GenericResponse()
                {
                    Status = "Success",
                    Details = { $"Service label '{label}' pull request created successfully." }
                };
                response.Details.AddRange(result);

                return output.Format(response);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Failed to create pull request for service label '{label}': {ex.Message}");

                var errorResponse = new GenericResponse()
                {
                    Status = "Failed",
                    Details = { $"Failed to create pull request for service label '{label}'. Error: {ex.Message}" }
                };
                return output.Format(errorResponse);
            }
        }

        private static List<string> ParseCsvLine(string line)
        {
            var columns = new List<string>();
            var currentColumn = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    columns.Add(currentColumn.ToString());
                    currentColumn.Clear();
                }
                else
                {
                    currentColumn.Append(c);
                }
            }

            // Add the last column
            columns.Add(currentColumn.ToString());

            return columns;
        }


        private int GetInsertionLineNumber(string csvContent, string newServiceLabel)
        {
            var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            if (lines.Length == 0) return 1;

            string newLabelName = $"Service - {newServiceLabel}";

            // Store service labels and original line indices
            var serviceLabels = new List<(string labelName, int originalLineIndex)>();

            for (int i = 0; i < lines.Length; i++)
            {
                var columns = ParseCsvLine(lines[i]);

                if (columns.Count >= 3)
                {
                    string labelName = columns[0].Trim();
                    string colorCode = columns[2].Trim();

                    // Only consider service labels
                    if (colorCode.Equals(serviceLabelColorCode, StringComparison.OrdinalIgnoreCase))
                    {
                        serviceLabels.Add((labelName, i));
                    }
                }
            }

            for (int i = 0; i < serviceLabels.Count; i++)
            {
                if (string.Compare(newLabelName, serviceLabels[i].labelName, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return serviceLabels[i].originalLineIndex + 1; // +1 to convert to 1-based line number
                }
            }

            // Insert after all existing service labels
            if (serviceLabels.Count > 0)
            {
                return serviceLabels.Last().originalLineIndex + 2;
            }

            return 1;
        }
    }


    public class ServiceLabelResponse
    {
        public string ServiceLabel { get; set; } = "";
        public bool Found { get; set; }
        public string? ColorCode { get; set; }
        public string? Description { get; set; }
        public string? ResponseError { get; set; }
    }
}
