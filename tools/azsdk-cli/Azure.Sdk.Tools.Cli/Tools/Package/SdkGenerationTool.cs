using System.CommandLine;
using System.ComponentModel;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Tools.Core;

namespace Azure.Sdk.Tools.Cli.Tools.Package
{
    [McpServerToolType, Description("This type contains the tools to generate SDK code locally.")]
    public class SdkGenerationTool(
        IGitHelper gitHelper,
        ILogger<SdkGenerationTool> logger,
        ITspClientHelper tspClientHelper
    ) : MCPTool
    {
        public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.Package];

        // Command names
        private const string GenerateSdkCommandName = "generate";
        private const string GenerateSdkToolName = "azsdk_package_generate_code";

        // Generate command options
        private readonly Option<string> localSdkRepoPathOpt = new("--local-sdk-repo-path", "-r")
        {
            Description = "Absolute path to the local azure-sdk-for-{language} repository",
            Required = false,
        };

        private readonly Option<string> tspConfigPathOpt = new("--tsp-config-path", "-t")
        {
            Description = "Path to the 'tspconfig.yaml' configuration file, it can be a local path or remote HTTPS URL",
            Required = false,
        };

        private readonly Option<string> tspLocationPathOpt = new("--tsp-location-path", "-l")
        {
            Description = "Absolute path to the 'tsp-location.yaml' configuration file",
            Required = false,
        };

        private readonly Option<string> emitterOpt = new("--emitter-options", "-e")
        {
            Description = "Emitter options in key-value format. Example: 'package-version=1.0.0-beta.1'",
            Required = false,
        };

        protected override Command GetCommand() =>
            new McpCommand(GenerateSdkCommandName, "Generates SDK code for a specified language based on the provided 'tspconfig.yaml' or 'tsp-location.yaml'", GenerateSdkToolName)
            {
                localSdkRepoPathOpt, tspConfigPathOpt, tspLocationPathOpt, emitterOpt,
            };

        public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
        {
            var localSdkRepoPath = parseResult.GetValue(localSdkRepoPathOpt);
            var tspConfigPath = parseResult.GetValue(tspConfigPathOpt);
            var tspLocationPath = parseResult.GetValue(tspLocationPathOpt);
            var emitterOptions = parseResult.GetValue(emitterOpt);
            return await GenerateSdkAsync(localSdkRepoPath, tspConfigPath, tspLocationPath, emitterOptions, ct);
        }

