// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services.SetupRequirements;

/// <summary>
/// Requirements specific to JavaScript/TypeScript SDK development.
/// </summary>
public static class JavaScriptRequirements
{
    public static IReadOnlyList<Requirement> All => [
        new PnpmRequirement()
    ];

    public class PnpmRequirement : Requirement
    {
        public override string Name => "pnpm";
        public override string[] CheckCommand => ["pnpm", "--version"];
        public override IReadOnlyList<string> DependsOn => ["Node.js"];
        public override bool IsAutoInstallable => true;

        public override string[][]? GetInstallCommands(RequirementContext ctx)
            => [["npm", "install", "-g", "pnpm"]];

        public override bool ShouldCheck(RequirementContext ctx) 
            => ctx.Languages.Contains(SdkLanguage.JavaScript);

        public override IReadOnlyList<string> GetInstructions(RequirementContext ctx)
        {
            return [
                "npm install -g pnpm",
                "Navigate to the root of the JavaScript SDK repository.",
                "pnpm install"
            ];
        }
    }
}
