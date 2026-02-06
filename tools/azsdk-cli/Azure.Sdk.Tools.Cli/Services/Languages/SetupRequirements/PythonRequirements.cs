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
    /// Handles venv creation/activation and pip install for Python tool requirements.
    /// 1. Checks if a venv exists at {repoRoot}/.venv; creates one if not.
    /// 2. Resolves the venv Python executable.
    /// 3. Runs pip install for the requirement's PipInstallTarget.
    /// </summary>
    public override async Task<RequirementCheckOutput> RunInstallAsync(
        Func<string[], Task<ProcessResult>> runCommand,
        RequirementContext ctx,
        CancellationToken ct = default)
    {
        var venvPath = Path.Combine(ctx.RepoRoot, ".venv");

        // Create venv if it doesn't exist
        if (!Directory.Exists(venvPath))
        {
            var createResult = await runCommand(["python", "-m", "venv", venvPath]);
            if (createResult.ExitCode != 0)
            {
                return new RequirementCheckOutput
                {
                    Success = false,
                    Output = createResult.Output?.Trim(),
                    Error = $"Failed to create venv at {venvPath}: {createResult.Output?.Trim()}"
                };
            }
        }

        // Resolve the venv Python executable
        var binDir = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Scripts" : "bin";
        var pythonExe = Path.Combine(venvPath, binDir, "python");
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            pythonExe += ".exe";
        }

        // Resolve pip install target path (may be relative to repo root)
        var installTarget = PipInstallTarget;
        if (!installTarget.StartsWith("http") && !Path.IsPathRooted(installTarget))
        {
            installTarget = Path.Combine(ctx.RepoRoot, installTarget);
        }

        var installResult = await runCommand([pythonExe, "-m", "pip", "install", installTarget]);

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