        [McpServerTool(Name = GenerateSdkToolName), Description("Generates SDK code for a specified language using either 'tspconfig.yaml' or 'tsp-location.yaml'. Runs locally.")]
        public async Task<PackageOperationResponse> GenerateSdkAsync(
            [Description("Absolute path to the local Azure SDK repository. REQUIRED. Example: 'path/to/azure-sdk-for-net'. If not provided, the tool attempts to discover the repo from the current working directory.")]
            string localSdkRepoPath,
            [Description("Path to the 'tspconfig.yaml' file. Can be a local file path or a remote HTTPS URL. Optional if running inside a local cloned azure-sdk-for-{language} repository, for example, inside 'azure-sdk-for-net' repository.")]
            string? tspConfigPath,
            [Description("Path to 'tsp-location.yaml'. Optional. May be left empty if running inside a local cloned 'azure-rest-api-specs' repository.")]
            string? tspLocationPath,
            [Description("Emitter options in key-value format. Optional. Leave empty for defaults. Example: 'package-version=1.0.0-beta.1'.")]
            string? emitterOptions,
            CancellationToken ct = default)
        {
            try
            {
                // Validate inputs
                if (string.IsNullOrEmpty(tspConfigPath) && string.IsNullOrEmpty(tspLocationPath))
                {
                    return PackageOperationResponse.CreateFailure("Both 'tspconfig.yaml' and 'tsp-location.yaml' paths aren't provided. At least one of them is required.");
                }

                // Handle tsp-location.yaml case
                if (!string.IsNullOrEmpty(tspLocationPath))
                {
                    if (!tspLocationPath.EndsWith("tsp-location.yaml", StringComparison.OrdinalIgnoreCase))
                    {
                        return PackageOperationResponse.CreateFailure($"The specified 'tsp-location.yaml' path is invalid: {tspLocationPath}. It must be an absolute path to local 'tsp-location.yaml' file.");
                    }
                    
                    if (!File.Exists(tspLocationPath))
                    {
                        return PackageOperationResponse.CreateFailure($"The 'tsp-location.yaml' file does not exist at the specified path: {tspLocationPath}");
                    }
                    
                    var tspLocationDirectory = Path.GetDirectoryName(tspLocationPath);
                    string sdkRepoName = gitHelper.GetRepoName(tspLocationPath);
                    logger.LogInformation("SDK Repository Name: {SdkRepoName}", sdkRepoName);
                    
                    string typeSpecProjectPath = GetTypeSpecProjectPathFromTspLocation(tspLocationPath);
                    logger.LogInformation("TypeSpec Project Path from tsp-location.yaml: {TypeSpecProjectPath}", typeSpecProjectPath);

                    // Run tsp-client update using the existing tsp-location.yaml
                    var tspResult = await tspClientHelper.UpdateGenerationAsync(tspLocationPath, tspLocationDirectory, ct: ct);
                    
                    if (!tspResult.IsSuccessful)
                    {
                        return PackageOperationResponse.CreateFailure(tspResult.ResponseError, sdkRepoName: sdkRepoName, typespecProjectPath: typeSpecProjectPath);
                    }
                    
                    return PackageOperationResponse.CreateSuccess(
                        $"SDK re-generation completed successfully using tsp-location.yaml.",
                        nextSteps: ["If the SDK is not Python, build the code"],
                        sdkRepoName: sdkRepoName,
                        typespecProjectPath: typeSpecProjectPath
                    );
                }

                // Handle tspconfig.yaml case
                return await GenerateSdkFromTspConfigAsync(localSdkRepoPath, tspConfigPath, emitterOptions, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while generating SDK");
                return PackageOperationResponse.CreateFailure($"An error occurred: {ex.Message}");
            }
        }

        // Run language-specific script to generate the SDK code from 'tspconfig.yaml'
        private async Task<PackageOperationResponse> GenerateSdkFromTspConfigAsync(string localSdkRepoPath, string tspConfigPath, string emitterOptions, CancellationToken ct)
        {
            string specRepoFullName = string.Empty;

            // white spaces will be added by agent when it's a URL
            tspConfigPath = tspConfigPath.Trim();
            string typespecProjectPath = GetTypeSpecProjectRelativePath(tspConfigPath);

            // Validate inputs
            logger.LogInformation("Generating SDK at repo: {LocalSdkRepoPath}", localSdkRepoPath);
            if (string.IsNullOrEmpty(localSdkRepoPath) || !Directory.Exists(localSdkRepoPath))
            {
                return PackageOperationResponse.CreateFailure($"The directory for the local sdk repo does not provide or exist at the specified path: {localSdkRepoPath}. Prompt user to clone the matched SDK repository users want to generate SDK against.", typespecProjectPath: typespecProjectPath);
            }

            // Get the generate script path
            string sdkRepoRoot = gitHelper.DiscoverRepoRoot(localSdkRepoPath);
            if (string.IsNullOrEmpty(sdkRepoRoot))
            {
                return PackageOperationResponse.CreateFailure($"Failed to discover local sdk repo with path: {localSdkRepoPath}.", typespecProjectPath: typespecProjectPath);
            }

            string sdkRepoName = gitHelper.GetRepoName(sdkRepoRoot);
            if (!tspConfigPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                // Validate arguments for local tspconfig.yaml case
                if (!File.Exists(tspConfigPath))
                {
                    return PackageOperationResponse.CreateFailure($"The 'tspconfig.yaml' file does not exist at the specified path: {tspConfigPath}. Prompt user to clone the azure-rest-api-specs repository locally if it does not have a local copy.", sdkRepoName: sdkRepoName, typespecProjectPath: typespecProjectPath);
                }
                specRepoFullName = await gitHelper.GetRepoFullNameAsync(tspConfigPath, findUpstreamParent: false);
            }
            else
            {
                // specRepoFullName doesn't need to be set in this case
                logger.LogInformation("Remote 'tspconfig.yaml' URL detected: {TspConfigPath}.", tspConfigPath);
                if (!IsValidRemoteGitHubUrlWithCommit(tspConfigPath))
                {
                    return PackageOperationResponse.CreateFailure($"Invalid remote GitHub URL with commit: {tspConfigPath}. The URL must include a valid commit SHA. Example: https://github.com/Azure/azure-rest-api-specs/blob/dee71463cbde1d416c47cf544e34f7966a94ddcb/specification/contosowidgetmanager/Contoso.Management/tspconfig.yaml", sdkRepoName: sdkRepoName, typespecProjectPath: typespecProjectPath);
                }
            }

            // Build additional arguments for tsp-client init
            var additionalArgs = new List<string>();
            
            if (!string.IsNullOrEmpty(specRepoFullName))
            {
                additionalArgs.Add("--repo");
                additionalArgs.Add(specRepoFullName);
            }

            if (!string.IsNullOrEmpty(emitterOptions))
            {
                additionalArgs.Add("--emitter-options");
                additionalArgs.Add(emitterOptions);
            }

            // Use the helper to initialize generation
            var tspResult = await tspClientHelper.InitializeGenerationAsync(
                localSdkRepoPath, 
                tspConfigPath,
                additionalArgs.Count > 0 ? additionalArgs.ToArray() : null,
                ct);

            if (!tspResult.IsSuccessful)
            {
                return PackageOperationResponse.CreateFailure(tspResult.ResponseError, sdkRepoName: sdkRepoName, typespecProjectPath: typespecProjectPath);
            }

            return PackageOperationResponse.CreateSuccess(
                $"SDK generation completed successfully using tspconfig.yaml.",
                nextSteps: ["If the SDK is not Python, build the code"],
                sdkRepoName: sdkRepoName,
                typespecProjectPath: typespecProjectPath
            );
        }



        // Validate remote GitHub URL with commit SHA
        private bool IsValidRemoteGitHubUrlWithCommit(string tspConfigPath)
        {
            // Extract the part after /blob/ and before the next /
            var blobIndex = tspConfigPath.IndexOf("/blob/", StringComparison.OrdinalIgnoreCase);
            if (blobIndex == -1)
            {
                return false;
            }

            var afterBlob = tspConfigPath.Substring(blobIndex + 6);
            var nextSlashIndex = afterBlob.IndexOf('/');
            if (nextSlashIndex == -1)
            {
                return false;
            }

            var commitOrBranch = afterBlob.Substring(0, nextSlashIndex);

            // Validate that it's a 40-character commit SHA, not a branch name
            return IsValidSha(commitOrBranch);
        }

        // Validate commitSha: must be a 40-character hex string
        private bool IsValidSha(string sha)
        {
            if (!string.IsNullOrEmpty(sha))
            {
                var match = Regex.Match(sha, @"^[a-f0-9]{40}$", RegexOptions.IgnoreCase);
                return match.Success;
            }

            return false;
        }

        // Get the TypeSpec project relative path from tspConfigPath (local path or remote URL)
        // Returns the path starting from "specification/..." or empty string if not found
        private string GetTypeSpecProjectRelativePath(string tspConfigPath)
        {
            if (string.IsNullOrEmpty(tspConfigPath))
            {
                return string.Empty;
            }

            // Normalize path separators for cross-platform compatibility
            var normalizedPath = tspConfigPath.Replace("\\", "/");

            // Case-insensitive search for "specification" to handle different OS conventions
            int specIndex = normalizedPath.IndexOf("specification", StringComparison.OrdinalIgnoreCase);
            if (specIndex < 0)
            {
                return string.Empty;
            }

            var relativePath = normalizedPath[specIndex..];

            // Remove tspconfig.yaml filename if present to get the project directory path
            if (relativePath.EndsWith("/tspconfig.yaml", StringComparison.OrdinalIgnoreCase))
            {
                relativePath = relativePath[..^"/tspconfig.yaml".Length];
            }

            return relativePath;
        }

        // Get the 'directory' value from a tsp-location.yaml file
        // Returns the directory path or empty string if not found
        private string GetTypeSpecProjectPathFromTspLocation(string tspLocationPath)
        {
            try
            {
                var lines = File.ReadAllLines(tspLocationPath);
                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    
                    // Skip empty lines and comments
                    if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith('#'))
                    {
                        continue;
                    }
                    
                    if (trimmedLine.StartsWith("directory:", StringComparison.OrdinalIgnoreCase))
                    {
                        // Extract the value after "directory:" and trim quotes if present
                        var value = trimmedLine["directory:".Length..].Trim().Trim('"', '\'');
                        return value;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to read tsp-location.yaml file: {TspLocationPath}", tspLocationPath);
            }

            return string.Empty;
        }
    }
}
