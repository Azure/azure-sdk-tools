using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Models;
using ModelContextProtocol.Server;
using System.CommandLine;
using Azure.Sdk.Tools.Cli.Contract;
using System.CommandLine.Invocation;
using System.Diagnostics;
using Azure.Sdk.Tools.CodeownersUtils.Parsing;
using System.Collections.Concurrent;
using Octokit;
using System.Text;
using System.Text.Json;

namespace Azure.Sdk.Tools.Cli.Tools
{
    [Description("This type contains test MCP tools for validation and testing purposes.")]
    [McpServerToolType]
    public class LanguagePullRequestTools(IGitHubService githubService,
        IOutputService output,
        ILogger<CommonValidationTool> logger) : MCPTool
    {

        // Command names
        private const string createCodeOwnerPRCommandName = "create-code-owner-pr";
        private const string updateFileCommandName = "update-file";

        // Command options
        private readonly Option<string> repoOwnerOpt = new(["--owner", "-o"], "Repository owner") { IsRequired = true };
        private readonly Option<string> repoNameOpt = new(["--repo", "-r"], "Repository name") { IsRequired = true };
        private readonly Option<string> headBranchOpt = new(["--branch", "-b"], "Head branch name") { IsRequired = true };
        private readonly Option<string> fileNameOpt = new(["--filename", "-f"], "File name to create") { IsRequired = true };
        private readonly Option<int> lineNumberOpt = new(["--line", "-l"], "Line number to insert text at (1-based, 0 to append at end)") { IsRequired = true };
        private readonly Option<string> textToInsertOpt = new(["--text", "-t"], "Text to insert") { IsRequired = true };

        // CreateCodeOwnerPR command options
        private readonly Option<string> pathExpressionOpt = new(["--path", "-p"], "Path expression for the CODEOWNERS entry") { IsRequired = true };
        private readonly Option<string[]> serviceLabelsOpt = new(["--service-labels", "-sl"], "Service labels (can specify multiple)") { IsRequired = true, AllowMultipleArgumentsPerToken = true };
        private readonly Option<string[]> serviceOwnersOpt = new(["--service-owners", "-so"], "Service owners (can specify multiple)") { IsRequired = true, AllowMultipleArgumentsPerToken = true };
        private readonly Option<string[]> azureSdkOwnersOpt = new(["--azure-sdk-owners", "-aso"], "Azure SDK owners (can specify multiple)") { IsRequired = false, AllowMultipleArgumentsPerToken = true };

        public override Command GetCommand()
        {
            var command = new Command("language-pr", "Language Pull Request Tools");
            var subCommands = new[]
            {
                new Command(createCodeOwnerPRCommandName, "Create a code owners pull request")
                {
                    repoOwnerOpt,
                    repoNameOpt,
                    headBranchOpt,
                    fileNameOpt,
                    pathExpressionOpt,
                    serviceLabelsOpt,
                    serviceOwnersOpt,
                    azureSdkOwnersOpt
                },
                new Command(updateFileCommandName, "Update an existing file with text insertion")
                {
                    repoOwnerOpt,
                    repoNameOpt,
                    headBranchOpt,
                    fileNameOpt,
                    lineNumberOpt,
                    textToInsertOpt
                },
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
                case createCodeOwnerPRCommandName:
                    var repoOwner = commandParser.GetValueForOption(repoOwnerOpt);
                    var repoName = commandParser.GetValueForOption(repoNameOpt);
                    var headBranch = commandParser.GetValueForOption(headBranchOpt);
                    var codeOwnerFileName = commandParser.GetValueForOption(fileNameOpt);
                    var pathExpression = commandParser.GetValueForOption(pathExpressionOpt);
                    var serviceLabels = commandParser.GetValueForOption(serviceLabelsOpt);
                    var serviceOwners = commandParser.GetValueForOption(serviceOwnersOpt);
                    var azureSdkOwners = commandParser.GetValueForOption(azureSdkOwnersOpt);

                    var serviceLabelResult = await CreateCodeOwnerPR(
                        repoOwner ?? "",
                        repoName ?? "",
                        headBranch ?? "",
                        codeOwnerFileName ?? "testdata.txt",
                        pathExpression ?? "",
                        serviceLabels?.ToList() ?? new List<string>(),
                        serviceOwners?.ToList() ?? new List<string>(),
                        azureSdkOwners?.ToList() ?? new List<string>());
                    output.Output($"Code owner PR result: {serviceLabelResult}");
                    return;
                default:
                    SetFailure();
                    output.OutputError($"Unknown command: '{command}'");
                    return;
            }
        }

        // This needs to be segmented into its own file.
        [McpServerTool(Name = "CreateCodeOwnerPR"), Description("Creates a code owners PR")]
        public async Task<string> CreateCodeOwnerPR(string repoOwner, string repoName, string branch, string fileName, string pathExpression, List<string> serviceLabels, List<string> serviceOwners, List<string> azureSdkOwners)
        {
            try
            {
                var contents = await githubService.GetContentsAsync("Azure", "azure-sdk-tools", "tools/github/data/common-labels.csv");

                if (contents == null || contents.Count == 0)
                {
                    throw new InvalidOperationException("Could not retrieve common-labels.csv file");
                }

                // Get the first (and should be only) file content
                var csvContent = contents[0].Content;

                return null;
                /*var (insertionLineNumber, updatingContent) = await findAlphaSortedLocation(serviceLabels[0], repoName);

                List<string> formattedEntry =
                [
                    $"\n# AzureSdkOwners: {string.Join(" ", azureSdkOwners.Select(owner => $"@{owner}"))}",
                    $"# ServiceLabel: %{serviceLabels[0]}",
                    $"# PRLabel: %{serviceLabels[0]}",
                    $"{pathExpression}\t\t\t\t\t{string.Join(" ", serviceOwners.Select(owner => $"@{owner}"))}",
                ];

                var formattedEntryString = string.Join("\n", formattedEntry);

                await UpdateFileWithTextInsertion(repoOwner, repoName, branch, fileName, insertionLineNumber, updatingContent, formattedEntryString);

                var pr = await githubService.CreatePullRequestAsync(
                    repoName: repoName,
                    repoOwner: repoOwner,
                    baseBranch: "master",
                    headBranch: branch,
                    title: "Making changes for " + serviceLabels[0],
                    body: "Making changes for " + serviceLabels[0],
                    draft: true
                );

                var result = string.Join("\n", pr);
                foreach (var v in pr)
                {
                    logger.LogInformation("GitHub Response: {response}", v);
                }
                return result;*/
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to create pull request");
                return $"Error: Failed to create pull request. {ex.Message}";
            }
        }

        public async Task<(int, bool)> findAlphaSortedLocation(string serviceLabel, string repoName)
        {
            try
            {
                var codeownerFiles = await githubService.GetContentsAsync("Azure", "azure-sdk-for-net", ".github/CODEOWNERS");

                if (codeownerFiles == null || codeownerFiles.Count == 0)
                {
                    throw new InvalidOperationException("Could not retrieve CODEOWNERS file");
                }

                var codeownersContent = codeownerFiles[0].Content;
                bool updatingContent = false;

                if (codeownersContent.Contains(serviceLabel))
                {
                    updatingContent = true;
                }

                var lines = codeownersContent.Split('\n');
                
                // Find all service labels with their line numbers
                var serviceLabels = new List<(string label, int lineNumber)>();
                
                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i].Trim();
                    if (line.StartsWith("# ServiceLabel:", StringComparison.OrdinalIgnoreCase) ||
                        line.StartsWith("# PRLabel:", StringComparison.OrdinalIgnoreCase))
                    {
                        // Extract the service label value
                        var labelValue = line.Substring(line.IndexOf(':') + 1).Trim();
                        // Remove the % prefix if present
                        if (labelValue.StartsWith("%"))
                        {
                            labelValue = labelValue.Substring(1);
                        }
                        serviceLabels.Add((labelValue, i));
                    }
                }

                // Remove % prefix from our target service label if present
                var targetLabel = serviceLabel.StartsWith("%") ? serviceLabel.Substring(1) : serviceLabel;
                logger.LogInformation("Looking for alphabetical position for: '{targetLabel}'", targetLabel);

                // Find the correct alphabetical position
                for (int i = 0; i < serviceLabels.Count; i++)
                {
                    var currentLabel = serviceLabels[i].label;
                    
                    if(string.Equals(targetLabel, currentLabel, StringComparison.OrdinalIgnoreCase) && updatingContent)
                    {
                        logger.LogInformation("Target label '{targetLabel}' already exists at line {lineNumber}", targetLabel, serviceLabels[i].lineNumber);
                        return (serviceLabels[i].lineNumber, updatingContent);
                    }

                    if (string.Compare(targetLabel, currentLabel, StringComparison.OrdinalIgnoreCase) < 0 && !updatingContent)
                    {
                        logger.LogInformation("Target label '{targetLabel}' should be inserted before '{currentLabel}' at line {lineNumber}",
                            targetLabel, currentLabel, serviceLabels[i].lineNumber);
                        return (serviceLabels[i].lineNumber, updatingContent);
                    }
                }

                // If we get here, our label should go at the end
                if (serviceLabels.Count > 0)
                {
                    var lastLabel = serviceLabels[serviceLabels.Count - 1];
                    logger.LogInformation("Target label '{targetLabel}' should be inserted after the last ServiceLabel '{lastLabel}' at line {lineNumber}", 
                        targetLabel, lastLabel.label, lastLabel.lineNumber + 1);
                    return (lastLabel.lineNumber + 1, updatingContent);
                }

                // If no service labels found, find the start of entries (after header) or end of file
                logger.LogInformation("No ServiceLabels found, finding appropriate insertion point");
                
                // Look for the first actual entry (non-comment, non-empty line)
                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i].Trim();
                    if (!string.IsNullOrEmpty(line) && !line.StartsWith("#"))
                    {
                        logger.LogInformation("Found first entry at line {lineNumber}, inserting before it", i);
                        return (i, updatingContent);
                    }
                }
                
                // If no entries found, insert at the end of the file
                logger.LogInformation("No entries found, inserting at end of file at line {lineNumber}", lines.Length);
                return (lines.Length, updatingContent);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to find alpha sorted location for service label: {serviceLabel}", serviceLabel);
                return (-1, false);
            }
        }

    }
}
