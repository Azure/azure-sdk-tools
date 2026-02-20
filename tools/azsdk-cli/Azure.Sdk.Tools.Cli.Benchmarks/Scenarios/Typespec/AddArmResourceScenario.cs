// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Benchmarks.Infrastructure;
using Azure.Sdk.Tools.Cli.Benchmarks.Validation;
using Azure.Sdk.Tools.Cli.Benchmarks.Validation.Validators;

namespace Azure.Sdk.Tools.Cli.Benchmarks.Scenarios;

/// <summary>
/// POC scenario that tests renaming a TypeSpec property's client name.
/// Target: AddFaceFromUrlRequest.url property in specification/ai/Face/models.common.tsp
/// </summary>
public class AddArmResourceScenario : BenchmarkScenario
{
    /// <inheritdoc />
    public override string Name => "add-arm-resource";

    /// <inheritdoc />
    public override string Description =>
        "Add a new ARM resource to the specification.";

    /// <inheritdoc />
    public override string[] Tags => ["typespec", "authoring", "poc"];

    /// <inheritdoc />
    public override RepoConfig Repo => new()
    {
        Owner = "Azure",
        Name = "azure-rest-api-specs",
        Ref = "main"
    };

    /// <inheritdoc />
    public override string Prompt => """
        In the specification/widget/resource-manager/Microsoft.Widget/Widget project,
        add an ARM resource named 'Asset' with CRUD operations.
        """;

    /// <inheritdoc />
    public override TimeSpan Timeout => TimeSpan.FromMinutes(3);

    /// <inheritdoc />
    public override async Task SetupAsync(Workspace workspace)
    {
        await workspace.RunCommandAsync("npm", "ci");
    }

    /// <inheritdoc />
    public override IEnumerable<IValidator> Validators =>
    [
        // Debug: Report what context is loaded
        // new ContextReportValidator(),

        // Verify the project entry point still exists
        new FileExistsValidator("Project entry point exists",
            "specification/widget/resource-manager/Microsoft.Widget/Widget/main.tsp"),

        // Verify the new Asset resource file was created
        new FileExistsValidator("Asset resource file created",
            "specification/widget/resource-manager/Microsoft.Widget/Widget/asset.tsp"),

        // Verify the new file contains the expected ARM resource patterns
        new ContainsValidator("Asset file has ARM resource model",
            filePath: "specification/widget/resource-manager/Microsoft.Widget/Widget/asset.tsp",
            patterns: ["model Asset is TrackedResource<AssetProperties>", "...ResourceNameParameter<Asset>", "model AssetProperties"]),

        // Verify CRUD operations are present in the new file
        new ContainsValidator("Asset file has CRUD operations",
            filePath: "specification/widget/resource-manager/Microsoft.Widget/Widget/asset.tsp",
            patterns: [
                "@armResourceOperations",
                "interface Assets",
                "get is ArmResourceRead<Asset>",
                "createOrUpdate is ArmResourceCreateOrReplaceAsync<Asset>",
                "update is ArmResourcePatchSync<Asset, AssetProperties>",
                "delete is ArmResourceDeleteWithoutOkAsync<Asset>",
                "listByResourceGroup is ArmResourceListByParent<Asset>",
                "listBySubscription is ArmListBySubscription<Asset>"
            ]),

        // Verify main.tsp imports the new asset file
        new ContainsValidator("main.tsp imports asset",
            filePath: "specification/widget/resource-manager/Microsoft.Widget/Widget/main.tsp",
            patterns: ["asset.tsp"]),

        // Verify the project compiles successfully
        new CommandValidator("tsp compile succeeds",
            command: "tsp",
            arguments: ["compile", "./specification/widget/resource-manager/Microsoft.Widget/Widget/main.tsp"])
    ];
}
