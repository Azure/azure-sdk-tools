// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.InteropServices;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Services.SetupRequirements;

/// <summary>
/// Base class for Python-specific requirements that handles executable resolution.
/// </summary>
public abstract class PythonRequirementBase : Requirement
{
    private const string PythonRepoName = "azure-sdk-for-python";
    private const string VenvEnvironmentVariable = "AZSDKTOOLS_PYTHON_VENV_PATH";

    /// <summary>
    /// The raw check command before Python executable resolution.
    /// </summary>
    protected abstract string[] RawCheckCommand { get; }

    /// <summary>
    /// The pip install target for this requirement (e.g., "eng/tools/azure-sdk-tools[build]").
    /// Override in subclasses to specify the pip install package/path.
    /// </summary>
    protected abstract string PipInstallTarget { get; }

    /// <summary>
    /// Whether the PipInstallTarget is a path relative to the azure-sdk-for-python repo root.
    /// When true, the install will validate that the context repo is azure-sdk-for-python
    /// and resolve the target relative to that repo root.
    /// Override to return false for requirements that install from PyPI (e.g., "pytest").
    /// </summary>
    protected virtual bool IsRepoRelativeInstall => true;

    /// <summary>
    /// Resolves the Python executable path at runtime.
    /// </summary>
    public override string[] CheckCommand
    {
        get
        {
            var cmd = RawCheckCommand.ToArray();
            if (cmd.Length > 0) 
            {
                cmd[0] = PythonOptions.ResolvePythonExecutable(cmd[0]);
            }
            return cmd;
        }
    }

    public override bool ShouldCheck(RequirementContext ctx)
        => ctx.Languages.Contains(SdkLanguage.Python);

    public override bool IsAutoInstallable => true;

    /// <summary>
    /// Resolves the venv path and Python executable for installs.
    /// Resolution order:
    /// 1. AZSDKTOOLS_PYTHON_VENV_PATH environment variable
    /// 2. Existing .venv directory at the repo root
    /// 3. Create a new .venv at the repo root
    /// </summary>
    private async Task<(string? pythonExe, RequirementCheckOutput? error)> ResolveVenvPythonAsync(
        Func<string[], Task<ProcessResult>> runCommand,
        RequirementContext ctx)
    {
        var binDir = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Scripts" : "bin";
        var exeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "python.exe" : "python";

        // 1. Check AZSDKTOOLS_PYTHON_VENV_PATH environment variable first
        var envVenvPath = Environment.GetEnvironmentVariable(VenvEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(envVenvPath))
        {
            if (!Directory.Exists(envVenvPath))
            {
                return (null, new RequirementCheckOutput
                {
                    Success = false,
                    Error = $"Python venv path specified in {VenvEnvironmentVariable} does not exist: {envVenvPath}"
                });
            }

            var envPythonExe = Path.Combine(envVenvPath, binDir, exeName);
            if (!File.Exists(envPythonExe))
            {
                return (null, new RequirementCheckOutput
                {
                    Success = false,
                    Error = $"Python executable not found in venv specified by {VenvEnvironmentVariable}: {envPythonExe}"
                });
            }

            return (envPythonExe, null);
        }

        // 2. Check for existing .venv at repo root
        var repoVenvPath = Path.Combine(ctx.RepoRoot, ".venv");
        var repoVenvPythonExe = Path.Combine(repoVenvPath, binDir, exeName);
        if (Directory.Exists(repoVenvPath) && File.Exists(repoVenvPythonExe))
        {
            return (repoVenvPythonExe, null);
        }

        // 3. Create a new .venv at repo root
        var createResult = await runCommand(["python", "-m", "venv", repoVenvPath]);
        if (createResult.ExitCode != 0)
        {
            return (null, new RequirementCheckOutput
            {
                Success = false,
                Output = createResult.Output?.Trim(),
                Error = $"Failed to create venv at {repoVenvPath}: {createResult.Output?.Trim()}"
            });
        }

        return (repoVenvPythonExe, null);
    }

