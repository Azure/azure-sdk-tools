using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models.Responses;
using Azure.Sdk.Tools.Cli.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.TeamFoundation.Common;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools
{
    [McpServerToolType, Description("This type contains the tools to generate SDK code and build SDK code.")]
    public class SpecGenSdkTools(ILogger<SpecGenSdkTools> logger, IOutputService output) : MCPTool
    {
        private readonly string commandName = "generate-sdk";
        private readonly Option<string> localSpecRepoPathOpt = new(["--local-spec-repo-path"], "Local azure-rest-api-specs repository path") { IsRequired = true };
        private readonly Option<string> localSdkRepoPathOpt = new(["--local-sdk-repo-path"], "Local SDK repository path") { IsRequired = true };
        private readonly Option<string> tspConfigPathOpt = new(["--tsp-config-path"], "Path to the TypeSpec config file for the SDK generation") { IsRequired = true };
        private readonly Option<string> specCommitShaOpt = new(["--spec-commit-sha"], "Spec commit SHA") { IsRequired = true };
        private readonly Option<string> specRepoUrlOpt = new(["--spec-repo-url"], "Spec repository URL") { IsRequired = false };
        private readonly Option<string> specApiVersionOpt = new(["--spec-api-version"], "The version of the API specification to generate SDK for") { IsRequired = true };
        private readonly Option<string> sdkReleaseTypeOpt = new(["--sdk-release-type"], "The release type of SDK to generate") { IsRequired = true };
        public static readonly string[] ValidLanguages = { ".NET", "Go", "Java", "JavaScript", "Python"};

        public override Command GetCommand()
        {
            var command = new Command(commandName, "Tools to generate SDK code and build SDK code.") { localSpecRepoPathOpt, localSdkRepoPathOpt, tspConfigPathOpt, specCommitShaOpt, specRepoUrlOpt, specApiVersionOpt, sdkReleaseTypeOpt };
            command.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });
            return command;
        }

        public async override Task HandleCommand(InvocationContext ctx, CancellationToken ct)
        {
            var localSpecRepoPath = ctx.ParseResult.GetValueForOption(localSpecRepoPathOpt);
            var localSdkRepoPath = ctx.ParseResult.GetValueForOption(localSdkRepoPathOpt);
            var tspConfigPath = ctx.ParseResult.GetValueForOption(tspConfigPathOpt);
            var specCommitSha = ctx.ParseResult.GetValueForOption(specCommitShaOpt);
            var specRepoUrl = ctx.ParseResult.GetValueForOption(specRepoUrlOpt);
            var result = await GenerateSdkLocallyAsync(localSpecRepoPath, localSdkRepoPath, tspConfigPath, specCommitSha, specRepoUrl);
            output.Output(result);
        }

        [McpServerTool(Name = "GenerateSdkLocally"), Description("Generates a complete SDK package from API specifications locally. Performs code generation, compilation, packaging, and API view creation for supported languages (.NET, Go, Java, JavaScript, Python).")]
        public async Task<string> GenerateSdkLocallyAsync(string localSpecRepoPath, string localSdkRepoPath, string tspConfigPath, string specCommitSha, string specRepoUrl = "https://github.com/Azure/azure-rest-api-specs")
        {
            try
            {
                logger.LogInformation($"Local spec repo root path: {localSpecRepoPath}");
                if (string.IsNullOrEmpty(localSpecRepoPath) || !Directory.Exists(localSpecRepoPath))
                {
                    return "Invalid local spec repo path. Please provide a valid local spec repo path.";
                }
                logger.LogInformation($"Local SDK repo root path: {localSdkRepoPath}");
                if (string.IsNullOrEmpty(localSdkRepoPath) || !Directory.Exists(localSdkRepoPath))
                {
                    return "Invalid local SDK repo path. Please provide a valid local SDK repo path.";
                }

                logger.LogInformation($"tspconfig.yaml file path: {tspConfigPath}");
                if (string.IsNullOrEmpty(tspConfigPath) || !File.Exists(tspConfigPath))
                {
                    return $"Invalid tspconfig path {tspConfigPath}. Please provide a valid TypeSpec file path.";
                }
                logger.LogInformation($"Spec commit SHA: {specCommitSha}");
                if (string.IsNullOrEmpty(specCommitSha))
                {
                    return "Invalid spec commit SHA. Please provide a valid spec commit SHA.";
                }
                logger.LogInformation($"Spec repo URL: {specRepoUrl}");
                
                if (!IsSpecGenSdkInstalled(localSdkRepoPath))
                {
                    logger.LogInformation("SpecGenSdk is not installed. Running npm ci...");
                    try
                    {
                        await RunNpmCiAsync(localSdkRepoPath);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to run npm ci.");
                        SetFailure();
                        return "Failed to run npm ci. Please ensure Node.js and npm are installed and configured correctly.";
                    }
                    logger.LogInformation("npm ci completed.");
                }

                var sdkRepoName = Path.GetFileName(localSdkRepoPath);
                var workingDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "tmp-spec-gen-sdk");
                var executionReportJson = string.Empty;
                logger.LogInformation("Running spec-gen-sdk...");
                try 
                {
                    Directory.CreateDirectory(workingDirectory);
                    await RunSpecGenSdkAsync(localSpecRepoPath, localSdkRepoPath, specCommitSha, specRepoUrl, tspConfigPath, workingDirectory);
                    var jsonFilePath = Path.Combine(workingDirectory, $"{sdkRepoName}_tmp", "execution-report.json");
                    if (File.Exists(jsonFilePath))
                    {
                        var rawJson = await File.ReadAllTextAsync(jsonFilePath);
                        try
                        {
                            var jsonDocument = JsonDocument.Parse(rawJson);
                            executionReportJson = JsonSerializer.Serialize(jsonDocument, new JsonSerializerOptions { WriteIndented = true });
                        }
                        catch (JsonException)
                        {
                            // If JSON parsing fails, use the raw content
                            executionReportJson = rawJson;
                        }
                        logger.LogInformation($"SpecGenSdk command completed successfully. Execution report JSON content: {executionReportJson}");
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to run spec-gen-sdk.");
                    SetFailure();
                    return "Failed to run spec-gen-sdk. Please ensure the tool is installed and configured correctly.";
                }
                
                return $"SpecGenSdk command completed successfully. Execution report JSON content is: {executionReportJson}";
            }
            catch (Exception ex)
            {
                SetFailure(1);
                return $"An error occurred: {ex.Message}";
            }
        }

        private bool IsSpecGenSdkInstalled(string repoRoot)
        {
            var specGenSdkExecutable = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "spec-gen-sdk.cmd" : "spec-gen-sdk";
            return File.Exists(Path.Combine(repoRoot, "node_modules", ".bin", specGenSdkExecutable));
        }

        public static async Task RunNpmCiAsync(string repoRoot)
        {
            var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            if (isWindows)
            {
                await RunProcessAsync("cmd.exe", "/C npm ci", repoRoot);
            }
            else
            {
                await RunProcessAsync("npm", "ci", repoRoot);
            }
        }

        private static async Task<string> RunSpecGenSdkAsync(string localSpecRepoPath, string localSdkRepoPath, string specCommitSha, string specRepoUrl, string tspConfigPath, string workingDirectory)
        {
            var arguments = new StringBuilder();
            arguments.Append($"--scp {localSpecRepoPath} --sdp {localSdkRepoPath} --spec-repo-url {specRepoUrl} --spec-commit-sha {specCommitSha} --tsp-config-path {tspConfigPath} --run-mode agent");
            var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            string output;
            if (isWindows)
            {
                output = await RunProcessAsync("cmd.exe", $"/C npx spec-gen-sdk {arguments}", workingDirectory);
            }
            else
            {
                output = await RunProcessAsync("npx", $"spec-gen-sdk {arguments}", workingDirectory);
            }
            return output;
        }

        private static async Task<string> RunProcessAsync(string command, string args, string workingDirectory)
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory
            };
            var output = new StringBuilder();
            using (var process = new Process())
            {
                process.StartInfo = processInfo;
                process.OutputDataReceived += (sender, args) =>
                {
                    if (args.Data != null)
                    {
                        output.AppendLine(args.Data);
                    }
                };

                process.ErrorDataReceived += (sender, args) =>
                {
                    if (args.Data != null)
                    {
                        output.AppendLine(args.Data);
                    }
                };
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                await process.WaitForExitAsync();
                if (process.ExitCode != 0)
                {
                    output.Append($"{Environment.NewLine} {command} {args} failed!");
                }
            }
            return output.ToString();
        }
    }
}
