using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using AzureSDKDevToolsMCP.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Services.CircuitBreaker;
using ModelContextProtocol.Server;

namespace AzureSDKDSpecTools.Tools
{
    [Description("This type contains tools to run various common tasks in specs repo")]
    [McpServerToolType]
    public class SpecCommonTools(IGitHelper _gitHelper, ILogger<SpecCommonTools> _logger)
    {
        private IGitHelper gitHelper = _gitHelper;
        private ILogger<SpecCommonTools> logger = _logger;

        static readonly string GET_CHANGED_TYPESPEC_PROJECT_SCRIPT = "eng/scripts/Get-TypeSpec-Folders.ps1";

        [McpServerTool, Description("This tool returns list of TypeSpec projects modified in current branch")]
        public List<string> GetModifiedTypeSpecProjects(string repoRootPath, string targetBranch = "main")
        {
            var baseCommitSha = gitHelper.GetMergeBaseCommitSha(repoRootPath, targetBranch);
            if (string.IsNullOrEmpty(baseCommitSha))
            {
                return [$"Failed to get merge base commit SHA for {repoRootPath}"];
            }

            var scriptPath = Path.Combine(repoRootPath, GET_CHANGED_TYPESPEC_PROJECT_SCRIPT);
            if (!File.Exists(scriptPath))
            {
                return [$"[{scriptPath}] path is not present"];
            }

            logger.LogInformation($"Getting changed files in current branch with diff against commit SHA {baseCommitSha}");
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "pwsh",
                    Arguments = $"{scriptPath}  -BaseCommitish {baseCommitSha} -IgnoreCoreFiles",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = repoRootPath
                };
                using var process = Process.Start(processInfo) ?? throw new Exception($"Failed to start the process: git diff {baseCommitSha}  --name-only");
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    return [$"Failed to execute 'pwsh {scriptPath}  -BaseCommitish {baseCommitSha} -IgnoreCoreFiles' to get modified TypeSpec projects. Please make sure you have PowerShell core is installed. Error {process.StandardError.ReadToEnd()}"];
                }
                var output = process.StandardOutput.ReadToEnd();
                return output.Split(Environment.NewLine).Where(o => o.StartsWith("specification")).ToList();
            }
            catch (Exception ex)
            {
                return [$"Failed to execute 'pwsh {scriptPath}  -BaseCommitish {baseCommitSha} -IgnoreCoreFiles' to get modified TypeSpec projects. Please make sure you have PowerShell core is installed. Error {ex.Message}"];
            }            
        }
    }
}
