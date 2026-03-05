// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using YamlDotNet.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

internal interface ICiPipelineYaml
{
    CiPipelineYamlParametersBase? Parameters { get; }
}

/// <summary>
/// Generic model for deserializing Azure DevOps CI pipeline YAML files (ci*.yml).
/// Allows each language to provide a derived parameters type.
/// </summary>
internal class CiPipelineYaml<TParameters> : ICiPipelineYaml where TParameters : CiPipelineYamlParametersBase
{
    [YamlMember(Alias = "extends")]
    public CiPipelineYamlExtends<TParameters>? Extends { get; set; }

    CiPipelineYamlParametersBase? ICiPipelineYaml.Parameters => Extends?.Parameters;
}

internal class CiPipelineYamlExtends<TParameters> where TParameters : CiPipelineYamlParametersBase
{
    [YamlMember(Alias = "parameters")]
    public TParameters? Parameters { get; set; }
}

public class CiPipelineYamlParametersBase
{
    [YamlMember(Alias = "MatrixConfigs")]
    public List<Dictionary<string, object>>? MatrixConfigs { get; set; }

    [YamlMember(Alias = "AdditionalMatrixConfigs")]
    public List<Dictionary<string, object>>? AdditionalMatrixConfigs { get; set; }

    [YamlMember(Alias = "TriggeringPaths")]
    public List<string>? TriggeringPaths { get; set; }

    [YamlMember(Alias = "Artifacts")]
    public List<CiPipelineYamlArtifact>? Artifacts { get; set; }
}

public class CiPipelineYamlArtifact
{
    [YamlMember(Alias = "name")]
    public string? Name { get; set; }

    [YamlMember(Alias = "groupId")]
    public string? GroupId { get; set; }

    [YamlMember(Alias = "triggeringPaths")]
    public List<string>? TriggeringPaths { get; set; }

    [YamlMember(Alias = "additionalValidationPackages")]
    public List<string>? AdditionalValidationPackages { get; set; }
}
