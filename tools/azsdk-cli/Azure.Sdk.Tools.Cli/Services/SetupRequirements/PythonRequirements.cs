// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services.SetupRequirements;

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

    public class AzPySdkRequirement : Requirement
    {
        public override string Name => "azpysdk";
        public override string[] CheckCommand => ["azpysdk", "--help"];

        public override bool ShouldCheck(RequirementContext ctx) 
            => ctx.Language == SdkLanguage.Python;

        public override string? Reason => "Required for validating Python SDKs";

        public override IReadOnlyList<string> GetInstructions(RequirementContext ctx)
        {
            return [
                "Navigate to the Python SDK repository root directory",
                "Ensure your virtual environment is activated",
                "python -m pip install eng/tools/azure-sdk-tools[build]"];
        }
    }

    public class PythonGeneratorRequirement : Requirement
    {
        public override string Name => "sdk_generator";
        public override string[] CheckCommand => ["sdk_generator", "--help"];

        public override bool ShouldCheck(RequirementContext ctx) 
            => ctx.Language == SdkLanguage.Python;

        public override IReadOnlyList<string> GetInstructions(RequirementContext ctx)
        {
            return [
                "Navigate to the Python SDK repository root directory",
                "Ensure your virtual environment is activated",
                "python -m pip install eng/tools/azure-sdk-tools[sdk_generator]"];
        }
    }

    public class GhToolsRequirement : Requirement
    {
        public override string Name => "ghtools";
        public override string[] CheckCommand => ["python", "-m", "pip", "show", "GitPython"];

        public override bool ShouldCheck(RequirementContext ctx) 
            => ctx.Language == SdkLanguage.Python;

        public override IReadOnlyList<string> GetInstructions(RequirementContext ctx)
        {
            return [
                "Navigate to the Python SDK repository root directory",
                "Ensure your virtual environment is activated",
                "python -m pip install eng/tools/azure-sdk-tools[ghtools]"];
        }
    }

    public class PytestRequirement : Requirement
    {
        public override string Name => "pytest";
        public override string? MinVersion => "8.3.5";
        public override string[] CheckCommand => ["pytest", "--version"];

        public override bool ShouldCheck(RequirementContext ctx) 
            => ctx.Language == SdkLanguage.Python;

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
