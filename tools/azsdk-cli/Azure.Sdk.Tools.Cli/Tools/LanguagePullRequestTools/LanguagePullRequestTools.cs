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

        public override Command GetCommand()
        {
            var command = new Command("language-pr", "Language Pull Request Tools");
            var subCommands = new[]
            {
                new Command(createCodeOwnerPRCommandName, "Create a code owners pull request"),
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
                    var serviceLabelResult = await CreateCodeOwnerPR("defaultOwner", "defaultRepo", "defaultBranch", "defaultFile");
                    output.Output($"Service label validation result: {serviceLabelResult}");
                    return;
                case updateFileCommandName:
                    var repoOwner = commandParser.GetValueForOption(repoOwnerOpt);
                    var repoName = commandParser.GetValueForOption(repoNameOpt);
                    var headBranch = commandParser.GetValueForOption(headBranchOpt);
                    var fileName = commandParser.GetValueForOption(fileNameOpt);
                    var lineNumber = commandParser.GetValueForOption(lineNumberOpt);
                    var textToInsert = commandParser.GetValueForOption(textToInsertOpt);
                    var updateResult = await UpdateFileWithTextInsertion(repoOwner ?? "", repoName ?? "", headBranch ?? "", fileName ?? "", lineNumber, textToInsert ?? "");
                    output.Output($"File update result: {updateResult}");
                    return;
                default:
                    SetFailure();
                    output.OutputError($"Unknown command: '{command}'");
                    return;
            }
        }

            // This needs to be segmented into its own file.
        [McpServerTool(Name = "CreateCodeOwnerPR"), Description("Creates a code owners PR")]
        public async Task<string> CreateCodeOwnerPR(string repoOwner, string repoName, string branch, string fileName)
        {
            try
            {
                var insertionLineNumber = findAlphaSortedLocation();
                var textToInsert = formatEntry();

                await UpdateFileWithTextInsertion(repoOwner, repoName, branch, fileName, insertionLineNumber, textToInsert);

                var pr = await githubService.CreatePullRequestAsync(
                    repoName : repoName, repoOwner : repoOwner, baseBranch : branch, headBranch : branch, title : "Test PR2, DONT MERGE", body : "Test PR2, DONT MERGE", draft : true
                );
                
                var result = string.Join("\n", pr);
                foreach (var v in pr)
                {
                    logger.LogInformation("GitHub Response: {response}", v);
                }
                return result;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to create pull request");
                return $"Error: Failed to create pull request. {ex.Message}";
            }
        }

        private List<string> formatEntry(string serviceLabel, string sdkCodeOwners, string codeOwners)
        {
            try
            {
                List<string> formattedEntry = [];
                CodeownersEntry codeOwnersEntry = new CodeownersEntry
                {
                    
                };
                
                formattedEntry.Add("# AzureSdkOwners: {ALL OF THE SDK OWNERS}");
                formattedEntry.Add("# ServiceLabel: {SERVICE_LABEL}");
                formattedEntry.Add("# PRLabel: {PR_LABEL_SAME_AS_SERVICE_LABEL???}");
                formattedEntry.Add("");
            }
            catch ()
            {

            }
        }

        [McpServerTool(Name = "UpdateFileWithTextInsertion"), Description("Updates a file by inserting text at a specific line number")]
        public async Task<string> UpdateFileWithTextInsertion(string repoOwner, string repoName, string branch, string fileName, int lineNumber, string textToInsert)
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
                if (contents != null)
                {
                    logger.LogInformation("Successfully retrieved {count} content(s) from branch {branch}", contents.Count, branch);
                    foreach (var content in contents)
                    {
                        logger.LogInformation("File: {name}, Size: {size}, SHA: {sha}, Type: {type}",
                            content.Name, content.Size, content.Sha, content.Type);
                    }
                }
                else
                {
                    logger.LogInformation("No contents returned from branch {branch} for file {fileName}", branch, fileName);
                }
                
                if (contents == null || !contents.Any())
                {
                    result.Success = false;
                    result.Error = $"File {fileName} not found in repository {repoOwner}/{repoName}";
                    return JsonSerializer.Serialize(result);
                }

                var fileContent = contents.First();
                logger.LogInformation($"contents = {fileContent.Content}");
                var currentContent = fileContent.Content;
                var currentSha = fileContent.Sha;

                // Split into lines
                var lines = currentContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).ToList();
                result.TotalLines = lines.Count;

                // Insert text at the specified line number
                if (lineNumber == 0)
                {
                    // Append at the end
                    lines.Add(textToInsert);
                    result.LineNumber = lines.Count;
                }
                else if (lineNumber > 0 && lineNumber <= lines.Count)
                {
                    // Insert at specified line (1-based indexing)
                    lines.Insert(lineNumber - 1, textToInsert);
                }
                else
                {
                    result.Success = false;
                    result.Error = $"Line number {lineNumber} is out of range. File has {lines.Count} lines.";
                    return JsonSerializer.Serialize(result);
                }

                // Reconstruct the file content
                var newContent = string.Join("\n", lines);

                // Update the file
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
    }
}
