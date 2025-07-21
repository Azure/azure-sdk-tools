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
        private const string validateServiceLabelCommandName = "validate-service-label";
        private const string isValidServiceCodeOwnersCommandName = "is-valid-service-code-owners";
        private const string isValidCodeOwnerCommandName = "is-valid-code-owner";

        // Command options
        private readonly Option<string> serviceNameOpt = new(["--service", "-s"], "Service name to find and validate the service label for") { IsRequired = true };
        private readonly Option<string> serviceLabelOpt = new(["--service", "-s"], "Confirmed service label to validate code owners for") { IsRequired = true };
        private readonly Option<string> gitHubAliasOpt = new(["--username", "-u"], "GitHub alias to validate") { IsRequired = false };

        public override Command GetCommand()
        {
            var command = new Command("common-validation-tools", "Tools for validating CODEOWNERS files.");
            var subCommands = new[]
            {
                new Command(validateServiceLabelCommandName, "Validate the service label for a given service name against the common-labels CSV") { serviceNameOpt },
                new Command(isValidServiceCodeOwnersCommandName, "Validate code owners for a service across all Azure SDK repositories") { serviceLabelOpt },
                new Command(isValidCodeOwnerCommandName, "Validate if a GitHub alias has proper organizational membership and write access") { gitHubAliasOpt },
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
                case validateServiceLabelCommandName:
                    var serviceName = commandParser.GetValueForOption(serviceNameOpt);
                    var serviceLabelResult = await ValidateServiceLabel(serviceName ?? "");
                    output.Output($"Service label validation result: {serviceLabelResult}");
                    return;
                case isValidServiceCodeOwnersCommandName:
                    var confirmedServiceLabel = commandParser.GetValueForOption(serviceLabelOpt);
                    var serviceValidationResult = await ValidateServiceCodeOwners(confirmedServiceLabel ?? "");
                    output.Output($"Service code owners validation result: {serviceValidationResult}");
                    return;
                case isValidCodeOwnerCommandName:
                    var gitHubAlias = commandParser.GetValueForOption(gitHubAliasOpt);
                    var aliasValidationResult = await isValidCodeOwner(gitHubAlias ?? "");
                    output.Output($"GitHub alias validation result: {aliasValidationResult}");
                    return;
                default:
                    SetFailure();
                    output.OutputError($"Unknown command: '{command}'");
                    return;
            }
        }

        [McpServerTool(Name = "ValidateServiceLabel"), Description("Validates the service label for a given service name against the common labels CSV.")]
        public async Task<string> ValidateServiceLabel(string serviceName)
        {
            try
            {
                var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
                var githubLabelsLogger = loggerFactory.CreateLogger<GitHubLabelsTool>();
                
                var githubLabelsTool = new GitHubLabelsTool(githubLabelsLogger, output, githubService);
                
                var result = await githubLabelsTool.CheckServiceLabel(serviceName);
                    
                if (result.Found)
                {
                    logger.LogInformation($"Service label '{result.ServiceLabel}' is valid (Color: {result.ColorCode})");
                    return result.ServiceLabel;
                }
                else
                {
                    logger.LogWarning($"Service label '{serviceName}' not found in common-labels.csv");
                    return $"Service label '{serviceName}' not found in common-labels.csv";
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Failed to validate service label for: {serviceName}");
                return $"Error: Failed to validate service label. {ex.Message}";
            }
        }

        [McpServerTool(Name = "ValidateServiceCodeOwners"), Description("Validates code owners for a service across all Azure SDK repositories.")]
        public async Task<string> ValidateServiceCodeOwners(string confirmedServiceLabel)
        {
            try
            {
                //validate the servicelabel

                var repositoryTasks = azureRepositories.Select(repo => 
                    ProcessRepositoryForService(repo.Key, repo.Value, confirmedServiceLabel)).ToArray();
                
                var results = await Task.WhenAll(repositoryTasks);

                var summary = GenerateServiceValidationSummary(results.ToList(), confirmedServiceLabel);
                return System.Text.Json.JsonSerializer.Serialize(summary, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            }
            catch (Exception ex)
            {
                var errorResponse = new GenericResponse()
                {
                    Status = "Failed",
                    Details = { $"Failed to validate service code owners. Error: {ex.Message}" }
                };
                return output.Format(errorResponse);
            }
        }

        private async Task<ServiceCodeOwnerResult> ProcessRepositoryForService(string repoType, string repoName, string serviceLabel)
        {
            var result = new ServiceCodeOwnerResult
            {
                Repository = repoName,
                RepositoryType = repoType,
                CodeOwners = new List<CodeOwnerValidationResult>(),
                Status = "Processing"
            };

            try
            {
                var codeownersUrl = $"https://raw.githubusercontent.com/Azure/{repoName}/main/.github/CODEOWNERS";

                var codeownersEntries = CodeownersParser.ParseCodeownersFile(codeownersUrl, "https://azuresdkartifacts.blob.core.windows.net/azure-sdk-write-teams/azure-sdk-write-teams-blob");
                var matchingEntry = FindServiceEntries(codeownersEntries, serviceLabel);

                if (matchingEntry != null)
                {
                    var allOwners = new List<string>();
                    if (matchingEntry.SourceOwners?.Any() == true) allOwners.AddRange(matchingEntry.SourceOwners);
                    if (matchingEntry.ServiceOwners?.Any() == true) allOwners.AddRange(matchingEntry.ServiceOwners);
                    if (matchingEntry.AzureSdkOwners?.Any() == true) allOwners.AddRange(matchingEntry.AzureSdkOwners);

                    var uniqueOwners = allOwners.Where(o => !string.IsNullOrEmpty(o)).Distinct().ToList();
                    
                    result.CodeOwners = await ValidateCodeOwnersConcurrently(uniqueOwners);

                    result.Status = "Success";
                    result.Message = $"Found 1 matching entry with {result.CodeOwners.Count} code owners";
                }
                else
                {
                    result.Status = "Service not found";
                    result.Message = $"Service label '{serviceLabel}' not found in {repoName}";
                }
            }
            catch (Exception ex)
            {
                result.Status = "Error";
                result.Message = $"Error processing {repoName}: {ex.Message}";
                logger.LogError(ex, "Error processing repository {repo}", repoName);
            }

            return result;
        }

        private CodeownersEntry? FindServiceEntries(IList<CodeownersEntry> entries, string serviceName)
        {
            CodeownersEntry? matchingEntry = null;

            foreach (var entry in entries)
            {
                // Check if service matches by ServiceLabels
                if (entry.ServiceLabels?.Any(label => label.Contains(serviceName, StringComparison.OrdinalIgnoreCase)) == true)
                {
                    matchingEntry = entry;
                    break;
                }

                // Check if service matches by PRLabels  
                if (entry.PRLabels?.Any(label => label.Contains(serviceName, StringComparison.OrdinalIgnoreCase)) == true)
                {
                    matchingEntry = entry;
                    break;
                }

                // Check if service matches by PathExpression
                if (entry.PathExpression?.Contains(serviceName, StringComparison.OrdinalIgnoreCase) == true)
                {
                    matchingEntry = entry;
                    break;
                }

                // Check if service matches by any owner team names
                var allOwners = new List<string>();
                if (entry.SourceOwners?.Any() == true) allOwners.AddRange(entry.SourceOwners);
                if (entry.ServiceOwners?.Any() == true) allOwners.AddRange(entry.ServiceOwners);
                if (entry.AzureSdkOwners?.Any() == true) allOwners.AddRange(entry.AzureSdkOwners);

                if (allOwners.Any(owner => owner.Contains(serviceName, StringComparison.OrdinalIgnoreCase)))
                {
                    matchingEntry = entry;
                    break;
                }
            }

            return matchingEntry;
        }

        private async Task<List<CodeOwnerValidationResult>> ValidateCodeOwnersConcurrently(List<string> owners)
        {
            var validationTasks = new List<Task<CodeOwnerValidationResult>>();
            
            foreach (var owner in owners)
            {
                var username = owner.TrimStart('@');
                
                if (codeOwnerValidationCache.ContainsKey(username))
                {
                    validationTasks.Add(Task.FromResult(codeOwnerValidationCache[username]));
                }
                else
                {
                    validationTasks.Add(ValidateCodeOwnerWithCaching(username));
                }
            }
            
            // Wait for all validations to complete
            var results = await Task.WhenAll(validationTasks);
            return results.ToList();
        }

        private async Task<CodeOwnerValidationResult> ValidateCodeOwnerWithCaching(string username)
        {
            var result = await ValidateCodeOwner(username);
            codeOwnerValidationCache.TryAdd(username, result);
            
            return result;
        }

        private async Task<CodeOwnerValidationResult> ValidateCodeOwner(string username)
        {
            var validationResult = new CodeOwnerValidationResult
            {
                Username = username,
                Status = "Processing"
            };

            try
            {
                // Call PowerShell script directly
                var powerShellOutput = await CallPowerShellScriptViaCMD($"-UserName \"{username}\"");
                
                if (string.IsNullOrEmpty(powerShellOutput))
                {
                    validationResult.Status = "Error";
                    validationResult.Message = "No output received from PowerShell script";
                    return validationResult;
                }

                // Parse the PowerShell output
                var requiredOrganizations = new Dictionary<string, bool>
                {
                    { "azure", false },
                    { "microsoft", false }
                };
                
                var organizations = ExtractOrganizations(powerShellOutput);
                foreach (var organization in organizations)
                {
                    var organizationLower = organization.ToLower();
                    if (requiredOrganizations.ContainsKey(organizationLower))
                    {
                        requiredOrganizations[organizationLower] = true;
                      }
                }

                validationResult.Status = "Success";
                validationResult.Organizations = requiredOrganizations;
                validationResult.HasWritePermission = ExtractWritePermission(powerShellOutput);
                validationResult.IsValidCodeOwner = ExtractCodeOwnerValidity(powerShellOutput);
            }
            catch (Exception ex)
            {
                validationResult.Status = "Error";
                validationResult.Message = $"Error validating {username}: {ex.Message}";
                logger.LogError(ex, "Error validating code owner {username}", username);
            }

            return validationResult;
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
                    logger.LogError($"Failed to start PowerShell process.");
                    return null;
                }
                var processOutput = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                return processOutput;
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }

        private List<string> ExtractOrganizations(string output)
        {
            var organizations = new List<string>();
            var lines = output.Split('\n');
            bool inOrgSection = false;

            foreach (var line in lines)
            {
                var cleanLine = line.Trim().Replace("\r", "");

                if (cleanLine.StartsWith("Required Org")) // Organization is spelled wrong in Validate-AzsdkCodeOwner.ps1 script.
                {
                    inOrgSection = true;
                    continue;
                }

                if (cleanLine.StartsWith("Required Permissions") || cleanLine.StartsWith("Validation result"))
                {
                    inOrgSection = false;
                    continue;
                }

                if (inOrgSection && !string.IsNullOrWhiteSpace(cleanLine))
                {
                    var orgName = cleanLine.Replace("✓", "").Replace("✗", "").Replace("\uFFFD", "").Trim();
                    if (!string.IsNullOrEmpty(orgName))
                    {
                        organizations.Add(orgName);
                    }
                }
            }

            return organizations;
        }

        private bool ExtractWritePermission(string output)
        {
            var lines = output.Split('\n');
            foreach (var line in lines)
            {
                var cleanLine = line.Trim().Replace("\r", "");
                if (cleanLine.Contains("write") && (cleanLine.Contains("✓") || cleanLine.Contains("\uFFFD")))
                {
                    return true;
                }
            }
            return false;
        }

        private bool ExtractCodeOwnerValidity(string output)
        {
            return output.Contains("Valid code owner");
        }

        private ServiceValidationSummary GenerateServiceValidationSummary(List<ServiceCodeOwnerResult> results, string serviceLabel)
        {
            var summary = new ServiceValidationSummary
            {
                ServiceLabel = serviceLabel,
                TotalRepositories = results.Count,
                RepositoriesWithService = results.Count(r => r.Status == "Success"),
                RepositoriesWithoutService = results.Count(r => r.Status == "Service not found"),
                RepositoriesWithErrors = results.Count(r => r.Status == "Error"),
                Results = results,
                AllCodeOwners = results.SelectMany(r => r.CodeOwners)
                    .GroupBy(co => co.Username)
                    .Select(g => g.First())
                    .ToList()
            };

            // Generate summary statistics
            summary.ValidCodeOwners = summary.AllCodeOwners.Count(co => co.IsValidCodeOwner);
            summary.InvalidCodeOwners = summary.AllCodeOwners.Count(co => !co.IsValidCodeOwner);
            summary.CodeOwnersWithErrors = summary.AllCodeOwners.Count(co => co.Status == "Error");

            return summary;
        }       // Data models
        public class ServiceCodeOwnerResult
        {
            public string Repository { get; set; } = "";
            public string RepositoryType { get; set; } = "";
            public string Status { get; set; } = "";
            public string Message { get; set; } = "";
            public List<CodeOwnerValidationResult> CodeOwners { get; set; } = new();
        }

        public class CodeOwnerValidationResult
        {
            public string Username { get; set; } = "";
            public string Status { get; set; } = "";
            public string Message { get; set; } = "";
            public bool IsValidCodeOwner { get; set; }
            public bool HasWritePermission { get; set; }
            public Dictionary<string, bool> Organizations { get; set; } = new();
        }

        public class ServiceValidationSummary
        {
            public string ServiceLabel { get; set; } = "";
            public int TotalRepositories { get; set; }
            public int RepositoriesWithService { get; set; }
            public int RepositoriesWithoutService { get; set; }
            public int RepositoriesWithErrors { get; set; }
            public int ValidCodeOwners { get; set; }
            public int InvalidCodeOwners { get; set; }
            public int CodeOwnersWithErrors { get; set; }
            public List<ServiceCodeOwnerResult> Results { get; set; } = new();
            public List<CodeOwnerValidationResult> AllCodeOwners { get; set; } = new();
        }
    }
}
