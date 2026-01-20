// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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

        public override bool ShouldCheck(RequirementContext ctx) 
            => ctx.Language == SdkLanguage.Go;

        public override IReadOnlyList<string> GetInstructions(RequirementContext ctx)
        {
            if (ctx.IsLinux) {
                return ["sudo snap install go"];
            }
            if (ctx.IsWindows)
                return ["Download and install the latest Go version of the compiler from https://go.dev/dl/"];
        }
    }

    public class GoImportsRequirement : Requirement
    {
        public override string Name => "goimports";
        public override string[] CheckCommand => ["pwsh", "-Command", "Get-Command", "goimports"];

        public override bool ShouldCheck(RequirementContext ctx) 
            => ctx.Language == SdkLanguage.Go;

        public override IReadOnlyList<string> GetInstructions(RequirementContext ctx)
        {
            return ["go install golang.org/x/tools/cmd/goimports@latest"];
        }
    }

    public class GolangCiLintRequirement : Requirement
    {
        public override string Name => "golangci-lint";
        public override string[] CheckCommand => ["golangci-lint", "--version"];

        public override bool ShouldCheck(RequirementContext ctx) 
            => ctx.Language == SdkLanguage.Go;

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

        public override bool ShouldCheck(RequirementContext ctx) 
            => ctx.Language == SdkLanguage.Go;

        public override IReadOnlyList<string> GetInstructions(RequirementContext ctx)
        {
            return ["go install github.com/Azure/azure-sdk-for-go/eng/tools/generator@latest"];
        }
    }
}
