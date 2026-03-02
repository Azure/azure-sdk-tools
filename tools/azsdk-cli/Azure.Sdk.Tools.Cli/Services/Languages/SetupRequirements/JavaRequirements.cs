// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services.SetupRequirements;

/// <summary>
/// Requirements specific to Java SDK development.
/// </summary>
public static class JavaRequirements
{
    public static IReadOnlyList<Requirement> All => [
        new JavaRequirement(),
        new MavenRequirement()
    ];

    public class JavaRequirement : Requirement
    {
        public override string Name => "Java";
        public override string? MinVersion => "17.0.0";
        public override string[] CheckCommand => ["java", "-version"];
        public override string? NotAutoInstallableReason => NotInstallableReasons.LanguageRuntime;

        public override bool ShouldCheck(RequirementContext ctx) 
            => ctx.Languages.Contains(SdkLanguage.Java);

        public override IReadOnlyList<string> GetInstructions(RequirementContext ctx)
        {
            var instructions = new List<string>();

            if (ctx.IsWindows)
            {
                instructions.Add("Download JDK.");
            }
            else if (ctx.IsMacOS)
            {
                instructions.Add("brew install openjdk@17");
            }
            else
            {
                instructions.Add("sudo apt install openjdk-17-jdk");
            }

            instructions.Add("Set JAVA_HOME environment variable to the JDK installation path");
            instructions.Add("Add JAVA_HOME/bin to your PATH environment variable");
            instructions.Add("Restart your IDE");

            return instructions;
        }
    }

    public class MavenRequirement : Requirement
    {
        public override string Name => "Maven";
        public override string[] CheckCommand => ["mvn", "-v"];
        public override IReadOnlyList<string> DependsOn => ["Java"];
        public override string? NotAutoInstallableReason => NotInstallableReasons.BuildTool;

        public override bool ShouldCheck(RequirementContext ctx) 
            => ctx.Languages.Contains(SdkLanguage.Java);

        public override IReadOnlyList<string> GetInstructions(RequirementContext ctx)
        {
            var instructions = new List<string>();

            if (ctx.IsWindows)
            {
                instructions.Add("Download the latest version of Maven");
            }
            else if (ctx.IsMacOS)
            {
                instructions.Add("brew install maven");
            }
            else
            {
                instructions.Add("sudo apt install maven");
            }

            instructions.Add("Set MAVEN_HOME environment variable to the Maven installation path");
            instructions.Add("Add MAVEN_HOME/bin to your PATH environment variable");
            instructions.Add("Restart your IDE");

            return instructions;
        }
    }
}
