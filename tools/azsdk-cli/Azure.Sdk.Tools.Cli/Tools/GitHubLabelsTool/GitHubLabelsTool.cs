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
        public async Task<bool> CheckServiceLabel(string serviceLabel)
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

                logger.LogInformation($"Service label '{serviceLabel}' found: {result}");
                return result != null;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while checking service label: {serviceLabel}", serviceLabel);
                SetFailure();
                return false;
            }
        }

        [McpServerTool(Name = "CreateServiceLabel"), Description("Creates a pull request to add a new service label to the common-labels.csv.")]
        public async Task<string> CreateServiceLabel(string label, string link)
        {
            // Create a new branch
            // Prepare the CSV line to insert
            // Determine insertion point
            // Update the CSV file
            // Create the pull request

            try
            {
                // Create a new branch
                var branchResult = await githubService.CreateBranchAsync("Azure", "azure-sdk-tools", $"add-service-label-{label}", "main");
                if (branchResult == null)
                {
                    throw new InvalidOperationException("Failed to create branch");
                }

                // Prepare the CSV line to insert
                var csvLine = $"{label},,e99695";

                // Determine insertion point 
                var csvContent = await githubService.GetContentsAsync("Azure", "azure-sdk-tools", "tools/github/data/common-labels.csv");

                if (csvContent == null || csvContent.Count == 0)
                {
                    throw new InvalidOperationException("Could not retrieve common-labels.csv file");
                }

                var csvContentString = csvContent[0].Content;

                var insertionLine = GetInsertionLineNumber(csvContentString, label);
                logger.LogInformation($"Inserting new service label at line {insertionLine}: {csvLine}");

                logger.LogInformation($"Creating new service label: {label}. Documentation link: {link}"); // Is this documentation or branding link?

                await UpdateFileWithTextInsertion("Azure", "azure-sdk-tools", $"add-service-label-{label}", "tools/github/data/common-labels.csv", insertionLine, false, csvLine);

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

            using var reader = new StringReader(line);
            using var csv = new CsvReader(reader, System.Globalization.CultureInfo.InvariantCulture);

            // Read & parse the CSV line
            if (csv.Read())
            {
                var fieldCount = csv.Parser.Count; // number of fields in the current record
                for (int i = 0; i < fieldCount; i++)
                {
                    columns.Add(csv.GetField(i) ?? string.Empty);
                }
            }

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

        public async Task<string> UpdateFileWithTextInsertion(string repoOwner, string repoName, string branch, string fileName, int lineNumber, bool updatingContent, string textToInsert)
        {
            var result = new FileUpdateResult
            {
                FileName = fileName,
                LineNumber = lineNumber,
                TextInserted = textToInsert
            };

            try
            {
                // Get the current file content
                var contents = await githubService.GetContentsAsync(repoOwner, repoName, fileName, branch);
                
                if (contents == null || !contents.Any())
                {
                    result.Success = false;
                    result.Error = $"File {fileName} not found in repository {repoOwner}/{repoName}";
                    return JsonSerializer.Serialize(result);
                }

                var fileContent = contents.First();
                var currentContent = fileContent.Content;
                var currentSha = fileContent.Sha;

                var lines = currentContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).ToList();
                result.TotalLines = lines.Count;

                var textToInsertLines = textToInsert.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                var normalizedTextToInsert = string.Join("\n", textToInsertLines.Select(line => line.Trim())).Trim();
                var normalizedCurrentContent = string.Join("\n", lines.Select(line => line.Trim())).Trim();

                if (lineNumber == 0 && !updatingContent)
                {
                    lines.Add(textToInsert);
                    result.LineNumber = lines.Count;
                }
                else if (lineNumber == 0 && updatingContent)
                {
                    // Append at the end of the file
                    lines.Add(textToInsert);
                    result.LineNumber = lines.Count;
                }
                else if (lineNumber > 0 && lineNumber <= lines.Count && !updatingContent)
                {
                    // Insert at specified line (1-based indexing)
                    lines.Insert(lineNumber - 1, textToInsert);
                }
                else if (lineNumber > 0 && lineNumber <= lines.Count && updatingContent)
                {   
                    lines.RemoveAt(lineNumber - 1);

                    lines.Insert(lineNumber - 1, textToInsert);
                    result.LineNumber = lineNumber;
                }
                else
                {
                    result.Success = false;
                    result.Error = $"Line number {lineNumber} is out of range. File has {lines.Count} lines.";
                    return JsonSerializer.Serialize(result);
                }

                var newContent = string.Join("\n", lines);

                var commitMessage = $"Update {fileName}: Insert text at line {result.LineNumber}";
                var updateResponse = await githubService.UpdateFileAsync(repoOwner, repoName, fileName, commitMessage, newContent, currentSha, branch);

                result.Success = true;
                result.Message = $"Successfully updated {fileName}";
                result.CommitSha = updateResponse.Commit.Sha;
                result.TotalLines = lines.Count;

                logger.LogInformation("Successfully updated file {fileName} in {repoOwner}/{repoName} at line {lineNumber}", fileName, repoOwner, repoName, result.LineNumber);
                
                return JsonSerializer.Serialize(result);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
                logger.LogError(ex, "Failed to update file {fileName} in {repoOwner}/{repoName}", fileName, repoOwner, repoName);
                return JsonSerializer.Serialize(result);
            }
        }

        // Data model
        public class FileUpdateResult
        {
            public string FileName { get; set; } = "";
            public int LineNumber { get; set; }
            public string TextInserted { get; set; } = "";
            public bool Success { get; set; }
            public string Error { get; set; } = "";
            public string Message { get; set; } = "";
            public string CommitSha { get; set; } = "";
            public int TotalLines { get; set; }
        }
    }
}
