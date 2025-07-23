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

namespace Azure.Sdk.Tools.Cli.Tools
{
    [Description("This type contains test MCP tools for validation and testing purposes.")]
    [McpServerToolType]
    public class CommonValidationTool(IGitHubService githubService,
        IOutputService output,
        ICodeOwnerValidationHelper validationHelper,
        ILogger<CommonValidationTool> logger) : MCPTool
    {
        private static ConcurrentDictionary<string, CodeOwnerValidationResult> codeOwnerValidationCache = new ConcurrentDictionary<string, CodeOwnerValidationResult>();
        private static readonly Dictionary<string, string> azureRepositories = new Dictionary<string, string>
        {
            { "dotnet", "azure-sdk-for-net" },
            { "cpp", "azure-sdk-for-cpp" },
            { "go", "azure-sdk-for-go" },
            { "java", "azure-sdk-for-java" },
            { "typescript", "azure-sdk-for-js" },
            { "python", "azure-sdk-for-python" },
            { "rest-api-specs", "azure-rest-api-specs" },
            { "rust", "azure-sdk-for-rust" }
        };

        // Command names
        private const string isValidCodeOwnerCommandName = "is-valid-code-owner";
        private const string validateCodeOwnersForServiceCommandName = "validate-code-owners-for-service";

        // Command options
        private readonly Option<string> serviceLabelOpt = new(["--service", "-s"], "Confirmed service label to validate code owners for") { IsRequired = true };
        private readonly Option<string> gitHubAliasOpt = new(["--username", "-u"], "GitHub alias to validate") { IsRequired = false };
        private readonly Option<string> repoNameOpt = new(["--repo", "-r"], "Repository name to process") { IsRequired = true };

        public override Command GetCommand()
        {
            var command = new Command("common-validation-tools", "Tools for validating CODEOWNERS files.");
            var subCommands = new[]
            {
                new Command(isValidCodeOwnerCommandName, "Validate if a GitHub alias has proper organizational membership and write access") { gitHubAliasOpt },
                new Command(validateCodeOwnersForServiceCommandName, "Process a specific repository to validate code owners for a service") { repoNameOpt, serviceLabelOpt },
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
                case isValidCodeOwnerCommandName:
                    var gitHubAlias = commandParser.GetValueForOption(gitHubAliasOpt);
                    var aliasValidationResult = await isValidCodeOwner(gitHubAlias ?? "");
                    output.Output($"GitHub alias validation result: {aliasValidationResult}");
                    return;
                case validateCodeOwnersForServiceCommandName:
                    var repoName = commandParser.GetValueForOption(repoNameOpt);
                    var serviceLabel = commandParser.GetValueForOption(serviceLabelOpt);
                    var repoValidationResult = await ValidateCodeOwnersForService(repoName ?? "", serviceLabel ?? "");
                    output.Output($"Repository validation result: {System.Text.Json.JsonSerializer.Serialize(repoValidationResult, new System.Text.Json.JsonSerializerOptions { WriteIndented = true })}");
                    return;
                default:
                    SetFailure();
                    output.OutputError($"Unknown command: '{command}'");
                    return;
            }
        }

        private ServiceCodeOwnerResult CreateErrorResult(string repository, string message)
        {
            return new ServiceCodeOwnerResult
            {
                Repository = repository,
                Status = "Error",
                Message = message,
                CodeOwners = new List<CodeOwnerValidationResult>()
            };
        }

        [McpServerTool(Name = "ValidateCodeOwnersForService"), Description("Validates code owners in a specific repository for a given service.")]
        public async Task<ServiceCodeOwnerResult> ValidateCodeOwnersForService(string repoName, string serviceLabel)
        {
            string? fullRepoName = null;
            try
            {
                // Validate repo name exists in our mapping
                if (!azureRepositories.TryGetValue(repoName.ToLowerInvariant(), out fullRepoName))
                {
                    return CreateErrorResult("", $"Unknown repository: {repoName}. Valid options: {string.Join(", ", azureRepositories.Keys)}");
                }

                var result = new ServiceCodeOwnerResult
                {
                    Repository = fullRepoName,
                    CodeOwners = new List<CodeOwnerValidationResult>(),
                    Status = "Processing"
                };

                var codeownersUrl = $"https://raw.githubusercontent.com/Azure/{fullRepoName}/main/.github/CODEOWNERS";

                // Need to check if the link provided is correct and won't break.
                var codeownersEntries = CodeownersParser.ParseCodeownersFile(codeownersUrl, "https://azuresdkartifacts.blob.core.windows.net/azure-sdk-write-teams/azure-sdk-write-teams-blob");
                var matchingEntry = validationHelper.FindServiceEntries(codeownersEntries, serviceLabel);

                if (matchingEntry != null)
                {
                    var uniqueOwners = validationHelper.ExtractUniqueOwners(matchingEntry);
                    
                    result.CodeOwners = await ValidateCodeOwnersConcurrently(uniqueOwners);

                    result.Status = "Success";
                    result.Message = $"Found 1 matching entry with {result.CodeOwners.Count} code owners";
                }
                else
                {
                    result.Status = "Service not found";
                    result.Message = $"Service label '{serviceLabel}' not found in {fullRepoName}";
                }

                return result;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing repository {repo}", fullRepoName ?? repoName);
                return CreateErrorResult(fullRepoName ?? repoName, $"Error processing repository: {ex.Message}");
            }
        }

        public async Task<List<CodeOwnerValidationResult>> ValidateCodeOwnersConcurrently(List<string> owners)
        {
            var results = new List<CodeOwnerValidationResult>();
            var asyncValidationTasks = new List<Task<CodeOwnerValidationResult>>();
            
            foreach (var owner in owners)
            {
                var username = owner.TrimStart('@');
                
                if (codeOwnerValidationCache.TryGetValue(username, out var cachedResult))
                {
                    results.Add(cachedResult);
                }
                else
                {
                    asyncValidationTasks.Add(ValidateCodeOwnerWithCaching(username));
                }
            }

            if (asyncValidationTasks.Count > 0)
            {
                var asyncResults = await Task.WhenAll(asyncValidationTasks);
                results.AddRange(asyncResults);
            }
            
            return results;
        }

        private async Task<CodeOwnerValidationResult> ValidateCodeOwnerWithCaching(string username)
        {
            var result = await ValidateCodeOwner(username);
            codeOwnerValidationCache.TryAdd(username, result);
            
            return result;
        }

        public async Task<CodeOwnerValidationResult> ValidateCodeOwner(string username)
        {
            try
            {
                // Call PowerShell script directly
                var powerShellOutput = await CallPowerShellScriptViaCMD($"-UserName \"{username}\"");

                var validationResult = validationHelper.ParsePowerShellOutput(powerShellOutput, username);
                
                return validationResult;
            }
            catch (Exception ex)
            {
                var validationResult = new CodeOwnerValidationResult
                {
                    Username = username,
                    Status = "Error",
                    Message = $"Error validating {username}: {ex.Message}",
                    IsValidCodeOwner = false,
                    HasWritePermission = false,
                    Organizations = new Dictionary<string, bool>()
                };
                logger.LogError(ex, "Error validating code owner {username}", username);
                return validationResult;
            }
        }

        [McpServerTool(Name = "isValidCodeOwner"), Description("Validates if the user is a code owner given their GitHub alias. (Default is the current user)")]
        public async Task<string> isValidCodeOwner(string githubAlias = "")
        {
            try
            {
                // Get the current user's GitHub username if not provided
                var user = await githubService.GetGitUserDetailsAsync();
                var userDetails = string.IsNullOrEmpty(githubAlias) ? user?.Login : githubAlias;
                
                if (string.IsNullOrEmpty(userDetails))
                {
                    var errorResponse = new GenericResponse()
                    {
                        Status = "Failed",
                        Details = { "Unable to determine GitHub username" }
                    };
                    return output.Format(errorResponse);
                }

                var validationResult = await ValidateCodeOwner(userDetails);
                
                // Convert to the expected JSON format
                var result = new
                {
                    validationResult.Organizations,
                    validationResult.HasWritePermission,
                    validationResult.IsValidCodeOwner
                };

                return System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            }
            catch (Exception ex)
            {
                var errorResponse = new GenericResponse()
                {
                    Status = "Failed",
                    Details = { $"Failed to validate GitHub code owner. Error: {ex.Message}" }
                };
                return output.Format(errorResponse);
            }
        }

        private async Task<string> CallPowerShellScriptViaCMD(string arguments = "")
        {
            try
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "pwsh.exe",
                    Arguments = $"-ExecutionPolicy Bypass -File \"..\\..\\..\\tools\\github\\scripts\\Validate-AzsdkCodeOwner.ps1\" {arguments}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processStartInfo);
                if (process == null)
                {
                    logger.LogError("Failed to start PowerShell process.");
                    return "Error: Failed to start PowerShell process";
                }
                var processOutput = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                return processOutput ?? "";
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }
    }
}
