using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Models;
using ModelContextProtocol.Server;
using System.CommandLine;
using Azure.Sdk.Tools.Cli.Contract;
using System.CommandLine.Invocation;
using Azure.Sdk.Tools.CodeownersUtils.Parsing;
using System.Collections.Concurrent;

namespace Azure.Sdk.Tools.Cli.Tools
{
    /// <summary>
    /// Tools for validating GitHub users for Azure SDK code owner requirements.
    /// This is a C# replacement for the Validate-AzsdkCodeOwner.ps1 PowerShell script.
    /// </summary>
    [Description("Tools for validating GitHub users for Azure SDK code owner requirements")]
    [McpServerToolType]
    public class CodeOwnerTools(IGitHubService githubService,
        IOutputService output,
        ICodeOwnerValidationHelper validationHelper,
        ICodeOwnerValidator codeOwnerValidator,
        ILogger<CodeOwnerTools> logger) : MCPTool
    {
        private static ConcurrentDictionary<string, CodeOwnerValidationResult> codeOwnerValidationCache = new ConcurrentDictionary<string, CodeOwnerValidationResult>();
        private static readonly Dictionary<string, (string RepoName, string ServiceCategory)> azureRepositories = new()
        {
            { "dotnet", ("azure-sdk-for-net", "# ######## Services ########") },
            { "cpp", ("azure-sdk-for-cpp", "# Client SDKs") },
            { "go", ("azure-sdk-for-go", "# SDK (track2)") },
            { "java", ("azure-sdk-for-java", "# ######## Services ########") },
            { "javascript", ("azure-sdk-for-js", "# SDK") },
            { "python", ("azure-sdk-for-python", "# Service team") },
            { "rest-api-specs", ("azure-rest-api-specs", "") },
            { "rust", ("azure-sdk-for-rust", "# Client SDKs") }
        };

        // Command names
        private const string isValidCodeOwnerCommandName = "is-valid-code-owner";
        private const string validateCodeOwnersForServiceCommandName = "validate-code-owners-for-service";
        private const string addCodeOwnerEntryCommandName = "add-codeowner-entry";
        private const string addCodeOwnerToEntryCommandName = "add-codeowner-to-entry";

        // Command options - made flexible to support different block types
        private readonly Option<string> serviceLabelOpt = new(["--service", "-s"], "Confirmed service label to validate code owners for") { IsRequired = true };
        private readonly Option<string> gitHubAliasOpt = new(["--username", "-u"], "GitHub alias to validate") { IsRequired = false };
        private readonly Option<string> repoNameOpt = new(["--repo", "-r"], "Repository name to process") { IsRequired = true };
        private readonly Option<List<string>> codeOwnersOpt = new(["--codeowners", "-c"], "List of code owners to add (required for source path/owner blocks)") { IsRequired = false };
        
        // New options for codeowner entry management
        private readonly Option<string> repoPathOpt = new(["--path", "-p"], "Repository path pattern for the codeowner entry (required for source path/owner blocks)") { IsRequired = false };
        private readonly Option<string> azureSdkOwnersOpt = new(["--azure-sdk-owners"], "Azure SDK owners (optional, requires service-label)") { IsRequired = false };
        private readonly Option<string> serviceOwnersOpt = new(["--service-owners"], "Service owners (for metadata-only blocks)") { IsRequired = false };
        private readonly Option<string> prLabelOpt = new(["--pr-label"], "PR label (only valid with source path/owner blocks)") { IsRequired = false };
        private readonly Option<string> serviceLabelOptional = new(["--service-label"], "Service label (optional for source blocks, required for metadata blocks)") { IsRequired = false };
        private readonly Option<string> codeOwnerToAddOpt = new(["--codeowner"], "Single code owner to add or remove") { IsRequired = true };

        public override Command GetCommand()
        {
            var command = new Command("common-validation-tools", "Tools for validating CODEOWNERS files.");
            var subCommands = new[]
            {
                new Command(isValidCodeOwnerCommandName, "Validate if a GitHub alias has proper organizational membership and write access") { gitHubAliasOpt },
                new Command(validateCodeOwnersForServiceCommandName, "Process a specific repository to validate code owners for a service") { repoNameOpt, serviceLabelOpt },
                new Command(addCodeOwnerEntryCommandName, "Add a new codeowner entry with metadata") { repoNameOpt, repoPathOpt, codeOwnersOpt, serviceLabelOptional, prLabelOpt, azureSdkOwnersOpt, serviceOwnersOpt },
                new Command(addCodeOwnerToEntryCommandName, "Add a codeowner to an existing entry") { repoNameOpt, repoPathOpt, codeOwnerToAddOpt },
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
                case addCodeOwnerEntryCommandName:
                    var entryRepoName = commandParser.GetValueForOption(repoNameOpt);
                    var entryRepoPath = commandParser.GetValueForOption(repoPathOpt);
                    var entryCodeOwners = commandParser.GetValueForOption(codeOwnersOpt);
                    var entryServiceLabel = commandParser.GetValueForOption(serviceLabelOptional);
                    var entryPrLabel = commandParser.GetValueForOption(prLabelOpt);
                    var entryAzureSdkOwners = commandParser.GetValueForOption(azureSdkOwnersOpt);
                    var entryServiceOwners = commandParser.GetValueForOption(serviceOwnersOpt);
                    var entryResult = await AddCodeOwnerEntry(entryRepoName ?? "", entryRepoPath ?? "", entryCodeOwners ?? new List<string>(), entryServiceLabel, entryPrLabel, entryAzureSdkOwners, entryServiceOwners);
                    output.Output($"Add entry result: {System.Text.Json.JsonSerializer.Serialize(entryResult, new System.Text.Json.JsonSerializerOptions { WriteIndented = true })}");
                    return;
                case addCodeOwnerToEntryCommandName:
                    var addToEntryRepoName = commandParser.GetValueForOption(repoNameOpt);
                    var addToEntryRepoPath = commandParser.GetValueForOption(repoPathOpt);
                    var addToEntryCodeOwner = commandParser.GetValueForOption(codeOwnerToAddOpt);
                    var addToEntryResult = await AddCodeOwnerToEntry(addToEntryRepoName ?? "", addToEntryRepoPath ?? "", addToEntryCodeOwner ?? "");
                    output.Output($"Add to entry result: {System.Text.Json.JsonSerializer.Serialize(addToEntryResult, new System.Text.Json.JsonSerializerOptions { WriteIndented = true })}");
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
                if (!azureRepositories.TryGetValue(repoName.ToLowerInvariant(), out var repoInfo))
                {
                    return CreateErrorResult("", $"Unknown repository: {repoName}. Valid options: {string.Join(", ", azureRepositories.Keys)}");
                }

                fullRepoName = repoInfo.RepoName;

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
            var result = await codeOwnerValidator.ValidateCodeOwnerAsync(username, verbose: false);
            codeOwnerValidationCache.TryAdd(username, result);

            return result;
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

                var validationResult = await codeOwnerValidator.ValidateCodeOwnerAsync(userDetails, verbose: false);

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

        [McpServerTool(Name = "AddCodeOwnerEntry"), Description("Add a new codeowner entry with metadata")]
        public async Task<ServiceCodeOwnerResult> AddCodeOwnerEntry(string repoName, string repoPath, List<string> codeOwners, 
            string? serviceLabel = null, string? prLabel = null, string? azureSdkOwners = null, string? serviceOwners = null)
        {
            string? fullRepoName = null;
            try
            {
                // Validate repo name
                if (!azureRepositories.TryGetValue(repoName.ToLowerInvariant(), out var repoInfo))
                {
                    return CreateErrorResult("", $"Unknown repository: {repoName}. Valid options: {string.Join(", ", azureRepositories.Keys)}");
                }

                fullRepoName = repoInfo.RepoName;

                // Validate requirements
                var validationResult = ValidateEntryRequirements(repoPath, codeOwners, serviceLabel, prLabel, azureSdkOwners, serviceOwners);
                if (!validationResult.IsValid)
                {
                    return CreateErrorResult(fullRepoName, validationResult.ErrorMessage);
                }

                // Get current CODEOWNERS file
                var codeownersFileContent = await githubService.GetContentsAsync("Azure", fullRepoName, ".github/CODEOWNERS");
                if (codeownersFileContent == null || codeownersFileContent.Count == 0)
                {
                    return CreateErrorResult(fullRepoName, "Could not retrieve CODEOWNERS file");
                }

                var currentContent = codeownersFileContent[0].Content;
                if (string.IsNullOrEmpty(currentContent))
                {
                    return CreateErrorResult(fullRepoName, "CODEOWNERS file is empty");
                }

                var blockToParse = findBlock(currentContent, repoInfo.ServiceCategory);

                // Check if entry already exists
                var existingEntries = CodeownersParser.ParseCodeownersFile($"https://raw.githubusercontent.com/Azure/{fullRepoName}/main/.github/CODEOWNERS", 
                    "https://azuresdkartifacts.blob.core.windows.net/azure-sdk-write-teams/azure-sdk-write-teams-blob",
                    blockToParse.StartLine,
                    blockToParse.EndLine);
                
                var existingEntry = existingEntries.FirstOrDefault(e => e.PathExpression == repoPath);
                if (existingEntry != null)
                {
                    return CreateErrorResult(fullRepoName, $"Entry with path '{repoPath}' already exists within the Azure Service category.");
                }

                // Build the new entry
                var newEntry = BuildCodeOwnerEntry(repoPath, codeOwners, serviceLabel, prLabel, azureSdkOwners, serviceOwners);
                
                // Create branch for the change
                var normalizedLabel = (serviceLabel ?? prLabel ?? "entry").Replace(" ", "-").Replace("/", "-").ToLowerInvariant();
                var branchName = $"add-codeowner-entry-{normalizedLabel}-{DateTime.Now:yyyyMMdd-HHmmss}";
                
                var branchResult = await githubService.CreateBranchAsync("Azure", fullRepoName, branchName, "main");
                if (branchResult.Contains("already exists"))
                {
                    return CreateErrorResult(fullRepoName, "Branch already exists, please try again");
                }

                var lines = currentContent.Split('\n').ToList();
                var insertIndex = blockToParse.EndLine == -1 ? lines.Count : blockToParse.EndLine;

                lines.Insert(insertIndex, "");
                lines.Insert(insertIndex, newEntry);
                
                var updatedContent = string.Join("\n", lines);

                // Update the file
                await githubService.UpdateFileAsync("Azure", fullRepoName, ".github/CODEOWNERS", 
                    $"Add codeowner entry for {repoPath}", updatedContent, codeownersFileContent[0].Sha, branchName);

                return new ServiceCodeOwnerResult
                {
                    Repository = fullRepoName,
                    Status = "Success",
                    Message = $"Successfully added codeowner entry for path '{repoPath}' in branch '{branchName}'",
                    CodeOwners = new List<CodeOwnerValidationResult>()
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error adding codeowner entry to repository {repo}", fullRepoName ?? repoName);
                return CreateErrorResult(fullRepoName ?? repoName, $"Error adding codeowner entry: {ex.Message}");
            }
        }

        /// <summary>
        /// Method 2: Add a codeowner to an existing entry
        /// </summary>
        [McpServerTool(Name = "AddCodeOwnerToEntry"), Description("Add a codeowner to an existing entry")]
        public async Task<ServiceCodeOwnerResult> AddCodeOwnerToEntry(string repoName, string repoPath, string codeOwner)
        {
            string? fullRepoName = null;
            try
            {
                // Validate repo name
                if (!azureRepositories.TryGetValue(repoName.ToLowerInvariant(), out var repoInfo))
                {
                    return CreateErrorResult("", $"Unknown repository: {repoName}. Valid options: {string.Join(", ", azureRepositories.Keys)}");
                }

                fullRepoName = repoInfo.RepoName;

                // Validate codeowner format
                if (string.IsNullOrWhiteSpace(codeOwner))
                {
                    return CreateErrorResult(fullRepoName, "Code owner cannot be empty");
                }

                var normalizedCodeOwner = codeOwner.StartsWith("@") ? codeOwner : $"@{codeOwner}";

                // Get current CODEOWNERS file
                var codeownersFileContent = await githubService.GetContentsAsync("Azure", fullRepoName, ".github/CODEOWNERS");
                if (codeownersFileContent == null || codeownersFileContent.Count == 0)
                {
                    return CreateErrorResult(fullRepoName, "Could not retrieve CODEOWNERS file");
                }

                var currentContent = codeownersFileContent[0].Content;
                if (string.IsNullOrEmpty(currentContent))
                {
                    return CreateErrorResult(fullRepoName, "CODEOWNERS file is empty");
                }

                // Find the entry to modify
                var lines = currentContent.Split('\n');
                var entryLineIndex = -1;
                
                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i].Trim();
                    if (!line.StartsWith("#") && !string.IsNullOrEmpty(line) && line.StartsWith(repoPath))
                    {
                        entryLineIndex = i;
                        break;
                    }
                }

                if (entryLineIndex == -1)
                {
                    return CreateErrorResult(fullRepoName, $"No entry found for path '{repoPath}'");
                }

                // Check if codeowner already exists
                if (lines[entryLineIndex].Contains(normalizedCodeOwner))
                {
                    return CreateErrorResult(fullRepoName, $"Code owner '{normalizedCodeOwner}' already exists in entry for '{repoPath}'");
                }

                // Add the codeowner to the line
                lines[entryLineIndex] = lines[entryLineIndex].TrimEnd() + " " + normalizedCodeOwner;

                // Create branch and update file
                var branchName = $"add-codeowner-{normalizedCodeOwner.Replace("@", "")}-{DateTime.Now:yyyyMMdd-HHmmss}";
                await githubService.CreateBranchAsync("Azure", fullRepoName, branchName, "main");

                var updatedContent = string.Join("\n", lines);
                await githubService.UpdateFileAsync("Azure", fullRepoName, ".github/CODEOWNERS", 
                    $"Add {normalizedCodeOwner} to {repoPath}", updatedContent, codeownersFileContent[0].Sha, branchName);

                return new ServiceCodeOwnerResult
                {
                    Repository = fullRepoName,
                    Status = "Success",
                    Message = $"Successfully added '{normalizedCodeOwner}' to entry for '{repoPath}' in branch '{branchName}'",
                    CodeOwners = new List<CodeOwnerValidationResult>()
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error adding codeowner to entry in repository {repo}", fullRepoName ?? repoName);
                return CreateErrorResult(fullRepoName ?? repoName, $"Error adding codeowner to entry: {ex.Message}");
            }
        }

        public (int StartLine, int EndLine) findBlock(string currentContent, string serviceCategory)
        {
            var lines = currentContent.Split('\n');

            int startLine = -1;
            int endLine = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Trim().Equals(serviceCategory))
                {
                    startLine = i;
                }
                else if (startLine != -1 && lines[i].Trim().Contains("###"))
                {
                    logger.LogInformation($"End line = {i}");
                    endLine = i;
                    return (startLine, endLine);
                }
            }
            return (0, lines.Length);
        }

        private (bool IsValid, string ErrorMessage) ValidateEntryRequirements(string repoPath, List<string> codeOwners,
            string? serviceLabel, string? prLabel, string? azureSdkOwners, string? serviceOwners)
        {
            // Determine what we have
            bool hasRepoPath = !string.IsNullOrWhiteSpace(repoPath);
            bool hasServiceLabel = !string.IsNullOrWhiteSpace(serviceLabel);
            bool hasPrLabel = !string.IsNullOrWhiteSpace(prLabel);
            bool hasAzureSdkOwners = !string.IsNullOrWhiteSpace(azureSdkOwners);
            bool hasServiceOwners = !string.IsNullOrWhiteSpace(serviceOwners);
            bool hasCodeOwners = codeOwners != null && codeOwners.Count > 0;

            // According to the docs, there are exactly 3 valid block types:

            // Block Type 1: Source path/owner block (ends with source path/owner line)
            // Can optionally have: AzureSdkOwners, ServiceLabel, PRLabel
            // Must have: repo path AND code owners
            // Cannot have: ServiceOwners (service owners are inferred from source owners)
            if (hasRepoPath)
            {
                // If we have a repo path, this MUST be a source path/owner block
                if (!hasCodeOwners)
                {
                    return (false, "Source path/owner block requires at least one code owner.");
                }

                // ServiceOwners cannot be part of a source path/owner block
                if (hasServiceOwners)
                {
                    return (false, "ServiceOwners cannot be part of a source path/owner block. Service owners are inferred from the source path owners.");
                }

                // AzureSdkOwners must be part of a block that contains ServiceLabel
                if (hasAzureSdkOwners && !hasServiceLabel)
                {
                    return (false, "AzureSdkOwners must be part of a block that contains a ServiceLabel entry.");
                }

                // PRLabel must be part of a block that ends in source path/owner line (which this is)
                // This is valid - no additional validation needed for PRLabel here

                return (true, string.Empty);
            }

            // Block Type 2: ServiceLabel + ServiceOwners block (no source path/owner line)
            // Must have: ServiceLabel AND ServiceOwners
            // Can optionally have: AzureSdkOwners
            // Cannot have: repo path, code owners, PRLabel
            if (hasServiceLabel && hasServiceOwners)
            {
                // PRLabel cannot be part of a non-source block
                if (hasPrLabel)
                {
                    return (false, "PRLabel must be part of a block that ends in a source path/owner line.");
                }

                // Code owners are not needed/allowed in this block type
                if (hasCodeOwners)
                {
                    return (false, "ServiceLabel/ServiceOwners block should not have code owners specified. Service owners provide the ownership information.");
                }

                return (true, string.Empty);
            }

            // Block Type 3: ServiceLabel + AzureSdkOwners block (no source path/owner line, no ServiceOwners)
            // Must have: ServiceLabel AND AzureSdkOwners
            // Cannot have: repo path, code owners, ServiceOwners, PRLabel
            if (hasServiceLabel && hasAzureSdkOwners && !hasServiceOwners)
            {
                // PRLabel cannot be part of a non-source block
                if (hasPrLabel)
                {
                    return (false, "PRLabel must be part of a block that ends in a source path/owner line.");
                }

                // Code owners are not needed/allowed in this block type
                if (hasCodeOwners)
                {
                    return (false, "ServiceLabel/AzureSdkOwners block should not have code owners specified. This is a metadata-only block.");
                }

                return (true, string.Empty);
            }

            // Invalid combinations - provide specific error messages
            if (hasServiceLabel && !hasRepoPath && !hasServiceOwners && !hasAzureSdkOwners)
            {
                return (false, "ServiceLabel must be part of a block that either: 1) ends in a source path/owner line, 2) contains ServiceOwners, or 3) contains AzureSdkOwners.");
            }

            if (hasAzureSdkOwners && !hasServiceLabel)
            {
                return (false, "AzureSdkOwners must be part of a block that contains a ServiceLabel entry.");
            }

            if (hasPrLabel && !hasRepoPath)
            {
                return (false, "PRLabel must be part of a block that ends in a source path/owner line.");
            }

            if (hasServiceOwners && !hasServiceLabel)
            {
                return (false, "ServiceOwners must be part of a block that contains a ServiceLabel entry.");
            }

            // Check if we have any valid starting point
            if (!hasRepoPath && !hasServiceLabel)
            {
                return (false, "Must specify either a repository path (for source path/owner block) or a service label (for metadata-only blocks).");
            }

            // If we get here, it's an unsupported combination
            return (false, "Invalid combination of metadata. Valid block types are: 1) Source path/owner with optional metadata, 2) ServiceLabel + ServiceOwners with optional AzureSdkOwners, 3) ServiceLabel + AzureSdkOwners only.");
        }

        /// <summary>
        /// Helper method to build a codeowner entry with metadata according to CODEOWNERS rules
        /// Simplified sequential block building approach
        /// </summary>
        private string BuildCodeOwnerEntry(string repoPath, List<string> codeOwners, 
            string? serviceLabel, string? prLabel, string? azureSdkOwners, string? serviceOwners)
        {
            var block = new List<string>();
            
            bool hasRepoPath = !string.IsNullOrWhiteSpace(repoPath);
            bool hasSourcePathOwnerLine = hasRepoPath && codeOwners?.Count > 0;

            // Build metadata in correct order: AzureSdkOwners, ServiceLabel, PRLabel, ServiceOwners, source path/owner line

            // Add AzureSdkOwners metadata (if present)
            if (!string.IsNullOrWhiteSpace(azureSdkOwners))
            {
                block.Add($"# AzureSdkOwners: {azureSdkOwners}");
            }

            // Add ServiceLabel metadata (if present)
            if (!string.IsNullOrWhiteSpace(serviceLabel))
            {
                block.Add($"# ServiceLabel: %{serviceLabel}");
            }

            // Add PRLabel metadata (only valid with source path/owner blocks)
            if (!string.IsNullOrWhiteSpace(prLabel) && hasSourcePathOwnerLine)
            {
                block.Add($"# PRLabel: %{prLabel}");
            }

            // Add ServiceOwners metadata (only for metadata-only blocks)
            if (!string.IsNullOrWhiteSpace(serviceOwners) && !hasSourcePathOwnerLine)
            {
                block.Add($"# ServiceOwners: {serviceOwners}");
            }

            // Add source path/owner line (if this is a source path/owner block)
            if (hasSourcePathOwnerLine)
            {
                var normalizedCodeOwners = codeOwners!.Select(owner => owner.StartsWith("@") ? owner : $"@{owner}");
                block.Add($"{repoPath.PadRight(30)} {string.Join(" ", normalizedCodeOwners)}");
            }

            return string.Join("\n", block);
        }

    }
}