    /// <summary>
    /// Handles venv resolution and pip install for Python tool requirements.
    /// 1. Resolves the venv Python executable (env var → existing .venv → create new).
    /// 2. Resolves the pip install target (validating azure-sdk-for-python repo for relative paths).
    /// 3. Runs pip install for the requirement's PipInstallTarget.
    /// </summary>
    public override async Task<RequirementCheckOutput> RunInstallAsync(
        Func<string[], Task<ProcessResult>> runCommand,
        RequirementContext ctx,
        CancellationToken ct = default)
    {
        // Resolve venv Python executable
        var (pythonExe, venvError) = await ResolveVenvPythonAsync(runCommand, ctx);
        if (venvError != null)
        {
            return venvError;
        }

        // Resolve pip install target
        var installTarget = PipInstallTarget;
        if (IsRepoRelativeInstall)
        {
            // Repo-relative installs require the azure-sdk-for-python repo
            if (!ctx.RepoName.Equals(PythonRepoName, StringComparison.OrdinalIgnoreCase))
            {
                return new RequirementCheckOutput
                {
                    Success = false,
                    Error = $"Cannot install '{Name}': the install target '{installTarget}' is relative to the " +
                            $"{PythonRepoName} repo, but the current repo is '{ctx.RepoName}'. " +
                            $"Please run this from the root of the {PythonRepoName} repository."
                };
            }
            installTarget = Path.Combine(ctx.RepoRoot, installTarget);
        }

        var installResult = await runCommand([pythonExe!, "-m", "pip", "install", installTarget]);

        return new RequirementCheckOutput
        {
            Success = installResult.ExitCode == 0,
            Output = installResult.ExitCode == 0 ? $"{Name} installed successfully" : installResult.Output?.Trim(),
            Error = installResult.ExitCode != 0 ? installResult.Output?.Trim() : null
        };
    }
}

/// <summary>
/// Requirements specific to Python SDK development.
/// </summary>
public static class PythonRequirements
{
    public static IReadOnlyList<Requirement> All => [
        new AzPySdkRequirement(),
        new PythonGeneratorRequirement(),
        new GhToolsRequirement(),
        new PytestRequirement()
    ];

    // Python language requirement is in CoreRequirements.cs

    public class AzPySdkRequirement : PythonRequirementBase
    {
        public override string Name => "azpysdk";
        protected override string[] RawCheckCommand => ["azpysdk", "--help"];
        protected override string PipInstallTarget => "eng/tools/azure-sdk-tools[build]";

        public override string? Reason => "Required for validating Python SDKs";

        public override IReadOnlyList<string> GetInstructions(RequirementContext ctx)
        {
            return [
                "Navigate to the Python SDK repository root directory",
                "Ensure your virtual environment is activated",
                "python -m pip install eng/tools/azure-sdk-tools[build]"];
        }
    }

    public class PythonGeneratorRequirement : PythonRequirementBase
    {
        public override string Name => "sdk_generator";
        protected override string[] RawCheckCommand => ["sdk_generator", "--help"];
        protected override string PipInstallTarget => "eng/tools/azure-sdk-tools[sdk_generator]";

        public override IReadOnlyList<string> GetInstructions(RequirementContext ctx)
        {
            return [
                "Navigate to the Python SDK repository root directory",
                "Ensure your virtual environment is activated",
                "python -m pip install eng/tools/azure-sdk-tools[sdk_generator]"];
        }
    }

    public class GhToolsRequirement : PythonRequirementBase
    {
        public override string Name => "ghtools";
        protected override string[] RawCheckCommand => ["python", "-m", "pip", "show", "GitPython"];
        protected override string PipInstallTarget => "eng/tools/azure-sdk-tools[ghtools]";

        public override IReadOnlyList<string> GetInstructions(RequirementContext ctx)
        {
            return [
                "Navigate to the Python SDK repository root directory",
                "Ensure your virtual environment is activated",
                "python -m pip install eng/tools/azure-sdk-tools[ghtools]"];
        }
    }

    public class PytestRequirement : PythonRequirementBase
    {
        public override string Name => "pytest";
        public override string? MinVersion => "8.3.5";
        protected override string[] RawCheckCommand => ["pytest", "--version"];
        protected override string PipInstallTarget => "pytest";
        protected override bool IsRepoRelativeInstall => false;

        public override IReadOnlyList<string> GetInstructions(RequirementContext ctx)
        {
            return [
                "Navigate to the Python SDK repository root directory",
                "Ensure your virtual environment is activated",
                "python -m pip install pytest",
            ];
        }
    }
}
