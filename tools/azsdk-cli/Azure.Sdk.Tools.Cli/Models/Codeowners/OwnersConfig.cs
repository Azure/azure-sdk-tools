// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Codeowners;

/// <summary>
/// Repository-level owner configuration found at <c>.github/owners-config.json</c>.
/// Used to configure repo-level behavior for owner information, such as skip gates
/// applied during package ownership validation.
/// </summary>
public class OwnersConfig
{
    public const string RelativePath = ".github/owners-config.json";

    private List<string> _skipGates = [];

    [JsonPropertyName("skipGates")]
    public List<string> SkipGates
    {
        get => _skipGates;
        set => _skipGates = value ?? [];
    }
}
