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
        In the file specification/ai/Face/models.common.tsp, find the AddFaceFromUrlRequest model.
        It has a property called 'url' with a @clientName decorator set to "uri" for csharp.
        
        Change the @clientName value from "uri" to "imageUri".
        
        The current line looks like:
        @clientName("uri", "csharp")
        
        Change it to:
        @clientName("imageUri", "csharp")
        
        Only make this single change. Do not modify anything else.
        """;

    /// <inheritdoc />
    public override TimeSpan Timeout => TimeSpan.FromMinutes(3);
}
