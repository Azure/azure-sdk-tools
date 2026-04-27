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
            return ["Download and install rustup from https://rust-lang.org/tools/install/", "Run `rustup install` from the root of the Rust repository to install the Rust toolchain"];
        }
    }
}
