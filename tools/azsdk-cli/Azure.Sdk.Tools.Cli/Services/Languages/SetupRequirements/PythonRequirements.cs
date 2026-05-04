// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Services.SetupRequirements;

/// <summary>
/// Base class for Python-specific requirements that handles executable resolution.
/// </summary>
public abstract class PythonRequirementBase : Requirement
{
    private const string PythonRepoName = "azure-sdk-for-python";
    private const string PythonPrRepoName = "azure-sdk-for-python-pr";

    public override IReadOnlyList<string> DependsOn => ["Python", "pip"];

    /// <summary>
    /// The raw check command before Python executable resolution.
    /// </summary>
    protected abstract string[] RawCheckCommand { get; }

    /// <summary>
    /// The pip install target for this requirement (e.g., "eng/tools/azure-sdk-tools").
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

    public override bool ShouldCheck(RequirementContext ctx)
        => ctx.Languages.Contains(SdkLanguage.Python);

    public override bool IsAutoInstallable => true;

    /// <summary>
    /// Resolves an executable path by checking for a venv in order:
    /// 1. AZSDKTOOLS_PYTHON_VENV_PATH environment variable (via PythonOptions)
    /// 2. Existing .venv directory at the given repo root
    /// Returns the original executable name if no venv is found.
    /// </summary>
    private static string ResolveVenvExecutable(string executableName, string repoRoot)
    {
        // 1. Try env var via PythonOptions
        try
        {
            var resolved = PythonOptions.ResolvePythonExecutable(executableName);
            if (!string.Equals(resolved, executableName, StringComparison.Ordinal))
            {
                // Verify the resolved path actually exists before returning;
                // fall through to .venv fallback if it doesn't.
                if (File.Exists(resolved))
                {
                    return resolved;
                }
            }
        }
        catch (DirectoryNotFoundException)
        {
            // Env var set but path invalid — fall through to .venv fallback
        }

        // 2. Check for existing .venv at repo root
        var repoVenvPath = Path.Combine(repoRoot, ".venv");
        if (Directory.Exists(repoVenvPath))
        {
            var candidate = PythonOptions.ResolveFromVenvPath(repoVenvPath, executableName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return executableName;
    }

    /// <summary>
    /// Overrides the base check to resolve executables from venv before running.
    /// Uses the same resolution order as install: env var → .venv at repo root.
    /// This ensures checks succeed after auto-install creates a .venv.
    /// </summary>
    public override async Task<RequirementCheckOutput> RunCheck(
        IProcessHelper processHelper,
        RequirementContext ctx,
        CancellationToken ct = default)
    {
        var cmd = RawCheckCommand.ToArray();
        if (cmd.Length > 0)
        {
            cmd[0] = ResolveVenvExecutable(cmd[0], ctx.RepoRoot);
        }

        var result = await RunCommand(processHelper, cmd, ctx, ct);
        return new RequirementCheckOutput
        {
            Success = result.ExitCode == 0,
            Output = result.Output?.Trim(),
            Error = result.ExitCode != 0 ? result.Output?.Trim() : null
        };
    }

    /// <summary>
    /// Resolves the venv path and Python executable for installs.
    /// Resolution order:
    /// 1. AZSDKTOOLS_PYTHON_VENV_PATH environment variable (via PythonOptions)
    /// 2. Existing .venv directory at the repo root
    /// 3. Create a new .venv at the repo root
    /// </summary>
    private async Task<(string? pythonExe, RequirementCheckOutput? error)> ResolveVenvPythonAsync(
        IProcessHelper processHelper,
        RequirementContext ctx,
        CancellationToken ct)
    {
        // 1. Check AZSDKTOOLS_PYTHON_VENV_PATH environment variable
        const string pythonName = "python";
        string resolved;
        try
        {
            resolved = PythonOptions.ResolvePythonExecutable(pythonName);
        }
        catch (DirectoryNotFoundException ex)
        {
            return (null, new RequirementCheckOutput
            {
                Success = false,
                Error = $"{ex.Message} Please update or unset the AZSDKTOOLS_PYTHON_VENV_PATH environment variable to point to a valid venv directory."
            });
        }
        if (!string.Equals(resolved, pythonName, StringComparison.Ordinal))
        {
            // PythonOptions resolved to a venv path from the environment variable
            if (!File.Exists(resolved))
            {
                return (null, new RequirementCheckOutput
                {
                    Success = false,
                    Error = $"Python executable resolved from AZSDKTOOLS_PYTHON_VENV_PATH does not exist: {resolved}"
                });
            }
            return (resolved, null);
        }

        // 2. Check for existing .venv at repo root
        var repoVenvPath = Path.Combine(ctx.RepoRoot, ".venv");
        var repoVenvPythonExe = PythonOptions.ResolveFromVenvPath(repoVenvPath, pythonName);
        if (Directory.Exists(repoVenvPath) && File.Exists(repoVenvPythonExe))
        {
            return (repoVenvPythonExe, null);
        }

        // 3. Create a new .venv at repo root
        var createResult = await RunCommand(
            processHelper, ["python", "-m", "venv", repoVenvPath], ctx, ct, InstallTimeout);
        if (createResult.ExitCode != 0)
        {
            return (null, new RequirementCheckOutput
            {
                Success = false,
                Output = createResult.Output?.Trim(),
                Error = $"Failed to create Python virtual environment at '{repoVenvPath}' using 'python -m venv' (exit code {createResult.ExitCode}). Command output: {createResult.Output?.Trim()}"
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
    public override async Task<RequirementCheckOutput> RunInstall(
        IProcessHelper processHelper,
        RequirementContext ctx,
        CancellationToken ct = default)
    {
        // Resolve venv Python executable
        var (pythonExe, venvError) = await ResolveVenvPythonAsync(processHelper, ctx, ct);
        if (venvError != null || pythonExe == null)
        {
            return venvError ?? new RequirementCheckOutput
            {
                Success = false,
                Error = "Failed to resolve Python virtual environment executable."
            };
        }

        // Resolve pip install target
        var installTarget = PipInstallTarget;
        if (IsRepoRelativeInstall)
        {
            // Repo-relative installs require the azure-sdk-for-python repo
            if (!ctx.RepoName.Equals(PythonRepoName, StringComparison.OrdinalIgnoreCase) && !ctx.RepoName.Equals(PythonPrRepoName, StringComparison.OrdinalIgnoreCase))
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
        
        if (MinVersion != null)
        {
            installTarget = $"{installTarget}>={MinVersion}";
        }

        var installOptions = new ProcessOptions(
            pythonExe,
            args: ["-m", "pip", "install", installTarget],
            timeout: InstallTimeout,
            logOutputStream: true,
            workingDirectory: ctx.PackagePath
        );
        var installResult = await processHelper.Run(installOptions, ct);

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
        protected override string PipInstallTarget => "eng/tools/azure-sdk-tools";

        public override string? Reason => "Required for validating Python SDKs";

        public override IReadOnlyList<string> GetInstructions(RequirementContext ctx)
        {
            return [
                "Navigate to the Python SDK repository root directory",
                "Ensure your virtual environment is activated",
                "python -m pip install eng/tools/azure-sdk-tools"];
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
                $"python -m pip install pytest>={MinVersion}",
            ];
        }
    }
}
