// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services.SetupRequirements;

/// <summary>
/// Core requirements needed for all SDK repos regardless of language.
/// </summary>
public static class CoreRequirements
{
    public static IReadOnlyList<Requirement> All => [
        new NodeRequirement(),
        new TspClientRequirement(),
        new TspRequirement(),
        new PowerShellRequirement(),
        new GitHubCliRequirement(),
        new LongPathsRequirement(),
        new PythonRequirement(),
        new PipRequirement()
    ];

    public class NodeRequirement : Requirement
    {
        public override string Name => "Node.js";
        public override string? MinVersion => "22.16.0";
        public override string[] CheckCommand => ["node", "--version"];
        public override string? NotAutoInstallableReason => NotInstallableReasons.LanguageRuntime;

        public override IReadOnlyList<string> GetInstructions(RequirementContext ctx)
        {
            if (ctx.IsLinux) 
            {
                return ["sudo apt install nodejs"];
            }
            return ["Download Node.js from https://nodejs.org"];            
        }
    }

    public class TspClientRequirement : Requirement
    {
        public override string Name => "tsp-client";
        public override string? MinVersion => "0.24.0";
        public override IReadOnlyList<string> DependsOn => ["Node.js"];
        public override bool IsAutoInstallable => true;

        public override string[][]? GetInstallCommands(RequirementContext ctx)
        {
            var workingDir = ctx.IsSpecsRepo()
                ? ctx.RepoRoot
                : Path.Combine(ctx.RepoRoot, "eng", "common", "tsp-client");

            return [["npm", "ci", "--prefix", workingDir]];
        }

        public override async Task<RequirementCheckOutput> RunCheck(
            IProcessHelper processHelper,
            RequirementContext ctx,
            CancellationToken ct = default)
        {
            // Use npm exec with --prefix to run the locally installed tsp-client
            var tspClientPath = ctx.IsSpecsRepo()
                ? ctx.RepoRoot
                : Path.Combine(ctx.RepoRoot, "eng", "common", "tsp-client");

            var command = new[] { "npm", "exec", "--prefix", tspClientPath, "--no", "--", "tsp-client", "--version" };

            var result = await RunCommand(processHelper, command, ctx, ct);
            return new RequirementCheckOutput
            {
                Success = result.ExitCode == 0,
                Output = result.Output?.Trim(),
                Error = result.ExitCode != 0 ? result.Output?.Trim() : null
            };
        }
    }

    public class TspRequirement : Requirement
    {
        public override string Name => "tsp";
        public override string? MinVersion => "1.0.0";
        public override string[] CheckCommand => ["tsp", "--version"];
        public override IReadOnlyList<string> DependsOn => ["Node.js"];
        public override bool IsAutoInstallable => true;

        public override string[][]? GetInstallCommands(RequirementContext ctx)
        {
            if (ctx.IsSpecsRepo())
            {
                return [["npm", "ci", "--prefix", ctx.RepoRoot]];
            }
            return [["npm", "install", "-g", "@typespec/compiler@latest"]];
        }

        public override async Task<RequirementCheckOutput> RunCheck(
            IProcessHelper processHelper,
            RequirementContext ctx,
            CancellationToken ct = default)
        {
            string[] command;

            if (ctx.IsSpecsRepo())
            {
                // Use npm exec with --prefix to run the locally installed tsp
                command = ["npm", "exec", "--prefix", ctx.RepoRoot, "--no", "--", "tsp", "--version"];
            }
            else
            {
                // Non-specs repos install globally, so use tsp directly
                command = CheckCommand;
            }

            var result = await RunCommand(processHelper, command, ctx, ct);
            return new RequirementCheckOutput
            {
                Success = result.ExitCode == 0,
                Output = result.Output?.Trim(),
                Error = result.ExitCode != 0 ? result.Output?.Trim() : null
            };
        }
    }

    public class PowerShellRequirement : Requirement
    {
        public override string Name => "PowerShell";
        public override string? MinVersion => "7.0";
        public override string[] CheckCommand => ["pwsh", "--version"];
        public override string? NotAutoInstallableReason => NotInstallableReasons.SystemTool;

        public override IReadOnlyList<string> GetInstructions(RequirementContext ctx)
        {
            return ["Download and install https://learn.microsoft.com/powershell/scripting/install/install-powershell?view=powershell-7.5"];
        }
    }

    public class GitHubCliRequirement : Requirement
    {
        public override string Name => "GitHub CLI";
        public override string? MinVersion => "2.30.0";
        public override string[] CheckCommand => ["gh", "--version"];
        public override string? NotAutoInstallableReason => NotInstallableReasons.SystemTool;

        public override IReadOnlyList<string> GetInstructions(RequirementContext ctx)
        {
            return ["Download and install from https://cli.github.com/"];
        }
    }

    public class LongPathsRequirement : Requirement
    {
        public override string Name => "Git long paths";
        public override bool IsAutoInstallable => true;

        public override string[][]? GetInstallCommands(RequirementContext ctx)
            => [["git", "config", "--global", "core.longpaths", "true"]];

        public override bool ShouldCheck(RequirementContext ctx)
        {
            return ctx.IsWindows;
        }

        public override async Task<RequirementCheckOutput> RunCheck(
            IProcessHelper processHelper,
            RequirementContext ctx,
            CancellationToken ct = default)
        {
            var result = await RunCommand(processHelper, ["git", "config", "--get", "core.longpaths"], ctx, ct);
            

            bool isEnabled = result.ExitCode == 0 && 
                             result.Output?.Trim().Equals("true", StringComparison.OrdinalIgnoreCase) == true;
            
            return new RequirementCheckOutput
            {
                Success = isEnabled,
                Output = isEnabled ? "Git long paths enabled" : null,
                Error = isEnabled ? null : "Git long paths not enabled, core.longpaths is not set to true"
            };
        }

        public override IReadOnlyList<string> GetInstructions(RequirementContext ctx)
        {
            return ["Check the Registry Editor to ensure that LongPathsEnabled exists and is set to 1 in `HKEY_LOCAL_MACHINE\\SYSTEM\\CurrentControlSet\\Control\\FileSystem`.", 
            "git config --global core.longpaths true"];
        }
    }

    public class PythonRequirement : Requirement
    {
        public override string Name => "Python";
        public override string[] CheckCommand => ["python", "--version"];

        public override string? MinVersion => "3.9";

        public override string? Reason => "Python is required for all repos because it's used in a common Verify-Readme Powershell script.";
        public override string? NotAutoInstallableReason => NotInstallableReasons.LanguageRuntime;

        public override IReadOnlyList<string> GetInstructions(RequirementContext ctx)
        {
            if (ctx.IsLinux) 
            {
                return [
                    "sudo apt install python3 python3-pip python3-venv",
                    "sudo apt install python-is-python3"
                ];
            }
            return ["Download from https://www.python.org/downloads/"];
        }
    }

    public class PipRequirement : Requirement
    {
        public override string Name => "pip";
        public override string[] CheckCommand => ["python", "-m", "pip", "--version"];
        public override IReadOnlyList<string> DependsOn => ["Python"];

        public override string? Reason => "Pip is required for all repos because it's used in a common Verify-Readme Powershell script.";
        public override bool IsAutoInstallable => false;

        public override string? NotAutoInstallableReason => NotInstallableReasons.BundledWithLanguage;

        public override IReadOnlyList<string> GetInstructions(RequirementContext ctx)
        {
            if (ctx.IsLinux)
            {
                return ["sudo apt install python3-pip"];
            }
            return ["python -m ensurepip --upgrade"];
        }
    }
}
