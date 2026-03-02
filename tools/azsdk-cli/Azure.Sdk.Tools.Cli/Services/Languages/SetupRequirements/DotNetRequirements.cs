// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services.SetupRequirements;

/// <summary>
/// Requirements specific to .NET SDK development.
/// </summary>
public static class DotNetRequirements
{
    public static IReadOnlyList<Requirement> All => [
        new DotNetSdkRequirement()
    ];

    public class DotNetSdkRequirement : Requirement
    {
        public override string Name => "Dotnet SDK";
        public override string? MinVersion => "9.0.306";
        public override string[] CheckCommand => ["dotnet", "--version"];
        public override string? NotAutoInstallableReason => NotInstallableReasons.LanguageRuntime;

        public override bool ShouldCheck(RequirementContext ctx) 
            => ctx.Languages.Contains(SdkLanguage.DotNet);

        public override IReadOnlyList<string> GetInstructions(RequirementContext ctx)
        {
            return ["Install .NET SDK with version at least higher than defined in global.json file in the repo root, from https://dotnet.microsoft.com/download"];
        }
    }
}
