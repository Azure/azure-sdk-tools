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

namespace Azure.Sdk.Tools.Cli.Tools
{
    [Description("This type contains test MCP tools for validation and testing purposes.")]
    [McpServerToolType]
    public class TestTools(IGitHubService githubService,
        IDevOpsService devopsService,
        IGitHelper gitHelper,
        ITypeSpecHelper typespecHelper,
        IOutputService output,
        ILogger<TestTools> logger) : MCPTool
    {
    private static readonly Dictionary<string, string> azureRepositories = new Dictionary<string, string>
        {
            { "dotnet", "azure-sdk-for-net" },
            { "cpp", "azure-sdk-for-cpp" },
            /*{ "go", "azure-sdk-for-go" },
            { "java", "azure-sdk-for-java" },
            { "javascript", "azure-sdk-for-js" },
            { "typescript", "azure-sdk-for-js" },
            { "python", "azure-sdk-for-python" },
            { "rest-api-specs", "azure-rest-api-specs" },
            { "rust", "azure-sdk-for-rust" }*/
        };

        // Command names
        private const string isValidCodeOwnerCommandName = "is-valid-code-owner";
        private const string validateServiceLabelCommandName = "validate-service-label";
        private const string isValidServiceCodeOwnersCommandName = "is-valid-service-code-owners"; // Proabably change to something more general.

        // Command options
        private readonly Option<string> gitHubAliasOpt = new(["--username", "-u"], "GitHub alias to validate") { IsRequired = false };
        private readonly Option<string> serviceNameOpt = new(["--service", "-s"], "Service name to find and validate the service label for") { IsRequired = true };
        private readonly Option<string> serviceLabelOpt = new(["--serviceLabel", "-sl"], "Confirmed service label to validate code owners for") { IsRequired = true };

        public override Command GetCommand()
        {
            var command = new Command("test-tools", "Test tools for validation and testing purposes.");
            var subCommands = new[]
            {
                new Command(isValidCodeOwnerCommandName, "Validate if a GitHub alias has proper organizational membership and write access") { gitHubAliasOpt },
                new Command(validateServiceLabelCommandName, "Validate the service label for a given service name against the common labels CSV") { serviceNameOpt },
                new Command(isValidServiceCodeOwnersCommandName, "Validate code owners for a service across all Azure SDK repositories") { serviceLabelOpt }
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
                // Process dotnet first to find the service label
                var codeownersUrl = $"https://raw.githubusercontent.com/Azure/{azureRepositories["dotnet"]}/main/.github/CODEOWNERS";
                var codeownersEntries = CodeownersParser.ParseCodeownersFile(codeownersUrl);

                var matchingEntry = FindServiceEntries(codeownersEntries, serviceName); // Does this need to be async?
                var confirmedServiceLabels = matchingEntry?.ServiceLabels != null ? string.Join(",", matchingEntry.ServiceLabels) : null;
                if (confirmedServiceLabels == null)
                {
                    return $"Service '{serviceName}' not found in the dotnet repository.";
                }
                return confirmedServiceLabels;
            }
            catch (Exception ex)
            {
                return $"Failed to validate service label. Error: {ex.Message}";
            }
        }

        [McpServerTool(Name = "ValidateServiceCodeOwners"), Description("Validates code owners for a service across all Azure SDK repositories.")]
        public async Task<string> ValidateServiceCodeOwners(string confirmedServiceLabels)
        {
            try
            {
                // Parse and validate service labels once at the top level
                var serviceLabels = confirmedServiceLabels.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
                var invalidLabels = await ValidateServiceLabels(serviceLabels);
                var validLabels = serviceLabels.Except(invalidLabels).ToList();

                var results = new List<ServiceCodeOwnerResult>();

                // Process each repository
                foreach (var repo in azureRepositories)
                {
                    var repoResult = await ProcessRepositoryForService(repo.Key, repo.Value, serviceLabels);
                    results.Add(repoResult);
                }

                // Generate summary
                var summary = GenerateServiceValidationSummary(results, confirmedServiceLabels, validLabels, invalidLabels);
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

        private async Task<ServiceCodeOwnerResult> ProcessRepositoryForService(string repoType, string repoName, List<string> serviceLabels)
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
                // Get CODEOWNERS file
                var codeownersUrl = $"https://raw.githubusercontent.com/Azure/{repoName}/main/.github/CODEOWNERS";
                var codeownersEntries = CodeownersParser.ParseCodeownersFile(codeownersUrl);

                // Find entries matching any of the service labels
                var matchingEntries = new List<CodeownersEntry>();
                foreach (var serviceLabel in serviceLabels)
                {
                    var matchingEntry = FindServiceEntries(codeownersEntries, serviceLabel);
                    if (matchingEntry != null && !matchingEntries.Contains(matchingEntry))
                    {
                        matchingEntries.Add(matchingEntry);
                    }
                }

                if (matchingEntries.Any())
                {
                    // Extract and validate code owners from all matching entries
                    var allOwners = new List<string>();
                    foreach (var entry in matchingEntries)
                    {
                        if (entry.SourceOwners?.Any() == true) allOwners.AddRange(entry.SourceOwners);
                        if (entry.ServiceOwners?.Any() == true) allOwners.AddRange(entry.ServiceOwners);
                        if (entry.AzureSdkOwners?.Any() == true) allOwners.AddRange(entry.AzureSdkOwners);
                    }

                    // Remove duplicates and validate each owner
                    var uniqueOwners = allOwners.Where(o => !string.IsNullOrEmpty(o)).Distinct().ToList();
                    foreach (var owner in uniqueOwners)
                    {
                        logger.LogInformation($"Validating code owner: {owner}");
                        var username = owner.TrimStart('@');
                        var ownerValidationResult = await ValidateCodeOwner(username);
                        result.CodeOwners.Add(ownerValidationResult);
                    }

                    result.Status = "Success";
                    result.Message = $"Found {matchingEntries.Count} matching entries with {result.CodeOwners.Count} code owners";
                }
                else
                {
                    result.Status = "Service not found";
                    result.Message = $"Service labels '{string.Join(",", serviceLabels)}' not found in {repoName}";
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

        private async Task<List<string>> ValidateServiceLabels(List<string> serviceLabels)
        {
            var invalidLabels = new List<string>();
            
            // Create a logger factory to create the correct logger type
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var githubLabelsLogger = loggerFactory.CreateLogger<GitHubLabelsTool>();
            
            var githubLabelsTool = new GitHubLabelsTool(githubLabelsLogger, output, githubService);
            
            foreach (var serviceLabel in serviceLabels)
            {
                try
                {
                    var result = await githubLabelsTool.CheckServiceLabel(serviceLabel);
                    
                    if (!result.Found)
                    {
                        invalidLabels.Add(serviceLabel);
                        logger.LogWarning($"Invalid service label found: {serviceLabel}");
                        
                        // Mock: Simulate creating a new label
                        await CreateServiceLabel(serviceLabel);
                    }
                    else
                    {
                        logger.LogInformation($"Service label '{serviceLabel}' is valid (Color: {result.ColorCode})");
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError($"Error validating service label '{serviceLabel}': {ex.Message}");
                    invalidLabels.Add(serviceLabel);
                }
            }
            return invalidLabels;
        }

        private async Task CreateServiceLabel(string label)
        {
            // Mock implementation for creating a new service label
            logger.LogInformation($"[MOCK] Creating new service label: {label}");
            
            // Simulate some async work
            await Task.Delay(100);
            
            logger.LogInformation($"[MOCK] Service label '{label}' created successfully");
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
                var gitHubAliasValidationJson = await isValidCodeOwner(username);
                var parsedValidation = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(gitHubAliasValidationJson);
                
                validationResult.Status = "Success";
                validationResult.IsValidCodeOwner = parsedValidation.GetProperty("IsValidCodeOwner").GetBoolean();
                validationResult.HasWritePermission = parsedValidation.GetProperty("WritePermission").GetBoolean();
                
                // Parse organizations
                if (parsedValidation.GetProperty("Organizations").ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    var organizationsProperty = parsedValidation.GetProperty("Organizations");
                    validationResult.Organizations = new Dictionary<string, bool>();
                    
                    foreach (var organizationProperty in organizationsProperty.EnumerateObject())
                    {
                        validationResult.Organizations[organizationProperty.Name] = organizationProperty.Value.GetBoolean();
                    }
                }
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
                //await test();
                //Gets the current users github username if not provided
                var user = await githubService.GetGitUserDetailsAsync();
                var userDetails = string.IsNullOrEmpty(githubAlias) ? user?.Login : githubAlias;
                var validation = await CallPowerShellScriptViaCMD($"-UserName \"{userDetails}\"");
                var parsedResult = ParseCodeOwnerValidation(validation);
                return parsedResult;
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
                    Arguments = $"-ExecutionPolicy Bypass -File \"C:\\Code\\azure-sdk-tools\\tools\\github\\scripts\\Validate-AzsdkCodeOwner.ps1\" {arguments}",
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

        private string ParseCodeOwnerValidation(string powerShellOutput)
        {
            if (string.IsNullOrEmpty(powerShellOutput))
            {
                return "No output received";
            }

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

            var result = new
            {
                Organizations = requiredOrganizations,
                WritePermission = ExtractWritePermission(powerShellOutput),
                IsValidCodeOwner = ExtractCodeOwnerValidity(powerShellOutput)
            };

            return System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }

        private List<string> ExtractOrganizations(string output)
        {
            var organizations = new List<string>();
            var lines = output.Split('\n');
            bool inOrgSection = false;

            foreach (var line in lines)
            {
                var cleanLine = line.Trim().Replace("\r", "");

                if (cleanLine.StartsWith("Required Org")) // Organization is spelled wrong in script
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

        private ServiceValidationSummary GenerateServiceValidationSummary(List<ServiceCodeOwnerResult> results, string serviceName, List<string> validLabels, List<string> invalidLabels)
        {
            var summary = new ServiceValidationSummary
            {
                ServiceName = serviceName,
                ValidServiceLabels = validLabels,
                InvalidServiceLabels = invalidLabels,
                TotalRepositories = results.Count,
                RepositoriesWithService = results.Count(r => r.Status == "Success"),
                RepositoriesWithoutService = results.Count(r => r.Status == "Service not found"),
                RepositoriesWithErrors = results.Count(r => r.Status == "Error"),
                Results = results,
                AllCodeOwners = results.SelectMany(r => r.CodeOwners).ToList()
            };

            // Generate summary statistics
            summary.ValidCodeOwners = summary.AllCodeOwners.Count(co => co.IsValidCodeOwner);
            summary.InvalidCodeOwners = summary.AllCodeOwners.Count(co => !co.IsValidCodeOwner);
            summary.CodeOwnersWithErrors = summary.AllCodeOwners.Count(co => co.Status == "Error");

            return summary;
        }

        // Data models
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
            public string ServiceName { get; set; } = "";
            public List<string> ValidServiceLabels { get; set; } = new();
            public List<string> InvalidServiceLabels { get; set; } = new();
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

        private async Task test()
        {
            var result = await githubService.GetContentsAsync("Azure", "azure-rest-api-specs", "eng");
            
            if (result != null)
            {
                logger.LogInformation($"Found {result.Count} items in the eng folder:");

                foreach (var item in result)
                {
                    logger.LogInformation($"- {item.Type}: {item.Name} (Path: {item.Path})");
                    logger.LogInformation(item.Content); // STRINGSSSSSSSS
                }
            }
            else
            {
                logger.LogInformation("No items found or result is null");
            }
        }
    }
}
