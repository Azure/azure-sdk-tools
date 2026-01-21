// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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

        public override IReadOnlyList<string> GetInstructions(RequirementContext ctx)
        {
            if (ctx.IsLinux) {
                return ["sudo apt install nodejs"];
            }
            return ["Download Node.js from https://nodejs.org", "Or run: winget install OpenJS.NodeJS"];            
        }
    }

    public class TspClientRequirement : Requirement
    {
        public override string Name => "tsp-client";
        public override string? MinVersion => "0.24.0";
        public override string[] CheckCommand => ["tsp-client", "--version"];

        public override IReadOnlyList<string> GetInstructions(RequirementContext ctx)
        {
            if (ctx.RepoName != null && ctx.RepoName.Equals("azure-rest-api-specs", StringComparison.OrdinalIgnoreCase))
            {
                return [
                    $"cd {ctx.RepoRoot}",
                    "npm ci"
                ];
            }
            return ["cd eng/common/tsp-client", "npm ci"];
        }
    }

    public class TspRequirement : Requirement
    {
        public override string Name => "tsp";
        public override string? MinVersion => "1.0.0";
        public override string[] CheckCommand => ["tsp", "--version"];

        public override IReadOnlyList<string> GetInstructions(RequirementContext ctx)
        {
            if (ctx.RepoName != null && ctx.RepoName.Equals("azure-rest-api-specs", StringComparison.OrdinalIgnoreCase))
            {
                return [
                    $"cd {ctx.RepoRoot}",
                    "npm ci"
                ];
            }
            return ["npm install -g @typespec/compiler@latest"];
        }
    }

    public class PowerShellRequirement : Requirement
    {
        public override string Name => "PowerShell";
        public override string? MinVersion => "7.0";
        public override string[] CheckCommand => ["pwsh", "--version"];

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

        public override IReadOnlyList<string> GetInstructions(RequirementContext ctx)
        {
            return ["Download and install from https://cli.github.com/"];
        }
    }

    public class LongPathsRequirement : Requirement
    {
        public override string Name => "Git long paths";
        public override string[] CheckCommand => ["pwsh", "-Command", "if ($IsWindows -and (git config --get core.longpaths) -ne 'true') { exit 1 }"];

        public override bool ShouldCheck(RequirementContext ctx)
        {
            return ctx.IsWindows;
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

        public override string MinVersion => "3.9";

        public override string? Reason => "Python is required for all repos because it's used in a common Verify-Readme Powershell script.";

        public override IReadOnlyList<string> GetInstructions(RequirementContext ctx)
        {
            if (ctx.IsLinux) {
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

        public override string? Reason => "Pip is required for all repos because it's used in a common Verify-Readme Powershell script.";

        public override IReadOnlyList<string> GetInstructions(RequirementContext ctx)
        {
            if (ctx.IsLinux)
            {
                return ["sudo apt install python3-pip", "Or run: python -m ensurepip"];
            }
            return ["python -m ensurepip"];
        }
    }
}
