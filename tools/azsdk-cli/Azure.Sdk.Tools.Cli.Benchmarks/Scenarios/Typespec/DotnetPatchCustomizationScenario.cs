// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Benchmarks.Infrastructure;
using Azure.Sdk.Tools.Cli.Benchmarks.Models;
using Azure.Sdk.Tools.Cli.Benchmarks.Validation;
using Azure.Sdk.Tools.Cli.Benchmarks.Validation.Validators;

namespace Azure.Sdk.Tools.Cli.Benchmarks.Scenarios;

/// <summary>
/// Scenario that tests the .NET error-driven patch template by introducing a breaking
/// property rename in a generated type and verifying that the agent patches the
/// customization file to use the new name.
///
/// Target: Azure.AI.DocumentIntelligence in azure-sdk-for-net.
/// The setup renames a property in the generated code to simulate a regeneration
/// breaking change, then the agent should patch the customization partial class.
/// </summary>
public class DotnetPatchCustomizationScenario : BenchmarkScenario
{
    private const string PackageDir = "sdk/documentintelligence/Azure.AI.DocumentIntelligence";
    private const string CustomizationFile = $"{PackageDir}/src/DocumentIntelligenceClient.cs";
    private const string GeneratedFile = $"{PackageDir}/src/Generated/DocumentIntelligenceClient.cs";

    /// <inheritdoc />
    public override string Name => "dotnet-patch-customization";

    /// <inheritdoc />
    public override string Description =>
        "Test .NET error-driven patching: rename a generated property and verify the agent patches the customization partial class";

    /// <inheritdoc />
    public override string[] Tags => ["dotnet", "customization", "patching", "poc"];

    /// <inheritdoc />
    public override RepoConfig Repo => new()
    {
        Owner = "Azure",
        Name = "azure-sdk-for-net",
        Ref = "main"
    };

    /// <inheritdoc />
    public override string Prompt => $"""
        The package at {PackageDir} has build errors after code regeneration.
        A property was renamed in the generated code, breaking the customization file.
        Use the CustomizedCodeUpdate tool to analyze the build errors and fix the customization files.
        """;

    /// <inheritdoc />
    public override TimeSpan Timeout => TimeSpan.FromMinutes(5);

    /// <inheritdoc />
    public override async Task SetupAsync(Workspace workspace)
    {
        // Read the generated file and rename a commonly-referenced property
        // to simulate a breaking regeneration change.
        // The customization partial class should reference the old name and fail to compile.
        if (File.Exists(Path.Combine(workspace.RepoPath, GeneratedFile)))
        {
            var content = await workspace.ReadFileAsync(GeneratedFile);
            // Introduce a property rename that will break the customization file
            content = content.Replace("AnalyzeDocument", "AnalyzeDocumentContent");
            await workspace.WriteFileAsync(GeneratedFile, content);
        }
    }

    /// <inheritdoc />
    public override IEnumerable<IValidator> Validators =>
    [
        new FileExistsValidator("Customization file exists",
            CustomizationFile),

        new ContainsValidator("Customization file references new property name",
            filePath: CustomizationFile,
            patterns: ["AnalyzeDocumentContent"]),
    ];
}
