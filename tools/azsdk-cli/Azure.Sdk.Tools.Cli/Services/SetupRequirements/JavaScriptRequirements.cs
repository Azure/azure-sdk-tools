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

        public override bool ShouldCheck(RequirementContext ctx) 
            => ctx.Languages.Contains(SdkLanguage.JavaScript);

        public override IReadOnlyList<string> GetInstructions(RequirementContext ctx)
        {
            var repoRoot = ctx.RepoRoot ?? "the repo root";

            return [
                "npm install -g pnpm",
                $"Run 'pnpm install' in {repoRoot}"
            ];
        }
    }
}
