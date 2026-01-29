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
    /// <summary>
    /// The raw check command before Python executable resolution.
    /// </summary>
    protected abstract string[] RawCheckCommand { get; }

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
