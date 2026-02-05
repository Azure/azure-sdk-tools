// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Benchmarks.Infrastructure;

namespace Azure.Sdk.Tools.Cli.Benchmarks.Scenarios;

/// <summary>
/// POC scenario that tests renaming a TypeSpec property's client name.
/// Target: AddFaceFromUrlRequest.url property in specification/ai/Face/models.common.tsp
/// </summary>
public class RenameClientPropertyScenario : BenchmarkScenario
{
    /// <inheritdoc />
    public override string Name => "rename-client-property";

    /// <inheritdoc />
    public override string Description =>
        "Rename the @clientName decorator value from 'uri' to 'imageUri' for the url property in AddFaceFromUrlRequest";

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
        In the specification/ai/Face project, find the AddFaceFromUrlRequest model.
        It has a property called 'url' that's been renamed to "uri" in c#.
        Change that to imageUri for c#.
        """;

    /// <inheritdoc />
    public override TimeSpan Timeout => TimeSpan.FromMinutes(3);
}
