// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Primitives;
using ModelContextProtocol;
using ModelContextProtocol.Protocol.Messages;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;

namespace AzureSDKDevToolsMCP.Tools
{
    /// <summary>
    /// This tool is used to validate the TypeSpec specification for Azure SDK service.
    /// </summary>
    [Description("TypeSpec validation tools")]
    [McpServerToolType]
    public class SpecValidationTools
    {
        static readonly string TSPCONFIG_FILENAME = "tspconfig.yaml";

        /// <summary>
        /// Validates the TypeSpec API specification.
        /// </summary>
        /// <param name="typeSpecProjectRootPath">The root path of the TypeSpec project.</param>
        /// <param name="githubRepoRoot">GitHub repo root path of cloned repo.</param>
        [McpServerTool, Description("Run TypeSpec validation. Validates TypeSpec specification for a TypeSpec project.")]
        public static async Task<IList<string>> RunTypeSpecValidation(
            IMcpServer server,
            RequestContext<CallToolRequestParams> context,
            string typeSpecProjectRootPath)
        {
            var validationResults = new List<string>();
            if (!IsValidTypeSpecProjectPath(typeSpecProjectRootPath))
            {
                validationResults.Add($"TypeSpec project is not found in {typeSpecProjectRootPath}. TypeSpec MCP tools can only be used for TypeSpec based spec projects.");
                return validationResults;
            }

            var progressToken = context.Params?.Meta?.ProgressToken;
            var specRepoRootPath = GetGitRepoRootPath(typeSpecProjectRootPath);

            try
            {
                // Run npm ci
                await SendNotificationAsync(server, progressToken, "Running npm ci...");
                RunNpmCi(specRepoRootPath);
                await SendNotificationAsync(server, progressToken, "Completed npm ci...");

                //Run TypeSpec validation
                await SendNotificationAsync(server, progressToken, $"Running TypeSpec validation for {typeSpecProjectRootPath}...");
                ValidateTypeSpec(typeSpecProjectRootPath, specRepoRootPath, validationResults);
                await SendNotificationAsync(server, progressToken, "Completed TypeSpec validation...");
            }
            catch (Exception ex)
            {
                validationResults.Add($"Error: {ex.Message}");
            }
            return validationResults;
        }

        public static void RunNpmCi(string repoRoot)
        {
            var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            if (isWindows)
            {
                _ = RunProcess("cmd.exe", "/C npm ci", repoRoot);
            }
            else
            {
                _ = RunProcess("npm", "ci", repoRoot);
            }
        }

        private static string ValidateTypeSpec(string typeSpecProjectRootPath, string specRepoRootPath, IList<string> validationResults)
        {
            var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            if (isWindows)
            {
                var output = RunProcess("cmd.exe", $"/C npx tsv {typeSpecProjectRootPath}", specRepoRootPath);
                validationResults.Add(output);
            }
            else
            {
                var output = RunProcess("npx", $"tsv {typeSpecProjectRootPath}", specRepoRootPath);
                validationResults.Add(output);
            }

            return "TypeSpec validation completed successfully";
        }

        private static bool IsValidTypeSpecProjectPath(string typeSpecProjectRootPath)
        {
            if (string.IsNullOrEmpty(typeSpecProjectRootPath))
            {
                throw new ArgumentException("TypeSpec project root path cannot be null or empty.", nameof(typeSpecProjectRootPath));
            }

            // Check if the path is a valid TypeSpec project path
            if(!Directory.Exists(typeSpecProjectRootPath) || !File.Exists(Path.Combine(typeSpecProjectRootPath, TSPCONFIG_FILENAME)))
            {
                return false;
            }
            return true;
        }

        private static async Task SendNotificationAsync(
            IMcpServer server,
            ProgressToken? progressToken,
            string message)
        {
            if (progressToken is not null)
            {
                // Send progress notification
                await server.SendNotificationAsync("notification/progress", new
                {
                    Token = progressToken,
                    Message = message
                });
            }
        }        

        private static string GetGitRepoRootPath(string typeSpecProjectRootPath)
        {
            var currentDirectory = new DirectoryInfo(typeSpecProjectRootPath);
            while (currentDirectory != null && !currentDirectory.Name.Equals("specification"))
            {
                currentDirectory = currentDirectory.Parent;
            }
            return currentDirectory?.Parent?.FullName ?? string.Empty;
        }

        private static string RunProcess(string command, string args, string workingDirectory)
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
            using var process = Process.Start(processInfo) ?? throw new Exception($"Failed to start the process: {args}");
            Console.WriteLine($"Started process to run {args}");
            StringBuilder output = new ();
            while (!process.HasExited)
            {
                Console.WriteLine($"Process is running {args}");
                Thread.Sleep(2000);
                process.Refresh();
                output.Append(process.StandardOutput.ReadToEnd());                
            }
            if (process.ExitCode != 0)
            {
                throw new Exception(process.StandardError.ReadToEnd());
            }
            return output.ToString();
        }
    }
}