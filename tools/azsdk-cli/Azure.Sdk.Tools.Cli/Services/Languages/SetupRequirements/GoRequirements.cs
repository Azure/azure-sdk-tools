// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services.SetupRequirements;

/// <summary>
/// Requirements specific to Go SDK development.
/// </summary>
public static class GoRequirements
{
    public static IReadOnlyList<Requirement> All => [
        new GoRequirement(),
        new GoImportsRequirement(),
        new GolangCiLintRequirement(),
        new GoGeneratorRequirement()
    ];

    public class GoRequirement : Requirement
    {
        public override string Name => "Go";
        public override string[] CheckCommand => ["go", "version"];
        public override string? NotAutoInstallableReason => NotInstallableReasons.LanguageRuntime;

        public override bool ShouldCheck(RequirementContext ctx) 
            => ctx.Languages.Contains(SdkLanguage.Go);

        public override IReadOnlyList<string> GetInstructions(RequirementContext ctx)
        {
            if (ctx.IsLinux) 
            {
                return ["sudo snap install go --classic"];
            }
           
            return ["Download and install the latest Go version of the compiler from https://go.dev/dl/"];
        }
    }

    public class GoImportsRequirement : Requirement
    {
        public override string Name => "goimports";
        public override IReadOnlyList<string> DependsOn => ["Go"];
        public override bool IsAutoInstallable => true;

        public override string[][]? GetInstallCommands(RequirementContext ctx)
            => [["go", "install", "golang.org/x/tools/cmd/goimports@latest"]];

        public override bool ShouldCheck(RequirementContext ctx) 
            => ctx.Languages.Contains(SdkLanguage.Go);

        public override async Task<RequirementCheckOutput> RunCheck(
            IProcessHelper processHelper,
            RequirementContext ctx,
            CancellationToken ct = default)
        {
            // Try running goimports with -h flag
            var result = await RunCommand(processHelper, ["goimports", "-h"], ctx, ct);
            
            // goimports -h returns exit code 2 but outputs help text, so check for output
            bool found = result.Output?.Contains("usage:") == true || result.ExitCode == 0;
            
            return new RequirementCheckOutput
            {
                Success = found,
                Output = found ? "goimports found" : null,
                Error = found ? null : "goimports not found in PATH"
            };
        }
    }

    public class GolangCiLintRequirement : Requirement
    {
        public override string Name => "golangci-lint";
        public override string[] CheckCommand => ["golangci-lint", "--version"];
        public override IReadOnlyList<string> DependsOn => ["Go"];
        public override string? NotAutoInstallableReason => NotInstallableReasons.SystemTool;

        public override bool ShouldCheck(RequirementContext ctx) 
            => ctx.Languages.Contains(SdkLanguage.Go);

        public override IReadOnlyList<string> GetInstructions(RequirementContext ctx)
        {
            return ["https://golangci-lint.run/docs/welcome/install/"];
        }
    }

    public class GoGeneratorRequirement : Requirement
    {
        public override string Name => "generator";
        public override string? MinVersion => "0.4.3";
        public override string[] CheckCommand => ["generator", "-v"];
        public override IReadOnlyList<string> DependsOn => ["Go"];
        public override bool IsAutoInstallable => true;

        public override string[][]? GetInstallCommands(RequirementContext ctx)
            => [["go", "install", "github.com/Azure/azure-sdk-for-go/eng/tools/generator@latest"]];

        public override bool ShouldCheck(RequirementContext ctx) 
            => ctx.Languages.Contains(SdkLanguage.Go);
    }
}
