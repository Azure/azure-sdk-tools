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
        // Commands
        private const string validateCodeOwnerCommandName = "validate-code-owner";
        private const string validateServiceCodeOwnersCommandName = "validate-service-code-owners";

        // Options
        private readonly Option<string> userNameOpt = new(["--username", "-u"], "GitHub username to validate") { IsRequired = false };
        private readonly Option<string> serviceNameOpt = new(["--service", "-s"], "Service name to validate code owners for") { IsRequired = true };

        public override Command GetCommand()
        {
            var command = new Command("test-tools", "Test tools for validation and testing purposes.");
            var subCommands = new[]
            {
                new Command(validateCodeOwnerCommandName, "Validate if a user is a GitHub code owner") { userNameOpt },
                new Command(validateServiceCodeOwnersCommandName, "Validate code owners for a service across all Azure SDK repositories") { serviceNameOpt }
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
                case validateCodeOwnerCommandName:
                    var userName = commandParser.GetValueForOption(userNameOpt);
                    var validationResult = await ValidateGithubCodeOwner(userName ?? "");
                    output.Output($"GitHub code owner validation result: {validationResult}");
                    return;
                case validateServiceCodeOwnersCommandName:
                    var serviceName = commandParser.GetValueForOption(serviceNameOpt);
                    var serviceValidationResult = await ValidateServiceCodeOwners(serviceName ?? "");
                    output.Output($"Service code owners validation result: {serviceValidationResult}");
                    return;
                default:
                    SetFailure();
                    output.OutputError($"Unknown command: '{command}'");
                    return;
            }
        }

        [McpServerTool(Name = "ValidateServiceCodeOwners"), Description("Validates code owners for a service across all Azure SDK repositories.")]
        public async Task<string> ValidateServiceCodeOwners(string serviceName)
        {
            try
            {
                var results = new List<ServiceCodeOwnerResult>();
                
                // Repository mapping
                var azureRepositories = new Dictionary<string, string>
                {
                    /*{ "cpp", "azure-sdk-for-cpp" },
                    { "go", "azure-sdk-for-go" },
                    { "java", "azure-sdk-for-java" },
                    { "javascript", "azure-sdk-for-js" },
                    { "typescript", "azure-sdk-for-js" },
                    { "dotnet", "azure-sdk-for-net" },
                    { "python", "azure-sdk-for-python" },
                    { "rest-api-specs", "azure-rest-api-specs" },*/
                    { "rust", "azure-sdk-for-rust" }
                };
                
                // Process each repository
                foreach (var repo in azureRepositories)
                {
                    var repoResult = await ProcessRepositoryForService(repo.Key, repo.Value, serviceName);
                    results.Add(repoResult);
                }

                // Generate summary
                var summary = GenerateServiceValidationSummary(results, serviceName);
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

        private async Task<ServiceCodeOwnerResult> ProcessRepositoryForService(string repoType, string repoName, string serviceName)
        {
            var result = new ServiceCodeOwnerResult
            {
                Repository = repoName,
                RepositoryType = repoType,
                ServiceName = serviceName,
                CodeOwners = new List<CodeOwnerValidationResult>(),
                ServiceLabels = new List<string>(),
                InvalidLabels = new List<string>(),
                Status = "Processing"
            };

            try
            {
                // Get CODEOWNERS file
                var codeownersUrl = $"https://raw.githubusercontent.com/Azure/{repoName}/main/.github/CODEOWNERS";
                var codeownersEntries = CodeownersParser.ParseCodeownersFile(codeownersUrl);

                // Find entries matching the service
                var matchingEntry = FindServiceEntries(codeownersEntries, serviceName);

                if (matchingEntry == null)
                {
                    result.Status = "Service not found";
                    result.Message = $"Service '{serviceName}' not found in {repoName}";
                    return result;
                }

                // Process each matching entry
                if (matchingEntry != null)
                {
                    // Extract service labels
                    if (matchingEntry.ServiceLabels?.Any() == true)
                    {
                        result.ServiceLabels.AddRange(matchingEntry.ServiceLabels);

                        // Validate service labels
                        var invalidLabels = await ValidateServiceLabels(matchingEntry.ServiceLabels);
                        result.InvalidLabels.AddRange(invalidLabels);
                    }

                    // Extract and validate code owners
                    var allOwners = new List<string>();
                    if (matchingEntry.SourceOwners?.Any() == true) allOwners.AddRange(matchingEntry.SourceOwners);
                    if (matchingEntry.ServiceOwners?.Any() == true) allOwners.AddRange(matchingEntry.ServiceOwners);
                    if (matchingEntry.AzureSdkOwners?.Any() == true) allOwners.AddRange(matchingEntry.AzureSdkOwners);

                    // Remove duplicates and validate each owner
                    var uniqueOwners = allOwners.Where(o => !string.IsNullOrEmpty(o)).Distinct().ToList();
                    foreach (var owner in uniqueOwners)
                    {
                        logger.LogInformation($"Validating code owner: {owner}");
                        var username = owner.TrimStart('@');
                        var ownerValidation = await ValidateCodeOwner(username);
                        result.CodeOwners.Add(ownerValidation);
                    }
                }

                result.Status = "Success";
                result.Message = $"Found {matchingEntry} matching entries with {result.CodeOwners.Count} code owners";
            }
            catch (Exception ex)
            {
                result.Status = "Error";
                result.Message = $"Error processing {repoName}: {ex.Message}";
                logger.LogError(ex, "Error processing repository {repo}", repoName);
            }

            return result;
        }

        private CodeownersEntry FindServiceEntries(IList<CodeownersEntry> entries, string serviceName)
        {
            CodeownersEntry matchingEntry = null;

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

        private async Task<List<string>> ValidateServiceLabels(string serviceLabel)
        {
            bool isValidLabel = false;

            // Mock implementation for validating against common-labels.csv
            GithubLabelsTool.CheckServiceLabel(serviceLabel);
            var validLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Azure.AI", "Azure.Storage", "Azure.KeyVault", "Azure.Identity", "Azure.Core",
                "Azure.Data", "Azure.Messaging", "Azure.Security", "Azure.Monitor"
            };

            if (!validLabels.Contains(serviceLabel))
            {
                isInvalidLabel = true;
                logger.LogWarning($"Invalid service label found: {serviceLabel}");

                // Mock: Simulate creating a new label
                await CreateServiceLabel(serviceLabel);
            }

            return isInvalidLabel;
            {
                if (!validLabels.Contains(label))
                {
                    invalidLabels.Add(label);
                    logger.LogWarning($"Invalid service label found: {label}");
                    
                    // Mock: Simulate creating a new label
                    await CreateServiceLabel(label);
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
            var result = new CodeOwnerValidationResult
            {
                Username = username,
                Status = "Processing"
            };

            try
            {
                var validationJson = await ValidateGithubCodeOwner(username);
                var validation = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(validationJson);
                
                result.Status = "Success";
                result.IsValidCodeOwner = validation.GetProperty("IsValidCodeOwner").GetBoolean();
                result.HasWritePermission = validation.GetProperty("WritePermission").GetBoolean();
                
                // Parse organizations
                if (validation.GetProperty("Organizations").ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    var orgs = validation.GetProperty("Organizations");
                    result.Organizations = new Dictionary<string, bool>();
                    
                    foreach (var org in orgs.EnumerateObject())
                    {
                        result.Organizations[org.Name] = org.Value.GetBoolean();
                    }
                }
            }
            catch (Exception ex)
            {
                result.Status = "Error";
                result.Message = $"Error validating {username}: {ex.Message}";
                logger.LogError(ex, "Error validating code owner {username}", username);
            }

            return result;
        }

        [McpServerTool(Name = "ValidateGithubCodeOwner"), Description("Validates if the user is a code owner for the TypeSpec project in the public Azure/azure-rest-api-specs repository.")]
        public async Task<string> ValidateGithubCodeOwner(string githubAlias = "")
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

        private ServiceValidationSummary GenerateServiceValidationSummary(List<ServiceCodeOwnerResult> results, string serviceName)
        {
            var summary = new ServiceValidationSummary
            {
                ServiceName = serviceName,
                TotalRepositories = results.Count,
                RepositoriesWithService = results.Count(r => r.Status == "Success"),
                RepositoriesWithoutService = results.Count(r => r.Status == "Service not found"),
                RepositoriesWithErrors = results.Count(r => r.Status == "Error"),
                Results = results,
                AllCodeOwners = results.SelectMany(r => r.CodeOwners).ToList(),
                AllServiceLabels = results.SelectMany(r => r.ServiceLabels).Distinct().ToList(),
                AllInvalidLabels = results.SelectMany(r => r.InvalidLabels).Distinct().ToList()
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
            public string ServiceName { get; set; } = "";
            public string Status { get; set; } = "";
            public string Message { get; set; } = "";
            public List<CodeOwnerValidationResult> CodeOwners { get; set; } = new();
            public List<string> ServiceLabels { get; set; } = new();
            public List<string> InvalidLabels { get; set; } = new();
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
            public int TotalRepositories { get; set; }
            public int RepositoriesWithService { get; set; }
            public int RepositoriesWithoutService { get; set; }
            public int RepositoriesWithErrors { get; set; }
            public int ValidCodeOwners { get; set; }
            public int InvalidCodeOwners { get; set; }
            public int CodeOwnersWithErrors { get; set; }
            public List<ServiceCodeOwnerResult> Results { get; set; } = new();
            public List<CodeOwnerValidationResult> AllCodeOwners { get; set; } = new();
            public List<string> AllServiceLabels { get; set; } = new();
            public List<string> AllInvalidLabels { get; set; } = new();
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
