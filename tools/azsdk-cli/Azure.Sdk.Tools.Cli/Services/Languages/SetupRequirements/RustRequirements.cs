// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services.SetupRequirements;

/// <summary>
/// Requirements specific to Rust SDK development.
/// </summary>
public static class RustRequirements
{
    public static IReadOnlyList<Requirement> All => [
        new RustupRequirement(),
        new CargoRequirement()
    ];

    public class RustupRequirement : Requirement
    {
        public override string Name => "rustup";
        public override string[] CheckCommand => ["rustup", "--version"];
        public override string? NotAutoInstallableReason => NotInstallableReasons.LanguageRuntime;

        public override bool ShouldCheck(RequirementContext ctx)
            => ctx.Languages.Contains(SdkLanguage.Rust);

        public override IReadOnlyList<string> GetInstructions(RequirementContext ctx)
        {
            return ["Download and install rustup from https://rust-lang.org/tools/install/"];
        }
    }

    public class CargoRequirement : Requirement
    {
        public override string Name => "cargo";
        public override string[] CheckCommand => ["cargo", "--version"];
        public override IReadOnlyList<string> DependsOn => ["rustup"];
        public override string? NotAutoInstallableReason => NotInstallableReasons.BundledWithLanguage;

        public override bool ShouldCheck(RequirementContext ctx)
            => ctx.Languages.Contains(SdkLanguage.Rust);

        public override IReadOnlyList<string> GetInstructions(RequirementContext ctx)
        {
            return ["Download and install rustup from https://rust-lang.org/tools/install/"];
        }
    }
}
